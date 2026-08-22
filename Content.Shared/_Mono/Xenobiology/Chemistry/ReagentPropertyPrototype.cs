// This file is licensed under the MIT license.
// Original implementation by MACMAN2003 in RussianCM.

using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._Mono.Xenobiology.Chemistry;

[Flags]
public enum ReagentPropertyType
{
    All = 0,
    Medicine = 1 << 0,
    Toxicant = 1 << 1,
    Stimulant = 1 << 2,
    Reactant = 1 << 3,
    Irritant = 1 << 4,
    Metabolite = 1 << 5,
    Anomalous = 1 << 6,
    Unadjustable = 1 << 7,
    Catalyst = 1 << 8,
    Combustible = 1 << 9,
}

public enum ReagentPropertyRarity
{
    Disabled,
    Common,
    Uncommon,
    Rare,
    Legendary,
    Admin,
}

public enum ReagentPropertyHint
{
    Negative,
    Neutral,
    Positive,
    Rare,
    Legendary,
    Disabled,
}

[Prototype]
[DataDefinition]
public sealed partial class ReagentPropertyPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    private LocId Name { get; set; }

    public string LocalizedName => Loc.GetString(Name);

    [DataField(required: true)]
    private LocId Description { get; set; }

    public string LocalizedDescription => Loc.GetString(Description);

    [DataField]
    public LocId Code = "mono-reagent-property-code-unknown";

    public string LocalizedCode => Loc.GetString(Code);

    [DataField]
    public string EffectName = string.Empty;

    [DataField]
    public bool GenerationDisabled;

    [DataField]
    public bool Starter;

    [DataField(required: true)]
    public ReagentPropertyType Category;

    [DataField(required: true)]
    public ReagentPropertyRarity Rarity;

    [DataField(required: true)]
    public ReagentPropertyHint Hint;

    [DataField]
    public int Level = 1;

    [DataField]
    public int Value;

    [DataField]
    public bool CostPenalty = true;

    [DataField]
    public int MaxLevel = 999;

    [DataField]
    public bool Volatile;

    [DataField]
    public bool UpdatesStats;

    [DataField]
    public FixedPoint2 IntensityModPerLevel;

    [DataField]
    public FixedPoint2 RadiusModPerLevel;

    [DataField]
    public FixedPoint2 DurationModPerLevel;

    [DataField]
    public FixedPoint2 IntensityPerLevel;

    [DataField]
    public FixedPoint2 RangePerLevel;

    [DataField]
    public FixedPoint2 DurationPerLevel;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ReagentPropertyPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }
}

public enum ProceduralReagentClass
{
    None,
    Basic,
    Common,
    Uncommon,
    Rare,
    Special,
    Ultra,
    Hydro,
}

[Flags]
public enum ProceduralReagentFlag
{
    None = 0,
    Medical = 1 << 0,
    Scannable = 1 << 1,
    NotIngestible = 1 << 2,
    CannotOverdose = 1 << 3,
    Stimulant = 1 << 4,
    NoGeneration = 1 << 5,
    Specialist = 1 << 6,
}
