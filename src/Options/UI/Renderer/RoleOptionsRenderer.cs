using System.Linq;
using UnityEngine;
using VentLib.Logging;
using VentLib.Options.UI.Tabs;
using VentLib.Options.Extensions;
using VentLib.Options.Interfaces;
using VentLib.Utilities.Extensions;
using Sentry.Unity.NativeUtils;
using VentLib.Options.UI.Options;
using Il2CppSystem;
using VentLib.Utilities.Optionals;
using TMPro;

namespace VentLib.Options.UI.Renderer;

public class RoleOptionsRenderer : IRoleOptionRender
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(SettingsRenderer));
    private static readonly Color[] Colors = { Color.green, Color.red, Color.blue };
    private RoleOption? lastRoleOption = null;
    private float ChancesHeight = 0f;
    private float Height = 0f;

    public void SetHeight(float height) => Height = height;
    public float GetHeight() => Height;
    public float GetChancesHeight() => ChancesHeight;
    public virtual void RenderTabs(IGameOptionTab[] tabs, RolesSettingsMenu menu)
    {
        // Tab Button
        float xPos = -1.928f;
        tabs.ForEach(tb =>
        {
            tb.SetPosition(new Vector2(xPos, 2.27f));
            xPos += 0.762f;
        });
        Height = 0.662f;
        // All Role Settings
        tabs.ForEach((tb, index) =>
        {
            // Create Title Card
            string name = tb.Name;
            Color[] colors = tb.HeaderColors().OrElseGet(() => GetColorsFromName(name));
            CategoryHeaderEditRole categoryHeaderEditRole = UnityEngine.Object.Instantiate<CategoryHeaderEditRole>(menu.categoryHeaderEditRoleOrigin, Vector3.zero, Quaternion.identity, menu.RoleChancesSettings.transform);
            categoryHeaderEditRole.SetHeader(name, 20, colors);
            categoryHeaderEditRole.transform.localPosition = new Vector3(4.986f, Height, -2f);
            Height -= 0.522f;
            tb.PreRender(1).ForEach(opt =>
            {
                if (opt.OptionType == Enum.OptionType.Title)
                {
                    UndefinedOption undefinedOption = (opt as UndefinedOption)!;
                    CategoryHeaderMasked categoryHeaderMasked = UnityEngine.Object.Instantiate(OptionExtensions.categoryHeaders.First(), Vector3.zero, Quaternion.identity, menu.RoleChancesSettings.transform);
                    categoryHeaderMasked.name = "ModdedCategory";
                    categoryHeaderMasked.SetHeader(undefinedOption.Name(), 20);
                    categoryHeaderMasked.transform.localScale = Vector3.one * 0.63f;
                    categoryHeaderMasked.transform.localPosition = new Vector3(-0.45f, Height - 0.2f, -2f);
                    undefinedOption.Header.IfPresent(header => header.gameObject.Destroy());
                    undefinedOption.Header = UnityOptional<CategoryHeaderMasked>.NonNull(categoryHeaderMasked);
                    PreRender(opt, null!, menu);
                    undefinedOption.BindPlusMinusButtons();
                    Height -= 0.64f;
                    return;
                } 
                if (opt is not RoleOption roleOptionInstance) return;
                RoleOptionSetting roleOptionSetting = UnityEngine.Object.Instantiate<RoleOptionSetting>(menu.roleOptionSettingOrigin, Vector3.zero, Quaternion.identity, menu.RoleChancesSettings.transform);
                roleOptionSetting.transform.localPosition = new Vector3(-0.15f, Height, -2f);
                roleOptionSetting.OnValueChanged = new System.Action<OptionBehaviour>(_ => { });
                roleOptionSetting.SetClickMask(menu.ButtonClickMask);
                roleOptionSetting.SetText(roleOptionInstance.Name(false), roleOptionInstance.Color);
                roleOptionSetting.titleText.color = IsColorBright(roleOptionInstance.Color) ? Color.black : Color.white;
                roleOptionSetting.name = "ModdedRoleSetting";
                roleOptionInstance.Behaviour.IfPresent(b => b.gameObject.Destroy());
                roleOptionInstance.Behaviour = UnityOptional<RoleOptionSetting>.NonNull(roleOptionSetting);
                menu.roleChances.Add(roleOptionSetting);
                roleOptionInstance.BindPlusMinusButtons();
                Height += -0.43f;
            });
            if (index + 1 != tabs.Count()) Height -= 0.22f;
        });
        // Height += -0.43f;
        ChancesHeight = -Height - 1.65f;
        Height = 0f;
    }

    public virtual void PreRender(GameOption option, RenderOptions renderOptions, RolesSettingsMenu menu)
    {
        if (option.IsTitle)
        {
            return;
        }
        else if (option.OptionType == Enum.OptionType.Role)
        {
            RoleOption roleOption = (option as RoleOption)!;
            var settingsHolder = roleOption.SettingsHolder.Get();
            settingsHolder.RoleHeader.FindChild<SpriteRenderer>("LabelSprite").color = roleOption.Color;
            var roleTitle = settingsHolder.RoleHeader.FindChild<TextMeshPro>("RoleTitle");
            roleTitle.color = IsColorBright(roleOption.Color) ? Color.black : Color.white;
            roleTitle.text = roleOption.Name(false) + " Settings";
            return;
        }
        OptionBehaviour Behaviour = option.GetBehaviour();
        // Behaviour.transform.FindChild("Title Text").GetComponent<RectTransform>().sizeDelta = new Vector2(3.5f, 0.37f);

        if (option.OptionType != Enum.OptionType.Bool)
        {
            Behaviour.transform.FindChild("Value_TMP (1)").localPosition += new Vector3(0.3f, 0f, 0f);
            Behaviour.transform.FindChild("MinusButton").localPosition += new Vector3(0.3f, 0f, 0f);
            Behaviour.transform.FindChild("PlusButton").localPosition += new Vector3(0.3f, 0f, 0f);
            Behaviour.transform.FindChild("ValueBox").gameObject.SetActive(false);
        }
        else
        {
            Behaviour.transform.FindChild("Toggle").localPosition += new Vector3(1.3f, 0f, 0f);
        }
        Behaviour.transform.Find("LabelBackground").localPosition += new Vector3(1.75f + (0.4f * (option.Level - 1)), option.Level == 1 ? 0.005f : 0, 0f);
        var titleText = Behaviour.FindChild<TextMeshPro>("Title Text");
        titleText.transform.localPosition -= new Vector3(0.15f, 0f, 0f);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 1.3f;
        titleText.fontSizeMax = 2.5f;
    }


    public virtual void Render(GameOption option, (int level, int index) info, RenderOptions renderOptions, RolesSettingsMenu menu)
    {
        int lvl = info.level - 1;
        if (lvl == 0)
        {
            if (option.OptionType != Enum.OptionType.Role) return;
            if (lastRoleOption != null)
            {
                Height -= 1.2f;
            }
            lastRoleOption = (option as RoleOption)!;
            var roleObject = lastRoleOption.SettingsHolder.Get();
            roleObject.MainObject.SetActive(true);
            int optionCount = option.GetDisplayedMembers().Count - 1;
            float heightOffset = 0.45f * optionCount;
            roleObject.Background.size = new Vector2(89.4628f, 15.4009f + heightOffset);
            roleObject.MainObject.transform.localPosition = new Vector3(0f, Height, -2f);
            roleObject.Background.transform.localPosition = new Vector3(1.4041f, -0.7688f - (0.033334f * optionCount), 0f);
            Height -= 1.5f;
            return;
        }
        if (option.OptionType == Enum.OptionType.Title)
        {
            CategoryHeaderMasked categoryHeader = (option as UndefinedOption)!.Header.Get();
            categoryHeader.transform.localPosition = new Vector3(-0.903f, Height, -2f);
            categoryHeader.gameObject.SetActive(true);
            Height -= 0.64f;
            return;
        }
        if (option.name == "Maximum" | option.name == "Percentage" && lvl == 1) return;
        lvl -= 1;
        OptionBehaviour Behaviour = option.GetBehaviour();

        Transform transform = Behaviour.transform;
        SpriteRenderer render = Behaviour.transform.Find("LabelBackground").GetComponent<SpriteRenderer>();
        if (lvl > 0)
        {
            render.color = Colors[Mathf.Clamp((lvl - 1) % 3, 0, 2)];
            Behaviour.transform.Find("Title Text").transform.localPosition = new Vector3(-0.885f + 0.23f * Mathf.Clamp(lvl - 1, 0, int.MaxValue), 0f);
        }
        render.size = new Vector2(7f - (0.2f * lvl), 0.55f);

        transform.localPosition = new Vector3(0.952f, Height, -2f);
        Behaviour.gameObject.SetActive(true);
        Height -= 0.45f;
    }

    public virtual void PostRender(RolesSettingsMenu menu)
    {
        menu.scrollBar.ContentYBounds.max = -Height - 1.65f;
        lastRoleOption = null;
    }

    public virtual void Close()
    {
    }

    public virtual Color[] GetColorsFromName(string name) => (name.ToLower().Contains("impostor") | name.ToLower().Contains("imposter"))
        ? new Color[] { Palette.ImpostorRoleHeaderTextRed, Palette.ImpostorRoleHeaderRed, Palette.ImpostorRoleHeaderVeryDarkRed, Palette.ImpostorRoleHeaderDarkRed }
        : new Color[] { Palette.CrewmateRoleHeaderTextBlue, Palette.CrewmateRoleHeaderBlue, Palette.CrewmateRoleHeaderVeryDarkBlue, Palette.CrewmateRoleHeaderDarkBlue };

    public virtual bool IsColorBright(Color color) => (float)(0.299f * color.r + 0.587f * color.g + 0.114f * color.b) > (float)0.6f;
}