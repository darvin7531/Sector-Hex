// SPDX-FileCopyrightText: 2026 Nous Research
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Polymorph.Systems;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Xenobiology.Abomination;

[RegisterComponent]
public sealed partial class AbominationInfectionComponent : Component
{
    [DataField]
    public TimeSpan InfectedAt;

    [DataField]
    public TimeSpan SymptomDelay = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan NextDamageAt;

    [DataField]
    public DamageSpecifier SymptomDamage = new();

    [DataField]
    public bool HasShownSymptoms;

    [DataField]
    public bool Converted;
}

public sealed partial class AbominationInfectionEffect : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;

    public override void Effect(EntityEffectBaseArgs args)
        => args.EntityManager.System<AbominationInfectionSystem>().TryInfect(args.TargetEntity);
}

public sealed class AbominationInfectionSystem : EntitySystem
{
    public const string AbominationPrototype = "MobAbomination";
    public static readonly ProtoId<PolymorphPrototype> ConversionPolymorph = "AbominationInfectionConversion";

    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationInfectionComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationInfectionComponent>();

        while (query.MoveNext(out var uid, out var infection))
        {
            if (_mobState.IsDead(uid) || now - infection.InfectedAt < infection.SymptomDelay)
                continue;

            infection.HasShownSymptoms = true;
            if (now < infection.NextDamageAt)
                continue;

            infection.NextDamageAt = now + infection.DamageInterval;
            _damage.TryChangeDamage(uid, infection.SymptomDamage, true);
        }
    }

    public bool TryInfect(EntityUid target)
    {
        if (!IsBiologicalTarget(target) ||
            _mobState.IsDead(target) ||
            HasComp<AbominationInfectionComponent>(target))
        {
            return false;
        }

        var now = _timing.CurTime;
        var infection = AddComp<AbominationInfectionComponent>(target);
        infection.InfectedAt = now;
        infection.NextDamageAt = now + infection.SymptomDelay;
        infection.SymptomDamage.DamageDict["Poison"] = 5;
        return true;
    }

    public bool TryConvert(EntityUid target)
    {
        if (!TryComp<AbominationInfectionComponent>(target, out var infection) ||
            infection.Converted ||
            !infection.HasShownSymptoms ||
            !_mobState.IsDead(target) ||
            !IsBiologicalTarget(target))
        {
            return false;
        }

        infection.Converted = true;
        _polymorph.PolymorphEntity(target, ConversionPolymorph);
        return true;
    }

    private bool IsBiologicalTarget(EntityUid target)
    {
        if (HasComp<SiliconComponent>(target) ||
            !HasComp<MobStateComponent>(target) ||
            MetaData(target).EntityPrototype?.ID == AbominationPrototype)
        {
            return false;
        }

        return HasComp<HumanoidAppearanceComponent>(target) || HasComp<BodyComponent>(target);
    }

    private void OnMobStateChanged(Entity<AbominationInfectionComponent> entity, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            TryConvert(entity.Owner);
    }
}
