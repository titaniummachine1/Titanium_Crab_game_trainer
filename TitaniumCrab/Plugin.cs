using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace TitaniumCrab
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class TitaniumCrabPlugin : BasePlugin
    {
        public static TitaniumCrabPlugin Instance { get; private set; }

        // --- Feature toggles (runtime, also bound to config) ---
        // Movement
        public bool BunnyhopEnabled;
        public bool AutoStrafeEnabled;
        public bool SpeedHackEnabled;
        public bool FlyEnabled;
        public bool NoclipEnabled;
        public bool AirJumpEnabled;
        public bool MegaJumpEnabled;
        public bool NoFreezeEnabled;
        public bool AntiBoundKillsEnabled;
        public bool StrongSprintEnabled;
        public int SlideJumpKey;

        // Combat
        public bool GodModeEnabled;
        public bool NoFallEnabled;
        public bool AntiEnvKillEnabled;
        public bool InfiniteAmmoEnabled;
        public bool AimbotEnabled;
        public bool AutoSlapEnabled;
        public bool SuperPunchEnabled;
        public bool AntiPushEnabled;

        // Visual
        public bool EspEnabled;
        public bool FovModifierEnabled;
        public bool FullbrightEnabled;
        public bool NoCameraShakeEnabled;

        // Misc
        public bool AntiAntiCheatEnabled = true;

        // --- Feature parameters ---
        public float SpeedMultiplier = 2.0f;
        public float FlySpeed = 10.0f;
        public float FovValue = 90.0f;
        public float AimbotFOV = 30.0f;
        public float AimbotSmooth = 5.0f;
        public float SuperPunchMultiplier = 5.0f;
        public float MegaJumpForce = 20.0f;
        public float StrongSprintMultiplier = 2.0f;
        public bool MenuVisible = true;

        // --- Config entries ---
        internal ConfigEntry<bool> CfgBunnyhop;
        internal ConfigEntry<bool> CfgAutoStrafe;
        internal ConfigEntry<bool> CfgSpeedHack;
        internal ConfigEntry<float> CfgSpeedMultiplier;
        internal ConfigEntry<bool> CfgFly;
        internal ConfigEntry<float> CfgFlySpeed;
        internal ConfigEntry<bool> CfgNoclip;
        internal ConfigEntry<bool> CfgAirJump;
        internal ConfigEntry<bool> CfgMegaJump;
        internal ConfigEntry<float> CfgMegaJumpForce;
        internal ConfigEntry<bool> CfgNoFreeze;
        internal ConfigEntry<bool> CfgAntiBoundKills;
        internal ConfigEntry<bool> CfgStrongSprint;
        internal ConfigEntry<float> CfgStrongSprintMultiplier;
        internal ConfigEntry<int> CfgSlideJumpKey;

        internal ConfigEntry<bool> CfgGodMode;
        internal ConfigEntry<bool> CfgNoFall;
        internal ConfigEntry<bool> CfgAntiEnvKill;
        internal ConfigEntry<bool> CfgInfiniteAmmo;
        internal ConfigEntry<bool> CfgAimbot;
        internal ConfigEntry<float> CfgAimbotFOV;
        internal ConfigEntry<float> CfgAimbotSmooth;
        internal ConfigEntry<bool> CfgAutoSlap;
        internal ConfigEntry<bool> CfgSuperPunch;
        internal ConfigEntry<float> CfgSuperPunchMultiplier;
        internal ConfigEntry<bool> CfgAntiPush;

        internal ConfigEntry<bool> CfgEsp;
        internal ConfigEntry<bool> CfgFovModifier;
        internal ConfigEntry<float> CfgFovValue;
        internal ConfigEntry<bool> CfgFullbright;
        internal ConfigEntry<bool> CfgNoCameraShake;

        internal ConfigEntry<bool> CfgAntiAntiCheat;
        internal ConfigEntry<int> CfgMenuKey;

        public override void Load()
        {
            Instance = this;

            // --- Movement ---
            CfgBunnyhop        = Config.Bind("Movement",  "Bunnyhop",        true,  "Auto-jump the moment you touch the ground while holding jump");
            CfgAutoStrafe      = Config.Bind("Movement",  "AutoStrafe",      false, "Automatically strafe in the air to gain speed (bunnyhop assist)");
            CfgSpeedHack       = Config.Bind("Movement",  "SpeedHack",       false, "Multiply player movement speed");
            CfgSpeedMultiplier = Config.Bind("Movement",  "SpeedMultiplier", 2.0f, new ConfigDescription("Speed multiplier", new AcceptableValueRange<float>(0.5f, 20f)));
            CfgFly             = Config.Bind("Movement",  "Fly",             false, "Free-fly with jump/crouch for vertical, WASD for horizontal");
            CfgFlySpeed        = Config.Bind("Movement",  "FlySpeed",        10.0f, new ConfigDescription("Fly mode speed", new AcceptableValueRange<float>(1f, 100f)));
            CfgNoclip          = Config.Bind("Movement",  "Noclip",          false, "Disable player colliders so you can walk through walls");
            CfgAirJump         = Config.Bind("Movement",  "AirJump",         false, "Allow jumping while airborne (infinite jumps)");
            CfgMegaJump        = Config.Bind("Movement",  "MegaJump",        false, "Jump much higher than normal");
            CfgMegaJumpForce   = Config.Bind("Movement",  "MegaJumpForce",   20.0f, new ConfigDescription("Mega jump upward velocity", new AcceptableValueRange<float>(5f, 100f)));
            CfgNoFreeze        = Config.Bind("Movement",  "NoFreeze",        false, "Move before the round officially starts (ignore freeze)");
            CfgAntiBoundKills  = Config.Bind("Movement",  "AntiBoundKills",  false, "Float above out-of-bounds kill zones (water, lava, etc.)");
            CfgStrongSprint    = Config.Bind("Movement",  "StrongSprint",    false, "Force sprint to stay active and multiply sprint speed");
            CfgStrongSprintMultiplier = Config.Bind("Movement", "StrongSprintMultiplier", 2.0f, new ConfigDescription("Sprint speed multiplier", new AcceptableValueRange<float>(1f, 20f)));
            CfgSlideJumpKey    = Config.Bind("Movement",  "SlideJumpKey",    (int)KeyCode.V, "Key to trigger slide-jump launch (jump + crouch simultaneously)");

            // --- Combat ---
            CfgGodMode         = Config.Bind("Combat",    "GodMode",         false, "Prevent all incoming damage");
            CfgNoFall          = Config.Bind("Combat",    "NoFall",          false, "Prevent fall damage only");
            CfgAntiEnvKill     = Config.Bind("Combat",    "AntiEnvKill",     false, "Prevent death from environmental hazards (falling off map, water, lava)");
            CfgInfiniteAmmo    = Config.Bind("Combat",    "InfiniteAmmo",    false, "Guns never run out of ammo");
            CfgAimbot          = Config.Bind("Combat",    "Aimbot",          false, "Automatically aim at the nearest visible player");
            CfgAimbotFOV       = Config.Bind("Combat",    "AimbotFOV",       30.0f, new ConfigDescription("Aimbot field-of-view (degrees)", new AcceptableValueRange<float>(1f, 180f)));
            CfgAimbotSmooth    = Config.Bind("Combat",    "AimbotSmooth",    5.0f,  new ConfigDescription("Aimbot smoothing (higher = slower)", new AcceptableValueRange<float>(1f, 30f)));
            CfgAutoSlap        = Config.Bind("Combat",    "AutoSlap",        false, "Machine-gun punching: no cooldown, slaps every tick");
            CfgSuperPunch      = Config.Bind("Combat",    "SuperPunch",      false, "Multiply punch knockback force");
            CfgSuperPunchMultiplier = Config.Bind("Combat", "SuperPunchMultiplier", 5.0f, new ConfigDescription("Super punch knockback multiplier", new AcceptableValueRange<float>(0f, 50f)));
            CfgAntiPush        = Config.Bind("Combat",    "AntiPush",        false, "Prevent other players from punching/pushing you");

            // --- Visual ---
            CfgEsp             = Config.Bind("Visual",    "ESP",             false, "Draw player boxes, names and distance through walls");
            CfgFovModifier     = Config.Bind("Visual",    "FovModifier",     false, "Override camera field-of-view");
            CfgFovValue        = Config.Bind("Visual",    "FovValue",        90.0f, new ConfigDescription("Camera FOV in degrees", new AcceptableValueRange<float>(20f, 170f)));
            CfgFullbright      = Config.Bind("Visual",    "Fullbright",      false, "Max out all scene lights (see in dark/Dorm mode)");
            CfgNoCameraShake   = Config.Bind("Visual",    "NoCameraShake",   false, "Disable all camera shake (gun, punch, damage) — useful with Infinite Slap");

            // --- Misc ---
            CfgAntiAntiCheat   = Config.Bind("Misc",      "AntiAntiCheat",   true,  "Bypass BepInEx / modding detection (recommended)");
            CfgMenuKey         = Config.Bind("Misc",      "MenuKey",         (int)KeyCode.Insert, "Key to toggle the trainer menu");

            SyncFromConfig();

            // Register and spawn the menu MonoBehaviour (same pattern as CrabCheat)
            ClassInjector.RegisterTypeInIl2Cpp<TrainerMenu>();
            GameObject menuObj = new("TitaniumCrab_Menu");
            UnityEngine.Object.DontDestroyOnLoad(menuObj);
            menuObj.hideFlags |= HideFlags.HideAndDontSave;
            menuObj.AddComponent<TrainerMenu>();

            Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll(typeof(Patches));

            // Disable ACTk anti-cheat detectors at startup (non-blocking approach)
            if (AntiAntiCheatEnabled)
            {
                Patches.StopAntiCheatDetectors();
                Log.LogInfo("Anti-cheat detectors disabled");
            }

            Log.LogInfo($"TitaniumCrab {MyPluginInfo.PLUGIN_VERSION} loaded — press {((KeyCode)CfgMenuKey.Value)} to toggle the menu");
        }

        /// <summary>Copy config values into the runtime fields.</summary>
        public void SyncFromConfig()
        {
            BunnyhopEnabled      = CfgBunnyhop.Value;
            AutoStrafeEnabled    = CfgAutoStrafe.Value;
            SpeedHackEnabled     = CfgSpeedHack.Value;
            SpeedMultiplier      = CfgSpeedMultiplier.Value;
            FlyEnabled           = CfgFly.Value;
            FlySpeed             = CfgFlySpeed.Value;
            NoclipEnabled        = CfgNoclip.Value;
            AirJumpEnabled       = CfgAirJump.Value;
            MegaJumpEnabled      = CfgMegaJump.Value;
            MegaJumpForce        = CfgMegaJumpForce.Value;
            NoFreezeEnabled      = CfgNoFreeze.Value;
            AntiBoundKillsEnabled= CfgAntiBoundKills.Value;
            StrongSprintEnabled  = CfgStrongSprint.Value;
            StrongSprintMultiplier = CfgStrongSprintMultiplier.Value;
            SlideJumpKey         = CfgSlideJumpKey.Value;

            GodModeEnabled       = CfgGodMode.Value;
            NoFallEnabled        = CfgNoFall.Value;
            AntiEnvKillEnabled   = CfgAntiEnvKill.Value;
            InfiniteAmmoEnabled  = CfgInfiniteAmmo.Value;
            AimbotEnabled        = CfgAimbot.Value;
            AimbotFOV            = CfgAimbotFOV.Value;
            AimbotSmooth         = CfgAimbotSmooth.Value;
            AutoSlapEnabled      = CfgAutoSlap.Value;
            SuperPunchEnabled    = CfgSuperPunch.Value;
            SuperPunchMultiplier = CfgSuperPunchMultiplier.Value;
            AntiPushEnabled      = CfgAntiPush.Value;

            EspEnabled           = CfgEsp.Value;
            FovModifierEnabled   = CfgFovModifier.Value;
            FovValue             = CfgFovValue.Value;
            FullbrightEnabled    = CfgFullbright.Value;
            NoCameraShakeEnabled = CfgNoCameraShake.Value;

            AntiAntiCheatEnabled = CfgAntiAntiCheat.Value;
        }

        /// <summary>Push runtime toggle state back into config so it persists.</summary>
        public void SyncToConfig()
        {
            CfgBunnyhop.Value      = BunnyhopEnabled;
            CfgAutoStrafe.Value    = AutoStrafeEnabled;
            CfgSpeedHack.Value     = SpeedHackEnabled;
            CfgSpeedMultiplier.Value = SpeedMultiplier;
            CfgFly.Value           = FlyEnabled;
            CfgFlySpeed.Value      = FlySpeed;
            CfgNoclip.Value        = NoclipEnabled;
            CfgAirJump.Value       = AirJumpEnabled;
            CfgMegaJump.Value      = MegaJumpEnabled;
            CfgMegaJumpForce.Value = MegaJumpForce;
            CfgNoFreeze.Value      = NoFreezeEnabled;
            CfgAntiBoundKills.Value= AntiBoundKillsEnabled;
            CfgStrongSprint.Value  = StrongSprintEnabled;
            CfgStrongSprintMultiplier.Value = StrongSprintMultiplier;
            CfgSlideJumpKey.Value  = SlideJumpKey;

            CfgGodMode.Value       = GodModeEnabled;
            CfgNoFall.Value        = NoFallEnabled;
            CfgAntiEnvKill.Value   = AntiEnvKillEnabled;
            CfgInfiniteAmmo.Value  = InfiniteAmmoEnabled;
            CfgAimbot.Value        = AimbotEnabled;
            CfgAimbotFOV.Value     = AimbotFOV;
            CfgAimbotSmooth.Value  = AimbotSmooth;
            CfgAutoSlap.Value      = AutoSlapEnabled;
            CfgSuperPunch.Value    = SuperPunchEnabled;
            CfgSuperPunchMultiplier.Value = SuperPunchMultiplier;
            CfgAntiPush.Value      = AntiPushEnabled;

            CfgEsp.Value           = EspEnabled;
            CfgFovModifier.Value   = FovModifierEnabled;
            CfgFovValue.Value      = FovValue;
            CfgFullbright.Value    = FullbrightEnabled;
            CfgNoCameraShake.Value = NoCameraShakeEnabled;

            CfgAntiAntiCheat.Value = AntiAntiCheatEnabled;
        }
    }
}
