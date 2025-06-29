﻿using Dalamud.Utility;

namespace SmartPings.Data;

public struct Status
{
    public uint Id;
    public string Name;
#if DEBUG
    public string Description;
    public uint Icon;
    public int ParamModifier;
    public uint VfxRowId;
    public ushort Log;
    public byte Unknown0;
#endif
    public byte MaxStacks;
    public byte StatusCategory;
    public byte PartyListPriority;
    public byte CanIncreaseRewards;
    public byte ParamEffect;
#if DEBUG
    public byte TargetType;
    public byte Flags;
    public byte Flag2;
    public byte Unknown_70_1;
    public sbyte Unknown2;
    public bool LockMovement;
    public bool LockActions;
    public bool LockControl;
    public bool Transfiguration;
    public bool IsGaze;
    public bool CanDispel;
    public bool InflictedByActor;
    public bool IsPermanent;
    public bool CanStatusOff;
    public bool IsFcBuff;
    public bool Invisibility;
#endif

    public float RemainingTime;
    public bool SourceIsSelf;
    public ushort Stacks;

    public readonly bool IsEnhancement => StatusCategory == 1 && CanIncreaseRewards == 0;
    public readonly bool IsOtherEnhancement => StatusCategory == 1 && CanIncreaseRewards == 1;
    public readonly bool IsConditionalEnhancement => StatusCategory == 1 && CanIncreaseRewards == 2;
    public readonly bool IsEnfeeblement => StatusCategory == 2;
    public readonly bool IsOtherEnfeeblement => IsEnfeeblement && CanIncreaseRewards == 1;
    // Some statuses are invisible and are in the status list but are not shown in any UI
    // Class-based damage buff in Criterion (ParamEffect 31)
    // Hoofing It in Occult Crescent (Id 1778)
    public readonly bool IsVisible => ParamEffect != 31 && Id != 1778;

    public Status(Lumina.Excel.Sheets.Status luminaStatus)
    {
        Id = luminaStatus.RowId;
        Name = luminaStatus.Name.ToDalamudString().TextValue;
#if DEBUG
        Description = luminaStatus.Description.ToDalamudString().TextValue;
        Icon = luminaStatus.Icon;
        ParamModifier = luminaStatus.ParamModifier;
        VfxRowId = luminaStatus.VFX.RowId;
        Log = luminaStatus.Log;
        Unknown0 = luminaStatus.Unknown0;
#endif
        MaxStacks = luminaStatus.MaxStacks;
        StatusCategory = luminaStatus.StatusCategory;
        PartyListPriority = luminaStatus.PartyListPriority;
        CanIncreaseRewards = luminaStatus.CanIncreaseRewards;
        ParamEffect = luminaStatus.ParamEffect;
#if DEBUG
        TargetType = luminaStatus.TargetType;
        Flags = luminaStatus.Flags;
        Flag2 = luminaStatus.Flag2;
        Unknown_70_1 = luminaStatus.Unknown_70_1;
        Unknown2 = luminaStatus.Unknown2;
        LockMovement = luminaStatus.LockMovement;
        LockActions = luminaStatus.LockActions;
        LockControl = luminaStatus.LockControl;
        Transfiguration = luminaStatus.Transfiguration;
        IsGaze = luminaStatus.IsGaze;
        CanDispel = luminaStatus.CanDispel;
        InflictedByActor = luminaStatus.InflictedByActor;
        IsPermanent = luminaStatus.IsPermanent;
        CanStatusOff = luminaStatus.CanStatusOff;
        IsFcBuff = luminaStatus.IsFcBuff;
        Invisibility = luminaStatus.Invisibility;
#endif
    }
}

public enum StatusType
{
    None = 0,

    SelfEnhancement = 1,
    SelfEnfeeblement = 2,
    SelfOther = 3,
    SelfConditionalEnhancement = 4,

    PartyListStatus = 10,
    TargetStatus = 11,
}
