namespace SmartPings.Data;

public struct HudElementInfo
{
    public enum Type : int
    {
        None = 0,
        Status = 1,
        Hp = 2,
        Mp = 3 ,
        Castbar = 4,
    }

    public Type ElementType;
    public Status Status;
    public GaugeValue Hp;
    public GaugeValue Mp;
    public string CastbarName;

    public string? OwnerName;
    public bool IsOnSelf;
    public bool IsOnPartyMember;
    public bool IsOnHostile;
    public string? TargetName;
    public bool IsTargetSelf;
    public bool IsTargetHostile;
}

public struct GaugeValue
{
    public uint Value;
    public uint MaxValue;
}
