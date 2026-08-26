// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Content.Shared._Mono.Xenobiology.Chemistry;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Xenobiology.Research;

[RegisterComponent, NetworkedComponent]
public sealed partial class ResearchDataTerminalComponent : Component;

[Serializable, NetSerializable]
public enum ResearchDataTerminalUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ResearchDataTerminalBuiState(
    List<GeneratedReagentData> contracts,
    List<ResearchReportData> knownScans,
    TimeSpan nextRefresh,
    int credits,
    int clearance,
    int? upgradeCost,
    bool picked) : BoundUserInterfaceState
{
    public readonly List<GeneratedReagentData> Contracts = contracts;
    public readonly List<ResearchReportData> KnownScans = knownScans;
    public readonly TimeSpan NextRefresh = nextRefresh;
    public readonly int Credits = credits;
    public readonly int Clearance = clearance;
    public readonly int? UpgradeCost = upgradeCost;
    public readonly bool Picked = picked;

    public bool CanUpgrade => UpgradeCost is { } cost && Credits >= cost;
    public bool CanSelect => !Picked;
}

[Serializable, NetSerializable]
public sealed class ResearchDataTerminalSelectMessage(string id) : BoundUserInterfaceMessage
{
    public readonly string ID = id;
}

[Serializable, NetSerializable]
public sealed class ResearchDataTerminalUpgradeMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ResearchDataTerminalReprintMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ResearchDataTerminalPrintReportMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}
