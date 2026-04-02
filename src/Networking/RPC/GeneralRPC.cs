using Hazel;
using VentLib.Logging.Default;
using VentLib.Utilities.Extensions;

namespace VentLib.Networking.RPC;

public static class GeneralRPC
{
    public static void SendGameData(int clientId = -1, SendOption sendOption = SendOption.Reliable)
    {
        // MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
        // writer.StartMessage((byte)(clientId == -1 ? 5 : 6)); //0x05 GameData
        // {
        //     writer.Write(AmongUsClient.Instance.GameId);
        //     if (clientId != -1)
        //         writer.WritePacked(clientId);
        //     writer.StartMessage(1); //0x01 Data
        //     {
        //         writer.WritePacked(GameManager.Instance.NetId);
        //         GameManager.Instance.Serialize(writer, true);
        //     }
        //     writer.EndMessage();
        // }
        // writer.EndMessage();

        // AmongUsClient.Instance.SendOrDisconnect(writer);
        // writer.Recycle();
        int messages = 0;
        int packingLimit = AmongUsClient.Instance.GetMaxMessagePackingLimit();

        MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
        writer.StartMessage((byte)(clientId == -1 ? 5 : 6));
        writer.Write(AmongUsClient.Instance.GameId);
        if (clientId != -1) writer.WritePacked(clientId);

        foreach (NetworkedPlayerInfo playerinfo in GameData.Instance.AllPlayers)
        {
            if (writer.Length > 500 || messages >= packingLimit)
            {
                messages = 0;
                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
                writer.Clear(SendOption.Reliable);
                writer.StartMessage((byte)(clientId == -1 ? 5 : 6));
                writer.Write(AmongUsClient.Instance.GameId);
                if (clientId != -1) writer.WritePacked(clientId);
            }

            writer.StartMessage(1);
            writer.WritePacked(playerinfo.NetId);
            playerinfo.Serialize(writer, false);
            writer.EndMessage();
            
            messages++;
        }

        writer.EndMessage();
        AmongUsClient.Instance.SendOrDisconnect(writer);
        writer.Recycle();
    }

    public static void SendMeetingHud(int clientId = -1, bool initialState = true, SendOption sendOption = SendOption.Reliable)
    {
        MessageWriter writer = MessageWriter.Get(sendOption);
        writer.StartMessage((byte)(clientId == -1 ? 5 : 6)); //0x05 GameData
        {
            writer.Write(AmongUsClient.Instance.GameId);
            if (clientId != -1)
                writer.WritePacked(clientId);
            writer.StartMessage(1); //0x01 Data
            {
                writer.WritePacked(MeetingHud.Instance.NetId);
                MeetingHud.Instance.Serialize(writer, initialState);
            }
            writer.EndMessage();
        }
        writer.EndMessage();

        AmongUsClient.Instance.SendOrDisconnect(writer);
        writer.Recycle();
    }
}