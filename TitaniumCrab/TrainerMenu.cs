using HarmonyLib;
using SteamworksNative;
using UnityEngine;


namespace TitaniumCrab
{
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

        public TrainerMenu(System.IntPtr ptr) : base(ptr) { }

        private void Awake()
        {
            _menuKey = (KeyCode)TitaniumCrabPlugin.Instance.CfgMenuKey.Value;
            _menuVisible = TitaniumCrabPlugin.Instance.MenuVisible;
            _antiCheatTimer = 30f;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_menuKey))
            {
                _menuVisible = !_menuVisible;
                TitaniumCrabPlugin.Instance.MenuVisible = _menuVisible;
            }

            RunFly();
            RunNoclip();
            RunFovModifier();
            RunAimbot();
            RunSpeedHack();
            RunAirJump();
            RunMegaJump();
            RunGodMode();
            RunInfiniteAmmo();
            RunAutoSlap();
            RunAntiBoundKills();
            RunFullbright();
            RunStrongSprint();
            RunSlideJump();

            // Delayed anti-cheat GameObject destruction (30s after load)
            if (_antiCheatTimer > 0f)
            {
                _antiCheatTimer -= Time.deltaTime;
                if (_antiCheatTimer <= 0f && TitaniumCrabPlugin.Instance.AntiAntiCheatEnabled)
                    Patches.DestroyAntiCheatObject();
            }
        }

        private void OnGUI()
        {
            if (TitaniumCrabPlugin.Instance.EspEnabled)
                EspRenderer.Draw();

            if (!_menuVisible)
                return;

            InitStyles();
            _menuRect = GUI.Window(9991, _menuRect, (GUI.WindowFunction)DrawMenuWindow, "");
        }

        private void InitStyles()
        {
            if (_bgTexture == null)
            {
                _bgTexture    = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.12f, 0.92f));
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
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(500));

            // --- Movement ---
            SectionLabel("MOVEMENT");
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
            GUILayout.Label($"Slide Jump Key: {((KeyCode)p.SlideJumpKey)} (hold to launch)", _labelStyle);

            GUILayout.Space(6);

            // --- Combat ---
            SectionLabel("COMBAT");
            p.AutoSlapEnabled      = ToggleRow("Auto Slap (MG)", p.AutoSlapEnabled);
            p.SuperPunchEnabled    = ToggleRow("Super Punch",    p.SuperPunchEnabled);
            if (p.SuperPunchEnabled)
                p.SuperPunchMultiplier = SliderRow("Knockback x", p.SuperPunchMultiplier, 0f, 50f);
            p.AntiPushEnabled      = ToggleRow("Anti Push",      p.AntiPushEnabled);
            p.GodModeEnabled       = ToggleRow("God Mode",       p.GodModeEnabled);
            p.NoFallEnabled        = ToggleRow("No Fall Damage", p.NoFallEnabled);
            p.InfiniteAmmoEnabled  = ToggleRow("Infinite Ammo",  p.InfiniteAmmoEnabled);
            p.AimbotEnabled        = ToggleRow("Aimbot",         p.AimbotEnabled);
            if (p.AimbotEnabled)
            {
                p.AimbotFOV        = SliderRow("Aimbot FOV",     p.AimbotFOV, 1f, 180f);
                p.AimbotSmooth     = SliderRow("Smoothing",      p.AimbotSmooth, 1f, 30f);
            }

            GUILayout.Space(6);

            // --- Visual ---
            SectionLabel("VISUAL");
            p.EspEnabled           = ToggleRow("Player ESP",     p.EspEnabled);
            p.FovModifierEnabled   = ToggleRow("FOV Modifier",   p.FovModifierEnabled);
            if (p.FovModifierEnabled)
                p.FovValue         = SliderRow("FOV",            p.FovValue, 20f, 170f);
            p.FullbrightEnabled    = ToggleRow("Fullbright",     p.FullbrightEnabled);

            GUILayout.Space(6);

            // --- World (one-shot buttons) ---
            SectionLabel("WORLD");
            if (GUILayout.Button("Break All Glass", GUILayout.Height(26)))
                BreakAllGlass();
            if (GUILayout.Button("Break All Ice", GUILayout.Height(26)))
                BreakAllIce();

            GUILayout.Space(6);

            // --- Misc ---
            SectionLabel("MISC");
            p.AntiAntiCheatEnabled = ToggleRow("Anti-AntiCheat", p.AntiAntiCheatEnabled);

            GUILayout.Space(8);

            if (GUILayout.Button("Save Settings to Config", GUILayout.Height(28)))
            {
                p.SyncToConfig();
                p.Config.Save();
            }

            GUILayout.Space(4);

            if (GUILayout.Button("Reload from Config", GUILayout.Height(24)))
                p.SyncFromConfig();

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 30));
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

        private bool ToggleRow(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(150));
            var oldColor = GUI.color;
            GUI.color = value ? Color.green : new Color(1f, 0.4f, 0.4f);
            bool newValue = GUILayout.Toggle(value, value ? " ON" : " OFF",
                "button", GUILayout.Height(22));
            GUI.color = oldColor;
            GUILayout.EndHorizontal();
            return newValue;
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

        private void RunAimbot()
        {
            var p = TitaniumCrabPlugin.Instance;
            if (!p.AimbotEnabled)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            PlayerManager localPm = GetLocalPlayerManager();
            if (localPm == null)
                return;

            ulong myId = localPm.steamProfile.m_SteamID;

            PlayerManager best = null;
            float bestAngle = p.AimbotFOV;
            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;

            var active = GameManager.Instance?.activePlayers;
            if (active == null)
                return;

            foreach (var entry in active)
            {
                PlayerManager pm = entry.Value;
                if (pm == null || pm.steamProfile.m_SteamID == myId)
                    continue;

                if (pm.GetComponent<PlayerRagdoll>() != null)
                    continue;

                Vector3 headPos = pm.transform.position + Vector3.up * 1.5f;
                Vector3 dir = (headPos - camPos).normalized;
                float angle = Vector3.Angle(camFwd, dir);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = pm;
                }
            }

            if (best == null)
                return;

            Vector3 targetDir = (best.transform.position + Vector3.up * 1.5f - camPos).normalized;
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

            PlayerStatus ps = GetLocalPlayerStatus();
            if (ps == null)
                return;

            try
            {
                // PlayerStatus.Instance.currentHp is the standard field
                var curField = AccessTools.Field(ps.GetType(), "currentHp")
                             ?? AccessTools.Field(ps.GetType(), "currentHealth")
                             ?? AccessTools.Field(ps.GetType(), "health")
                             ?? AccessTools.Field(ps.GetType(), "hp");
                var maxField = AccessTools.Field(ps.GetType(), "maxHp")
                             ?? AccessTools.Field(ps.GetType(), "maxHealth");
                if (curField != null && maxField != null)
                {
                    float maxVal = System.Convert.ToSingle(maxField.GetValue(ps));
                    float curVal = System.Convert.ToSingle(curField.GetValue(ps));
                    if (maxVal > 0 && curVal < maxVal)
                        curField.SetValue(ps, maxVal);
                }
            }
            catch { /* field names may differ */ }
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
        /// Auto Slap / Infinity Punch: force the punch component to be active
        /// and reset its cooldown every physics tick, producing machine-gun slaps.
        /// Based on CodeName-Anti's InfinityPunchModule.
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

            // field_Private_Boolean_0 = is punching
            // field_Private_Single_0   = punch cooldown timer
            // Setting the timer above the game's threshold keeps the punch
            // firing every tick with no cooldown.
            try
            {
                var punchingField = AccessTools.Field(punch.GetType(), "field_Private_Boolean_0");
                var cooldownField = AccessTools.Field(punch.GetType(), "field_Private_Single_0");
                if (punchingField != null)
                    punchingField.SetValue(punch, true);
                if (cooldownField != null)
                    cooldownField.SetValue(punch, 3.1f);
            }
            catch { /* field names may differ */ }
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
            var p = TitaniumCrabPlugin.Instance;
            var slideKey = (KeyCode)p.SlideJumpKey;

            if (!Input.GetKey(slideKey))
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

        // =====================================================================
        //  World actions (one-shot buttons)
        // =====================================================================

        /// <summary>
        /// Break all glass panes on Glass Jump maps.
        /// Based on CodeName-Anti's GlassBreakerModule.
        /// </summary>
        private void BreakAllGlass()
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

                try
                {
                    glass.LocalInteract();
                    glass.AllInteract(myId);
                    count++;
                }
                catch { /* skip broken entries */ }
            }

            TitaniumCrabPlugin.Instance.Log.LogInfo($"BreakAllGlass: broke {count} panes");
        }

        /// <summary>
        /// Break all ice tiles on Falling Platforms / ice maps.
        /// Finds all Tile components and triggers their break/interact method.
        /// </summary>
        private void BreakAllIce()
        {
            // Ice tiles in Crab Game are typically "Tile" components that have
            // a break/fall interaction. We find all tiles and trigger them.
            // The Tile type has LocalInteract/AllInteract similar to GlassBreak.
            int count = 0;
            ulong myId = SteamUser.GetSteamID().m_SteamID;

            // Find all objects with "Tile" in their name that have an interact method
            var tiles = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in tiles)
            {
                if (mb == null)
                    continue;

                string typeName = mb.GetIl2CppType().Name;
                // Tile components are typically named with "Tile" or are part of
                // the PiecesManager system. We check for interact methods.
                if (!typeName.Contains("Tile") && !typeName.Contains("Piece"))
                    continue;

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

            TitaniumCrabPlugin.Instance.Log.LogInfo($"BreakAllIce: broke {count} tiles");
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
