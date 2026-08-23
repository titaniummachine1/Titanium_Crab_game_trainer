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

            // --- Auto-Strafe (velocity vector steering) ---
            // Like Minecraft's "accurate walk": smoothly rotates the player's
            // velocity vector to match the desired movement direction with high
            // acceleration. This gives precise control when turning — no drift,
            // no wobble. Works both grounded and airborne.
            if (p.AutoStrafeEnabled)
            {
                PlayerMovement pm = TrainerMenu.GetLocalPlayerMovement();
                if (pm == null)
                    return;

                Rigidbody rb = pm.GetRb();
                if (rb == null)
                    return;

                Camera cam = Camera.main;
                if (cam == null)
                    return;

                // Get desired movement direction from WASD input relative to camera
                Vector3 forward = cam.transform.forward;
                forward.y = 0f;
                forward.Normalize();
                Vector3 right = cam.transform.right;
                right.y = 0f;
                right.Normalize();

                Vector3 desiredDir = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) desiredDir += forward;
                if (Input.GetKey(KeyCode.S)) desiredDir -= forward;
                if (Input.GetKey(KeyCode.A)) desiredDir -= right;
                if (Input.GetKey(KeyCode.D)) desiredDir += right;

                if (desiredDir.sqrMagnitude > 0.01f)
                {
                    desiredDir.Normalize();

                    // Get current horizontal velocity
                    Vector3 vel = rb.velocity;
                    Vector3 horizontal = new(vel.x, 0f, vel.z);
                    float currentSpeed = horizontal.magnitude;

                    if (currentSpeed > 0.1f)
                    {
                        // Steer: rotate velocity vector toward desired direction
                        // High lerp factor = fast response, but not instant (no snap)
                        // 0.3 = ~3 frames to fully turn at 60fps, feels responsive
                        float steerSpeed = 8f * Time.fixedDeltaTime;
                        Vector3 currentDir = horizontal.normalized;
                        Vector3 newDir = Vector3.Lerp(currentDir, desiredDir, steerSpeed);

                        // Preserve speed but redirect it
                        rb.velocity = new Vector3(
                            newDir.x * currentSpeed,
                            vel.y,
                            newDir.z * currentSpeed
                        );
                    }
                }
            }
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
