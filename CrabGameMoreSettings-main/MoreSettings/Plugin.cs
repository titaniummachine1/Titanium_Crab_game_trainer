using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using SteamworksNative;
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.UI;

namespace MoreSettings
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public sealed class MoreSettings : BasePlugin
    {
        internal static MoreSettings Instance { get; private set; }

        internal MoreSettingsSave save;
        
        internal MyBoolSetting recentlyPlayed;
        internal ControlSetting alternativeJump;
        internal MyBoolSetting holdJump;
        internal MyBoolSetting holdSprint;
        internal ControlSetting alternativeCrouch;
        internal MyBoolSetting holdInteract;
        internal MyBoolSetting holdAttack;

        internal Dictionary<ulong, PlayerListingPrefab> playerListPlayers = [];
        internal Dictionary<ulong, RawImage> playerListMicImages = [];


        public override void Load()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Instance = this;

            ClassInjector.RegisterTypeInIl2Cpp<ModListViewSteamProfileButton>();
            ClassInjector.RegisterTypeInIl2Cpp<MicDropDownCallbacks>();
            ClassInjector.RegisterTypeInIl2Cpp<MyVolumeSlider>();
            //ClassInjector.RegisterTypeInIl2Cpp<Rainbow>();

            Harmony harmony = new(MyPluginInfo.PLUGIN_NAME);
            harmony.PatchAll(typeof(Patches));

            Log.LogInfo($"Initialized [{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION}]");
        }

        internal T CreateSetting<T>(Setting baseSetting, int siblingIndex, string settingName) where T : Setting
        {
            GameObject secondJumpGameObject = UnityEngine.Object.Instantiate(baseSetting.gameObject, baseSetting.transform.parent);
            secondJumpGameObject.transform.SetSiblingIndex(siblingIndex);
            secondJumpGameObject.transform.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>().text = settingName;
            T setting = secondJumpGameObject.GetComponent<T>();
            setting.m_OnClick = new();
            return setting;
        }

        internal bool PlayerIsTalking(ulong clientId)
        {
            var state = DissonanceManager.Instance.comms.FindPlayer(clientId.ToString());
            return state != null && state.prop_Boolean_1;
        }
    }

    [Serializable]
    public sealed class MoreSettingsSave
    {
        public bool recentlyPlayed = false;
        public int alternativeJump = (int)KeyCode.Space;
        public bool holdJump = false;
        public bool holdSprint = true;
        public int alternativeCrouch = (int)KeyCode.C;
        public bool holdInteract = false;
        public bool holdAttack = false;
        public string microphone = string.Empty;
    }

    public sealed class ModListViewSteamProfileButton : MonoBehaviour
    {
        public void Clicked()
            => SteamFriends.ActivateGameOverlayToUser("steamid", new(ManagePlayerListing.Instance.field_Private_UInt64_0));

    }

    public sealed class MicDropDownCallbacks : MonoBehaviour
    {
        public MicDropDown devices;

        public void SelectSetting(int _)
        {
            MoreSettings.Instance.Log.LogInfo($"Selecting mic: {devices.field_Private_List_1_String_0[devices.dropdown.value]}");

            devices.currentSetting = devices.dropdown.value;
        }

        public void ApplySetting()
        {
            string mic = devices.field_Private_List_1_String_0[devices.currentSetting];
            MoreSettings.Instance.Log.LogInfo($"Applying mic: {mic}");

            MoreSettings.Instance.save.microphone = mic;
            DissonanceManager.Instance.comms.prop_String_1 = mic;
            SaveManager.Instance.Save();
        }

    }

    public sealed class MyVolumeSlider : MonoBehaviour
    {
        public Slider volumeSlider;

        public void SliderUpdated(float value)
        {
            MoreSettings.Instance.Log.LogInfo($"Updated: {value}");

            if (ManagePlayerListing.Instance == null)
            {
                volumeSlider.value = 100f;
                return;
            }

            var playerState = DissonanceManager.Instance.comms.FindPlayer(ManagePlayerListing.Instance.field_Private_UInt64_0.ToString());
            if (playerState != null && playerState.prop_InterfacePrivateAbstractBoSiBoVoVeBoVoQuObVoUnique_0 != null)
                playerState.prop_InterfacePrivateAbstractBoSiBoVoVeBoVoQuObVoUnique_0.prop_Single_0 = value / 100f;
            else
                volumeSlider.value = 100f;
        }
    }

    /*public sealed class Rainbow : MonoBehaviour
    {
        public float duration = 1f;
        private Renderer renderer;

        public void Start()
        {
            renderer = GetComponent<Renderer>();
        }

        public void Update()
        {
            float hue = Mathf.Repeat(Time.time / duration, 1f);
            Color rainbowColor = Color.HSVToRGB(hue, 1f, 1f);

            if (renderer != null)
                renderer.material.color = rainbowColor;
        }
    }*/
}