// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using System.Text;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared._Mono.Xenobiology.Research;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Xenobiology.Research;

public sealed class ResearchDataTerminalSystem : EntitySystem
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan PickDelay = TimeSpan.FromSeconds(360);

    [Dependency] private readonly SharedResearchDataTerminalSystem _progress = default!;
    [Dependency] private readonly ProceduralReagentGeneratorSystem _generator = default!;
    [Dependency] private readonly ProceduralReagentRegistrySystem _registry = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;

    private readonly HashSet<string> _completedResearch = [];
    private GeneratedReagentData? _lastContract;
    private bool _picked;

    public int Credits => _progress.Credits;
    public int Clearance => _progress.Clearance;
    public int? UpgradeCost => _progress.UpgradeCost;
    public int CompletedResearchCount => _completedResearch.Count;
    public List<GeneratedReagentData> Contracts { get; } = [];
    public List<ResearchReportData> KnownScans { get; } = [];
    public TimeSpan NextRefresh { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        Subs.BuiEvents<ResearchDataTerminalComponent>(ResearchDataTerminalUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<ResearchDataTerminalSelectMessage>(OnSelect);
            subs.Event<ResearchDataTerminalUpgradeMessage>(OnUpgrade);
            subs.Event<ResearchDataTerminalReprintMessage>(OnReprint);
            subs.Event<ResearchDataTerminalPrintReportMessage>(OnPrintReport);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (Contracts.Count > 0 && _timing.CurTime >= NextRefresh)
            RefreshContracts();
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
        UpdateAllUi();
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

        var nextClearance = Clearance + 1;
        _progress.SetProgress(Credits - cost, nextClearance);
        if (nextClearance == 6)
            RaiseLocalEvent(new ResearchClearanceSixBreakthroughEvent());
        UpdateAllUi();
        return true;
    }

    public void RefreshContracts()
    {
        Contracts.Clear();
        _generator.PreparePools();
        var ids = new HashSet<string>();

        while (Contracts.Count < 6)
        {
            var data = new GeneratedReagentData
            {
                Class = ProceduralReagentClass.Ultra,
                GenTier = _random.Next(1, 4),
            };
            _generator.GenerateName(ref data);
            if (!ids.Add(data.ID))
                continue;

            _generator.GenerateStats(ref data);
            data.ScanPointYield = data.GenTier switch
            {
                1 => 3,
                2 => 5,
                _ => 7,
            };
            data.RecipeHint = PickRecipeHint(data.GenTier);
            data.PropertyHint = _random.Pick(data.Effects.Keys);
            Contracts.Add(data);
        }

        _picked = false;
        NextRefresh = _timing.CurTime + RefreshDelay;
        UpdateAllUi();
    }

    public bool TrySelectContract(string id, out GeneratedReagentData contract)
    {
        contract = default;
        if (_picked && _timing.CurTime < NextRefresh)
            return false;

        var index = Contracts.FindIndex(candidate => candidate.ID == id);
        if (index < 0)
            return false;

        contract = Contracts[index];
        if (!_generator.GenerateRecipe(ref contract, [contract.RecipeHint]))
            return false;

        _registry.Register(contract);
        Contracts.RemoveAt(index);
        _lastContract = contract;
        _picked = true;
        NextRefresh = _timing.CurTime + PickDelay;
        UpdateAllUi();
        return true;
    }

    public void AddKnownScan(ResearchReportData report)
    {
        KnownScans.Add(report);
        UpdateAllUi();
    }

    public ResearchDataTerminalBuiState GetState()
    {
        return new ResearchDataTerminalBuiState(
            [.. Contracts],
            [.. KnownScans],
            NextRefresh,
            Credits,
            Clearance,
            UpgradeCost,
            _picked && _timing.CurTime < NextRefresh);
    }

    private string PickRecipeHint(int tier)
    {
        var roll = _random.Next(1, 101);
        var pool = tier switch
        {
            1 => roll <= 60 ? "C1" : "C2",
            2 => roll <= 40 ? "C2" : "C3",
            _ => "H1",
        };
        return _random.Pick(_generator.ReagentClassPools[pool]);
    }

    private void OnUiOpened(Entity<ResearchDataTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (Contracts.Count == 0)
            RefreshContracts();
        _ui.SetUiState(ent.Owner, ResearchDataTerminalUiKey.Key, GetState());
    }

    private void OnSelect(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalSelectMessage args)
    {
        if (TrySelectContract(args.ID, out var contract))
            PrintContract(ent.Owner, contract);
    }

    private void OnUpgrade(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalUpgradeMessage args)
    {
        TryUpgradeClearance();
    }

    private void OnReprint(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalReprintMessage args)
    {
        if (_lastContract is { } contract)
            PrintContract(ent.Owner, contract);
    }

    private void OnPrintReport(Entity<ResearchDataTerminalComponent> ent, ref ResearchDataTerminalPrintReportMessage args)
    {
        if (args.Index < 0 || args.Index >= KnownScans.Count)
            return;

        var report = KnownScans[args.Index];
        PrintPaper(ent.Owner, report.Name, report.Info);
    }

    private void PrintContract(EntityUid terminal, GeneratedReagentData contract)
    {
        var text = new StringBuilder();
        text.AppendLine(Loc.GetString("research-data-terminal-contract-header", ("name", contract.Name)));
        foreach (var (id, requirement) in contract.Recipe)
        {
            var reagent = _prototypes.Index<ReagentPrototype>(id);
            text.AppendLine(Loc.GetString("research-data-terminal-contract-reagent",
                ("amount", requirement.Amount),
                ("name", reagent.LocalizedName),
                ("catalyst", requirement.Catalyst ? Loc.GetString("research-data-terminal-catalyst") : string.Empty)));
        }
        PrintPaper(terminal, Loc.GetString("research-data-terminal-contract-title", ("name", contract.Name)), text.ToString());
    }

    private void PrintPaper(EntityUid terminal, string name, string content)
    {
        var paper = SpawnNextToOrDrop("Paper", terminal);
        _metadata.SetEntityName(paper, name);
        _paper.SetContent((paper, Comp<PaperComponent>(paper)), content);
    }

    private void UpdateAllUi()
    {
        var state = GetState();
        var query = EntityQueryEnumerator<ResearchDataTerminalComponent>();
        while (query.MoveNext(out var uid, out _))
            _ui.SetUiState(uid, ResearchDataTerminalUiKey.Key, state);
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        _completedResearch.Clear();
        Contracts.Clear();
        KnownScans.Clear();
        _lastContract = null;
        _picked = false;
        NextRefresh = TimeSpan.Zero;
        _progress.SetProgress(0, 1);
    }
}
