using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VentLib.Utilities.Optionals;

namespace VentLib.Options.UI.Tabs.Vanilla;

public sealed class GuardianAngelTab: VanillaTab
{
    public static GuardianAngelTab Instance = null!;

    private UnityOptional<PassiveButton> highlight = UnityOptional<PassiveButton>.Null();
    private UnityOptional<RolesSettingsMenu> innerMenu = UnityOptional<RolesSettingsMenu>.Null();

    public GuardianAngelTab()
    {
        Instance = this;
    }

    public override OptionBehaviour InitializeOption(OptionBehaviour sourceBehavior)
    {
        if (!innerMenu.Exists()) throw new ArgumentException("Cannot Initialize Behaviour because menu does not exist");
        return UnityEngine.Object.Instantiate(sourceBehavior, innerMenu.Get().transform);
    }

    public override void Setup(RolesSettingsMenu menu)
    {
        RelatedMenu = UnityOptional<RolesSettingsMenu>.NonNull(menu);
        roleCategory = GameManager.Instance.GameSettingsList.AllRoles.ToArray().Where(cat => cat.Role.Role == AmongUs.GameOptions.RoleTypes.GuardianAngel).First();

        RoleSettingsTabButton button = UnityEngine.Object.Instantiate<RoleSettingsTabButton>(menu.roleSettingsTabButtonOrigin, Vector3.zero, Quaternion.identity, menu.AllButton.transform.parent);
        TabButton = UnityOptional<RoleSettingsTabButton>.NonNull(button);
        menu.roleTabs.Add(button.Button);
        button.SetButton(roleCategory.Role, (Action)(() => {
            menu.ChangeTab(roleCategory, button.Button);
        }));
        highlight = UnityOptional<PassiveButton>.NonNull(button.Button);
        button.Button.OnClick.AddListener((Action)HandleClick);
    }

    public override List<GameOption> PreRender(int? targetLevel = null)
    {
        return new List<GameOption>();
    }

    protected override UnityOptional<PassiveButton> PassiveButton() => highlight;
}