using HarmonyLib;
using SteamworksNative;
using UnityEngine;

namespace TitaniumCrab
{
    /// <summary>
    /// All Harmony patches for TitaniumCrab trainer features.
    /// Per-frame logic (fly, noclip, speed, FOV, aimbot, ESP, auto-slap, etc.)
    /// is handled in <see cref="TrainerMenu"/>.Update.
    /// </summary>
    internal static class Patches
    {
        // =====================================================================
        //  Anti-Anti-Cheat: bypass BepInEx / modding detection
        // =====================================================================

        [HarmonyPatch(typeof(EffectManager), nameof(EffectManager.Method_Private_Void_GameObject_Boolean_Vector3_Quaternion_0))]
        [HarmonyPrefix]
        internal static bool PreEffectManagerDetection()
            => !TitaniumCrabPlugin.Instance.AntiAntiCheatEnabled;

        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.Method_Private_Void_0))]
        [HarmonyPrefix]
        internal static bool PreLobbyManagerDetection()
            => !TitaniumCrabPlugin.Instance.AntiAntiCheatEnabled;

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
        //  Super Punch + Anti Push: patch GameManager.PunchPlayer
        //  param_1 = fromClient (ulong), param_2 = punchedPlayer (ulong),
        //  param_3 = direction (Vector3)
        // =====================================================================

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PunchPlayer))]
        [HarmonyPrefix]
        internal static bool PrePunchPlayer(ref ulong param_1, ref ulong param_2, ref Vector3 param_3)
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

            // --- Auto-Strafe (bunnyhop assist) ---
            // Instead of adding raw force (which causes wobble), we scale the
            // player's max speed up and let the game's own movement handle
            // acceleration. This gives smooth, controllable strafing.
            if (p.AutoStrafeEnabled)
            {
                PlayerMovement pm = TrainerMenu.GetLocalPlayerMovement();
                if (pm != null && !TrainerMenu.IsGrounded(pm))
                {
                    // Increase max speeds so the game allows faster air control
                    pm.SetMaxRunSpeed(20f);
                    pm.SetMaxSpeed(15f);
                }
                else if (pm != null && TrainerMenu.IsGrounded(pm))
                {
                    // Restore defaults when grounded so normal movement isn't affected
                    pm.SetMaxRunSpeed(13f);
                    pm.SetMaxSpeed(6.5f);
                }
            }

            // Restore frozen state after SetInput runs (so we don't permanently
            // break game logic — the patch only needs it cleared for this call)
            // Note: we don't restore because the game re-sets it next frame anyway.
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
