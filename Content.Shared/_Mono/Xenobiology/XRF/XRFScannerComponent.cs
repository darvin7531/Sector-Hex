// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Mono.Xenobiology.XRF;

[RegisterComponent]
public sealed partial class XRFScannerComponent : Component
{
    public const string SampleSlotId = "xrf-sample";

    [DataField]
    public ItemSlot SampleSlot = new();

    [DataField]
    public TimeSpan ProcessDuration = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public bool Processing;

    [ViewVariables]
    public TimeSpan NextScan;

    [ViewVariables]
    public XRFScanReport LastReport = XRFScanReport.Missing;
}

public readonly record struct XRFScanReport(
    XRFScanStatus Status,
    string? ReagentId,
    string? Name,
    ProceduralReagentClass Class,
    int Reward,
    bool RewardGranted)
{
    public static readonly XRFScanReport Missing = new(
        XRFScanStatus.Missing,
        null,
        null,
        ProceduralReagentClass.None,
        0,
        false);
}

public enum XRFScanStatus : byte
{
    Missing,
    Insufficient,
    Contaminated,
    Invalid,
    Valid,
}

public enum XRFScannerVisuals : byte
{
    State,
}

public enum XRFScannerState : byte
{
    Idle,
    Sample,
    Processing,
    Error,
    Failed,
    Finished,
}
