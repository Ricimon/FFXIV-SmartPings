using Dalamud.Game.Text;
using SmartPings.Data;

namespace SmartPings.Network;

public struct ServerMessage
{
    public struct Payload
    {
        public enum Action : int
        {
            None = 0,
            UpdatePlayersInRoom = 1,
            AddGroundPing = 2,
            SendUiPing = 3,

            Close = 10,
        }

        public struct GroundPingPayload
        {
            public GroundPing.Type pingType;
            public string author;
            // JavaScript (the server language) can only support up to 53-bit integers, so long/ulong need to be converted to raw byte arrays
            public byte[] authorId;
            public byte[] startTimestamp;
            public string mapId;
            public float worldPositionX;
            public float worldPositionY;
            public float worldPositionZ;
        }

        public struct UiPingPayload
        {
            public string? sourceName;
            public HudElementInfo hudElementInfo;
        }

        public Action action;
        public string[]? players;
        public GroundPingPayload? groundPingPayload;
        public UiPingPayload? uiPingPayload;
    }

    public string from;
    public string target;
    public Payload payload;
}
