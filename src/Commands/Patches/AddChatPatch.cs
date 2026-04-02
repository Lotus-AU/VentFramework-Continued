using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using VentLib.Logging.Default;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Harmony.Attributes;

namespace VentLib.Commands.Patches;

[HarmonyPriority(Priority.First)]
[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class AddChatPatch
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static bool Prefix(ChatController __instance, PlayerControl sourcePlayer, string chatText)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        chatText = chatText.Trim(); // Trim spaces from chatText
        if (!chatText.StartsWith(CommandRunner.Prefix)) return true;
        if (sourcePlayer.IsHost()) return true;
        CommandRunner.Instance.Execute(new CommandContext(sourcePlayer, chatText[CommandRunner.Prefix.Length..]));
        return false; // hide command from host as well
    }
    
    [QuickPrefix(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat), Priority.First)]
    internal static bool HostCommandCheck(PlayerControl __instance, string chatText, ref bool __result)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        chatText = chatText.Trim(); // Trim spaces from chatText
        if (!chatText.StartsWith(CommandRunner.Prefix)) return true;
        if (!__instance.IsHost()) return true;
        CommandRunner.Instance.Execute(new CommandContext(__instance, chatText[CommandRunner.Prefix.Length..]));
        __result = false;
        return false;
    }
}