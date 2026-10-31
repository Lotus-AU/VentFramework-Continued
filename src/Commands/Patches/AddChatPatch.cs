using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        var prefix = CommandRunner.Prefixes
            .OrderByDescending(p => p.Length)
            .FirstOrOptional(chatText.StartsWith);
        if (!prefix.Exists()) return true;
        if (sourcePlayer.IsHost()) return true;
        var actual = prefix.Get();
        CommandRunner.Instance.Execute(new CommandContext(sourcePlayer, chatText[actual.Length..], actual));
        return actual == "/cmd"; // hide command from host as well
    }
    
    [QuickPrefix(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat), Priority.First)]
    internal static bool HostCommandCheck(PlayerControl __instance, string chatText, ref bool __result)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        chatText = chatText.Trim(); // Trim spaces from chatText
        var prefix = CommandRunner.Prefixes
            .OrderByDescending(p => p.Length)
            .FirstOrOptional(chatText.StartsWith);
        if (!prefix.Exists()) return true;
        if (!__instance.IsHost()) return true;
        var actual = prefix.Get();
        CommandRunner.Instance.Execute(new CommandContext(__instance, chatText[actual.Length..], actual));
        __result = false;
        return actual != "/cmd";
    }
}