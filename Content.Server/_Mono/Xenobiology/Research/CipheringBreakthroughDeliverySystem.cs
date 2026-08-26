// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Content.Shared.GameTicking;
using Robust.Shared.Map;

namespace Content.Server._Mono.Xenobiology.Research;

public sealed partial class CipheringBreakthroughDeliverySystem : EntitySystem
{
    private bool _unclaimed;
    private bool _delivered;
    public int BreakthroughCount { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchClearanceSixBreakthroughEvent>(_ =>
        {
            BreakthroughCount++;
            if (!_delivered)
                _unclaimed = true;
        });
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _unclaimed = false;
            _delivered = false;
            BreakthroughCount = 0;
        });
    }

    public bool TryDeliver(EntityCoordinates coordinates, out EntityUid egg)
    {
        egg = default;
        if (!_unclaimed || _delivered)
            return false;

        egg = Spawn("MonoXenoEgg", coordinates);
        _unclaimed = false;
        _delivered = true;
        return true;
    }
}

public sealed class ResearchClearanceSixBreakthroughEvent : EntityEventArgs;
