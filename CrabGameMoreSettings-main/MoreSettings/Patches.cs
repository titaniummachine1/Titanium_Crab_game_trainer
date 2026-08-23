using BepInEx.IL2CPP.Utils;
//using CrabDevKit.Utilities.CrabUi;
using HarmonyLib;
using SteamworksNative;
using System.Collections;
using System.IO;
using System.Xml.Serialization;
using TMPro;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static MoreSettings.MoreSettings;

namespace MoreSettings
{
    internal static class Patches
    {
        //   Anti Bepinex detection (Thanks o7Moon: https://github.com/o7Moon/CrabGame.AntiAntiBepinex)
        [HarmonyPatch(typeof(EffectManager), nameof(EffectManager.Method_Private_Void_GameObject_Boolean_Vector3_Quaternion_0))] // Ensures effectSeed is never set to 4200069 (if it is, modding has been detected)
        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.Method_Private_Void_0))] // Ensures connectedToSteam stays false (true means modding has been detected)
        // [HarmonyPatch(typeof(Deobf_MenuSnowSpeedModdingDetector), nameof(Deobf_MenuSnowSpeedModdingDetector.Method_Private_Void_0))] // Would ensure snowSpeed is never set to Vector3.zero (though it is immediately set back to Vector3.one due to an accident on Dani's part lol)
        [HarmonyPrefix]
        internal static bool PreBepinexDetection()
            => false;


        // Toggle sprint, alt crouch key, alt jump key, and hold to jump
        internal static bool sprintToggled = true;
        [HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.SetInput))]
        [HarmonyPrefix]
        internal static void PrePlayerMovementSetInput(ref bool param_2, ref bool param_3, ref bool param_4)
        {
            if (!Instance.save.holdSprint)
            {
                if (Input.GetKeyDown((KeyCode)InputManager.sprint))
                    sprintToggled = !sprintToggled;
                param_4 = sprintToggled;
            }

            if (PersistentPlayerData.frozen || PersistentPlayerData.hnsFrozen)
                return;
            
            if (CurrentSettings.holdCrouch)
                param_2 = param_2 || PlayerInput.CheckInput(Instance.save.alternativeCrouch);
            else if (PlayerInput.CheckInputDown(Instance.save.alternativeCrouch))
            {
                PlayerInput.Instance.crouching = !PlayerInput.Instance.crouching;
                param_2 = PlayerInput.Instance.crouching;
            }
            
            if (Instance.save.holdJump)
                param_3 = PlayerInput.CheckInput(InputManager.jump) || PlayerInput.CheckInput(Instance.save.alternativeJump);
            else
                param_3 = param_3 || PlayerInput.CheckInputDown(Instance.save.alternativeJump);
        }

        // Hold to use items
        internal static bool checkingInteract = false;
        internal static bool checkingLeftClick = false;
        [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.NotFrozenInput))]
        [HarmonyPrefix]
        internal static void PrePlayerInputNotFrozenInput(PlayerInput __instance)
        {
            if (!__instance.playerMovement)
                return;

            checkingInteract = true;
            checkingLeftClick = true;
        }


        internal static float lastAttack = 0f;
        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), [typeof(KeyCode)])]
        [HarmonyPrefix]
        internal static bool PreInputGetKeyDown(ref bool __result, KeyCode key)
        {
            if (checkingInteract && key == (KeyCode)InputManager.interact)
            {
                checkingInteract = false;
                if (Instance.save.holdInteract)
                {
                    __result = Input.GetKey((KeyCode)InputManager.interact);
                    return false;
                }
                return true;
            }

            if (checkingLeftClick && key == (KeyCode)InputManager.leftClick)
            {
                checkingLeftClick = false;
                if (Instance.save.holdAttack)
                {
                    if (PlayerInput.Instance.playerInventory.currentItem != null)
                    {
                        if (!PlayerInput.Instance.playerInventory.currentItem.GetComponent<ItemPrefab>().field_Protected_Boolean_0)
                            return true;

                        if (Input.GetKeyDown(key))
                        {
                            lastAttack = Time.time;
                            return true;
                        }

                        if (Time.time - lastAttack < 0.2f)
                            return true;
                    }

                    __result = Input.GetKey((KeyCode)InputManager.leftClick);
                    if (PlayerInput.Instance.playerInventory.currentItem != null && __result)
                        lastAttack = Time.time;

                    return false;
                }
                return true;
            }

            return true;
        }


        // Makes all items 'automatic' so that holding down PlayerKeybinds.leftClick works
        [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.Awake))]
        [HarmonyPostfix]
        internal static void PostItemManagerAwake()
        {
            foreach (ItemData itemData in ItemManager.idToItem.Values)
                if (itemData.gunComponent != null)
                    itemData.gunComponent.automatic = true;
        }


        // Add custom setting options to ui
        [HarmonyPatch(typeof(Settings), nameof(Settings.Start))]
        [HarmonyPostfix]
        internal static void PostSettingsStart(Settings __instance)
        {
            Instance.recentlyPlayed = Instance.CreateSetting<MyBoolSetting>(__instance.streamerMode, 10, "Enable Recently Played With");
            Instance.recentlyPlayed.SetSetting(Instance.save.recentlyPlayed);


            Instance.alternativeJump = Instance.CreateSetting<ControlSetting>(__instance.jump, 5, "Jump (Alternative)");
            Instance.alternativeJump.SetSetting(Instance.save.alternativeJump, "Jump (Alternative)");

            Instance.holdJump = Instance.CreateSetting<MyBoolSetting>(__instance.holdCrouch, 6, "Hold to jump");
            Instance.holdJump.SetSetting(Instance.save.holdJump);

            Instance.holdSprint = Instance.CreateSetting<MyBoolSetting>(__instance.holdCrouch, 9, "Hold to sprint");
            Instance.holdSprint.SetSetting(Instance.save.holdSprint);
            
            Instance.alternativeCrouch = Instance.CreateSetting<ControlSetting>(__instance.crouch, 11, "Crouch / Slide (Alternative)");
            Instance.alternativeCrouch.SetSetting(Instance.save.alternativeCrouch, "Crouch / Slide (Alternative)");

            Instance.holdInteract = Instance.CreateSetting<MyBoolSetting>(__instance.holdCrouch, 14, "Hold to interact");
            Instance.holdInteract.SetSetting(Instance.save.holdInteract);

            Instance.holdAttack = Instance.CreateSetting<MyBoolSetting>(__instance.holdCrouch, 19, "Hold to attack");
            Instance.holdAttack.SetSetting(Instance.save.holdAttack);


            /*new SettingBuilder()
                .WithName("testing")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Testing!!"),

                    new SliderComponent()
                        .WithMinState(2)
                        .WithMaxState(7)
                        .WithInitialState(6)
                        .WithCallback((state, _) => { Instance.Log.LogInfo($"Slider: {state}"); })
                )
                .Build();

            new SettingBuilder()
                .WithName("text")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("So"),

                    new LabelComponent()
                        .WithLabel("much"),

                    new LabelComponent()
                        .WithLabel("text!")
                )
                .Build();

            new SettingBuilder()
                .WithName("text2")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("So"),

                    new LabelComponent()
                        .WithLabel("much"),

                    new LabelComponent()
                        .WithLabel("text!"),

                    new LabelComponent()
                        .WithLabel("So"),

                    new LabelComponent()
                        .WithLabel("much"),

                    new LabelComponent()
                        .WithLabel("text!"),

                    new LabelComponent()
                        .WithLabel("So"),

                    new LabelComponent()
                        .WithLabel("much"),

                    new LabelComponent()
                        .WithLabel("text!"),

                    new LabelComponent()
                        .WithLabel("So"),

                    new LabelComponent()
                        .WithLabel("much"),

                    new LabelComponent()
                        .WithLabel("text!")
                )
                .Build();

            new SettingBuilder()
                .WithName("test toggle")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Toggle!!"),

                    new ToggleComponent()
                        .WithInitialState(true)
                        .WithCallback((state, _) => { Instance.Log.LogInfo($"Toggle: {state}"); })
                )
                .Build();

            new SettingBuilder()
                .WithName("test toggle2")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Toggle 2!!"),

                    new ToggleComponent()
                        .WithInitialState(false)
                        .WithCallback((state, _) => { Instance.Log.LogInfo($"Toggle 2: {state}"); })
                )
                .Build();

            new SettingBuilder()
                .WithName("test toggle3")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Toggle 3!!"),

                    new ToggleComponent()
                        .WithInitialState(false)
                        .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle 3: {state}"); if (state) press?.Invoke(); })
                )
                .Build();

            new SettingBuilder()
                .WithName("test toggles")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Toggles!!"),

                    new ToggleComponent()
                        .WithInitialState(true)
                        .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle A: {state}"); }),

                    new ToggleComponent()
                        .WithInitialState(false)
                        .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle B: {state}"); }),

                    new ToggleComponent()
                        .WithInitialState(false)
                        .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle C: {state}"); }),

                    new ToggleComponent()
                        .WithInitialState(false)
                        .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle D: {state}"); }),

                    new ToggleComponent()
                        .WithInitialState(false)
                        .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle E: {state}"); })
                )
                .Build();

            new SettingBuilder()
                .WithName("test toggles2")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Toggles2!!"),

                    new SettingBuilder()
                        .WithName("toggles")
                        .WithSize(0f, 40f)
                        .WithBackgroundColor(Color.clear)
                        .WithChildControl(false, false)
                        .WithPadding(0, 10, 0, 0)
                        .WithComponents(

                            new ToggleComponent()
                                .WithInitialState(true)
                                .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle A: {state}"); }),

                            new ToggleComponent()
                                .WithInitialState(false)
                                .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle B: {state}"); }),

                            new ToggleComponent()
                                .WithInitialState(false)
                                .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle C: {state}"); }),

                            new ToggleComponent()
                                .WithInitialState(false)
                                .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle D: {state}"); }),

                            new ToggleComponent()
                                .WithInitialState(false)
                                .WithCallback((state, press) => { Instance.Log.LogInfo($"Toggle E: {state}"); })
                        )
                )
                .Build();

            string[] options = ["Hey", "Yo", "Wassupp", "turtles"];
            new SettingBuilder()
                .WithName("test scroll")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Scrolling!!"),

                    new ScrollComponent()
                        .WithInitialState(2)
                        .WithOptions(options)
                        .WithCallback((state, _, _) => { Instance.Log.LogInfo($"Scroll: {state} {options[state]}"); })
                )
                .Build();

            object[] options2 = ["Hey", "Yo", "Wassupp", "turtles", 3, 4, 5];
            new SettingBuilder()
                .WithName("test scroll2")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Scrolling 2!!"),

                    new ScrollComponent()
                        .WithInitialState(5)
                        .WithOptions(options2)
                        .WithCallback((state, _, _) => { Instance.Log.LogInfo($"Scroll 2: {state} {options2[state]}"); })
                )
                .Build();

            new SettingBuilder()
                .WithName("test key")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Keybinds!!"),

                    new KeybindComponent()
                        .WithInitialKey(KeyCode.V)
                        .WithCallback((key, _) => { Instance.Log.LogInfo($"Keybind: {key}"); })
                )
                .Build();

            new SettingBuilder()
                .WithName("test button")
                .WithParent(__instance.holdCrouch.transform.parent)
                .WithComponents(
                    new LabelComponent()
                        .WithLabel("Button!!"),

                    new ButtonComponent()
                        .WithText("Sup! :3")
                        .WithCallback(() => { Instance.Log.LogInfo("Button pressed"); Prompt.Instance.NewPrompt("Hey!", "Why'd you push me!!"); })
                )
                .Build();*/


            __instance.devices.gameObject.SetActive(true); // Show microphone dropdown
            __instance.devices.transform.parent.GetChild(6).gameObject.SetActive(false); // Hide Input Device

            Object.Destroy(__instance.devices.gameObject.GetComponent<DropdownSetting>());
            MicDropDownCallbacks callbacks = __instance.devices.gameObject.AddComponent<MicDropDownCallbacks>();
            callbacks.devices = __instance.devices;

            __instance.devices.dropdown.onValueChanged.AddListener(
                callbacks,
                callbacks.GetIl2CppType().GetMethod(nameof(MicDropDownCallbacks.SelectSetting))
            );

            Button applyButton = __instance.devices.transform.GetChild(1).GetChild(1).GetComponent<Button>();
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(
                callbacks,
                callbacks.GetIl2CppType().GetMethod(nameof(MicDropDownCallbacks.ApplySetting))
            );
        }

        [HarmonyPatch(typeof(DissonanceManager), nameof(DissonanceManager.Start))]
        [HarmonyPostfix]
        internal static void PostDissonanceManagerStart(DissonanceManager __instance)
        {
            if (__instance != DissonanceManager.Instance)
                return;

            DissonanceManager.Instance.comms.prop_String_1 = Instance.save.microphone;
        }

        [HarmonyPatch(typeof(MicDropDown), nameof(MicDropDown.SetSettings))]
        [HarmonyPrefix]
        internal static void PreMicDropDownSetSettings(ref string param_1)
        {
            param_1 = DissonanceManager.Instance.comms.prop_String_1;
        }

        [HarmonyPatch(typeof(ControlSetting), nameof(ControlSetting.SetKey))]
        [HarmonyPostfix]
        internal static void PostControlSettingSetKey(ControlSetting __instance)
        {
            if (__instance == Instance.alternativeJump)
            {
                Instance.save.alternativeJump = __instance.currentKey;
                SaveManager.Instance.Save();
                return;
            }

            if (__instance == Instance.alternativeCrouch)
            {
                Instance.save.alternativeCrouch = __instance.currentKey;
                SaveManager.Instance.Save();
                return;
            }
        }
        [HarmonyPatch(typeof(MyBoolSetting), nameof(MyBoolSetting.ToggleSetting))]
        [HarmonyPostfix]
        internal static void PostMyBoolSettingToggleSetting(MyBoolSetting __instance)
        {
            if (__instance == Instance.recentlyPlayed)
            {
                Instance.save.recentlyPlayed = __instance.currentSetting == 1;
                SaveManager.Instance.Save();

                if (!Instance.save.recentlyPlayed)
                    return;

                CSteamID currentLobby = SteamManager.Instance.currentLobby;
                if (currentLobby == CSteamID.Nil)
                    return;

                int members = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
                for (int i = 0; i < members; i++)
                {
                    CSteamID steamId = SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i);
                    SteamFriends.SetPlayedWith(steamId);
                }
                return;
            }

            if (__instance == Instance.holdJump)
            {
                Instance.save.holdJump = __instance.currentSetting == 1;
                SaveManager.Instance.Save();
                return;
            }
            
            if (__instance == Instance.holdSprint)
            {
                Instance.save.holdSprint = __instance.currentSetting == 1;
                SaveManager.Instance.Save();
                return;
            }

            if (__instance == Instance.holdInteract)
            {
                Instance.save.holdInteract = __instance.currentSetting == 1;
                SaveManager.Instance.Save();
                return;
            }

            if (__instance == Instance.holdAttack)
            {
                Instance.save.holdAttack = __instance.currentSetting == 1;
                SaveManager.Instance.Save();
                return;
            }
        }

        // Manage the save for MoreSettings
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Load))]
        [HarmonyPrefix]
        internal static void PreSaveManagerLoad()
        {
            if (PlayerPrefs.HasKey("moreSettingsSave"))
            {
                XmlSerializer xmlSerializer = new(typeof(MoreSettingsSave));
                StringReader stringReader = new(PlayerPrefs.GetString("moreSettingsSave"));
                Instance.save = (MoreSettingsSave)xmlSerializer.Deserialize(stringReader);
            }

            Instance.save ??= new();
        }
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.NewSave))]
        [HarmonyPrefix]
        internal static void PreSaveManagerNewSave()
            => Instance.save = new();
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save))]
        [HarmonyPrefix]
        internal static void PreSaveManagerSave()
        {
            XmlSerializer xmlSerializer = new(typeof(MoreSettingsSave));
            StringWriter stringWriter = new();
            xmlSerializer.Serialize(stringWriter, Instance.save);
            PlayerPrefs.SetString("moreSettingsSave", stringWriter.ToString());
        }


        // Who's talking? Player list
        [HarmonyPatch(typeof(PlayerList), nameof(PlayerList.Awake))]
        [HarmonyPostfix]
        internal static void PostPlayerListAwake()
        {
            Instance.playerListPlayers.Clear();
            Instance.playerListMicImages.Clear();

            PlayerList.Instance.StartCoroutine(CoroUpdateMicImages());
        }

        private static IEnumerator CoroUpdateMicImages()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.1f);

                if (GameManager.Instance && PlayerList.Instance.parent.activeInHierarchy)
                {
                    foreach (ulong clientId in GameManager.Instance.activePlayers.Keys)
                        if (Instance.playerListMicImages.ContainsKey(clientId) && Instance.playerListMicImages[clientId] != null)
                            Instance.playerListMicImages[clientId].enabled = Instance.PlayerIsTalking(clientId);

                    foreach (ulong clientId in GameManager.Instance.spectators.Keys)
                        if (Instance.playerListMicImages.ContainsKey(clientId) && Instance.playerListMicImages[clientId] != null)
                            Instance.playerListMicImages[clientId].enabled = Instance.PlayerIsTalking(clientId);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerListingPrefab), nameof(PlayerListingPrefab.SetPlayer))]
        [HarmonyPostfix]
        internal static void PostPlayerListingPrefabSetPlayer(PlayerListingPrefab __instance, PlayerManager param_1)
        {
            // Check if the mic hasn't already been created
            if (!Instance.playerListPlayers.ContainsKey(param_1.steamProfile.m_SteamID) || Instance.playerListPlayers[param_1.steamProfile.m_SteamID] != __instance || Instance.playerListMicImages[param_1.steamProfile.m_SteamID] == null)
            {
                Instance.playerListPlayers[param_1.steamProfile.m_SteamID] = __instance;

                // Make room for the mic in the ui
                __instance.ping.GetComponent<RectTransform>().sizeDelta -= new Vector2(35/*width (25) + spacing (10)*/, 0);

                // Create the mic icon
                GameObject micObject = UnityEngine.Object.Instantiate(__instance.icon.gameObject, __instance.transform);
                micObject.name = "Talking";
                micObject.transform.SetSiblingIndex(4);

                Instance.playerListMicImages[param_1.steamProfile.m_SteamID] = micObject.GetComponent<RawImage>();
                Instance.playerListMicImages[param_1.steamProfile.m_SteamID].texture = StatusUI.Instance.hpCircle.transform.parent.GetChild(2).GetChild(0).GetComponent<RawImage>().texture;
            }

            // Show the mic image if the player is talking
            Instance.playerListMicImages[param_1.steamProfile.m_SteamID].enabled = Instance.PlayerIsTalking(param_1.steamProfile.m_SteamID);
        }

        [HarmonyPatch(typeof(PlayerListingPrefab), nameof(PlayerListingPrefab.SetSpectator))]
        [HarmonyPostfix]
        internal static void PostPlayerListingPrefabSetSpectator(PlayerListingPrefab __instance, ulong param_1)
        {
            // Check if the mic hasn't already been created
            if (!Instance.playerListPlayers.ContainsKey(param_1) || Instance.playerListPlayers[param_1] != __instance || Instance.playerListMicImages[param_1] == null)
            {
                Instance.playerListPlayers[param_1] = __instance;

                // Make room for the mic in the ui
                __instance.ping.GetComponent<RectTransform>().sizeDelta -= new Vector2(35/*width (25) + spacing (10)*/, 0);

                // Create the mic icon
                GameObject micObject = UnityEngine.Object.Instantiate(__instance.icon.gameObject, __instance.transform);
                micObject.name = "Talking";
                micObject.transform.SetSiblingIndex(4);

                Instance.playerListMicImages[param_1] = micObject.GetComponent<RawImage>();
                Instance.playerListMicImages[param_1].texture = StatusUI.Instance.hpCircle.transform.parent.GetChild(2).GetChild(0).GetComponent<RawImage>().texture;
            }

            // Show the mic image if the player is talking
            Instance.playerListMicImages[param_1].enabled = Instance.PlayerIsTalking(param_1);
        }


        // View Steam Profile in Player List
        internal static Slider volumeSlider;
        [HarmonyPatch(typeof(ManagePlayerListing), nameof(ManagePlayerListing.Awake))]
        [HarmonyPostfix]
        internal static void PostManagePlayerListingAwake(ManagePlayerListing __instance)
        {
            Transform tr = __instance.transform;
            RectTransform rectTr = tr.GetChild(0).GetComponent<RectTransform>();

            // Increase the height of the background to fit the slider (slider height(40) + spacing(5) = 45)
            rectTr.sizeDelta += new Vector2(0f, 45f);


            Instance.Log.LogInfo("New Slider/Interact/Slider");
            GameObject newSliderInteractSlider = new("Slider");
            newSliderInteractSlider.transform.SetParent(tr.GetChild(0).GetChild(1));
            newSliderInteractSlider.transform.localScale = Vector3.one;

            RectTransform rectInteractSlider = newSliderInteractSlider.AddComponent<RectTransform>();
            rectInteractSlider.sizeDelta = new Vector2(150f, 40f);

            MyVolumeSlider slider = newSliderInteractSlider.AddComponent<MyVolumeSlider>();

            Slider sliderInteractSlider = volumeSlider = newSliderInteractSlider.AddComponent<Slider>();
            sliderInteractSlider.wholeNumbers = true;
            sliderInteractSlider.minValue = 0f;
            sliderInteractSlider.maxValue = 300f;
            sliderInteractSlider.value = 100f;
            sliderInteractSlider.onValueChanged.AddListener(
                slider,
                slider.GetIl2CppType().GetMethod(nameof(MyVolumeSlider.SliderUpdated))
            );

            slider.volumeSlider = volumeSlider;


            Instance.Log.LogInfo("New Slider/Interact/Slider/Background");
            GameObject newSliderInteractSliderBackground = new("Background");
            newSliderInteractSliderBackground.transform.SetParent(newSliderInteractSlider.transform);
            newSliderInteractSliderBackground.transform.localScale = Vector3.one;

            RectTransform rectInteractSliderBackground = newSliderInteractSliderBackground.AddComponent<RectTransform>();
            rectInteractSliderBackground.anchorMax = new Vector2(0.9787f, 0.75f);
            rectInteractSliderBackground.anchorMin = new Vector2(0f, 0.25f);
            rectInteractSliderBackground.sizeDelta = new Vector2(0f, -10f);
            rectInteractSliderBackground.localPosition = new Vector3(rectInteractSliderBackground.localPosition.x, 0f);

            CanvasRenderer canvasRendInteractSliderBackground = newSliderInteractSliderBackground.AddComponent<CanvasRenderer>();
            canvasRendInteractSliderBackground.cullTransparentMesh = false;

            Image imageInteractSliderBackground = newSliderInteractSliderBackground.AddComponent<Image>();
            imageInteractSliderBackground.color = new Color(0f, 0f, 0f, 0.5804f);


            Instance.Log.LogInfo("New Slider/Interact/Slider/Fill Area");
            GameObject newSliderInteractSliderFillArea = new("Fill Area");
            newSliderInteractSliderFillArea.transform.SetParent(newSliderInteractSlider.transform);
            newSliderInteractSliderFillArea.transform.localScale = Vector3.one;

            RectTransform rectInteractSliderFillArea = newSliderInteractSliderFillArea.AddComponent<RectTransform>();
            rectInteractSliderFillArea.anchorMax = new Vector2(0.9787f, 0.75f);
            rectInteractSliderFillArea.anchorMin = new Vector2(0f, 0.25f);
            rectInteractSliderFillArea.sizeDelta = new Vector2(0f, -10f);
            rectInteractSliderFillArea.localPosition = new Vector3(rectInteractSliderFillArea.localPosition.x, 0f);


            Instance.Log.LogInfo("New Slider/Interact/Slider/Fill Area/Fill");
            GameObject newSliderInteractSliderFillAreaFill = new("Fill");
            newSliderInteractSliderFillAreaFill.transform.SetParent(newSliderInteractSliderFillArea.transform);
            newSliderInteractSliderFillAreaFill.transform.localScale = Vector3.one;

            RectTransform rectInteractSliderFillAreaFill = newSliderInteractSliderFillAreaFill.AddComponent<RectTransform>();
            rectInteractSliderFillAreaFill.anchorMax = new Vector2(1f, 1f);
            rectInteractSliderFillAreaFill.anchorMin = new Vector2(0f, 0f);
            rectInteractSliderFillAreaFill.sizeDelta = new Vector2(0f, 0f);
            rectInteractSliderFillAreaFill.localPosition = Vector3.zero;

            CanvasRenderer canvasRendInteractSliderFillAreaFill = newSliderInteractSliderFillAreaFill.AddComponent<CanvasRenderer>();
            canvasRendInteractSliderFillAreaFill.cullTransparentMesh = false;

            Image imageInteractSliderFillAreaFill = newSliderInteractSliderFillAreaFill.AddComponent<Image>();
            imageInteractSliderFillAreaFill.color = new Color(0.2235f, 0.2745f, 0.8f);

            sliderInteractSlider.fillRect = rectInteractSliderFillAreaFill;


            Instance.Log.LogInfo("New Slider/Interact/Slider/Handle Slide Area");
            GameObject newSliderInteractSliderHandleSlideArea = new("Handle Slide Area");
            newSliderInteractSliderHandleSlideArea.transform.SetParent(newSliderInteractSlider.transform);
            newSliderInteractSliderHandleSlideArea.transform.localScale = Vector3.one;

            RectTransform rectInteractSliderHandleSlideArea = newSliderInteractSliderHandleSlideArea.AddComponent<RectTransform>();
            rectInteractSliderHandleSlideArea.anchorMax = new Vector2(1f, 1f);
            rectInteractSliderHandleSlideArea.anchorMin = new Vector2(0f, 0f);
            rectInteractSliderHandleSlideArea.sizeDelta = new Vector2(-20f, 0f);
            rectInteractSliderHandleSlideArea.localPosition = new Vector3(rectInteractSliderHandleSlideArea.localPosition.x, 0f);


            Instance.Log.LogInfo("New Slider/Interact/Slider/Handle Slide Area/Handle");
            GameObject newSliderInteractSliderHandleSlideAreaHandle = new("Handle");
            newSliderInteractSliderHandleSlideAreaHandle.transform.SetParent(newSliderInteractSliderHandleSlideArea.transform);
            newSliderInteractSliderHandleSlideAreaHandle.transform.localScale = Vector3.one;

            RectTransform rectInteractSliderHandleSlideAreaHandle = newSliderInteractSliderHandleSlideAreaHandle.AddComponent<RectTransform>();
            rectInteractSliderHandleSlideAreaHandle.anchorMax = new Vector2(1f, 1f);
            rectInteractSliderHandleSlideAreaHandle.anchorMin = new Vector2(1f, 0f);
            rectInteractSliderHandleSlideAreaHandle.sizeDelta = new Vector2(15f, 0f);
            rectInteractSliderHandleSlideAreaHandle.localPosition = new Vector3(rectInteractSliderHandleSlideAreaHandle.localPosition.x, 0f);

            CanvasRenderer canvasRendInteractSliderHandleSlideAreaHandle = newSliderInteractSliderHandleSlideAreaHandle.AddComponent<CanvasRenderer>();
            canvasRendInteractSliderHandleSlideAreaHandle.cullTransparentMesh = false;

            newSliderInteractSliderHandleSlideAreaHandle.AddComponent<Image>();

            newSliderInteractSliderHandleSlideAreaHandle.AddComponent<ButtonSfx>();

            sliderInteractSlider.handleRect = rectInteractSliderHandleSlideAreaHandle;


            // Increase the height of the background to fit the extra button (button height(32) + spacing(5) = 37)
            rectTr.sizeDelta += new Vector2(0f, 37f);

            // Create viewBtn from muteBtn
            Transform muteBtn = tr.GetChild(0).GetChild(1).GetChild(3);
            GameObject viewBtn = UnityEngine.Object.Instantiate(muteBtn.gameObject, muteBtn.parent);

            // Change viewBtn visuals
            viewBtn.GetComponent<Graphic>().color = new Color(0.25f, 0.25f, 0.75f);
            viewBtn.transform.GetChild(4).GetComponent<TextMeshProUGUI>().text = "Profile";

            // Set viewBtn's onClick event
            UnityEvent ev = viewBtn.GetComponent<Button>().onClick;
            ev.m_PersistentCalls.Clear();
            ev.AddListener(viewBtn.AddComponent<ModListViewSteamProfileButton>(), UnityEventBase.GetValidMethodInfo(Il2CppType.Of<ModListViewSteamProfileButton>(), "Clicked", new Il2CppReferenceArray<Il2CppSystem.Type>(0)));
        }


        [HarmonyPatch(typeof(ManagePlayerListing), nameof(ManagePlayerListing.SetPlayer))]
        [HarmonyPostfix]
        internal static void PostManagePlayerListingSetPlayer()
        {
            float volume = 1f;

            var playerState = DissonanceManager.Instance.comms.FindPlayer(ManagePlayerListing.Instance.field_Private_UInt64_0.ToString());
            if (playerState != null && playerState.prop_InterfacePrivateAbstractBoSiBoVoVeBoVoQuObVoUnique_0 != null)
                volume = playerState.prop_InterfacePrivateAbstractBoSiBoVoVeBoVoQuObVoUnique_0.prop_Single_0;

            volumeSlider.value = volume * 100f;
        }



        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Method_Private_Void_LobbyEnter_t_PDM_1))]
        [HarmonyPostfix]
        internal static void PostSteamManagerLobbyEnter(LobbyEnter_t param_1)
        {
            if (!Instance.save.recentlyPlayed)
                return;

            CSteamID currentLobby = SteamManager.Instance.currentLobby;
            if (param_1.m_ulSteamIDLobby != currentLobby.m_SteamID)
                return;

            int members = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
            for (int i = 0; i < members; i++)
            {
                CSteamID steamId = SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i);
                SteamFriends.SetPlayedWith(steamId);
            }
        }

        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Method_Private_Void_LobbyChatUpdate_t_PDM_3))]
        [HarmonyPostfix]
        internal static void PostSteamManagerPlayerJoinOrLeave(LobbyChatUpdate_t param_1)
        {
            if (!Instance.save.recentlyPlayed)
                return;

            CSteamID currentLobby = SteamManager.Instance.currentLobby;
            if (param_1.m_ulSteamIDLobby != currentLobby.m_SteamID)
                return;

            CSteamID steamId = new(param_1.m_ulSteamIDUserChanged);
            SteamFriends.SetPlayedWith(steamId);
        }

        [HarmonyPatch(typeof(SteamMatchmaking), nameof(SteamMatchmaking.LeaveLobby))]
        [HarmonyPrefix]
        internal static void PreSteamMatchmakingLeaveLobby(CSteamID steamIDLobby)
        {
            if (!Instance.save.recentlyPlayed)
                return;

            CSteamID currentLobby = SteamManager.Instance.currentLobby;
            if (steamIDLobby != currentLobby)
                return;

            int members = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
            for (int i = 0; i < members; i++)
            {
                CSteamID steamId = SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i);
                SteamFriends.SetPlayedWith(steamId);
            }
        }
    }
}