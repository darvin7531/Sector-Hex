using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared.FixedPoint;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Chemistry.Reagent;

public sealed partial class ReagentPrototype
{
    [DataField]
    public bool Unknown;

    [DataField]
    public FixedPoint2? Overdose;

    [DataField]
    public FixedPoint2? CriticalOverdose;

    [DataField]
    public ProceduralReagentClass Class = ProceduralReagentClass.None;

    [DataField]
    public ProceduralReagentFlag Flags;

    [DataField]
    public int GenTier;

    [DataField]
    public bool Generated;

    [DataField]
    public int Reward = 2;

    [DataField]
    public bool Lockdown;
}
