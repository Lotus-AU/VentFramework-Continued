using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using VentLib.Logging.Default;
using VentLib.Options.Enum;
using VentLib.Options.Events;
using VentLib.Options.Extensions;
using VentLib.Options.Interfaces;
using VentLib.Options.UI;
using VentLib.Options.UI.Controllers;
using VentLib.Options.UI.Options;
using VentLib.Utilities;
using VentLib.Utilities.Attributes;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Harmony.Attributes;
using VentLib.Utilities.Optionals;
using Object = UnityEngine.Object;

namespace VentLib.Options.Patches;

[HarmonyPatch(typeof(LobbyViewSettingsPane))]
[LoadStatic]
public class LobbyViewSettingsPanePatch
{
    private static UnityOptional<LobbyViewSettingsPane> _lastInitialized = new();

    static LobbyViewSettingsPanePatch()
    {
        SettingsOptionController.RegisterEventHandler(SettingsEvent);
        RoleOptionController.RegisterEventHandler(RoleEvent);
    }

    private static void SettingsEvent(IControllerEvent @event)
    {
        if (!SettingsOptionController.Enabled) return;
        if (!_lastInitialized.Exists()) return;
        if (!_lastInitialized.Get().gameObject.activeSelf) return;
        if (@event is not MainSettingChangeEvent changeEvent) return;
        
        _lastInitialized.Get().FindChildOrEmpty<PassiveButton>("ModdedSettings").IfPresent(pb =>
        {
            pb.gameObject.SetActive(changeEvent.Source() != null);
            pb.FindChild<TextMeshPro>("Text_TMP").text = changeEvent.Source()?.ButtonText ?? "OFF";
            if (pb.selected) Async.Schedule(() => RefreshTabForSettings(_lastInitialized.Get()), .1f);
        });
    }

    private static void RoleEvent(IControllerEvent @event)
    {
        if (!RoleOptionController.Enabled) return;
        if (!_lastInitialized.Exists()) return;
        if (!_lastInitialized.Get().gameObject.activeSelf) return;
        if (@event is not TabChangeEvent changeEvent) return;
        
        if (_lastInitialized.Get().rolesTabButton.selected) Async.Schedule(() => RefreshTabForRole(_lastInitialized.Get()), .1f);
    }
    
    [QuickPostfix(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.Awake))]
    private static void AwakePatch(LobbyViewSettingsPane __instance)
    {
        _lastInitialized = UnityOptional<LobbyViewSettingsPane>.NonNull(__instance);
        if (SettingsOptionController.Enabled) SetupCustomSettingTab(__instance);
        if (RoleOptionController.Enabled) HijackRoleTab(__instance);
    }
    
    [QuickPostfix(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.ChangeTab))]
    private static void ChangeTabPatch(LobbyViewSettingsPane __instance)
    {
        var moddedSettingTab = __instance.FindChildOrEmpty<PassiveButton>("ModdedSettings");
        moddedSettingTab.IfPresent(p => p.SelectButton(false));
    }

    private static void HijackRoleTab(LobbyViewSettingsPane pane)
    {
        pane.rolesTabButton.OnClick.RemoveAllListeners();
        pane.rolesTabButton.OnClick.AddListener((Action)(() =>
        {
            pane.FindChildOrEmpty<PassiveButton>("ModdedSettings").IfPresent(p => p.SelectButton(false));
            pane.rolesTabButton.SelectButton(true);
            pane.taskTabButton.SelectButton(false);
            
            RefreshTabForRole(pane);
            pane.scrollBar.ScrollToTop();
        }));
    }

    private static void SetupCustomSettingTab(LobbyViewSettingsPane pane)
    {
        PassiveButton customSettingTab = Object.Instantiate(pane.FindChild<PassiveButton>("RolesTabs"), pane.transform);
        customSettingTab.transform.localPosition += new Vector3(3.471f, 0f, 0);
        customSettingTab.gameObject.SetActive(true);
        customSettingTab.name = "ModdedSettings";

        TextMeshPro textTMP = customSettingTab.FindChild<TextMeshPro>("Text_TMP");
        textTMP.GetComponent<TextTranslatorTMP>().Destroy();

        textTMP.text = SettingsOptionController.MainSettingsTab.ButtonText;
        
        customSettingTab.OnClick.RemoveAllListeners();
        customSettingTab.OnClick.AddListener((Action)(() =>
        {
            pane.rolesTabButton.SelectButton(false);
            pane.taskTabButton.SelectButton(false);
            customSettingTab.SelectButton(true);
            
            RefreshTabForSettings(pane);
            pane.scrollBar.ScrollToTop();
        }));
    }

    private static void GlobalRefresh(LobbyViewSettingsPane pane)
    {
        pane.settingsInfo.ForEach((Action<GameObject>)(o => o.Destroy()));
        pane.settingsInfo.Clear();
    }

    private static void RefreshTabForSettings(LobbyViewSettingsPane pane)
    {
        NoDepLogger.Info("reloading settings tab");
        GlobalRefresh(pane);
        IMainSettingTab? currentTab = SettingsOptionController.MainSettingsTab;
        if (currentTab == null) return;
        float num = 1.44f;
        int xindex = 0;
        bool firstTitle = true;
        currentTab.PreRender().ForEach(option =>
        {
            if (option.OptionType == OptionType.Title)
            {
                if (firstTitle) firstTitle = false;
                else num -= 0.85f;
                CategoryHeaderMasked categoryHeaderMasked = Object.Instantiate(pane.categoryHeaderOrigin, pane.settingsContainer);
                categoryHeaderMasked.SetHeader(option.Name(), 61);
                categoryHeaderMasked.transform.localScale = Vector3.one;
                categoryHeaderMasked.transform.localPosition = new(-9.77f, num, -2f);
                num -= 1.05f;
                xindex = 0;
                pane.settingsInfo.Add(categoryHeaderMasked.gameObject);
                return;
            }
            ViewSettingsInfoPanel viewSettingsInfoPanel = Object.Instantiate(pane.infoPanelOrigin, pane.settingsContainer);
            viewSettingsInfoPanel.transform.localScale = Vector3.one;
            float x;
            if (xindex % 2 == 0)
            {
                x = -8.95f;
                if (xindex > 0) num -= 0.85f;
            }
            else x = -3f;

            xindex += 1;

            viewSettingsInfoPanel.transform.localPosition = new(x, num, -2f);
            if (option.OptionType == OptionType.Bool)
            {
                // Display checkmark
                bool isOn = (bool)option.GetValue();
                viewSettingsInfoPanel.titleText.text = option.Name();
                viewSettingsInfoPanel.settingText.text = string.Empty;
                viewSettingsInfoPanel.checkMarkOff.gameObject.SetActive(!isOn);
                viewSettingsInfoPanel.background.gameObject.SetActive(true);
                viewSettingsInfoPanel.checkMark.gameObject.SetActive(isOn);
                viewSettingsInfoPanel.SetMaskLayer(61);
            }
            else
            {
                viewSettingsInfoPanel.titleText.text = option.Name();
                viewSettingsInfoPanel.settingText.text = option.GetValueText();
                viewSettingsInfoPanel.disabledBackground.gameObject.SetActive(false);
                viewSettingsInfoPanel.background.gameObject.SetActive(true);
                viewSettingsInfoPanel.SetMaskLayer(61);
            }
            pane.settingsInfo.Add(viewSettingsInfoPanel.gameObject);
        });
        // pane.scrollBar.CalculateAndSetYBounds((float)(pane.settingsInfo.Count + 10), 2f, 6f, 0.85f);
        pane.scrollBar.SetYBoundsMax(-num);
    }

    private static void RefreshTabForRole(LobbyViewSettingsPane pane)
    {
        NoDepLogger.Info("reloading role tab");
        GlobalRefresh(pane);
        
        CategoryHeaderMasked categoryHeaderMasked = Object.Instantiate(pane.categoryHeaderOrigin, pane.settingsContainer);
        categoryHeaderMasked.SetHeader(StringNames.RoleQuotaLabel, 61);
        categoryHeaderMasked.transform.localScale = Vector3.one;
        categoryHeaderMasked.transform.localPosition = new Vector3(-9.77f, 1.26f, -2f);
        pane.settingsInfo.Add(categoryHeaderMasked.gameObject);
        
        IGameOptionTab[] allTabs = RoleOptionController.AllTabs();

        float num = .95f;
        float x = -6.53f;

        List<RoleOption> roleOptions = new();
        
        // Generate Headers
        allTabs.ForEach(tab =>
        {
            CategoryHeaderRoleVariant categoryHeaderRoleVariant = Object.Instantiate(pane.categoryHeaderRoleOrigin, pane.settingsContainer);
            categoryHeaderRoleVariant.SetHeader(tab.Name, 61);
            categoryHeaderRoleVariant.transform.localScale = Vector3.one;
            categoryHeaderRoleVariant.transform.localPosition = new Vector3(0.09f, num, -2f);
            pane.settingsInfo.Add(categoryHeaderRoleVariant.gameObject);
            num -= 0.696f;
            tab.PreRender(1).ForEach(option =>
            {
                if (option.OptionType == OptionType.Title)
                {
                    CategoryHeaderMasked titleHeaderMask = Object.Instantiate(pane.categoryHeaderOrigin, pane.settingsContainer);
                    titleHeaderMask.SetHeader(option.Name(), 61);
                    titleHeaderMask.transform.localScale = Vector3.one;
                    titleHeaderMask.transform.localPosition = new(-9.77f, num, -2f);
                    pane.settingsInfo.Add(titleHeaderMask.gameObject);
                    num -= 1.05f;
                    return;
                }
                if (option is not RoleOption roleOptionInstance) return; // Only allow role options if dev accidentally added smth else.
                
                // what a mouthful.
                ViewSettingsInfoPanelRoleVariant roleInfoPanel = Object.Instantiate(pane.infoPanelRoleOrigin, pane.settingsContainer);
                roleInfoPanel.transform.localScale = Vector3.one;
                roleInfoPanel.transform.localPosition = new(x, num, -2f);

                int chancePerGame = roleOptionInstance.PercentageOption?.GetValue<int>() ?? 0;
                int numPerGame = roleOptionInstance.MaximumOption?.GetValue<int>() ?? 0;
                bool isOn = chancePerGame > 0 && numPerGame > 0;
                
                roleInfoPanel.titleText.text = option.Name(false);
                roleInfoPanel.settingText.text = numPerGame.ToString();
                roleInfoPanel.chanceText.text = chancePerGame.ToString();
                roleInfoPanel.iconSprite.gameObject.SetActive(false);
                if (isOn)
                {
                    roleInfoPanel.titleText.color = RoleOptionController._renderer.IsColorBright(roleOptionInstance.Color) ? Color.black : Color.white;
                    roleInfoPanel.labelBackground.color = roleOptionInstance.Color;
                    if (tab.GetType().Name.Contains("impostor") | tab.GetType().Name.ToLower().Contains("imposter"))
                    {
                        roleInfoPanel.chanceBackground.sprite = roleInfoPanel.impostorCube;
                        roleInfoPanel.background.sprite = roleInfoPanel.impostorCube;
                    }
                    else
                    {
                        roleInfoPanel.chanceBackground.sprite = roleInfoPanel.crewmateCube;
                        roleInfoPanel.background.sprite = roleInfoPanel.crewmateCube;
                    }
                }
                else
                {
                    roleInfoPanel.titleText.color = Palette.White_75Alpha;
                    roleInfoPanel.chanceTitle.color = Palette.White_75Alpha;
                    roleInfoPanel.chanceBackground.sprite = roleInfoPanel.disabledCube;
                    roleInfoPanel.background.sprite = roleInfoPanel.disabledCube;
                    roleInfoPanel.labelBackground.color = Palette.DisabledGrey;
                }
                roleInfoPanel.SetMaskLayer(61);
                if (isOn) roleOptions.Add(roleOptionInstance);
                
                pane.settingsInfo.Add(roleInfoPanel.gameObject);
                num -= 0.664f;
            });
        });
        
        // Generate Role Options;
        roleOptions.ForEach(roleOptionInstance =>
        {
            CategoryHeaderMasked titleHeaderMask = Object.Instantiate(pane.categoryHeaderOrigin, pane.settingsContainer);
            titleHeaderMask.SetHeader(roleOptionInstance.Name(), 61);
            titleHeaderMask.transform.localScale = Vector3.one;
            titleHeaderMask.transform.localPosition = new(-9.77f, num, -2f);
            pane.settingsInfo.Add(titleHeaderMask.gameObject);
            num -= 2.1f;
            

            List<GameOption> childOptions = roleOptionInstance.Children.GetConditionally(roleOptionInstance.GetValue())
                .Cast<GameOption>()
                .SelectMany(child => child.GetDisplayedMembers())
                .ToList();
            
            int xindex = 0;
            childOptions.ForEach(option =>
            {
                if (option.OptionType == OptionType.Title)
                {
                    CategoryHeaderMasked categoryHeaderMasked = Object.Instantiate(pane.categoryHeaderOrigin, pane.settingsContainer);
                    categoryHeaderMasked.SetHeader(option.Name(), 61);
                    categoryHeaderMasked.transform.localScale = Vector3.one;
                    categoryHeaderMasked.transform.localPosition = new(-9.77f, num, -2f);
                    pane.settingsInfo.Add(categoryHeaderMasked.gameObject);
                    num -= 1.05f;
                    xindex = 0;
                    return;
                }
                ViewSettingsInfoPanel viewSettingsInfoPanel = Object.Instantiate(pane.infoPanelOrigin, pane.settingsContainer);
                viewSettingsInfoPanel.transform.localScale = Vector3.one;
                float x;
                if (xindex % 2 == 0)
                {
                    x = -8.95f;
                    if (xindex > 0) num -= 0.85f;
                }
                else x = -3f;
                xindex += 1;
                viewSettingsInfoPanel.transform.localPosition = new(x, num, -2f);
                
                if (option.OptionType == OptionType.Bool)
                {
                    // Display checkmark
                    bool isOn = (bool)option.GetValue();
                    viewSettingsInfoPanel.titleText.text = option.Name();
                    viewSettingsInfoPanel.settingText.text = string.Empty;
                    viewSettingsInfoPanel.checkMarkOff.gameObject.SetActive(!isOn);
                    viewSettingsInfoPanel.background.gameObject.SetActive(true);
                    viewSettingsInfoPanel.checkMark.gameObject.SetActive(isOn);
                    viewSettingsInfoPanel.SetMaskLayer(61);
                }
                else
                {
                    viewSettingsInfoPanel.titleText.text = option.Name();
                    viewSettingsInfoPanel.settingText.text = option.GetValueText();
                    viewSettingsInfoPanel.disabledBackground.gameObject.SetActive(false);
                    viewSettingsInfoPanel.background.gameObject.SetActive(true);
                    viewSettingsInfoPanel.SetMaskLayer(61);
                }
                pane.settingsInfo.Add(viewSettingsInfoPanel.gameObject);
            });
            num -= 0.85f;
        });
        
        pane.scrollBar.SetYBoundsMax(-num);
    }
}