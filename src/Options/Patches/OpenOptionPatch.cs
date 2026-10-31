using HarmonyLib;
using UnityEngine;
using VentLib.Options.UI.Controllers;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using VentLib.Utilities.Optionals;
using VentLib.Utilities.Extensions;
using VentLib.Options.UI;
using VentLib.Options.UI.Controllers.Search;

namespace VentLib.Options.Patches;

[HarmonyPriority(Priority.First)]
[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
internal static class OpenGameSettingsPatch
{
    internal static void Prefix(GameSettingMenu __instance)
    {
        // Unlocks map/impostor amount changing in online (for testing on your custom servers)
        // Changed to be able to change the map in online mode without having to re-establish the room.
        __instance.GameSettingsTab.HideForOnline = new Il2CppReferenceArray<Transform>(0);
    }
    internal static void Postfix(GameSettingMenu __instance)
    {
        SettingsOptionController.Start(__instance);
        SearchBarController.HandleOpen(__instance);
        PresetManager.EditSettingsMenu(__instance);
        
        if (GameManager.Instance.IsHideAndSeek())
        {
            __instance.RoleSettingsTab.OpenMenu(false);
            __instance.RoleSettingsTab.gameObject.SetActive(false);
        }
    }
}

[HarmonyPriority(Priority.First)]
[HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.SetQuotaTab))]
public static class OpenRoleSettingsPatch
{
    public static bool Prefix(RolesSettingsMenu __instance)
    {
        if (RoleOptionController.Enabled)
        {
            if (RoleOptionIntializer.RoleTemplate.Exists()) return false;
            RoleOptionIntializer.RoleTemplate = UnityOptional<GameObject>.Of(__instance.transform.Find("Scroller/SliderInner/AdvancedTab").gameObject);
            if (RoleOptionIntializer.RoleTemplate.Exists()) {
                RoleOptionIntializer.RoleTemplate.Get().transform.Find("CategoryHeaderMasked").gameObject.SetActive(false);
                RoleOptionController.HandleOpen(__instance);
            }
        }
        return !RoleOptionController.Enabled;
    }
}
