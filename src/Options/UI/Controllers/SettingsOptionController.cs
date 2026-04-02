using TMPro;
using System;
using System.Linq;
using UnityEngine;
using VentLib.Logging;
using VentLib.Utilities;
using AmongUs.GameOptions;
using VentLib.Options.Enum;
using VentLib.Options.UI.Tabs;
using VentLib.Options.Interfaces;
using VentLib.Options.Extensions;
using VentLib.Options.UI.Options;
using VentLib.Options.UI.Renderer;
using VentLib.Utilities.Optionals;
using VentLib.Utilities.Attributes;
using VentLib.Utilities.Extensions;
using VentLib.Options.UI.Controllers.Search;
using Rewired.Utils;
using VentLib.Options.Events;
using VentLib.Utilities.Collections;

namespace VentLib.Options.UI.Controllers;

[LoadStatic]
public static class SettingsOptionController
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(SettingsOptionController));
    private static IGameOptionRenderer OptionRenderer = new SettingsRenderer();
    internal static UnityOptional<GameSettingMenu> _lastInitialized = new();
    private static OrderedSet<Action<IControllerEvent>> _tabEvents = new();
    public static RenderOptions RenderOptions { get; set; } = new();
    private static IMainSettingTab _mainSettingsTab = null!;
    internal static bool ModSettingsOpened = false;
    internal static bool Enabled;

    public static IMainSettingTab MainSettingsTab
    {
        get => _mainSettingsTab;
        set
        {
            SwitchTab(value);
            _mainSettingsTab = value;
        }
    }

    static SettingsOptionController()
    {

    }

    public static void Enable() => Enabled = true;

    public static void SetMainTab(IMainSettingTab tab) => MainSettingsTab = tab;

    public static void SetRenderer(IGameOptionRenderer iRenderer)
    {
        OptionRenderer = iRenderer;
    }
    
    public static void RegisterEventHandler(Action<IControllerEvent> eventHandler)
    {
        _tabEvents.Add(eventHandler);
    }

    internal static void Start(GameSettingMenu menu)
    {
        _lastInitialized = UnityOptional<GameSettingMenu>.NonNull(menu);
        if (!Enabled) return;

        var gamesettings = menu.GameSettingsButton;
        var rolesettings = menu.RoleSettingsButton;
        GameObject modSettingsButton = menu.GamePresetsButton.gameObject;
        modSettingsButton.name = "Mod Settings";

        PassiveButton button = menu.GamePresetsButton;
        button.OnClick.RemoveAllListeners();
        button.OnMouseOut.RemoveAllListeners();
        button.OnMouseOver.RemoveAllListeners();
        button.OnClick.AddListener((Action)(() => OpenModSettings(menu, button)));
        button.OnMouseOut.AddListener((Action)(() => {
            if (ModSettingsOpened) return;
            button.SelectButton(false);
        }));
        button.OnMouseOver.AddListener((Action)(() => {
            if (ModSettingsOpened) return;
            button.SelectButton(true);
        }));
        rolesettings.GetComponent<PassiveButton>().OnClick.AddListener((Action)(() => {
            ModSettingsOpened = false;
            button.SelectButton(false);
            OptionExtensions.categoryHeaders.ForEach(header => {
                if (header.IsNullOrDestroyed() || !UnityEngine.Object.IsNativeObjectAlive(header)) return;
                header.gameObject.SetActive(header.name != "LotusCategory"); 
            });
        }));
        PassiveButton gameSettingsButton = gamesettings.GetComponent<PassiveButton>();
        gameSettingsButton.OnClick.RemoveAllListeners();
        gameSettingsButton.OnClick.AddListener((Action)(() => {
            ModSettingsOpened = false;
            OptionExtensions.categoryHeaders.ForEach(header => {
                if (header.IsNullOrDestroyed() || !UnityEngine.Object.IsNativeObjectAlive(header)) return;
                header.gameObject.SetActive(header.name != "LotusCategory"); 
            });
            if (menu.GameSettingsTab.Children != null) {
                menu.GameSettingsTab.Children.ToArray().ForEach(child => {
                    child.gameObject.SetActive(child.name != "LotusSetting"); 
                });
            }
            button.SelectButton(false);
            menu.ChangeTab(1, false);
        }));
        gameSettingsButton.OnMouseOver.RemoveAllListeners();
        gameSettingsButton.OnMouseOver.AddListener((Action)(() => {
            if (ModSettingsOpened) gameSettingsButton.SelectButton(true); else menu.ChangeTab(1, true);
        }));
        gameSettingsButton.OnMouseOut.RemoveAllListeners();
        gameSettingsButton.OnMouseOut.AddListener((Action)(() => {
            if (ModSettingsOpened) gameSettingsButton.SelectButton(false); else if (!menu.GameSettingsTab.gameObject.active) gameSettingsButton.SelectButton(false);
        }));
        var label = button.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
        Async.Schedule(() => { label.text = MainSettingsTab.ButtonText; }, 0.05f);
        if (ModSettingsOpened) Async.Schedule(() => { OpenModSettings(menu, button, true); }, 0.25f);
        // MainSettingsTab.GetOptions().ForEach(child => child.Setup = false);
    }

    internal static void OpenModSettings(GameSettingMenu menu, PassiveButton modSettingsButton, bool skip = false)
    {
        if (ModSettingsOpened && !skip) return;
        ModSettingsOpened = true;
        if (menu.GameSettingsButton.gameObject.transform.Find("Selected").gameObject.active) {
            menu.GameSettingsButton.gameObject.transform.Find("Inactive").gameObject.SetActive(true);
            menu.GameSettingsButton.gameObject.transform.Find("Selected").gameObject.SetActive(false);
            menu.GameSettingsTab.gameObject.SetActive(false);
            TextMeshPro text = menu.GameSettingsButton.gameObject.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
            text.color = new Color(0.6706f, 0.8902f, 0.8667f);
        } else {
            menu.RoleSettingsButton.gameObject.transform.Find("Inactive").gameObject.SetActive(true);
            menu.RoleSettingsButton.gameObject.transform.Find("Selected").gameObject.SetActive(false);
            menu.RoleSettingsTab.gameObject.SetActive(false);
            TextMeshPro text = menu.RoleSettingsButton.gameObject.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
            text.color = new Color(0.6706f, 0.8902f, 0.8667f);
        }
        modSettingsButton.SelectButton(true);
        menu.MenuDescriptionText.text = MainSettingsTab.AreaDescription;
        menu.GameSettingsTab.gameObject.SetActive(true);
    }

    internal static void DoRender(GameOptionsMenu menu)
    {
        if (!Enabled) return;
        MainSettingsTab.GetOptions().ForEach(child => {
            switch (child.OptionType) {
                case OptionType.String:
                    (child as TextOption)!.HideMembers();
                    break;
                case OptionType.Bool:
                    (child as BoolOption)!.HideMembers();
                    break;
                case OptionType.Int:
                case OptionType.Float:
                    (child as FloatOption)!.HideMembers();
                    break;
                default:
                    (child as UndefinedOption)!.HideMembers();
                    break;
            }
        });
        OptionRenderer.SetHeight(MainSettingsTab.StartHeight());
        MainSettingsTab.PreRender().Where(o => o.name.ToLower().Contains(SearchBarController.CurrentText)).ForEach((option, index) => RenderCheck(option, index, menu));
        OptionRenderer.PostRender(menu);
    }

    internal static void ValidateOptionBehaviour(GameOption option, GameOptionsMenu menu, bool preRender = true)
    {
        if (option.BehaviourExists()) return;
        switch (option.OptionType) 
        {
            case OptionType.String:
                TextOption textOption = (option as TextOption)!;
                StringGameSetting stringGameSetting = ScriptableObject.CreateInstance<StringGameSetting>();
                stringGameSetting.Values = Enumerable.Repeat(StringNames.None, option.Values.Count).ToArray();
                stringGameSetting.OptionName = Int32OptionNames.Invalid;
                stringGameSetting.Index = option.DefaultIndex;
                stringGameSetting.Type = OptionTypes.String;
                StringOption stringBehavior = UnityEngine.Object.Instantiate<StringOption>(menu.stringOptionOrigin, Vector3.zero, Quaternion.identity, menu.settingsContainer);
                stringBehavior.name = "ModdedSetting";
			    stringBehavior.SetClickMask(menu.ButtonClickMask);
			    stringBehavior.SetUpFromData(stringGameSetting, 20);
                stringBehavior.Value = stringBehavior.oldValue = -1;
                stringBehavior.TitleText.text = option.Name();
                stringBehavior.ValueText.text = option.GetValueText();
                stringBehavior.OnValueChanged = new Action<OptionBehaviour>(_ => { });
                textOption.Behaviour = UnityOptional<StringOption>.NonNull(stringBehavior);
                if (preRender) OptionRenderer.PreRender(option, RenderOptions, menu);
                textOption.BindPlusMinusButtons();
                break;
            case OptionType.Bool:
                BoolOption boolOption = (option as BoolOption)!;
                CheckboxGameSetting checkboxSettings = ScriptableObject.CreateInstance<CheckboxGameSetting>();
                checkboxSettings.OptionName = BoolOptionNames.Invalid;
                checkboxSettings.Type = OptionTypes.Checkbox;
                ToggleOption toggleBehavior = UnityEngine.Object.Instantiate<ToggleOption>(menu.checkboxOrigin, Vector3.zero, Quaternion.identity);
                toggleBehavior.name = "ModdedSetting";
			    toggleBehavior.SetClickMask(menu.ButtonClickMask);
			    toggleBehavior.SetUpFromData(checkboxSettings, 20);
                toggleBehavior.CheckMark.enabled = (bool)option.GetValue();
                toggleBehavior.TitleText.text = option.Name();
                toggleBehavior.OnValueChanged = new Action<OptionBehaviour>(_ => { });
                boolOption.Behaviour = UnityOptional<ToggleOption>.NonNull(toggleBehavior);
                if (preRender) OptionRenderer.PreRender(option, RenderOptions, menu);
                boolOption.BindPlusMinusButtons();
                break;
            case OptionType.Int:
            case OptionType.Float:
                FloatOption floatOption = (option as FloatOption)!;
                StringGameSetting numberGameSetting = ScriptableObject.CreateInstance<StringGameSetting>();
                numberGameSetting.Values = Enumerable.Repeat(StringNames.None, option.Values.Count).ToArray();
                numberGameSetting.OptionName = Int32OptionNames.Invalid;
                numberGameSetting.Index = option.DefaultIndex;
                numberGameSetting.Type = OptionTypes.String;
                StringOption numberBehavior = UnityEngine.Object.Instantiate<StringOption>(menu.stringOptionOrigin, Vector3.zero, Quaternion.identity, menu.settingsContainer);
                numberBehavior.name = "ModdedSetting";
			    numberBehavior.SetClickMask(menu.ButtonClickMask);
			    numberBehavior.SetUpFromData(numberGameSetting, 20);
                numberBehavior.Value = numberBehavior.oldValue = -1;
                numberBehavior.TitleText.text = option.Name();
                numberBehavior.ValueText.text = option.GetValueText();
                numberBehavior.OnValueChanged = new Action<OptionBehaviour>(_ => { });
                floatOption.Behaviour = UnityOptional<StringOption>.NonNull(numberBehavior);
                if (preRender) OptionRenderer.PreRender(option, RenderOptions, menu);
                floatOption.BindPlusMinusButtons();
                break;
            case OptionType.Player:
                UndefinedOption undefinedPlayerOption = (option as UndefinedOption)!;
                PlayerSelectionGameSetting playerGameSetting = ScriptableObject.CreateInstance<PlayerSelectionGameSetting>();
                playerGameSetting.OptionName = Int32OptionNames.Invalid;
                playerGameSetting.Type = OptionTypes.Player;
                PlayerOption optionBehaviour = UnityEngine.Object.Instantiate<PlayerOption>(menu.playerOptionOrigin, Vector3.zero, Quaternion.identity, menu.settingsContainer);
                optionBehaviour.name = "ModdedSetting";
			    optionBehaviour.SetClickMask(menu.ButtonClickMask);
			    optionBehaviour.SetUpFromData(playerGameSetting, 20);
                optionBehaviour.TitleText.text = option.Name();
                undefinedPlayerOption.Behaviour = UnityOptional<PlayerOption>.NonNull(optionBehaviour);
                if (preRender) OptionRenderer.PreRender(option, RenderOptions, menu);
                undefinedPlayerOption.BindPlusMinusButtons();
                break;
            default:
                UndefinedOption undefinedOption = (option as UndefinedOption)!;
                CategoryHeaderMasked categoryHeaderMasked = UnityEngine.Object.Instantiate(menu.categoryHeaderOrigin, Vector3.zero, Quaternion.identity, menu.settingsContainer);
                categoryHeaderMasked.name = "ModdedCategory";
			    categoryHeaderMasked.SetHeader(undefinedOption.Name(), 20);
			    categoryHeaderMasked.transform.localScale = Vector3.one * 0.63f;
                undefinedOption.Header = UnityOptional<CategoryHeaderMasked>.NonNull(categoryHeaderMasked);
                if (preRender) OptionRenderer.PreRender(option, RenderOptions, menu);
                undefinedOption.BindPlusMinusButtons();
                break;
        }
    }

    private static void RenderCheck(GameOption option, int index, GameOptionsMenu menu)
    {
        ValidateOptionBehaviour(option, menu);
        OptionRenderer.Render(option, (option.Level, index), RenderOptions, menu);
    }

    private static void SwitchTab(IMainSettingTab? newTab)
    {
        _mainSettingsTab?.Deactivate();
        newTab?.Activate();
        if (!_lastInitialized.Exists())
        {
            log.Warn("Unable to Switch Tab for Option Controller");
            return;
        }
        log.Debug($"Switching tab to {newTab?.ButtonText ?? "No tab"}");
        _lastInitialized.Get().MenuDescriptionText.text = newTab?.AreaDescription ?? "Modify the custom settings to your liking.";
        var label = _lastInitialized.Get().GamePresetsButton.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
        label.text = newTab?.ButtonText ?? "MOD SETTINGS";
        
        MainSettingChangeEvent changeEvent = new(_mainSettingsTab, newTab);
        _tabEvents.ForEach(handler => handler(changeEvent));
    }
}