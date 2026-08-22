// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Content.Shared._Mono.Xenobiology.Research;
using Content.Shared.GameTicking;

namespace Content.Server._Mono.Xenobiology.Research;

public sealed class ResearchDataTerminalSystem : EntitySystem
{
    [Dependency] private readonly SharedResearchDataTerminalSystem _progress = default!;

    private readonly HashSet<string> _completedResearch = [];

    public int Credits => _progress.Credits;
    public int Clearance => _progress.Clearance;
    public int? UpgradeCost => _progress.UpgradeCost;
    public int CompletedResearchCount => _completedResearch.Count;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
    }

    public bool TryCompleteResearch(string id, int reward)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Research requires a chemical ID.", nameof(id));
        if (reward is not (3 or 5 or 7))
            throw new ArgumentOutOfRangeException(nameof(reward), "Research rewards must be 3, 5, or 7 credits.");
        if (!_completedResearch.Add(id))
            return false;

        _progress.SetProgress(checked(Credits + reward), Clearance);
        RaiseNetworkEvent(new ResearchCompletedEvent(id, reward));
        return true;
    }

    public bool IsResearchCompleted(string id)
    {
        return _completedResearch.Contains(id);
    }

    public bool TryUpgradeClearance()
    {
        if (UpgradeCost is not { } cost || Credits < cost)
            return false;

        _progress.SetProgress(Credits - cost, Clearance + 1);
        return true;
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        _completedResearch.Clear();
        _progress.SetProgress(0, 1);
    }
}
