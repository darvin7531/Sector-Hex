using Content.Shared._Mono.Xenobiology.Xeno;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Xenobiology.Xeno;

public sealed partial class XenoLifecycleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    private static readonly ProtoId<PolymorphPrototype> AdultEvolution = "MonoXenoAdultEvolution";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoEggComponent, UseInHandEvent>(OnEggUse);
        SubscribeLocalEvent<XenoEggComponent, ActivateInWorldEvent>(OnEggActivate);
        SubscribeLocalEvent<XenoParasiteComponent, InteractHandEvent>(OnParasiteInteract);
        SubscribeLocalEvent<XenoLarvaComponent, ComponentStartup>(OnLarvaStartup);
    }

    private void OnLarvaStartup(Entity<XenoLarvaComponent> larva, ref ComponentStartup args)
    {
        larva.Comp.EvolveAt = _timing.CurTime + larva.Comp.EvolutionDelay;
        Dirty(larva);
    }

    private void OnEggUse(Entity<XenoEggComponent> egg, ref UseInHandEvent args)
    {
        if (args.Handled || !TryPlace(egg))
            return;

        _transform.SetCoordinates(egg, Transform(args.User).Coordinates);
        args.Handled = true;
    }

    private void OnEggActivate(Entity<XenoEggComponent> egg, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !TryOpen(egg))
            return;

        args.Handled = true;
    }

    private void OnParasiteInteract(Entity<XenoParasiteComponent> parasite, ref InteractHandEvent args)
    {
        if (!args.Handled && TryInfect(parasite, args.User))
            args.Handled = true;
    }

    public bool TryPlace(Entity<XenoEggComponent> egg)
    {
        if (egg.Comp.State != XenoEggState.Item || egg.Comp.PlacementAt != null)
            return false;

        egg.Comp.PlacementAt = _timing.CurTime + egg.Comp.PlacementDelay;
        Dirty(egg);
        return true;
    }

    public bool TryOpen(Entity<XenoEggComponent> egg)
    {
        if (egg.Comp.State != XenoEggState.Grown)
            return false;

        egg.Comp.State = XenoEggState.Opening;
        egg.Comp.OpenAt = _timing.CurTime + egg.Comp.OpeningDelay;
        Dirty(egg);
        return true;
    }

    public bool TryInfect(EntityUid parasite, EntityUid host)
    {
        if (!HasComp<XenoParasiteComponent>(parasite) ||
            !TryComp(host, out InfectableHostComponent? infectable) ||
            HasComp<XenoInfectionComponent>(host))
        {
            return false;
        }

        var infection = AddComp<XenoInfectionComponent>(host);
        infection.SpawnAt = _timing.CurTime + infectable.IncubationDelay;
        infection.LarvaPrototype = infectable.LarvaPrototype;
        Dirty(host, infection);
        return true;
    }

    public EntityUid? TryEvolveLarva(EntityUid larva)
    {
        if (!RemComp<XenoLarvaComponent>(larva))
            return null;

        return _polymorph.PolymorphEntity(larva, AdultEvolution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var larvae = EntityQueryEnumerator<XenoLarvaComponent>();
        while (larvae.MoveNext(out var larvaUid, out var larva))
        {
            if (larva.EvolveAt <= now)
                TryEvolveLarva(larvaUid);
        }

        var eggs = EntityQueryEnumerator<XenoEggComponent>();
        while (eggs.MoveNext(out var uid, out var egg))
        {
            if (egg.State == XenoEggState.Item && egg.PlacementAt <= now)
            {
                egg.PlacementAt = null;
                egg.State = XenoEggState.Growing;
                egg.GrowAt = now + egg.GrowthDelay;
                _transform.AnchorEntity(uid, Transform(uid));
                Dirty(uid, egg);
                continue;
            }

            if (egg.State == XenoEggState.Growing && egg.GrowAt <= now)
            {
                egg.GrowAt = null;
                egg.State = XenoEggState.Grown;
                Dirty(uid, egg);
                continue;
            }

            if (egg.State != XenoEggState.Opening || egg.OpenAt > now)
                continue;

            egg.OpenAt = null;
            egg.SpawnedParasite ??= Spawn(egg.ParasitePrototype, Transform(uid).Coordinates);
            egg.State = XenoEggState.Opened;
            Dirty(uid, egg);
        }

        var infections = EntityQueryEnumerator<XenoInfectionComponent>();
        while (infections.MoveNext(out var uid, out var infection))
        {
            if (infection.SpawnedLarva != null || infection.SpawnAt > now)
                continue;

            var larva = Spawn(infection.LarvaPrototype, Transform(uid).Coordinates);
            var faction = EnsureComp<NpcFactionMemberComponent>(larva);
            _factions.ClearFactions((larva, faction));
            _factions.AddFaction((larva, faction), infection.LarvaFaction);
            infection.SpawnedLarva = larva;
            Dirty(uid, infection);
        }
    }
}
