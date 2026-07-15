using Content.Shared.Item;
using Robust.Shared.Prototypes;

namespace Content.Shared._Hex.Weapons.Ranged.Prototypes;

/// <summary>
/// Defines which prototype families are eligible for the Hex akimbo system.
/// </summary>
[Prototype("hexAkimboConfig")]
public sealed partial class HexAkimboConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<EntProtoId> PistolParents = new();

    [DataField]
    public List<EntProtoId> SubMachineGunParents = new();

    [DataField]
    public List<EntProtoId> DeniedPrototypes = new();

    [DataField]
    public ProtoId<ItemSizePrototype> MaximumItemSize = "Large";
}
