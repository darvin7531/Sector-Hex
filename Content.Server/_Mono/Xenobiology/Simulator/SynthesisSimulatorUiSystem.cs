using System.Linq;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared._Mono.Xenobiology.Simulator;
using Robust.Server.GameObjects;

namespace Content.Server._Mono.Xenobiology.Simulator;

public sealed partial class SynthesisSimulatorUiSystem : EntitySystem
{
    [Dependency] private readonly SynthesisSimulatorSystem _simulator = default!;
    [Dependency] private readonly ProceduralReagentRegistrySystem _registry = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<SynthesisSimulatorComponent>(SynthesisSimulatorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<SynthesisSimulatorRunMessage>(OnRun);
        });
    }

    private void OnOpened(Entity<SynthesisSimulatorComponent> entity, ref BoundUIOpenedEvent args)
    {
        UpdateState(entity);
    }

    private void OnRun(Entity<SynthesisSimulatorComponent> entity, ref SynthesisSimulatorRunMessage args)
    {
        if (!_registry.ReagentData.TryGetValue(args.TargetId, out var target))
        {
            entity.Comp.Error = "Unknown target reagent.";
            UpdateState(entity);
            return;
        }

        GeneratedReagentData? reference = null;
        if (!string.IsNullOrWhiteSpace(args.ReferenceId))
        {
            if (!_registry.ReagentData.TryGetValue(args.ReferenceId, out var referenceData))
            {
                entity.Comp.Error = "Unknown reference reagent.";
                UpdateState(entity);
                return;
            }
            reference = referenceData;
        }

        entity.Comp.Target = target;
        entity.Comp.Reference = reference;
        entity.Comp.Mode = args.Mode;
        entity.Comp.TargetProperty = args.TargetProperty;
        entity.Comp.ReferenceProperty = args.ReferenceProperty;
        entity.Comp.OverrideConflicts = args.OverrideConflicts;
        _simulator.TrySimulate(entity, out _);
        UpdateState(entity);
    }

    private void UpdateState(Entity<SynthesisSimulatorComponent> entity)
    {
        var state = new SynthesisSimulatorBuiState(
            _registry.ReagentData.Values.OrderBy(data => data.Name).ToList(),
            entity.Comp.Target?.ID,
            entity.Comp.Reference?.ID,
            entity.Comp.Mode,
            entity.Comp.TargetProperty,
            entity.Comp.ReferenceProperty,
            entity.Comp.OverrideConflicts,
            entity.Comp.Result,
            entity.Comp.Error);
        _ui.SetUiState(entity.Owner, SynthesisSimulatorUiKey.Key, state);
    }
}
