using Robust.Shared.GameStates;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mono.Xenobiology.Xeno;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class XenoEggComponent : Component
{
    [DataField, AutoNetworkedField]
    public XenoEggState State;

    [DataField, AutoNetworkedField]
    public TimeSpan PlacementDelay = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public TimeSpan GrowthDelay = TimeSpan.FromMinutes(2);

    [DataField, AutoNetworkedField]
    public TimeSpan OpeningDelay = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? PlacementAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? GrowAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? OpenAt;

    [DataField, AutoNetworkedField]
    public EntProtoId ParasitePrototype = "MonoXenoParasite";

    [DataField, AutoNetworkedField]
    public EntityUid? SpawnedParasite;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class XenoParasiteComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class InfectableHostComponent : Component
{
    [DataField]
    public TimeSpan IncubationDelay = TimeSpan.FromMinutes(8);

    [DataField]
    public EntProtoId LarvaPrototype = "MonoXenoLarva";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class XenoInfectionComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan SpawnAt;

    [DataField, AutoNetworkedField]
    public EntProtoId LarvaPrototype = "MonoXenoLarva";

    [DataField, AutoNetworkedField]
    public ProtoId<NpcFactionPrototype> LarvaFaction = "Xeno";

    [DataField, AutoNetworkedField]
    public EntityUid? SpawnedLarva;
}

[Serializable, NetSerializable]
public enum XenoEggState : byte
{
    Item,
    Growing,
    Grown,
    Opening,
    Opened,
}
