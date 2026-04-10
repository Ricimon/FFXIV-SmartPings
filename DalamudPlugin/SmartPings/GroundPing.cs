using System.Numerics;

namespace SmartPings;

public class GroundPing
{
    public enum Type : int
    {
        None = 0,
        Basic = 1,
        Question = 2,
        Danger = 3,
        Assist = 4,
        OnMyWay = 5,
    }

    public Type PingType;
    public long StartTimestamp;
    public string? Author;
    public ulong AuthorId;
    public string? MapId;
    public Vector3 WorldPosition;
    public float DrawDuration;

    public int SfxId;
}
