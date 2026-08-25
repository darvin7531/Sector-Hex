// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Content.Server._Mono.Xenobiology.Research;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared._Mono.Xenobiology.XRF;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Xenobiology.XRF;

public sealed class XRFScannerSystem : EntitySystem
{
    private const int MinimumVolume = 30;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ResearchDataTerminalSystem _research = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XRFScannerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<XRFScannerComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<XRFScannerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<XRFScannerComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnInit(Entity<XRFScannerComponent> ent, ref ComponentInit args)
    {
        _slots.AddItemSlot(ent, XRFScannerComponent.SampleSlotId, ent.Comp.SampleSlot);
        SetState(ent, XRFScannerState.Idle);
    }

    private void OnRemove(Entity<XRFScannerComponent> ent, ref ComponentRemove args)
    {
        _slots.RemoveItemSlot(ent, ent.Comp.SampleSlot);
    }

    private void OnInserted(Entity<XRFScannerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == XRFScannerComponent.SampleSlotId)
            TryStartScan(ent.Owner, ent.Comp);
    }

    private void OnRemoved(Entity<XRFScannerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == XRFScannerComponent.SampleSlotId && !ent.Comp.Processing)
            SetState(ent, XRFScannerState.Idle);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<XRFScannerComponent>();
        while (query.MoveNext(out var uid, out var scanner))
        {
            if (scanner.Processing && scanner.NextScan <= _timing.CurTime)
                FinishScan((uid, scanner));
        }
    }

    public bool TryStartScan(EntityUid uid, XRFScannerComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.Processing || component.SampleSlot.Item == null)
            return false;

        component.Processing = true;
        component.NextScan = _timing.CurTime + component.ProcessDuration;
        _slots.SetLock(uid, component.SampleSlot, true);
        SetState((uid, component), XRFScannerState.Processing);
        return true;
    }

    public XRFScanReport FinishScan(Entity<XRFScannerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return XRFScanReport.Missing;

        var report = Analyze(ent.Comp.SampleSlot.Item);
        ent.Comp.LastReport = report;
        ent.Comp.Processing = false;
        _slots.SetLock(ent, ent.Comp.SampleSlot, false);
        SetState((ent, ent.Comp), report.Status switch
        {
            XRFScanStatus.Valid => XRFScannerState.Finished,
            XRFScanStatus.Invalid => XRFScannerState.Error,
            _ => XRFScannerState.Failed,
        });
        return report;
    }

    private XRFScanReport Analyze(EntityUid? sample)
    {
        if (sample == null || !_solutions.TryGetSolution(sample.Value, "beaker", out var solution))
            return sample == null ? XRFScanReport.Missing : Invalid(XRFScanStatus.Invalid);

        var contents = solution.Value.Comp.Solution;
        if (contents.Volume < MinimumVolume)
            return Invalid(XRFScanStatus.Insufficient);
        if (contents.Contents.Count != 1)
            return Invalid(XRFScanStatus.Contaminated);

        var reagentId = contents.Contents[0].Reagent.Prototype;
        if (!_prototypes.TryIndex<ReagentPrototype>(reagentId, out var reagent))
            return Invalid(XRFScanStatus.Invalid);

        var reward = ResearchReward(reagent);
        var rewarded = _research.TryCompleteResearch(reagent.ID, reward);
        return new XRFScanReport(
            XRFScanStatus.Valid,
            reagent.ID,
            reagent.LocalizedName,
            reagent.Class,
            reward,
            rewarded);
    }

    private static XRFScanReport Invalid(XRFScanStatus status)
    {
        return XRFScanReport.Missing with { Status = status };
    }

    private static int ResearchReward(ReagentPrototype reagent)
    {
        if (reagent.Reward is 3 or 5 or 7)
            return reagent.Reward;

        return reagent.Class switch
        {
            <= ProceduralReagentClass.Common => 3,
            <= ProceduralReagentClass.Rare => 5,
            _ => 7,
        };
    }

    private void SetState(Entity<XRFScannerComponent> ent, XRFScannerState state)
    {
        _appearance.SetData(ent, XRFScannerVisuals.State, state);
    }
}
