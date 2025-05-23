using System;
using System.Linq;
using AmongUs.Data;
using HarmonyLib;
using VentLib.Logging;
using VentLib.Networking;
using VentLib.Utilities;

namespace VentLib.Lobbies.Patches;

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
public class LobbyPublicPatch
{
    private static StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(LobbyPublicPatch));
    private static DateTime? _lastUpdate;
    public static void Prefix(GameStartManager __instance)
    {
        if (_lastUpdate != null && DateTime.Now.Subtract(_lastUpdate.Value).TotalSeconds < 30.5f) return;
        _lastUpdate = DateTime.Now;
        if (!AmongUsClient.Instance.AmHost) return;
        log.Info($"Lobby Created: {AmongUsClient.Instance.GameId}", "ModdedLobbyCheck");
        if (!NetworkRules.AllowRoomDiscovery) return;
        log.Info("Posting Room to Public", "RoomDiscovery");
        Async.Execute(LobbyChecker.PostLobbyToEndpoints(AmongUsClient.Instance.GameId, 
            DataManager.Player.customization.name, 
            PlayerControl.AllPlayerControls.ToArray().Where(p => p != null && !p.Data.Disconnected).Count()));
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    public static void CheckLobbyState(AmongUsClient __instance)
    {
        Async.Schedule(() =>
        {
            if (__instance == null) return;
            if (GameStartManager.Instance == null) return;
            if (__instance.IsGamePublic) Prefix(GameStartManager.Instance);
        }, 0.1f);
    }
}