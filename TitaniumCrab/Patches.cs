using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SteamworksNative;
using UnityEngine;

namespace TitaniumCrab
{
    /// <summary>
    /// All Harmony patches for TitaniumCrab trainer features.
    /// Per-frame logic (fly, noclip, speed, FOV, aimbot, auto-slap, etc.)
    /// is handled in <see cref="TrainerMenu"/>.Update.
    /// </summary>
    internal static class Patches
    {
        // Shared silent aim target — set by TrainerMenu.RunAimbot, read by patches
        internal static Vector3? SilentAimTarget = null;
        // =====================================================================
        //  Anti-Anti-Cheat: disable ACTk detectors + destroy hidden GameObject
        //  Based on CodeName-Anti's AntiCheat.cs — NOT Harmony patches that
        //  block game logic (which was preventing lobby joins).
        // =====================================================================

        /// <summary>
        /// Call all public static Stop* methods on ACTk detector types.
        /// This disables all anti-cheat detectors without breaking game logic.
        /// </summary>
        internal static void StopAntiCheatDetectors()
        {
            try
            {
                Type actkType = typeof(CodeStage.AntiCheat.Common.ACTk);
                Assembly actkAssembly = Assembly.GetAssembly(actkType);

                foreach (Type t in actkAssembly.GetTypes().Where(t => t.IsPublic))
                {
                    foreach (MethodInfo method in t.GetMethods()
                        .Where(m => m.IsStatic && m.IsPublic && m.Name.Contains("Stop")))
                    {
                        try { method.Invoke(null, null); }
                        catch { /* some methods may require args */ }
                    }
                }
            }
            catch (Exception ex)
            {
                TitaniumCrabPlugin.Instance.Log.LogWarning($"StopAntiCheatDetectors failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Destroy the hidden anti-cheat GameObject that ACTk spawns.
        /// Called after a delay so the game has time to initialize.
        /// </summary>
        internal static void DestroyAntiCheatObject()
        {
            try
            {
                GameObject obj = GameObject.Find("Managers/MoreSoundEffects/Sfx/Definitely just sfx here lol");
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                    TitaniumCrabPlugin.Instance.Log.LogInfo("Anti-cheat GameObject destroyed");
                }
            }
            catch { /* not found yet */ }
        }

        // =====================================================================
        //  Silent Aim: override shoot/use direction without moving camera
        //  Patches ClientSend.ShootGun (Vector2 angles) and ClientSend.UseItem
        //  (Vector3 direction) to redirect toward the silent aim target.
        //  Uses string-based patching because ShootGun isn't exposed in interop.
        // =====================================================================

        internal static Harmony _silentAimHarmony;

        internal static void ApplySilentAimPatches(Harmony harmony)
        {
            _silentAimHarmony = harmony;

            // Patch ShootGun(Vector2) — gun shoot sends camera angles
            var shootGunMethod = AccessTools.Method(typeof(ClientSend), "ShootGun");
            if (shootGunMethod != null)
            {
                var prefix = new HarmonyMethod(typeof(Patches), nameof(PreShootGun));
                harmony.Patch(shootGunMethod, prefix: prefix);
            }

            // Patch UseItem(int, Vector3) — throwable use sends direction
            var useItemMethod = AccessTools.Method(typeof(ClientSend), "UseItem");
            if (useItemMethod != null)
            {
                var prefix = new HarmonyMethod(typeof(Patches), nameof(PreUseItem));
                harmony.Patch(useItemMethod, prefix: prefix);
            }
        }

        internal static void PreShootGun(ref Vector2 param_1)
        {
            if (!SilentAimTarget.HasValue)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            Vector3 target = SilentAimTarget.Value;
            Vector3 dir = (target - cam.transform.position).normalized;

            float pitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            param_1 = new Vector2(pitch, yaw);
        }

        internal static void PreUseItem(ref Vector3 param_2)
        {
            if (!SilentAimTarget.HasValue)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            Vector3 target = SilentAimTarget.Value;
            param_2 = (target - cam.transform.position).normalized;
        }

        // =====================================================================
        //  No Camera Shake: disable all camera shake methods
        //  (gun shake, push shake, damage shake)
        // =====================================================================

        [HarmonyPatch(typeof(CameraShaker), nameof(CameraShaker.GunShake))]
        [HarmonyPatch(typeof(CameraShaker), nameof(CameraShaker.PushShake))]
        [HarmonyPatch(typeof(CameraShaker), nameof(CameraShaker.DamageShake))]
        [HarmonyPrefix]
        internal static bool DisableCameraShake()
            => !TitaniumCrabPlugin.Instance.NoCameraShakeEnabled;

        // =====================================================================
        //  God Mode + No Fall: patch PlayerStatus.DamagePlayer
        //  itemId == -2 is fall damage.
        // =====================================================================

        [HarmonyPatch(typeof(PlayerStatus), nameof(PlayerStatus.DamagePlayer))]
        [HarmonyPrefix]
        internal static bool PreDamagePlayer(int param_1)
        {
            var p = TitaniumCrabPlugin.Instance;
            if (p.GodModeEnabled)
                return false;
            if (p.NoFallEnabled && param_1 == -2)
                return false;
            return true;
        }

        // =====================================================================
        //  Anti Env Kill: prevent death from environmental hazards
        //  (falling off map, water, lava, out-of-bounds)
        //  Patches PlayerDied to block death when the killer is environment
        //  (param_2 == 0 means no killer player = environmental death)
        // =====================================================================

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerDied))]
        [HarmonyPrefix]
        internal static bool PrePlayerDied(ulong param_1, ulong param_2)
        {
            if (!TitaniumCrabPlugin.Instance.AntiEnvKillEnabled)
                return true;

            ulong myId = SteamUser.GetSteamID().m_SteamID;
            if (param_1 != myId)
                return true;

            // param_2 == 0 means no killer = environmental death (fall, water, lava)
            // Block it — the player will just stay alive
            if (param_2 == 0)
                return false;

            return true;
        }

        // =====================================================================
        //  Super Punch + Anti Push: patch GameManager.PunchPlayer
        //  param_1 = fromClient (ulong), param_2 = punchedPlayer (ulong),
        //  param_3 = direction (Vector3)
        // =====================================================================

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PunchPlayer))]
        [HarmonyPrefix]
        internal static bool PrePunchPlayer(ulong param_1, ulong param_2, ref Vector3 param_3)
        {
            var p = TitaniumCrabPlugin.Instance;
            ulong myId = SteamUser.GetSteamID().m_SteamID;

            // Super Punch: multiply knockback when WE punch someone
            if (p.SuperPunchEnabled && param_1 == myId)
                param_3 *= p.SuperPunchMultiplier;

            // Anti Push: block punches directed at us
            if (p.AntiPushEnabled && param_2 == myId)
                return false;

            return true;
        }

        // =====================================================================
        //  No Freeze: ignore the frozen state so we can move before round start
        // =====================================================================

        [HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.SetInput))]
        [HarmonyPrefix]
        internal static void PrePlayerMovementSetInput(ref bool param_2, ref bool param_3, ref bool param_4)
        {
            var p = TitaniumCrabPlugin.Instance;
            if (p == null)
                return;

            // --- No Freeze: temporarily clear frozen flags during SetInput ---
            bool wasFrozen = PersistentPlayerData.frozen || PersistentPlayerData.hnsFrozen;
            if (p.NoFreezeEnabled && wasFrozen)
            {
                PersistentPlayerData.frozen = false;
                PersistentPlayerData.hnsFrozen = false;
            }

            // --- Bunnyhop ---
            if (p.BunnyhopEnabled)
                param_3 = param_3 || PlayerInput.CheckInput(InputManager.jump);

            // --- Air Jump ---
            if (p.AirJumpEnabled)
                param_3 = param_3 || PlayerInput.CheckInputDown(InputManager.jump);

            // Note: Auto-Strafe is handled in TrainerMenu.FixedUpdate
            // so it runs AFTER the game applies its own movement forces.
        }

        // =====================================================================
        //  Make all guns automatic (so hold-to-attack works smoothly)
        // =====================================================================

        [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.Awake))]
        [HarmonyPostfix]
        internal static void PostItemManagerAwake()
        {
            foreach (ItemData itemData in ItemManager.idToItem.Values)
            {
                if (itemData.gunComponent != null)
                    itemData.gunComponent.automatic = true;
            }
        }

        // =====================================================================
        //  Infinite Ammo: refill gun ammo after every shot
        // =====================================================================

        [HarmonyPatch(typeof(Gun), "Method_Private_Void_0")]
        [HarmonyPostfix]
        internal static void PostGunFire(Gun __instance)
        {
            if (!TitaniumCrabPlugin.Instance.InfiniteAmmoEnabled)
                return;

            try
            {
                var ammoField = AccessTools.Field(__instance.GetType(), "currentAmmo")
                             ?? AccessTools.Field(__instance.GetType(), "ammoInMag")
                             ?? AccessTools.Field(__instance.GetType(), "ammo");
                var maxField  = AccessTools.Field(__instance.GetType(), "magSize")
                             ?? AccessTools.Field(__instance.GetType(), "clipSize")
                             ?? AccessTools.Field(__instance.GetType(), "maxAmmo");
                if (ammoField != null && maxField != null)
                    ammoField.SetValue(__instance, maxField.GetValue(__instance));
            }
            catch { /* field layout may differ */ }
        }
    }
}
