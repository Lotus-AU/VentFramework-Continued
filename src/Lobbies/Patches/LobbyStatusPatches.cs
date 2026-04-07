using System;
using InnerNet;
using HarmonyLib;
using System.Linq;
using VentLib.Logging;
using VentLib.Networking;
using VentLib.Utilities.Harmony.Attributes;
using VentLib.Utilities.Extensions;
using AmongUs.GameOptions;

namespace VentLib.Lobbies.Patches;
public static class LobbyStatusPatches
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(LobbyStatusPatches));
    internal static int LastPlayerCount = 0;

    [QuickPrefix(typeof(AmongUsClient), nameof(AmongUsClient.StartGame))]
    private static void UpdateStatusInGame(AmongUsClient __instance)
    {
        if (!__instance.AmHost) return;
        if (!NetworkRules.AllowRoomDiscovery) return;

        log.Info($"Updating Lobby Status: {LobbyStatus.InGame}");
        LobbyChecker.UpdateLobbyStatus(__instance.GameId, PlayerControl.AllPlayerControls.ToArray().Where(p => p != null && !p.Data.Disconnected).Count(), LobbyStatus.InGame);
    }
    
    [QuickPrefix(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
    private static void UpdateStatusClosed(InnerNetClient __instance, DisconnectReasons reason)
    {
        if (reason is DisconnectReasons.NewConnection || !__instance.AmHost) return;
        if (!NetworkRules.AllowRoomDiscovery) return;
        log.Info($"Updating Lobby Status: {LobbyStatus.Closed}");
        LobbyChecker.UpdateLobbyStatus(__instance.GameId, PlayerControl.AllPlayerControls.ToArray().Where(p => p != null && !p.Data.Disconnected).Count(), LobbyStatus.Closed);
    }
    
    [QuickPostfix(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    private static void UpdateStatusStart(LobbyBehaviour __instance)
    {   
        if (!AmongUsClient.Instance.AmHost) return;
        if (!NetworkRules.AllowRoomDiscovery) return;

        int newPlayerCount = PlayerControl.AllPlayerControls.ToArray().Where(p => p != null && !p.Data.Disconnected).Count();
        LastPlayerCount = newPlayerCount;
        log.Info($"Updating Lobby Status: {LobbyStatus.Open}");
        LobbyChecker.UpdateLobbyStatus(AmongUsClient.Instance.GameId, newPlayerCount, LobbyStatus.Open);
    }
    
    [QuickPrefix(typeof(PlayerPhysics), nameof(PlayerPhysics.CoSpawnPlayer))]
    private static void UpdatePlayersOnJoin(PlayerPhysics __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!NetworkRules.AllowRoomDiscovery) return;
        int newPlayerCount = PlayerControl.AllPlayerControls.ToArray().Where(p => p != null && !p.Data.Disconnected).Count();
        if (newPlayerCount == LastPlayerCount) return;
        LastPlayerCount = newPlayerCount;
        LobbyStatus curStatus = LobbyBehaviour.Instance == null ? LobbyStatus.InGame : LobbyStatus.Open;
        log.Info($"Updating number of players {curStatus}, {newPlayerCount}.");
        LobbyChecker.UpdateLobbyStatus(AmongUsClient.Instance.GameId, newPlayerCount, curStatus);
    }

    [QuickPostfix(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
    private static void UpdatePlayersOnLeave(AmongUsClient __instance, ClientData data)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!NetworkRules.AllowRoomDiscovery) return;
        int newPlayerCount = PlayerControl.AllPlayerControls.ToArray().Where(p => p != null && !p.Data.Disconnected).Count();
        if (newPlayerCount == LastPlayerCount) return;
        LastPlayerCount = newPlayerCount;
        LobbyStatus curStatus = LobbyBehaviour.Instance == null ? LobbyStatus.InGame : LobbyStatus.Open;
        log.Info($"Updating number of players {curStatus}, {newPlayerCount}.");
        LobbyChecker.UpdateLobbyStatus(AmongUsClient.Instance.GameId, newPlayerCount, curStatus);
    }

    [QuickPrefix(typeof(NormalGameOptionsV10), nameof(NormalGameOptionsV10.SetByte))]
    private static void UpdateMapOnChange(NormalGameOptionsV10 __instance, ByteOptionNames optionName, byte value)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!NetworkRules.AllowRoomDiscovery) return;
        if (optionName != ByteOptionNames.MapId) return;
        log.Info($"Updating map ID {value}.");
        LobbyChecker.UpdateLobbyMap(AmongUsClient.Instance.GameId, value);
    }
}