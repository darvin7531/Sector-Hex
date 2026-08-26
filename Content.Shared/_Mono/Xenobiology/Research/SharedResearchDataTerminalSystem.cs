// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Xenobiology.Research;

public sealed class SharedResearchDataTerminalSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    public int Credits { get; private set; }
    public int Clearance { get; private set; } = 1;
    public int? UpgradeCost => GetUpgradeCost(Clearance);

    public override void Initialize()
    {
        base.Initialize();

        if (_net.IsClient)
            SubscribeNetworkEvent<ResearchProgressUpdatedEvent>(OnProgressUpdated);
    }

    public static int? GetUpgradeCost(int clearance)
    {
        return clearance switch
        {
            1 => 4,
            2 => 7,
            3 => 10,
            4 => 13,
            5 => 5,
            _ => null,
        };
    }

    public void SetProgress(int credits, int clearance)
    {
        if (!_net.IsServer)
            throw new InvalidOperationException("Only the server may change research progression.");
        if (credits < 0)
            throw new ArgumentOutOfRangeException(nameof(credits));
        if (clearance is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(clearance));

        ApplyProgress(credits, clearance);
        RaiseNetworkEvent(new ResearchProgressUpdatedEvent(credits, clearance));
    }

    private void OnProgressUpdated(ResearchProgressUpdatedEvent ev, EntitySessionEventArgs args)
    {
        ApplyProgress(ev.Credits, ev.Clearance);
    }

    private void ApplyProgress(int credits, int clearance)
    {
        Credits = credits;
        Clearance = clearance;
    }
}

[Serializable, NetSerializable]
public sealed class ResearchProgressUpdatedEvent(int credits, int clearance) : EntityEventArgs
{
    public readonly int Credits = credits;
    public readonly int Clearance = clearance;
}

[Serializable, NetSerializable]
public sealed class ResearchCompletedEvent(string id, int reward) : EntityEventArgs
{
    public readonly string ID = id;
    public readonly int Reward = reward;
}
