// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Xenobiology.Simulator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SynthesisSimulatorComponent : Component
{
    [AutoNetworkedField] public SynthesisSimulatorMode Mode = SynthesisSimulatorMode.Amplify;
    [AutoNetworkedField] public GeneratedReagentData? Target;
    [AutoNetworkedField] public GeneratedReagentData? Reference;
    [AutoNetworkedField] public string? TargetProperty;
    [AutoNetworkedField] public string? ReferenceProperty;
    [AutoNetworkedField] public bool OverrideConflicts;
    [AutoNetworkedField] public GeneratedReagentData? Result;
    [AutoNetworkedField] public string? Error;
}

[Serializable, NetSerializable]
public enum SynthesisSimulatorMode
{
    Amplify = 1,
    Suppress,
    Relate,
    Add,
}

[Serializable, NetSerializable]
public enum SynthesisSimulatorUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SynthesisSimulatorBuiState(
    List<GeneratedReagentData> available,
    string? targetId,
    string? referenceId,
    SynthesisSimulatorMode mode,
    string? targetProperty,
    string? referenceProperty,
    bool overrideConflicts,
    GeneratedReagentData? result,
    string? error) : BoundUserInterfaceState
{
    public readonly List<GeneratedReagentData> Available = available;
    public readonly string? TargetId = targetId;
    public readonly string? ReferenceId = referenceId;
    public readonly SynthesisSimulatorMode Mode = mode;
    public readonly string? TargetProperty = targetProperty;
    public readonly string? ReferenceProperty = referenceProperty;
    public readonly bool OverrideConflicts = overrideConflicts;
    public readonly GeneratedReagentData? Result = result;
    public readonly string? Error = error;

    public bool CanSimulate => !string.IsNullOrWhiteSpace(TargetId) && Mode switch
    {
        SynthesisSimulatorMode.Amplify or SynthesisSimulatorMode.Suppress =>
            !string.IsNullOrWhiteSpace(TargetProperty),
        SynthesisSimulatorMode.Relate => !string.IsNullOrWhiteSpace(TargetProperty) &&
            !string.IsNullOrWhiteSpace(ReferenceId) && !string.IsNullOrWhiteSpace(ReferenceProperty),
        SynthesisSimulatorMode.Add =>
            !string.IsNullOrWhiteSpace(ReferenceId) && !string.IsNullOrWhiteSpace(ReferenceProperty),
        _ => false,
    };
}

[Serializable, NetSerializable]
public sealed class SynthesisSimulatorRunMessage(
    string targetId,
    string? referenceId,
    SynthesisSimulatorMode mode,
    string? targetProperty,
    string? referenceProperty,
    bool overrideConflicts) : BoundUserInterfaceMessage
{
    public readonly string TargetId = targetId;
    public readonly string? ReferenceId = referenceId;
    public readonly SynthesisSimulatorMode Mode = mode;
    public readonly string? TargetProperty = targetProperty;
    public readonly string? ReferenceProperty = referenceProperty;
    public readonly bool OverrideConflicts = overrideConflicts;
}

public sealed record SynthesisSimulationRequest
{
    public GeneratedReagentData Target { get; init; }
    public SynthesisSimulatorMode Mode { get; init; }
    public GeneratedReagentData? Reference { get; init; }
    public string? TargetProperty { get; init; }
    public string? ReferenceProperty { get; init; }
    public bool OverrideConflicts { get; init; }

    public SynthesisSimulationRequest(
        GeneratedReagentData target,
        SynthesisSimulatorMode mode,
        GeneratedReagentData? reference = null,
        string? targetProperty = null,
        string? referenceProperty = null,
        bool overrideConflicts = false)
    {
        Target = target;
        Mode = mode;
        Reference = reference;
        TargetProperty = targetProperty;
        ReferenceProperty = referenceProperty;
        OverrideConflicts = overrideConflicts;
    }
}

public sealed partial class SynthesisSimulatorSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ProceduralReagentGeneratorSystem _generator = default!;
    [Dependency] private readonly ProceduralReagentRegistrySystem _registry = default!;

    public bool TrySimulate(Entity<SynthesisSimulatorComponent> entity, out GeneratedReagentData result)
    {
        var component = entity.Comp;
        try
        {
            if (component.Target is not { } target)
                throw new InvalidOperationException("A target reagent is required.");

            result = Simulate(new SynthesisSimulationRequest(
                target,
                component.Mode,
                component.Reference,
                component.TargetProperty,
                component.ReferenceProperty,
                component.OverrideConflicts));
            component.Result = result;
            component.Error = null;
            Dirty(entity);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            component.Result = null;
            component.Error = exception.Message;
            result = default;
            Dirty(entity);
            return false;
        }
    }

    public GeneratedReagentData Simulate(SynthesisSimulationRequest request)
    {
        _generator.ReloadRules();
        ValidateAvailable(request.Target, "target");
        var result = Copy(request.Target);
        result.OriginalID = string.IsNullOrEmpty(request.Target.OriginalID)
            ? request.Target.ID
            : request.Target.OriginalID;
        result.ModifiedChems.Clear();

        switch (request.Mode)
        {
            case SynthesisSimulatorMode.Amplify:
                Relevel(ref result, RequiredProperty(request.TargetProperty, "target"), 1);
                ApplyOverdosePenalty(ref result);
                break;
            case SynthesisSimulatorMode.Suppress:
                Relevel(ref result, RequiredProperty(request.TargetProperty, "target"), -1);
                ApplyOverdosePenalty(ref result);
                break;
            case SynthesisSimulatorMode.Relate:
                Relate(ref result, request);
                ApplyOverdosePenalty(ref result);
                break;
            case SynthesisSimulatorMode.Add:
                Add(ref result, request);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Mode), request.Mode, null);
        }

        Encode(ref result);
        result.GenTier = Math.Max(
            Math.Max(Math.Min((int) result.Class, (int) ProceduralReagentClass.Common), result.GenTier),
            1);
        if (result.Class == ProceduralReagentClass.Special)
            result.GenTier = 4;
        result.Class = ProceduralReagentClass.Rare;
        _registry.Register(result);
        _registry.TrackModified(request.Target, result.ID);
        if (request.Mode == SynthesisSimulatorMode.Add)
            _registry.LockLineage(request.Reference!.Value);
        return result;
    }

    private void Relate(ref GeneratedReagentData result, SynthesisSimulationRequest request)
    {
        var targetProperty = RequiredProperty(request.TargetProperty, "target");
        var (reference, referenceProperty, level) = GetReferenceProperty(request);
        ValidateAvailable(reference, "reference");
        if (!result.Effects.TryGetValue(targetProperty, out var targetLevel))
            throw new InvalidOperationException($"The target reagent has no '{targetProperty}' property.");
        if (targetLevel != level)
            throw new InvalidOperationException("Related properties must have equal levels.");
        if (result.Effects.ContainsKey(referenceProperty))
            throw new InvalidOperationException("The reference property is already present in the target.");
        if (!request.OverrideConflicts && _generator.HasConflict(result, referenceProperty))
            throw new InvalidOperationException("The reference property conflicts with the target.");

        result.Effects.Remove(targetProperty);
        _generator.InsertProperty(ref result, referenceProperty, level);
    }

    private void Add(ref GeneratedReagentData result, SynthesisSimulationRequest request)
    {
        var (reference, referenceProperty, level) = GetReferenceProperty(request);
        ValidateAvailable(reference, "reference");
        if (result.Effects.ContainsKey(referenceProperty))
            throw new InvalidOperationException("The reference property is already present in the target.");
        _generator.InsertProperty(ref result, referenceProperty, level);
    }

    private static (GeneratedReagentData Reference, string Property, int Level) GetReferenceProperty(
        SynthesisSimulationRequest request)
    {
        if (request.Reference is not { } reference)
            throw new InvalidOperationException("A reference reagent is required.");
        var property = RequiredProperty(request.ReferenceProperty, "reference");
        if (!reference.Effects.TryGetValue(property, out var level))
            throw new InvalidOperationException($"The reference reagent has no '{property}' property.");
        return (reference, property, level);
    }

    private void Relevel(ref GeneratedReagentData result, string property, int delta)
    {
        if (!result.Effects.TryGetValue(property, out var level))
            throw new InvalidOperationException($"The target reagent has no '{property}' property.");
        var next = level + delta;
        if (delta > 0 && _prototypes.TryIndex<ReagentPropertyPrototype>(property, out var prototype) &&
            next > prototype.MaxLevel)
            throw new InvalidOperationException($"Property '{property}' is already at its maximum level.");
        if (next <= 0)
            result.Effects.Remove(property);
        else
            result.Effects[property] = next;
    }

    private void ValidateAvailable(GeneratedReagentData data, string role)
    {
        if (string.IsNullOrWhiteSpace(data.ID))
            throw new ArgumentException($"The {role} reagent requires an ID.");
        if (_registry.IsLockedDown(data.ID))
            throw new InvalidOperationException($"The {role} reagent is locked down.");
    }

    private void Encode(ref GeneratedReagentData result)
    {
        var signature = string.Join(' ', result.Effects.OrderBy(effect => effect.Key)
            .Select(effect => $"{effect.Key}{effect.Value}"));
        var hash = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(signature)))[..2];
        var source = result.OriginalID.Replace(' ', '-');
        result.ID = $"TAU-{_registry.GeneratedReagents.Count}-{source}-{hash}";
        result.Name = $"{result.Name} {hash}";
    }

    private static string RequiredProperty(string? property, string role)
    {
        return string.IsNullOrWhiteSpace(property)
            ? throw new ArgumentException($"A {role} property is required.")
            : property;
    }

    private static void ApplyOverdosePenalty(ref GeneratedReagentData result)
    {
        result.Overdose = result.Overdose <= 5
            ? FixedPoint2.Max(result.Overdose - 1, 1)
            : FixedPoint2.Max(result.Overdose - 5, 5);
        result.CriticalOverdose = FixedPoint2.Max(
            FixedPoint2.Min(result.Overdose * 2, result.Overdose + 30),
            10);
    }

    private static GeneratedReagentData Copy(GeneratedReagentData source)
    {
        return new GeneratedReagentData
        {
            ID = source.ID,
            Name = source.Name,
            Effects = new Dictionary<string, int>(source.Effects),
            Recipe = new Dictionary<string, (int Amount, bool Catalyst)>(source.Recipe),
            RecipeYield = source.RecipeYield,
            ScanPointYield = source.ScanPointYield,
            Color = source.Color,
            Overdose = source.Overdose,
            CriticalOverdose = source.CriticalOverdose,
            MetabolismRate = source.MetabolismRate,
            GenTier = source.GenTier,
            RecipeHint = source.RecipeHint,
            PropertyHint = source.PropertyHint,
            OriginalID = source.OriginalID,
            ModifiedChems = new HashSet<string>(source.ModifiedChems),
            Class = source.Class,
        };
    }
}
