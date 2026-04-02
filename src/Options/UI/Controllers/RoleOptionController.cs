using System;
using System.Linq;
using UnityEngine;
using VentLib.Logging;
using AmongUs.GameOptions;
using VentLib.Options.Enum;
using VentLib.Options.Events;
using VentLib.Options.UI.Tabs;
using System.Collections.Generic;
using VentLib.Options.Interfaces;
using VentLib.Options.Extensions;
using VentLib.Options.UI.Options;
using VentLib.Options.UI.Renderer;
using VentLib.Utilities.Optionals;
using VentLib.Utilities.Attributes;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Collections;
using VentLib.Options.UI.Tabs.Vanilla;
using VentLib.Options.UI.Controllers.Search;
using Rewired.Utils;

namespace VentLib.Options.UI.Controllers;

[LoadStatic]
public static class RoleOptionController
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(RoleOptionController));

    private static readonly List<IGameOptionTab> BuiltinGameTabs =
    [
        new EngineerTab(), new GuardianAngelTab(), new ScientistTab(), new TrackerTab(), new DetectiveTab(), 
        new NoisemakerTab(), new ShapeshifterTab(), new PhantomTab(), new ViperTab(),
    ];
    private static UnityOptional<RolesSettingsMenu> _lastInitialized = new();
    internal static IRoleOptionRender _renderer = new RoleOptionsRenderer();
    private static OrderedSet<Action<IControllerEvent>> _tabEvents = new();
    public static RenderOptions RenderOptions { get; set; } = new();
    private static readonly OrderedSet<GameOptionTab> Tabs = new();
    private static IGameOptionTab _currentTab;
    internal static bool Enabled;
    static RoleOptionController()
    {
        BuiltinGameTabs.ForEach(tb => tb.AddEventListener(tb2 => CurrentTab = tb2));
        _currentTab = null!;
    }

    public static IGameOptionTab CurrentTab
    {
        get => _currentTab;
        set
        {
            SwitchTab(value);
            _currentTab = value;
        }
    }

    internal static IGameOptionTab[] AllTabs(bool allowIgnoredTabs = false) => BuiltinGameTabs.Concat(Tabs).Where(t => allowIgnoredTabs | !t.Ignore()).ToArray();

    public static void Enable() => Enabled = true;
    
    public static void AddTab(GameOptionTab tab)
    {
        Tabs.Add(tab);
        tab.AddEventListener(tb => CurrentTab = tb);
        Refresh();
        tab.Show();
    }

    public static void RemoveTab(GameOptionTab tab)
    {
        Tabs.Remove(tab);
        Refresh();
        tab.Hide();
    }

    public static void ClearTabs()
    {
        Tabs.ToArray().ForEach(RemoveTab);
    }

    public static void RemoveBuiltInTabs(Type? type = null)
    {
        if (type != null) {
            BuiltinGameTabs.RemoveAll(tb => {
                if (tb.GetType() == type) {
                    tb.Hide(); tb.Deactivate();
                    return true;
                } else return false;
            });
            return;
        }
        BuiltinGameTabs.ForEach(tb => {tb.Hide(); tb.Deactivate();});
        BuiltinGameTabs.Clear();
    }

    public static void RegisterEventHandler(Action<IControllerEvent> eventHandler)
    {
        _tabEvents.Add(eventHandler);
    }

    internal static void HandleOpen(RolesSettingsMenu menu)
    {
        _lastInitialized = UnityOptional<RolesSettingsMenu>.NonNull(menu);
        menu.QuotaTabSelectables = new Il2CppSystem.Collections.Generic.List<UiElement>();
		menu.roleChances = new Il2CppSystem.Collections.Generic.List<RoleOptionSetting>();
        menu.roleTabs = new Il2CppSystem.Collections.Generic.List<PassiveButton>();
        menu.roleTabs.Add(menu.AllButton);

        OptionExtensions.categoryHeaders.ForEach(header => {
            if (header.IsNullOrDestroyed() || !UnityEngine.Object.IsNativeObjectAlive(header)) return;
            header.gameObject.SetActive(header.name != "LotusCategory"); 
        });
        AllTabs(true).ForEach(tab => tab.Setup(_lastInitialized.Get()));
        SettingsOptionController._lastInitialized.IfPresent(gsm => gsm.RoleSettingsButton.gameObject.SetActive(AllTabs(true).Any()));

        _renderer.RenderTabs(AllTabs(true), menu);
        
        OptionOpenEvent openEvent = new();
        _tabEvents.ForEach(handler => handler(openEvent));
        OpenChancesTab(menu);
    }

    internal static void OpenChancesTab(RolesSettingsMenu menu)
    {
        if (!Enabled) return;
        CurrentTab = null!;
        menu.scrollBar.ContentYBounds.max = _renderer.GetChancesHeight();
    }

    internal static void Refresh()
    {
        if (!_lastInitialized.Exists())
        {
            log.Warn("Unable to Refresh Option Controller", "OptionController");
            return;
        }
        _lastInitialized.Get().RoleChancesSettings.transform.DestroyChildren();
        AllTabs().ForEach(tab => tab.Setup(_lastInitialized.Get()));
        _renderer.RenderTabs(AllTabs(), _lastInitialized.Get());
        SettingsOptionController._lastInitialized.IfPresent(gsm => gsm.RoleSettingsButton.gameObject.SetActive(AllTabs(true).Any()));
    }

    internal static void DoRender(RolesSettingsMenu menu)
    {
        if (!Enabled) return;
        if (CurrentTab == null) {
            // All tab detection.
            menu.scrollBar.ContentYBounds.max = _renderer.GetChancesHeight();
            return;
        }

        List<GameOption> currentOptions = CurrentTab.GetOptions();
        currentOptions.ForEach(child => {
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
                case OptionType.Role:
                    (child as RoleOption)!.HideMembers();
                    break;
                default:
                    (child as UndefinedOption)!.HideMembers();
                    break;
            }
        });
        _renderer.SetHeight(-0.1f);
        string currentText = SearchBarController.CurrentText;
        if (currentText == "") CurrentTab.PreRender().ForEach((option, index) => RenderCheck(option, index, menu));
        else currentOptions.Where(o => o.name.ToLower().Contains(currentText)).SelectMany(opt => opt.GetDisplayedMembers()).ForEach((option, index) => RenderCheck(option, index, menu));
        _renderer.PostRender(menu);
    }

    internal static void ValidateOptionBehaviour(GameOption option, RolesSettingsMenu menu, bool preRender = true)
    {
        if (option.BehaviourExists()) return;
        // log.Debug($"Making Behaviour for {option.name}");

        if (option.Level == 1) {
            if (option.OptionType == OptionType.Title) {
                UndefinedOption undefinedOption = (option as UndefinedOption)!;
                CategoryHeaderMasked categoryHeaderMasked = UnityEngine.Object.Instantiate(OptionExtensions.categoryHeaders.First(), Vector3.zero, Quaternion.identity, menu.RoleChancesSettings.transform);
                categoryHeaderMasked.name = "ModdedCategory";
			    categoryHeaderMasked.SetHeader(undefinedOption.Name(), 20);
			    categoryHeaderMasked.transform.localScale = Vector3.one * 0.63f;
                undefinedOption.Header = UnityOptional<CategoryHeaderMasked>.NonNull(categoryHeaderMasked);
                _renderer.PreRender(option, null!, menu);
                undefinedOption.BindPlusMinusButtons();
                return;
            } else if (option.OptionType != OptionType.Role) {
                throw new Exception($"Top Option Type must be a Role Option or a Title, not {option.OptionType.ToString()}");
            }
            // move role setting code here too when I get the chance.
            RoleOption roleOption = (option as RoleOption)!;
            RoleOptionIntializer.RoleOptionIntialized settingsHolder = RoleOptionIntializer.Intitialize();
            settingsHolder.RoleSetting = roleOption.Behaviour.Get();
            roleOption.SettingsHolder = Optional<RoleOptionIntializer.RoleOptionIntialized>.NonNull(settingsHolder);
            settingsHolder.RoleDesc.text = option.Description.OrElseGet(() => "Please use .Description on the Option Builder to set the text here.");
            settingsHolder.MainObject.transform.parent = roleOption.Tab!.OptionParent();
            settingsHolder.MainObject.transform.localScale = Vector3.one;
            settingsHolder.MainObject.name = "ModdedSettingsHolder";
            settingsHolder.MainObject.FindChildOrEmpty<CategoryHeaderMasked>("CategoryHeaderMasked").IfPresent(chm => chm.gameObject.Destroy());
            _renderer.PreRender(option, RenderOptions, menu);
            if (roleOption.roleInitializerSetup != null) roleOption.roleInitializerSetup(settingsHolder);
            return;
        }

        RoleOption roleParent = (option.GetAncestor() as RoleOption)!;

        switch (option.OptionType) 
        {
            case OptionType.String:
                TextOption textOption = (option as TextOption)!;
                StringGameSetting stringGameSetting = ScriptableObject.CreateInstance<StringGameSetting>();
                stringGameSetting.Values = Enumerable.Repeat(StringNames.None, option.Values.Count).ToArray();
                stringGameSetting.OptionName = Int32OptionNames.Invalid;
                stringGameSetting.Index = option.DefaultIndex;
                stringGameSetting.Type = OptionTypes.String;
                StringOption stringBehavior = UnityEngine.Object.Instantiate<StringOption>(menu.stringOptionOrigin, Vector3.zero, Quaternion.identity, CurrentTab.OptionParent());
                stringBehavior.name = "ModdedSetting";
			    stringBehavior.SetClickMask(menu.ButtonClickMask);
			    stringBehavior.SetUpFromData(stringGameSetting, 20);
                stringBehavior.Value = stringBehavior.oldValue = -1;
                stringBehavior.TitleText.text = option.Name();
                stringBehavior.ValueText.text = option.GetValueText();
                stringBehavior.OnValueChanged = new Action<OptionBehaviour>(_ => { });
                textOption.Behaviour = UnityOptional<StringOption>.NonNull(stringBehavior);
                if (preRender) _renderer.PreRender(option, RenderOptions, menu);
                textOption.BindPlusMinusButtons();
                break;
            case OptionType.Bool:
                BoolOption boolOption = (option as BoolOption)!;
                CheckboxGameSetting checkboxSettings = ScriptableObject.CreateInstance<CheckboxGameSetting>();
                checkboxSettings.OptionName = BoolOptionNames.Invalid;
                checkboxSettings.Type = OptionTypes.Checkbox;
                ToggleOption toggleBehavior = UnityEngine.Object.Instantiate<ToggleOption>(menu.checkboxOrigin, Vector3.zero, Quaternion.identity, CurrentTab.OptionParent());
                toggleBehavior.name = "ModdedSetting";
			    toggleBehavior.SetClickMask(menu.ButtonClickMask);
			    toggleBehavior.SetUpFromData(checkboxSettings, 20);
                toggleBehavior.CheckMark.enabled = option.GetValueText() == true.ToString();
                toggleBehavior.TitleText.text = option.Name();
                toggleBehavior.OnValueChanged = new Action<OptionBehaviour>(_ => { });
                boolOption.Behaviour = UnityOptional<ToggleOption>.NonNull(toggleBehavior);
                if (preRender) _renderer.PreRender(option, RenderOptions, menu);
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
                StringOption numberBehavior = UnityEngine.Object.Instantiate<StringOption>(menu.stringOptionOrigin, Vector3.zero, Quaternion.identity, CurrentTab.OptionParent());
                numberBehavior.name = "ModdedSetting";
			    numberBehavior.SetClickMask(menu.ButtonClickMask);
			    numberBehavior.SetUpFromData(numberGameSetting, 20);
                numberBehavior.Value = numberBehavior.oldValue = -1;
                numberBehavior.TitleText.text = option.Name();
                numberBehavior.ValueText.text = option.GetValueText();
                numberBehavior.OnValueChanged = new Action<OptionBehaviour>(_ => { });
                floatOption.Behaviour = UnityOptional<StringOption>.NonNull(numberBehavior);
                if (option.name == "Maximum" | option.name == "Percentage" && option.Level == 2) numberBehavior.gameObject.SetActive(false);
                if (preRender) _renderer.PreRender(option, RenderOptions, menu);
                floatOption.BindPlusMinusButtons();
                break;
            case OptionType.Player:
                UndefinedOption undefinedPlayerOption = (option as UndefinedOption)!;
                PlayerSelectionGameSetting playerGameSetting = new() {
                    OptionName = Int32OptionNames.Invalid
                };
                PlayerOption optionBehaviour = UnityEngine.Object.Instantiate(GameSettingMenu.Instance.GameSettingsTab.playerOptionOrigin, Vector3.zero, Quaternion.identity, CurrentTab.OptionParent());
                optionBehaviour.name = "ModdedSetting";
			    optionBehaviour.SetClickMask(menu.ButtonClickMask);
			    optionBehaviour.SetUpFromData(playerGameSetting, 20);
                optionBehaviour.TitleText.text = option.Name();
                undefinedPlayerOption.Behaviour = UnityOptional<PlayerOption>.NonNull(optionBehaviour);
                if (preRender) _renderer.PreRender(option, RenderOptions, menu);
                undefinedPlayerOption.BindPlusMinusButtons();
                break;
            case OptionType.Role: // not supposed to be here
                break;
            default:
                UndefinedOption undefinedOption = (option as UndefinedOption)!;
                CategoryHeaderMasked template = OptionExtensions.categoryHeaders.First();
                CategoryHeaderMasked categoryHeaderMasked = UnityEngine.Object.Instantiate(template, Vector3.zero, Quaternion.identity, roleParent.SettingsHolder.Get().MainObject.transform);
                categoryHeaderMasked.name = "ModdedCategory";
			    categoryHeaderMasked.SetHeader(undefinedOption.Name(), 20);
                undefinedOption.Header = UnityOptional<CategoryHeaderMasked>.NonNull(categoryHeaderMasked);
                if (preRender) _renderer.PreRender(option, RenderOptions, menu);
                undefinedOption.BindPlusMinusButtons();
                break;
        }
    }

    private static void RenderCheck(GameOption option, int index, RolesSettingsMenu menu)
    {
        ValidateOptionBehaviour(option, menu);
        _renderer.Render(option, (option.Level, index), RenderOptions, menu);
    }

    private static void SwitchTab(IGameOptionTab? newTab)
    {
        _currentTab?.Deactivate();
        newTab?.Activate();
        TabChangeEvent changeEvent = new(_currentTab, newTab);
        _tabEvents.ForEach(handler => handler(changeEvent));
    }
}