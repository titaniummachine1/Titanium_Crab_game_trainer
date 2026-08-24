using HarmonyLib;
using SteamworksNative;
using System.Collections.Generic;
using UnityEngine;


namespace TitaniumCrab
{
    internal enum KeybindMode : byte { None = 0, Always = 1, Hold = 2, Toggle = 3, Release = 4 }

    internal class KeybindEntry
    {
        public string Feature;
        public KeyCode Key;
        public KeybindMode Mode;
        public bool ToggleState;
        public bool IsAction;
    }

    /// <summary>
    /// MonoBehaviour registered into Il2Cpp that renders an IMGUI trainer overlay
    /// and runs per-frame feature logic (fly, noclip, FOV, ESP, aimbot, auto-slap, etc.).
    /// </summary>
    public class TrainerMenu : MonoBehaviour
    {
        private Rect _menuRect = new(20f, 20f, 340f, 580f);
        private bool _menuVisible = true;
        private KeyCode _menuKey = KeyCode.Insert;

        private Vector2 _scrollPos = Vector2.zero;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private Texture2D _bgTexture;
        private Texture2D _headerTexture;

        // Cached kill height for Anti-Bound-Kills
        private float _killHeight = float.NaN;

        // Timer for delayed anti-cheat GameObject destruction
        private float _antiCheatTimer;

        // Keybind system
        private Dictionary<string, KeybindEntry> _keybinds = new();
        private string _pendingKeybindFeature;
        private KeybindMode _pendingKeybindMode;
        private bool _waitingForKeyPress;
        private Rect _keybindPopupRect = new(350, 100, 220, 180);

        // Menu tab state (0=Movement, 1=Combat, 2=Visual, 3=World, 4=Misc)
        private int _currentTab = 0;
        private string[] _tabNames = { "Movement", "Combat", "Visual", "World", "Misc" };

        // Glass/Ice destroy sub-menu state (0=none, 1=glass choosing, 2=ice choosing)
        private int _destroySubMenu = 0;

        // Saved position for Save/Restore Position feature
        private Vector3? _savedPosition = null;

        // Chat spammer timer
        private float _chatSpamTimer;

        public TrainerMenu(System.IntPtr ptr) : base(ptr) { }

        private void Awake()
        {
            _menuKey = (KeyCode)TitaniumCrabPlugin.Instance.CfgMenuKey.Value;
            _menuVisible = TitaniumCrabPlugin.Instance.MenuVisible;
            _antiCheatTimer = 30f;
            LoadKeybinds();
        }

        private void Update()
        {
            if (Input.GetKeyDown(_menuKey))
            {
                _menuVisible = !_menuVisible;
                TitaniumCrabPlugin.Instance.MenuVisible = _menuVisible;
            }

            ProcessKeybinds();

            RunFly();
            RunNoclip();
            RunFovModifier();
            RunAimbot();
            RunSpeedHack();
            RunAirJump();
            RunMegaJump();
            RunGodMode();
            RunInfiniteAmmo();
            RunAntiBoundKills();
            RunFullbright();
            RunStrongSprint();
            RunSlideJump();
            RunGravityToggle();
            RunPermaSlide();
            RunRapidfire();
            RunChatSpammer();

            // Delayed anti-cheat GameObject destruction (30s after load)
            if (_antiCheatTimer > 0f)
            {
                _antiCheatTimer -= Time.deltaTime;
                if (_antiCheatTimer <= 0f && TitaniumCrabPlugin.Instance.AntiAntiCheatEnabled)
                    Patches.DestroyAntiCheatObject();
            }
        }

        private void FixedUpdate()
        {
            // Auto-Strafe and Infinite Slap run in FixedUpdate so they
            // execute AFTER the game applies its own movement/punch forces
            RunAutoStrafe();
            RunAutoSlap();
            RunInfiniteSnowballs();
            RunNoThrowCooldown();
        }

        private void OnGUI()
        {
            if (TitaniumCrabPlugin.Instance.EspEnabled)
                EspRenderer.Draw();

            if (!_menuVisible)
                return;

            InitStyles();
            _menuRect = GUI.Window(9991, _menuRect, (GUI.WindowFunction)DrawMenuWindow, "");

            if (_pendingKeybindFeature != null)
                _keybindPopupRect = GUI.Window(9992, _keybindPopupRect, (GUI.WindowFunction)DrawKeybindPopup, "");
        }

        private void DrawKeybindPopup(int id)
        {
            GUILayout.Label($"Keybind for:", _labelStyle);
            GUILayout.Label(_pendingKeybindFeature, new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.4f, 0.6f, 0.9f) }
            });
            GUILayout.Space(4);

            if (_waitingForKeyPress)
            {
                GUILayout.Label("Press any key...", _labelStyle);
                GUILayout.Label("(ESC to cancel)", _labelStyle);

                foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(kc))
                    {
                        if (kc == KeyCode.Escape)
                        {
                            _pendingKeybindFeature = null;
                            _waitingForKeyPress = false;
                            return;
                        }
                        SetKeybind(_pendingKeybindFeature, kc, _pendingKeybindMode);
                        _pendingKeybindFeature = null;
                        _waitingForKeyPress = false;
                        return;
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Always On", GUILayout.Height(24)))
                {
                    SetKeybind(_pendingKeybindFeature, KeyCode.None, KeybindMode.Always);
                    _pendingKeybindFeature = null;
                }
                if (GUILayout.Button("Hold", GUILayout.Height(24)))
                {
                    _pendingKeybindMode = KeybindMode.Hold;
                    _waitingForKeyPress = true;
                }
                if (GUILayout.Button("Toggle", GUILayout.Height(24)))
                {
                    _pendingKeybindMode = KeybindMode.Toggle;
                    _waitingForKeyPress = true;
                }
                if (GUILayout.Button("On Release", GUILayout.Height(24)))
                {
                    _pendingKeybindMode = KeybindMode.Release;
                    _waitingForKeyPress = true;
                }
                if (GUILayout.Button("Remove Keybind", GUILayout.Height(24)))
                {
                    SetKeybind(_pendingKeybindFeature, KeyCode.None, KeybindMode.None);
                    _pendingKeybindFeature = null;
                }
                if (GUILayout.Button("Cancel", GUILayout.Height(24)))
                    _pendingKeybindFeature = null;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void InitStyles()
        {
            if (_bgTexture == null)
            {
                _bgTexture    = MakeTex(2, 2, new Color(0.06f, 0.06f, 0.10f, 0.98f));
                _headerTexture = MakeTex(2, 2, new Color(0.15f, 0.35f, 0.65f, 1f));
            }

            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.85f, 0.85f, 0.9f) }
                };
            }
        }

        private void DrawMenuWindow(int id)
        {
            var p = TitaniumCrabPlugin.Instance;

            // Header
            GUILayout.BeginVertical(MakeStyle(_headerTexture));
            GUILayout.Label($"TitaniumCrab v{MyPluginInfo.PLUGIN_VERSION}", _headerStyle);
            GUILayout.Label($"Press {_menuKey} to toggle", new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.75f) }
            });
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // Tab bar
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _tabNames.Length; i++)
            {
                bool isActive = (_currentTab == i);
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? new Color(0.3f, 0.6f, 1f) : new Color(0.2f, 0.2f, 0.25f);
                if (GUILayout.Button(_tabNames[i], GUILayout.Height(24)))
                    _currentTab = i;
                GUI.backgroundColor = oldBg;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(440));

            switch (_currentTab)
            {
                case 0: DrawMovementTab(p); break;
                case 1: DrawCombatTab(p); break;
                case 2: DrawVisualTab(p); break;
                case 3: DrawWorldTab(p); break;
                case 4: DrawMiscTab(p); break;
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 30));
        }

        private void DrawMovementTab(TitaniumCrabPlugin p)
        {
            p.BunnyhopEnabled      = ToggleRow("Bunnyhop",       p.BunnyhopEnabled);
            p.AutoStrafeEnabled    = ToggleRow("Auto-Strafe",    p.AutoStrafeEnabled);
            p.SpeedHackEnabled     = ToggleRow("Speed Hack",     p.SpeedHackEnabled);
            if (p.SpeedHackEnabled)
                p.SpeedMultiplier  = SliderRow("Speed x",        p.SpeedMultiplier, 0.5f, 20f);
            p.FlyEnabled           = ToggleRow("Fly Mode",       p.FlyEnabled);
            if (p.FlyEnabled)
                p.FlySpeed         = SliderRow("Fly Speed",      p.FlySpeed, 1f, 100f);
            p.NoclipEnabled        = ToggleRow("No-Clip",        p.NoclipEnabled);
            p.AirJumpEnabled       = ToggleRow("Air Jump",       p.AirJumpEnabled);
            p.MegaJumpEnabled      = ToggleRow("Mega Jump",      p.MegaJumpEnabled);
            if (p.MegaJumpEnabled)
                p.MegaJumpForce    = SliderRow("Jump Force",     p.MegaJumpForce, 5f, 100f);
            p.NoFreezeEnabled      = ToggleRow("No Freeze",      p.NoFreezeEnabled);
            p.AntiBoundKillsEnabled= ToggleRow("Anti-Bound Kills",p.AntiBoundKillsEnabled);
            p.StrongSprintEnabled  = ToggleRow("Strong Sprint",  p.StrongSprintEnabled);
            if (p.StrongSprintEnabled)
                p.StrongSprintMultiplier = SliderRow("Sprint x", p.StrongSprintMultiplier, 1f, 20f);
            p.SlideJumpEnabled     = ToggleRow("Slide Jump",     p.SlideJumpEnabled);
            p.GravityToggleEnabled = ToggleRow("Gravity Toggle", p.GravityToggleEnabled);
            p.PermaSlideEnabled    = ToggleRow("Perma Slide",    p.PermaSlideEnabled);
            p.BlinkEnabled         = ToggleRow("Blink",          p.BlinkEnabled);

            GUILayout.Space(4);
            SectionLabel("ACTIONS (right-click for keybind)");
            if (ButtonRow("Click TP"))
                DoClickTp();
            if (ButtonRow("Save Position"))
                DoSavePos();
            if (ButtonRow("Restore Position"))
                DoRestorePos();
        }

        private void DrawCombatTab(TitaniumCrabPlugin p)
        {
            p.GodModeEnabled       = ToggleRow("God Mode",       p.GodModeEnabled);
            p.NoFallEnabled        = ToggleRow("No Fall Damage", p.NoFallEnabled);
            p.AntiEnvKillEnabled   = ToggleRow("Anti Env Kill",  p.AntiEnvKillEnabled);
            p.InfiniteAmmoEnabled  = ToggleRow("Infinite Ammo",  p.InfiniteAmmoEnabled);
            p.InfiniteSnowballs    = ToggleRow("Infinite Snowballs", p.InfiniteSnowballs);
            p.NoThrowCooldown      = ToggleRow("No Throw Cooldown", p.NoThrowCooldown);
            p.AutoSlapEnabled      = ToggleRow("Infinite Slap",  p.AutoSlapEnabled);
            p.SuperPunchEnabled    = ToggleRow("Super Punch",    p.SuperPunchEnabled);
            if (p.SuperPunchEnabled)
                p.SuperPunchMultiplier = SliderRow("Knockback x", p.SuperPunchMultiplier, 0f, 50f);
            p.AntiPushEnabled      = ToggleRow("Anti Push",      p.AntiPushEnabled);
            p.NoRecoilEnabled      = ToggleRow("No Recoil",      p.NoRecoilEnabled);
            p.RapidfireEnabled     = ToggleRow("Rapidfire",      p.RapidfireEnabled);
            p.DisableTrapsEnabled  = ToggleRow("Disable Traps",  p.DisableTrapsEnabled);
            p.AntiTagEnabled       = ToggleRow("Anti Tag",       p.AntiTagEnabled);

            GUILayout.Space(4);
            SectionLabel("AIMBOT (right-click for keybind)");
            p.AimbotEnabled        = ToggleRow("Aimbot",         p.AimbotEnabled);
            if (p.AimbotEnabled)
            {
                p.AimbotSilent     = ToggleRow("Silent Aim",    p.AimbotSilent);
                p.AimbotProjectile = ToggleRow("Projectile Lead", p.AimbotProjectile);
                p.AimbotFOV        = SliderRow("Aimbot FOV",    p.AimbotFOV, 1f, 180f);
                p.AimbotSmooth     = SliderRow("Smoothing",     p.AimbotSmooth, 1f, 30f);
            }
        }

        private void DrawVisualTab(TitaniumCrabPlugin p)
        {
            p.EspEnabled           = ToggleRow("Player ESP",     p.EspEnabled);
            p.FovModifierEnabled   = ToggleRow("FOV Modifier",   p.FovModifierEnabled);
            if (p.FovModifierEnabled)
                p.FovValue         = SliderRow("FOV",            p.FovValue, 20f, 170f);
            p.FullbrightEnabled    = ToggleRow("Fullbright",     p.FullbrightEnabled);
            p.NoCameraShakeEnabled = ToggleRow("No Camera Shake",p.NoCameraShakeEnabled);
        }

        private void DrawWorldTab(TitaniumCrabPlugin p)
        {
            if (_destroySubMenu == 1)
            {
                SectionLabel("DESTROY GLASS");
                if (GUILayout.Button("All Glass", GUILayout.Height(28)))
                {
                    BreakAllGlass(weakOnly: false);
                    _destroySubMenu = 0;
                }
                if (GUILayout.Button("Weak Glass Only", GUILayout.Height(28)))
                {
                    BreakAllGlass(weakOnly: true);
                    _destroySubMenu = 0;
                }
                if (GUILayout.Button("Cancel", GUILayout.Height(24)))
                    _destroySubMenu = 0;
            }
            else if (_destroySubMenu == 2)
            {
                SectionLabel("DESTROY ICE");
                if (GUILayout.Button("All Ice", GUILayout.Height(28)))
                {
                    BreakAllIce(weakOnly: false);
                    _destroySubMenu = 0;
                }
                if (GUILayout.Button("Weak Ice Only", GUILayout.Height(28)))
                {
                    BreakAllIce(weakOnly: true);
                    _destroySubMenu = 0;
                }
                if (GUILayout.Button("Cancel", GUILayout.Height(24)))
                    _destroySubMenu = 0;
            }
            else
            {
                SectionLabel("WORLD ACTIONS (right-click for keybind)");
                if (ButtonRow("Destroy Glass"))
                    _destroySubMenu = 1;
                if (ButtonRow("Destroy Ice"))
                    _destroySubMenu = 2;
            }
        }

        private void DrawMiscTab(TitaniumCrabPlugin p)
        {
            p.AntiAntiCheatEnabled = ToggleRow("Anti-AntiCheat", p.AntiAntiCheatEnabled);
            p.ChatSpammerEnabled   = ToggleRow("Chat Spammer",   p.ChatSpammerEnabled);
            if (p.ChatSpammerEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Spam Text:", _labelStyle, GUILayout.Width(80));
                p.ChatSpammerText = GUILayout.TextField(p.ChatSpammerText, GUILayout.Width(200));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);

            if (GUILayout.Button("Save Settings to Config", GUILayout.Height(28)))
            {
                p.SyncToConfig();
                p.Config.Save();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("Reload from Config", GUILayout.Height(24)))
                p.SyncFromConfig();
        }

        private void SectionLabel(string text)
        {
            GUILayout.Space(2);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.4f, 0.6f, 0.9f) }
            };
            GUILayout.Label($"--- {text} ---", style);
            GUILayout.Space(1);
        }

        // =====================================================================
        //  Keybind system — right-click any toggle/button to assign a keybind
        // =====================================================================

        private static readonly HashSet<string> ActionFeatures = new()
        {
            "Click TP", "Save Position", "Restore Position", "Slide Jump",
            "Destroy Glass", "Destroy Ice"
        };

        private bool IsActionFeature(string feature) => ActionFeatures.Contains(feature);

        private void LoadKeybinds()
        {
            _keybinds.Clear();
            string raw = TitaniumCrabPlugin.Instance.CfgKeybinds?.Value;
            if (string.IsNullOrEmpty(raw))
                return;

            foreach (string part in raw.Split(';'))
            {
                string[] tokens = part.Split(':');
                if (tokens.Length < 3) continue;
                if (!byte.TryParse(tokens[1], out byte modeByte)) continue;
                if (!int.TryParse(tokens[2], out int keyCode)) continue;

                string feature = tokens[0];
                _keybinds[feature] = new KeybindEntry
                {
                    Feature = feature,
                    Mode = (KeybindMode)modeByte,
                    Key = (KeyCode)keyCode,
                    IsAction = IsActionFeature(feature)
                };
            }
        }

        private void SaveKeybinds()
        {
            var parts = new List<string>();
            foreach (var entry in _keybinds.Values)
            {
                if (entry.Mode != KeybindMode.None)
                    parts.Add($"{entry.Feature}:{(int)entry.Mode}:{(int)entry.Key}");
            }
            TitaniumCrabPlugin.Instance.CfgKeybinds.Value = string.Join(";", parts);
        }

        private void SetKeybind(string feature, KeyCode key, KeybindMode mode)
        {
            if (!_keybinds.ContainsKey(feature))
                _keybinds[feature] = new KeybindEntry { Feature = feature };
            var entry = _keybinds[feature];
            entry.Key = key;
            entry.Mode = mode;
            entry.ToggleState = false;
            entry.IsAction = IsActionFeature(feature);
            SaveKeybinds();
        }

        private string GetKeybindText(string feature)
        {
            if (_keybinds.TryGetValue(feature, out var kb) && kb.Mode != KeybindMode.None)
            {
                if (kb.Mode == KeybindMode.Always)
                    return " [Always]";
                return $" [{kb.Mode}: {kb.Key}]";
            }
            return "";
        }

        private void ProcessKeybinds()
        {
            foreach (var entry in _keybinds.Values)
            {
                if (entry.Mode == KeybindMode.None) continue;

                if (entry.IsAction)
                {
                    bool trigger = entry.Mode == KeybindMode.Release
                        ? Input.GetKeyUp(entry.Key)
                        : Input.GetKeyDown(entry.Key);
                    if (trigger)
                        TriggerAction(entry.Feature);
                    continue;
                }

                switch (entry.Mode)
                {
                    case KeybindMode.Always:
                        ApplyFeature(entry.Feature, true);
                        break;
                    case KeybindMode.Hold:
                        ApplyFeature(entry.Feature, Input.GetKey(entry.Key));
                        break;
                    case KeybindMode.Toggle:
                        if (Input.GetKeyDown(entry.Key))
                            entry.ToggleState = !entry.ToggleState;
                        ApplyFeature(entry.Feature, entry.ToggleState);
                        break;
                    case KeybindMode.Release:
                        if (Input.GetKeyUp(entry.Key))
                            entry.ToggleState = !entry.ToggleState;
                        ApplyFeature(entry.Feature, entry.ToggleState);
                        break;
                }
            }
        }

        private void ApplyFeature(string feature, bool value)
        {
            var p = TitaniumCrabPlugin.Instance;
            switch (feature)
            {
                case "Bunnyhop":          p.BunnyhopEnabled = value; break;
                case "Auto-Strafe":       p.AutoStrafeEnabled = value; break;
                case "Speed Hack":        p.SpeedHackEnabled = value; break;
                case "Fly Mode":          p.FlyEnabled = value; break;
                case "No-Clip":           p.NoclipEnabled = value; break;
                case "Air Jump":          p.AirJumpEnabled = value; break;
                case "Mega Jump":         p.MegaJumpEnabled = value; break;
                case "No Freeze":         p.NoFreezeEnabled = value; break;
                case "Anti-Bound Kills":  p.AntiBoundKillsEnabled = value; break;
                case "Strong Sprint":     p.StrongSprintEnabled = value; break;
                case "Slide Jump":        p.SlideJumpEnabled = value; break;
                case "Gravity Toggle":    p.GravityToggleEnabled = value; break;
                case "Perma Slide":       p.PermaSlideEnabled = value; break;
                case "Blink":             p.BlinkEnabled = value; break;
                case "God Mode":          p.GodModeEnabled = value; break;
                case "No Fall Damage":    p.NoFallEnabled = value; break;
                case "Anti Env Kill":     p.AntiEnvKillEnabled = value; break;
                case "Infinite Ammo":     p.InfiniteAmmoEnabled = value; break;
                case "Infinite Snowballs":p.InfiniteSnowballs = value; break;
                case "No Throw Cooldown": p.NoThrowCooldown = value; break;
                case "Infinite Slap":     p.AutoSlapEnabled = value; break;
                case "Super Punch":       p.SuperPunchEnabled = value; break;
                case "Anti Push":         p.AntiPushEnabled = value; break;
                case "No Recoil":         p.NoRecoilEnabled = value; break;
                case "Rapidfire":         p.RapidfireEnabled = value; break;
                case "Disable Traps":     p.DisableTrapsEnabled = value; break;
                case "Anti Tag":          p.AntiTagEnabled = value; break;
                case "Aimbot":            p.AimbotEnabled = value; break;
                case "Silent Aim":        p.AimbotSilent = value; break;
                case "Projectile Lead":   p.AimbotProjectile = value; break;
                case "Player ESP":        p.EspEnabled = value; break;
                case "FOV Modifier":      p.FovModifierEnabled = value; break;
                case "Fullbright":        p.FullbrightEnabled = value; break;
                case "No Camera Shake":   p.NoCameraShakeEnabled = value; break;
                case "Anti-AntiCheat":    p.AntiAntiCheatEnabled = value; break;
                case "Chat Spammer":      p.ChatSpammerEnabled = value; break;
            }
        }

        private void TriggerAction(string feature)
        {
            switch (feature)
            {
                case "Click TP":          DoClickTp(); break;
                case "Save Position":     DoSavePos(); break;
                case "Restore Position":  DoRestorePos(); break;
                case "Destroy Glass":     BreakAllGlass(false); break;
                case "Destroy Ice":       BreakAllIce(false); break;
            }
        }

        // =====================================================================
        //  UI helpers — ToggleRow and ButtonRow with right-click keybind
        // =====================================================================

        private bool ToggleRow(string label, bool value)
        {
            string kbText = GetKeybindText(label);
            var oldColor = GUI.color;
            GUI.color = value ? Color.green : new Color(1f, 0.4f, 0.4f);
            bool newValue = GUILayout.Toggle(value, $"{label}{kbText}{(value ? "  ON" : "  OFF")}",
                "button", GUILayout.Height(22));
            GUI.color = oldColor;

            Rect rect = GUILayoutUtility.GetLastRect();
            if (Event.current != null && Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                _pendingKeybindFeature = label;
                _waitingForKeyPress = false;
                Event.current.Use();
            }
            return newValue;
        }

        private bool ButtonRow(string label)
        {
            string kbText = GetKeybindText(label);
            bool clicked = GUILayout.Button($"{label}{kbText}", GUILayout.Height(26));

            Rect rect = GUILayoutUtility.GetLastRect();
            if (Event.current != null && Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                _pendingKeybindFeature = label;
                _waitingForKeyPress = false;
                Event.current.Use();
            }
            return clicked;
        }

        private float SliderRow(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:F1}", _labelStyle, GUILayout.Width(150));
            float result = GUILayout.HorizontalSlider(value, min, max, GUILayout.Height(22));
            GUILayout.EndHorizontal();
            return result;
        }

        // =====================================================================
        //  Per-frame feature implementations
        // =====================================================================

        private void RunFly()
        {
            var p = TitaniumCrabPlugin.Instance;
            if (!p.FlyEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            Rigidbody rb = pm.GetRb();
            if (rb == null)
                return;

            rb.useGravity = false;
            rb.drag = 5f;

            Vector3 vel = Vector3.zero;
            Transform cam = Camera.main != null ? Camera.main.transform : pm.transform;

            float speed = p.FlySpeed;
            if (Input.GetKey(KeyCode.W)) vel += cam.forward * speed;
            if (Input.GetKey(KeyCode.S)) vel -= cam.forward * speed;
            if (Input.GetKey(KeyCode.A)) vel -= cam.right * speed;
            if (Input.GetKey(KeyCode.D)) vel += cam.right * speed;
            if (Input.GetKey((KeyCode)InputManager.jump))   vel += Vector3.up * speed;
            if (Input.GetKey((KeyCode)InputManager.crouch)) vel -= Vector3.up * speed;

            rb.velocity = vel;
        }

        private void RunNoclip()
        {
            if (!TitaniumCrabPlugin.Instance.NoclipEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            foreach (Collider c in pm.GetComponentsInChildren<Collider>())
                c.enabled = false;
        }

        private void RunFovModifier()
        {
            var p = TitaniumCrabPlugin.Instance;
            Camera cam = Camera.main;
            if (cam == null)
                return;

            if (p.FovModifierEnabled && Mathf.Abs(cam.fieldOfView - p.FovValue) > 0.1f)
                cam.fieldOfView = p.FovValue;
        }

        private void RunSpeedHack()
        {
            var p = TitaniumCrabPlugin.Instance;
            if (!p.SpeedHackEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            Rigidbody rb = pm.GetRb();
            if (rb == null)
                return;

            Vector3 vel = rb.velocity;
            Vector3 horizontal = new(vel.x, 0f, vel.z);
            if (horizontal.sqrMagnitude > 0.01f)
            {
                float targetMag = Mathf.Min(horizontal.magnitude * p.SpeedMultiplier, 60f);
                Vector3 dir = horizontal.normalized;
                rb.velocity = new Vector3(dir.x * targetMag, vel.y, dir.z * targetMag);
            }
        }

        /// <summary>
        /// Find the best target within FOV. Returns the target PlayerManager or null.
        /// </summary>
        private PlayerManager FindAimbotTarget(Camera cam, ulong myId, float fov)
        {
            PlayerManager best = null;
            float bestAngle = fov;
            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;

            var active = GameManager.Instance?.activePlayers;
            if (active == null)
                return null;

            foreach (var entry in active)
            {
                PlayerManager pm = entry.Value;
                if (pm == null || pm.steamProfile.m_SteamID == myId)
                    continue;

                if (pm.GetComponent<PlayerRagdoll>() != null)
                    continue;

                // Use head transform if available, otherwise estimate
                Vector3 headPos = pm.transform.position + Vector3.up * 1.5f;
                Vector3 dir = (headPos - camPos).normalized;
                float angle = Vector3.Angle(camFwd, dir);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = pm;
                }
            }

            return best;
        }

        /// <summary>
        /// Check if the current weapon is a projectile (snowball/throwable).
        /// Returns the projectile speed if it is, 0 if it's a hitscan gun.
        /// Uses reflection because currentItem/throwPrefab/snowSpeed aren't
        /// exposed in the IL2CPP interop assembly.
        /// </summary>
        private float GetProjectileSpeed()
        {
            try
            {
                ItemManager im = ItemManager.Instance;
                if (im == null)
                    return 0f;

                var currentItemField = AccessTools.Field(im.GetType(), "currentItem");
                if (currentItemField == null)
                    return 0f;

                var currentItem = currentItemField.GetValue(im);
                if (currentItem == null)
                    return 0f;

                var itemType = currentItem.GetType();

                // Check if it has a throwPrefab (throwable item like snowball)
                var throwPrefabField = AccessTools.Field(itemType, "throwPrefab");
                if (throwPrefabField == null || throwPrefabField.GetValue(currentItem) == null)
                    return 0f; // It's a gun (hitscan)

                // Get snowSpeed for projectile prediction
                var snowSpeedField = AccessTools.Field(itemType, "snowSpeed");
                if (snowSpeedField != null)
                    return System.Convert.ToSingle(snowSpeedField.GetValue(currentItem));

                return 30f; // Default snowball speed if field not found
            }
            catch { return 0f; }
        }

        /// <summary>
        /// Calculate predicted target position for projectile leading.
        /// Estimates where the target will be when the projectile arrives.
        /// </summary>
        private Vector3 PredictTargetPosition(Vector3 targetPos, Vector3 targetVel, Vector3 sourcePos, float projSpeed)
        {
            float distance = Vector3.Distance(sourcePos, targetPos);
            float travelTime = distance / projSpeed;

            // Predict where target will be after travel time
            return targetPos + targetVel * travelTime;
        }

        /// <summary>
        /// Get the target's velocity for projectile leading.
        /// </summary>
        private Vector3 GetTargetVelocity(PlayerManager pm)
        {
            Rigidbody rb = pm.GetComponent<Rigidbody>();
            if (rb != null)
                return rb.velocity;

            // Fall back to PlayerMovement's rigidbody
            PlayerMovement move = pm.GetComponent<PlayerMovement>();
            if (move != null)
            {
                Rigidbody moveRb = move.GetRb();
                if (moveRb != null)
                    return moveRb.velocity;
            }

            return Vector3.zero;
        }

        private void RunAimbot()
        {
            var p = TitaniumCrabPlugin.Instance;
            Patches.SilentAimTarget = null; // Clear by default

            // AimbotEnabled is controlled by the keybind system or manual toggle
            if (!p.AimbotEnabled)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            PlayerManager localPm = GetLocalPlayerManager();
            if (localPm == null)
                return;

            ulong myId = localPm.steamProfile.m_SteamID;

            PlayerManager best = FindAimbotTarget(cam, myId, p.AimbotFOV);
            if (best == null)
                return;

            Vector3 camPos = cam.transform.position;
            Vector3 targetPos = best.transform.position + Vector3.up * 1.5f;

            // Projectile prediction for throwables (snowballs)
            float projSpeed = GetProjectileSpeed();
            if (p.AimbotProjectile && projSpeed > 0f)
            {
                Vector3 targetVel = GetTargetVelocity(best);
                targetPos = PredictTargetPosition(targetPos, targetVel, camPos, projSpeed);
            }

            // Silent aim: don't rotate camera, just set the target for patches
            if (p.AimbotSilent)
            {
                Patches.SilentAimTarget = targetPos;
                return;
            }

            // Normal aimbot: rotate camera toward target
            Vector3 targetDir = (targetPos - camPos).normalized;
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot,
                Time.deltaTime * p.AimbotSmooth);
        }

        private void RunAirJump()
        {
            if (!TitaniumCrabPlugin.Instance.AirJumpEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            if (Input.GetKeyDown((KeyCode)InputManager.jump))
            {
                Rigidbody rb = pm.GetRb();
                if (rb != null && !IsGrounded(pm))
                {
                    Vector3 vel = rb.velocity;
                    vel.y = Mathf.Max(vel.y, 7f);
                    rb.velocity = vel;
                }
            }
        }

        private void RunMegaJump()
        {
            var p = TitaniumCrabPlugin.Instance;
            if (!p.MegaJumpEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            if (Input.GetKeyDown((KeyCode)InputManager.jump))
            {
                Rigidbody rb = pm.GetRb();
                if (rb != null && IsGrounded(pm))
                {
                    Vector3 vel = rb.velocity;
                    vel.y = p.MegaJumpForce;
                    rb.velocity = vel;
                }
            }
        }

        private void RunGodMode()
        {
            if (!TitaniumCrabPlugin.Instance.GodModeEnabled)
                return;

            // Set currentHp to 100 every frame as ObscuredInt (ACTk)
            // This is the same approach as CrabCheat's GodModeModule
            try
            {
                PlayerStatus.Instance.currentHp = new CodeStage.AntiCheat.ObscuredTypes.ObscuredInt(100);
            }
            catch { /* not in game yet */ }
        }

        private void RunInfiniteAmmo()
        {
            if (!TitaniumCrabPlugin.Instance.InfiniteAmmoEnabled)
                return;

            PlayerInput pi = PlayerInput.Instance;
            if (pi == null || pi.playerInventory == null)
                return;

            var currentItem = pi.playerInventory.currentItem;
            if (currentItem == null)
                return;

            Gun gun = currentItem.GetComponent<Gun>();
            if (gun == null)
                return;

            try
            {
                var ammoField = AccessTools.Field(gun.GetType(), "currentAmmo")
                             ?? AccessTools.Field(gun.GetType(), "ammoInMag")
                             ?? AccessTools.Field(gun.GetType(), "ammo");
                var maxField  = AccessTools.Field(gun.GetType(), "magSize")
                             ?? AccessTools.Field(gun.GetType(), "clipSize")
                             ?? AccessTools.Field(gun.GetType(), "maxAmmo");
                if (ammoField != null && maxField != null)
                    ammoField.SetValue(gun, maxField.GetValue(gun));
            }
            catch { /* field names may differ */ }
        }

        /// <summary>
        /// Auto-Strafe (accurate walk): gives perfect control over movement.
        /// - When holding WASD: smoothly rotates velocity vector toward desired
        ///   direction with high acceleration (like Minecraft accurate walk)
        /// - When no input: kills horizontal velocity immediately (no sliding)
        /// - Preserves normal speed, just adds precise directional control
        /// Runs in FixedUpdate so it overrides the game's velocity AFTER
        /// the game applies its own movement forces.
        /// </summary>
        private void RunAutoStrafe()
        {
            if (!TitaniumCrabPlugin.Instance.AutoStrafeEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            Rigidbody rb = pm.GetRb();
            if (rb == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            // Get desired movement direction from WASD relative to camera
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

            Vector3 vel = rb.velocity;
            Vector3 horizontal = new(vel.x, 0f, vel.z);
            float currentSpeed = horizontal.magnitude;

            if (desiredDir.sqrMagnitude > 0.01f)
            {
                // Player is giving input — steer velocity toward desired direction
                desiredDir.Normalize();

                if (currentSpeed > 0.1f)
                {
                    // Rotate velocity vector toward desired direction
                    // High lerp = fast response (~2-3 frames to turn 90deg)
                    float steerSpeed = 12f * Time.fixedDeltaTime;
                    Vector3 currentDir = horizontal.normalized;
                    Vector3 newDir = Vector3.Lerp(currentDir, desiredDir, steerSpeed);

                    rb.velocity = new Vector3(newDir.x * currentSpeed, vel.y, newDir.z * currentSpeed);
                }
            }
            else
            {
                // No input — kill horizontal velocity immediately (infinite friction)
                // Only do this when grounded so air movement isn't affected
                if (IsGrounded(pm) && currentSpeed > 0.1f)
                {
                    float stopSpeed = currentSpeed * (1f - 20f * Time.fixedDeltaTime);
                    stopSpeed = Mathf.Max(0f, stopSpeed);
                    Vector3 stopDir = horizontal.normalized;
                    rb.velocity = new Vector3(stopDir.x * stopSpeed, vel.y, stopDir.z * stopSpeed);
                }
            }
        }

        /// <summary>
        /// Infinite Slap: when M1 (primary fire) is held, continuously call
        /// Punch() with no cooldown. Sets the cooldown timer above threshold
        /// and calls the Punch method directly every physics tick.
        /// </summary>
        private void RunAutoSlap()
        {
            if (!TitaniumCrabPlugin.Instance.AutoSlapEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            PunchPlayers punch = pm.punchPlayers;
            if (punch == null)
                return;

            // Always reset cooldown so punch is always ready
            punch.field_Private_Single_0 = 3.1f;

            // Only trigger punch when M1 (left mouse) is held
            if (Input.GetKey(KeyCode.Mouse0))
            {
                punch.field_Private_Boolean_0 = true;
                // Call the Punch method directly to trigger the slap
                punch.Punch();
            }
        }

        /// <summary>
        /// Infinite Snowballs: keep the player's snowball ammo at max every tick.
        /// PlayerStatus has currentAmmo and maxAmmo fields. When holding snowballs,
        /// this refills the ammo so you never run out.
        /// </summary>
        private void RunInfiniteSnowballs()
        {
            if (!TitaniumCrabPlugin.Instance.InfiniteSnowballs)
                return;

            try
            {
                PlayerStatus ps = PlayerStatus.Instance;
                if (ps == null)
                    return;

                var type = ps.GetType();
                var curField = AccessTools.Field(type, "currentAmmo");
                var maxField = AccessTools.Field(type, "maxAmmo");
                if (curField == null || maxField == null)
                    return;

                int max = System.Convert.ToInt32(maxField.GetValue(ps));
                int cur = System.Convert.ToInt32(curField.GetValue(ps));
                if (max > 0 && cur < max)
                    curField.SetValue(ps, max);
            }
            catch { /* not in game yet */ }
        }

        /// <summary>
        /// No Throw Cooldown: reset the throw cooldown every tick so you can
        /// machine-gun snowballs. The cooldown field is on the item component.
        /// </summary>
        private void RunNoThrowCooldown()
        {
            if (!TitaniumCrabPlugin.Instance.NoThrowCooldown)
                return;

            try
            {
                ItemManager im = ItemManager.Instance;
                if (im == null)
                    return;

                // Use reflection to get currentItem
                var currentItemField = AccessTools.Field(im.GetType(), "currentItem");
                if (currentItemField == null)
                    return;

                var currentItem = currentItemField.GetValue(im);
                if (currentItem == null)
                    return;

                // Check if it's a throwable (has throwPrefab)
                var itemType = currentItem.GetType();
                var throwPrefabField = AccessTools.Field(itemType, "throwPrefab");
                if (throwPrefabField == null || throwPrefabField.GetValue(currentItem) == null)
                    return; // Not a throwable

                // Find ItemPrefab component on the player and reset cooldown
                PlayerMovement pm = GetLocalPlayerMovement();
                if (pm == null)
                    return;

                var itemPrefab = pm.GetComponentInChildren<ItemPrefab>();
                if (itemPrefab != null)
                {
                    var type = itemPrefab.GetType();
                    var cooldownField = AccessTools.Field(type, "cooldown")
                                      ?? AccessTools.Field(type, "field_Private_Single_0")
                                      ?? AccessTools.Field(type, "field_Private_Single_1");
                    if (cooldownField != null)
                        cooldownField.SetValue(itemPrefab, 0f);
                }
            }
            catch { /* not in game yet */ }
        }

        /// <summary>
        /// Anti Bound Kills: float above the map's kill height so water/lava/
        /// out-of-bounds zones don't kill you.
        /// </summary>
        private void RunAntiBoundKills()
        {
            if (!TitaniumCrabPlugin.Instance.AntiBoundKillsEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            // Cache the kill height from the map's KillPlayerOutOfBounds component
            if (float.IsNaN(_killHeight))
            {
                var killBounds = UnityEngine.Object.FindObjectOfType<KillPlayerOutOfBounds>();
                if (killBounds == null)
                    return;

                var khField = AccessTools.Field(killBounds.GetType(), "killHeight");
                if (khField != null)
                    _killHeight = (float)khField.GetValue(killBounds);
                else
                    return;
            }

            Rigidbody rb = pm.GetRb();
            if (rb == null)
                return;

            Vector3 pos = rb.position;
            if (pos.y < _killHeight + 2f)
            {
                // Remove downward velocity so we slide instead of bouncing
                rb.velocity = Vector3.Exclude(Vector3.up, rb.velocity);
                pos.y = _killHeight + 2f;
                rb.position = pos;
            }
        }

        /// <summary>
        /// Fullbright: max out all scene lights so dark areas (Dorm mode) are visible.
        /// </summary>
        private void RunFullbright()
        {
            if (!TitaniumCrabPlugin.Instance.FullbrightEnabled)
                return;

            foreach (Light light in UnityEngine.Object.FindObjectsOfType<Light>())
            {
                light.intensity = 3f;
                light.range = 100f;
            }
        }

        /// <summary>
        /// Strong Sprint: once you start sprinting, continuously re-enable
        /// sprint every frame so nothing can stop it (stamina, game modes,
        /// knockback, etc.). Also multiplies the max run speed by the
        /// configured multiplier so you sprint faster than normal.
        /// </summary>
        private void RunStrongSprint()
        {
            var p = TitaniumCrabPlugin.Instance;
            if (!p.StrongSprintEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            // Force sprint to stay on every frame — even if the game tries to
            // disable it (stamina drain, game mode rules, etc.)
            pm.SetSprinting(true);

            // Multiply the game's default max run speed (13) by our multiplier
            // Default max run speed is 13, default max walk speed is 6.5
            pm.SetMaxRunSpeed(13f * p.StrongSprintMultiplier);
            pm.SetMaxSpeed(6.5f * p.StrongSprintMultiplier);
        }

        /// <summary>
        /// Slide Jump: when the configured key is held, simultaneously trigger
        /// jump + crouch to launch the player in the camera's facing direction.
        /// This is the "crouch jump" mechanic from Crab Game — pressing jump
        /// and crouch at the same time launches you forward/upward.
        /// We apply a forward boost based on camera direction + upward velocity.
        /// </summary>
        private void RunSlideJump()
        {
            if (!TitaniumCrabPlugin.Instance.SlideJumpEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            Rigidbody rb = pm.GetRb();
            if (rb == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            // Get camera forward direction (flattened to horizontal)
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            fwd.Normalize();

            // Apply launch: strong forward boost + upward boost
            // This mimics the slide-jump/crouch-jump mechanic
            float launchForce = 15f;
            float upForce = 8f;

            Vector3 vel = rb.velocity;
            // Only boost if we're moving slowly enough (prevents infinite stacking)
            float horizontalSpeed = new Vector2(vel.x, vel.z).magnitude;
            if (horizontalSpeed < 25f)
            {
                vel.x += fwd.x * launchForce * Time.deltaTime * 10f;
                vel.z += fwd.z * launchForce * Time.deltaTime * 10f;
            }
            // Only boost upward if we're near ground or moving up slowly
            if (vel.y < 5f && IsGrounded(pm))
            {
                vel.y = upForce;
            }

            rb.velocity = vel;
        }

        /// <summary>
        /// Click TP: press T to teleport to where you're looking (raycast hit).
        /// Adjusts teleport position based on:
        /// - Surface normal: offsets by half player height along the normal
        ///   so you don't clip into the surface
        /// - Pitch: if looking up (positive pitch), uses top of hitbox as pivot
        ///   so you don't teleport above the ceiling and fall back
        /// </summary>
        private void DoClickTp()
        {
            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            Rigidbody rb = pm.GetRb();
            if (rb == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            // Raycast from camera to find hit point + normal
            Vector3 origin = cam.transform.position;
            Vector3 direction = cam.transform.forward;

            // Use a generous max distance and hit everything
            int layerMask = ~0; // all layers
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, 500f, layerMask))
                return;

            // Player collision box dimensions (approximate)
            float playerHeight = 2f;  // total height
            float playerRadius = 0.4f; // half-width

            // Get the surface normal of what we hit
            Vector3 normal = hit.normal.normalized;

            // Calculate the teleport position
            // Offset along the normal by half player height so we don't clip
            Vector3 targetPos = hit.point + normal * (playerHeight * 0.5f);

            // If looking up (positive pitch), we're likely hitting a ceiling
            // or high wall — use the bottom of the hitbox as pivot instead
            // so we teleport below the surface, not above it
            float pitch = cam.transform.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f; // normalize to -180..180

            if (pitch > 10f) // looking up
            {
                // Pivot from bottom of hitbox — teleport so our feet are at hit point
                targetPos = hit.point + normal * 0.1f;
                // But still offset along normal slightly to avoid clipping
                targetPos -= Vector3.up * (playerHeight * 0.5f);
            }

            // Also offset horizontally based on normal direction
            // If we hit a wall (normal is mostly horizontal), push us back from wall
            if (Mathf.Abs(normal.y) < 0.5f)
            {
                // Wall hit — offset by player radius along normal
                targetPos += normal * playerRadius;
            }

            // Teleport the player
            rb.position = targetPos;
            rb.velocity = Vector3.zero; // kill momentum to prevent glitching

            TitaniumCrabPlugin.Instance.Log.LogInfo($"ClickTP: teleported to {targetPos}");
        }

        // =====================================================================
        //  New movement/combat/misc features
        // =====================================================================

        /// <summary>
        /// Gravity Toggle: remove gravity from the player's rigidbody.
        /// </summary>
        private void RunGravityToggle()
        {
            if (!TitaniumCrabPlugin.Instance.GravityToggleEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            Rigidbody rb = pm.GetRb();
            if (rb == null)
                return;

            rb.useGravity = false;
            // Add slight drag so player doesn't drift forever
            if (rb.drag < 2f)
                rb.drag = 2f;
        }

        /// <summary>
        /// Perma Slide: force the player into sliding state.
        /// Uses reflection to set the crouch/slide state on PlayerMovement.
        /// </summary>
        private void RunPermaSlide()
        {
            if (!TitaniumCrabPlugin.Instance.PermaSlideEnabled)
                return;

            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null)
                return;

            // PlayerMovement has a crouch state enum — force it to sliding
            // The CrouchState enum has values like Standing, Crouching, Sliding
            // We try to set it via reflection
            try
            {
                var type = pm.GetType();
                // Try to find the crouch state field
                var stateField = AccessTools.Field(type, "crouchState")
                               ?? AccessTools.Field(type, "field_Public_EnumNPrivateSealedvaNoCrSl4vUnique_0");
                if (stateField != null)
                {
                    // Enum values: 0=Standing, 1=Crouching, 2=Sliding (typically)
                    // Try setting to 2 (Sliding)
                    stateField.SetValue(pm, System.Enum.ToObject(stateField.FieldType, 2));
                }
            }
            catch { /* not in game or field not found */ }
        }

        /// <summary>
        /// Save current position for later restoration.
        /// </summary>
        private void DoSavePos()
        {
            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null) return;
            Rigidbody rb = pm.GetRb();
            if (rb == null) return;
            _savedPosition = rb.position;
            TitaniumCrabPlugin.Instance.Log.LogInfo($"Position saved: {_savedPosition}");
        }

        /// <summary>
        /// Teleport to previously saved position.
        /// </summary>
        private void DoRestorePos()
        {
            if (!_savedPosition.HasValue) return;
            PlayerMovement pm = GetLocalPlayerMovement();
            if (pm == null) return;
            Rigidbody rb = pm.GetRb();
            if (rb == null) return;
            rb.position = _savedPosition.Value;
            rb.velocity = Vector3.zero;
            TitaniumCrabPlugin.Instance.Log.LogInfo($"Position restored: {_savedPosition}");
        }

        /// <summary>
        /// Rapidfire: auto-fire guns while holding M1.
        /// Calls ShootGun repeatedly while left mouse is held.
        /// </summary>
        private void RunRapidfire()
        {
            if (!TitaniumCrabPlugin.Instance.RapidfireEnabled)
                return;

            if (!Input.GetMouseButton(0))
                return;

            try
            {
                // Call ClientSend.ShootGun with current camera angles
                Camera cam = Camera.main;
                if (cam == null)
                    return;

                Vector2 angles = new Vector2(
                    -cam.transform.eulerAngles.x,
                    cam.transform.eulerAngles.y
                );

                // Use reflection since ShootGun isn't exposed in interop
                var shootMethod = AccessTools.Method(typeof(ClientSend), "ShootGun");
                if (shootMethod != null)
                    shootMethod.Invoke(null, new object[] { angles });
            }
            catch { /* not in game or can't shoot */ }
        }

        /// <summary>
        /// Chat Spammer: auto-send chat messages at regular intervals.
        /// Uses reflection to call ClientSend.SendChatMessage.
        /// </summary>
        private void RunChatSpammer()
        {
            if (!TitaniumCrabPlugin.Instance.ChatSpammerEnabled)
                return;

            _chatSpamTimer -= Time.deltaTime;
            if (_chatSpamTimer > 0f)
                return;

            _chatSpamTimer = 1.5f; // spam every 1.5 seconds

            try
            {
                var sendMethod = AccessTools.Method(typeof(ClientSend), "SendChatMessage");
                if (sendMethod != null)
                    sendMethod.Invoke(null, new object[] { TitaniumCrabPlugin.Instance.ChatSpammerText });
            }
            catch { /* not in game or chat not available */ }
        }

        // =====================================================================
        //  World actions (one-shot buttons)
        // =====================================================================

        /// <summary>
        /// Break all glass panes on Glass Jump maps.
        /// Based on CodeName-Anti's GlassBreakerModule.
        /// </summary>
        private void BreakAllGlass(bool weakOnly = false)
        {
            GlassManager gm = GlassManager.Instance;
            if (gm == null)
            {
                TitaniumCrabPlugin.Instance.Log.LogWarning("BreakAllGlass: no GlassManager found");
                return;
            }

            ulong myId = SteamUser.GetSteamID().m_SteamID;
            int count = 0;

            foreach (GlassBreak glass in gm.pieces)
            {
                if (glass == null)
                    continue;

                // Skip solid (non-breakable) panes — they show the correct path
                if (glass.gameObject.name.Contains("Solid"))
                    continue;

                // Weak only: skip panes that are solid/fake (wouldn't break if walked on)
                if (weakOnly)
                {
                    // Check solidPiece field — if true, it's a solid/fake pane
                    var solidField = AccessTools.Field(glass.GetType(), "solidPiece");
                    if (solidField != null)
                    {
                        bool isSolid = System.Convert.ToBoolean(solidField.GetValue(glass));
                        if (isSolid)
                            continue;
                    }
                }

                try
                {
                    glass.LocalInteract();
                    glass.AllInteract(myId);
                    count++;
                }
                catch { /* skip broken entries */ }
            }

            TitaniumCrabPlugin.Instance.Log.LogInfo($"BreakAllGlass({(weakOnly ? "weak" : "all")}): broke {count} panes");
        }

        /// <summary>
        /// Break all ice tiles on Falling Platforms / ice maps.
        /// Finds all Tile components and triggers their break/interact method.
        /// </summary>
        private void BreakAllIce(bool weakOnly = false)
        {
            int count = 0;
            ulong myId = SteamUser.GetSteamID().m_SteamID;

            var tiles = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in tiles)
            {
                if (mb == null)
                    continue;

                string typeName = mb.GetIl2CppType().Name;
                if (!typeName.Contains("Tile") && !typeName.Contains("Piece"))
                    continue;

                // Weak only: skip solid tiles (wouldn't break if walked on)
                if (weakOnly)
                {
                    var solidField = AccessTools.Field(mb.GetType(), "solidPiece");
                    if (solidField != null)
                    {
                        bool isSolid = System.Convert.ToBoolean(solidField.GetValue(mb));
                        if (isSolid)
                            continue;
                    }
                }

                try
                {
                    var localInteract = AccessTools.Method(mb.GetType(), "LocalInteract");
                    var allInteract   = AccessTools.Method(mb.GetType(), "AllInteract");
                    if (localInteract != null)
                    {
                        localInteract.Invoke(mb, null);
                        count++;
                    }
                    if (allInteract != null)
                        allInteract.Invoke(mb, new object[] { myId });
                }
                catch { /* skip non-interactable tiles */ }
            }

            TitaniumCrabPlugin.Instance.Log.LogInfo($"BreakAllIce({(weakOnly ? "weak" : "all")}): broke {count} tiles");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        internal static PlayerMovement GetLocalPlayerMovement()
        {
            PlayerInput pi = PlayerInput.Instance;
            if (pi == null)
                return null;
            return pi.playerMovement;
        }

        internal static PlayerManager GetLocalPlayerManager()
        {
            return GetLocalPlayerMovement()?.GetComponent<PlayerManager>();
        }

        internal static PlayerStatus GetLocalPlayerStatus()
        {
            return GetLocalPlayerMovement()?.GetComponent<PlayerStatus>();
        }

        internal static bool IsGrounded(PlayerMovement pm)
        {
            if (pm == null)
                return false;
            return Physics.Raycast(pm.transform.position + Vector3.up * 0.1f,
                Vector3.down, 0.15f);
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = col;
            Texture2D tex = new(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static GUIStyle MakeStyle(Texture2D bg)
        {
            var s = new GUIStyle
            {
                normal = { background = bg }
            };
            return s;
        }
    }

    /// <summary>
    /// Static ESP renderer — draws boxes and distance for every other player.
    /// </summary>
    internal static class EspRenderer
    {
        internal static void Draw()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            PlayerManager localPm = TrainerMenu.GetLocalPlayerManager();
            ulong myId = localPm != null ? localPm.steamProfile.m_SteamID : 0;

            var active = GameManager.Instance?.activePlayers;
            if (active == null)
                return;

            GUIStyle boxStyle = new(GUI.skin.box)
            {
                normal = { textColor = Color.green },
                fontSize = 11
            };

            foreach (var entry in active)
            {
                PlayerManager pm = entry.Value;
                if (pm == null || pm.steamProfile.m_SteamID == myId)
                    continue;

                Vector3 worldPos = pm.transform.position + Vector3.up * 1f;
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (screenPos.z <= 0f)
                    continue;

                float screenY = Screen.height - screenPos.y;
                float distance = Vector3.Distance(cam.transform.position, worldPos);

                float boxW = 60f;
                float boxH = 120f;
                float boxX = screenPos.x - boxW / 2f;
                float boxY = screenY - boxH / 2f;

                Color prevColor = GUI.color;
                GUI.color = new Color(0f, 1f, 0f, 0.6f);
                DrawBox(new Rect(boxX, boxY, boxW, boxH), 2f);
                GUI.color = prevColor;

                string label = $"[{distance:F0}m]";
                var content = new GUIContent(label);
                Vector2 size = boxStyle.CalcSize(content);
                Rect labelRect = new(screenPos.x - size.x / 2f, screenY - boxH / 2f - size.y - 2f, size.x, size.y);

                var shadowStyle = new GUIStyle(boxStyle) { normal = { textColor = Color.black } };
                GUI.Label(new Rect(labelRect.x + 1, labelRect.y + 1, labelRect.width, labelRect.height), label, shadowStyle);
                GUI.Label(labelRect, label, boxStyle);
            }
        }

        private static void DrawBox(Rect rect, float thickness)
        {
            DrawRectFilled(new Rect(rect.x, rect.y, rect.width, thickness));
            DrawRectFilled(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness));
            DrawRectFilled(new Rect(rect.x, rect.y, thickness, rect.height));
            DrawRectFilled(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height));
        }

        private static void DrawRectFilled(Rect rect)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, GUI.color, 0f, 0f);
        }
    }
}
