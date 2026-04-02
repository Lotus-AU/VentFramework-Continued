using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VentLib.Options.Enum;
using VentLib.Utilities.Optionals;
using VentLib.Utilities.Extensions;

namespace VentLib.Options.UI.Options;
public class RoleOption: GameOption, IGameOptionInstance
{
    internal Optional<RoleOptionIntializer.RoleOptionIntialized> SettingsHolder = new();
    internal UnityOptional<RoleOptionSetting> Behaviour = new();

    internal Action<RoleOptionIntializer.RoleOptionIntialized>? roleInitializerSetup;
    internal FloatOption? PercentageOption = null!;
    internal FloatOption? MaximumOption = null!;

    internal void HideMembers()
    {
        // Behaviour.IfPresent(behaviour => behaviour.gameObject.SetActive(false));
        SettingsHolder.IfPresent(holder => {
            if (UnityEngine.Object.IsNativeObjectAlive(holder.MainObject)) holder.MainObject.SetActive(false); else {
                SettingsHolder = Optional<RoleOptionIntializer.RoleOptionIntialized>.Null();
            }
        });
        Children.ForEach(child => {
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
                case OptionType.Player:
                    (child as UndefinedOption)!.HideMembers();
                    break;
                default:
                    (child as UndefinedOption)!.HideMembers();
                    break;
            }
        });
    }

    public void IncrementChance()
    {
        PercentageOption?.Increment();
        Behaviour.IfPresent(b => {
            b.chanceText.text = PercentageOption?.GetValueText();
        });
    }

    public void DecrementChance()
    {
        PercentageOption?.Decrement();
        Behaviour.IfPresent(b => {
            b.chanceText.text = PercentageOption?.GetValueText();
        });
    }

    public void IncrementRoleCount()
    {
        MaximumOption?.Increment();
        Behaviour.IfPresent(b => {
            b.countText.text = MaximumOption?.GetValueText();
        });
    }

    public void DecrementRoleCount()
    {
        MaximumOption?.Decrement();
        Behaviour.IfPresent(b => {
            b.countText.text = MaximumOption?.GetValueText();
        });
    }

    public void UpdateOption()
    {
        MaximumOption?.Manager?.Load(MaximumOption, true);
        MaximumOption?.UpdateOption();
        
        PercentageOption?.Manager?.Load(PercentageOption, true);
        PercentageOption?.UpdateOption();
        
        Behaviour.IfPresent(b => {
            b.countText.text = MaximumOption?.GetValueText();
            b.chanceText.text = PercentageOption?.GetValueText();
        });
    }

    internal void BindPlusMinusButtons()
    {
        Behaviour.IfPresent(b => {
            MaximumOption = Children.Find(child => child.Key() == "Maximum") as FloatOption;
            if (MaximumOption != null) {
                PassiveButton plusButtonNumber = b.FindChild<PassiveButton>("Role #/PlusButton");
                PassiveButton minusButtonNumber = b.FindChild<PassiveButton>("Role #/MinusButton");

                plusButtonNumber.OnClick = new Button.ButtonClickedEvent();
                plusButtonNumber.OnMouseOut = new UnityEngine.Events.UnityEvent();
                plusButtonNumber.OnMouseOver = new UnityEngine.Events.UnityEvent();
                plusButtonNumber.OnClick.AddListener((Action)IncrementRoleCount);
                SpriteRenderer plusActiveSpriteNumber = plusButtonNumber.FindChild<SpriteRenderer>("ButtonSprite");
                plusButtonNumber.OnMouseOut.AddListener((Action)(() => plusActiveSpriteNumber.color = Color.white));
                plusButtonNumber.OnMouseOver.AddListener((Action)(() => plusActiveSpriteNumber.color = Color.cyan));

                minusButtonNumber.OnClick = new Button.ButtonClickedEvent();
                minusButtonNumber.OnMouseOut = new UnityEngine.Events.UnityEvent();
                minusButtonNumber.OnMouseOver = new UnityEngine.Events.UnityEvent();
                minusButtonNumber.OnClick.AddListener((Action)DecrementRoleCount);
                SpriteRenderer minusActiveSpriteNumber = minusButtonNumber.FindChild<SpriteRenderer>("ButtonSprite");
                minusButtonNumber.OnMouseOut.AddListener((Action)(() => minusActiveSpriteNumber.color = Color.white));
                minusButtonNumber.OnMouseOver.AddListener((Action)(() => minusActiveSpriteNumber.color = Color.cyan));
                b.countText.text = MaximumOption.GetValueText();
            } else {
                b.transform.FindChild("Role #").gameObject.SetActive(false);
            }
            PercentageOption = Children.Find(child => child.Key() == "Percentage") as FloatOption;
            if (PercentageOption != null)
            {
                PassiveButton plusButton = b.FindChild<PassiveButton>("Chance %/PlusButton");
                PassiveButton minusButton = b.FindChild<PassiveButton>("Chance %/MinusButton");

                plusButton.OnClick = new Button.ButtonClickedEvent();
                plusButton.OnMouseOut = new UnityEngine.Events.UnityEvent();
                plusButton.OnMouseOver = new UnityEngine.Events.UnityEvent();
                plusButton.OnClick.AddListener((Action)IncrementChance);
                SpriteRenderer plusActiveSprite = plusButton.FindChild<SpriteRenderer>("ButtonSprite");
                plusButton.OnMouseOut.AddListener((Action)(() => plusActiveSprite.color = Color.white));
                plusButton.OnMouseOver.AddListener((Action)(() => plusActiveSprite.color = Color.cyan));

                minusButton.OnClick = new Button.ButtonClickedEvent();
                minusButton.OnMouseOut = new UnityEngine.Events.UnityEvent();
                minusButton.OnMouseOver = new UnityEngine.Events.UnityEvent();
                minusButton.OnClick.AddListener((Action)DecrementChance);
                SpriteRenderer minusActiveSprite = minusButton.FindChild<SpriteRenderer>("ButtonSprite");
                minusButton.OnMouseOut.AddListener((Action)(() => minusActiveSprite.color = Color.white));
                minusButton.OnMouseOver.AddListener((Action)(() => minusActiveSprite.color = Color.cyan));
                // b.chanceText.text = PercentageOption.GetValueText();
                b.chanceText.text = $"{PercentageOption.GetValue()}%";
                // Value.Map(v => v.Value).IfNotPresent(() => b.chanceText.text = "0%");
            } else b.transform.FindChild("Chance %").gameObject.SetActive(false);
        });
    }

    public static RoleOption From(GameOption option)
    {
        RoleOption roleOption = new RoleOption() {
            name = option.name,
            key = option.key,
            Description = option.Description,
            IOSettings = option.IOSettings,
            OptionType = OptionType.Role,
            Values = option.Values,
            DefaultIndex = option.DefaultIndex,
            Attributes = option.Attributes,
        };
        if (option.valueType != null) roleOption.valueType = option.valueType;
        option.EventHandlers.ForEach(roleOption.RegisterEventHandler);
        option.Children.ForEach(roleOption.Children.Add);
        return roleOption;
    }
}