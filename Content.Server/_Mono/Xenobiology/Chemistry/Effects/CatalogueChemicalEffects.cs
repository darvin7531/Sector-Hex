// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Content.Server._Mono.Xenobiology.Abomination;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Medical;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Server.Stunnable;
using Content.Shared._Mono.Xenobiology.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Drunk;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Jittering;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Xenobiology.Chemistry.Effects;

public abstract partial class MonoServerChemicalEffect : MonoChemicalEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
}

public abstract partial class MonoTypedDamageEffect : MonoServerChemicalEffect
{
    protected abstract ProtoId<DamageTypePrototype> DamageType { get; }
    protected virtual FixedPoint2 Multiplier => 1;

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        damageable.TryChangeDamage(
            args.TargetEntity,
            new DamageSpecifier(DamageType, potency * Multiplier),
            true,
            interruptsDoAfters: false,
            canSever: false);
    }
}

public sealed partial class Hypoxemic : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Asphyxiation";
}

public sealed partial class Corrosive : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Caustic";
}

public sealed partial class Biocidic : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Cellular";
}

public sealed partial class Carcinogenic : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Cellular";
    protected override FixedPoint2 Multiplier => 0.5;
}

public sealed partial class Electrogenetic : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Shock";
}

public sealed partial class DNADisintegrating : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Cellular";
    protected override FixedPoint2 Multiplier => 5;
}

public sealed partial class Antitoxic : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Poison";
    protected override FixedPoint2 Multiplier => -1;
}

public sealed partial class Anticorrosive : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Caustic";
    protected override FixedPoint2 Multiplier => -1;
}

public sealed partial class Oxygenating : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Asphyxiation";
    protected override FixedPoint2 Multiplier => -1;
}

public sealed partial class Anticarcinogenic : MonoTypedDamageEffect
{
    protected override ProtoId<DamageTypePrototype> DamageType => "Cellular";
    protected override FixedPoint2 Multiplier => -1;
}

public sealed partial class Repairing : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => ChangeDamage(damageable, args, potency, "Blunt", "Slash", "Piercing", "Heat");

    internal static void ChangeDamage(
        DamageableSystem damageable,
        EntityEffectReagentArgs args,
        FixedPoint2 potency,
        params ProtoId<DamageTypePrototype>[] types)
    {
        var damage = new DamageSpecifier();
        foreach (var type in types)
            damage.DamageDict[type] = -potency;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false, canSever: false);
    }
}

public sealed partial class Hypergenetic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => Repairing.ChangeDamage(
            damageable,
            args,
            potency,
            "Blunt", "Slash", "Piercing", "Heat", "Cold", "Shock", "Poison", "Cellular", "Asphyxiation");
}

public sealed partial class Omnipotent : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => Repairing.ChangeDamage(
            damageable,
            args,
            potency * 5,
            "Blunt", "Slash", "Piercing", "Heat", "Cold", "Shock", "Poison", "Cellular", "Asphyxiation", "Bloodloss");
}

public sealed partial class Hemolytic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.EntityManager.TryGetComponent<BloodstreamComponent>(args.TargetEntity, out var blood))
            args.EntityManager.System<BloodstreamSystem>().TryModifyBloodLevel(args.TargetEntity, -potency * 5, blood);
    }
}

public sealed partial class Hemorrhaging : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.EntityManager.TryGetComponent<BloodstreamComponent>(args.TargetEntity, out var blood))
            args.EntityManager.System<BloodstreamSystem>().TryModifyBleedAmount(args.TargetEntity, potency.Float(), blood);
    }
}

public sealed partial class Hemogenic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.EntityManager.TryGetComponent<BloodstreamComponent>(args.TargetEntity, out var blood))
            args.EntityManager.System<BloodstreamSystem>().TryModifyBloodLevel(args.TargetEntity, potency, blood);
        if (args.EntityManager.TryGetComponent<HungerComponent>(args.TargetEntity, out var hunger))
            args.EntityManager.System<HungerSystem>().ModifyHunger(args.TargetEntity, -potency.Float(), hunger);
    }
}

public sealed partial class Yautjahemogenic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.EntityManager.TryGetComponent<BloodstreamComponent>(args.TargetEntity, out var blood))
            args.EntityManager.System<BloodstreamSystem>().TryModifyBloodLevel(args.TargetEntity, potency, blood);
    }
}

public sealed partial class Hemostatic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.EntityManager.TryGetComponent<BloodstreamComponent>(args.TargetEntity, out var blood))
            args.EntityManager.System<BloodstreamSystem>().TryModifyBleedAmount(args.TargetEntity, -potency.Float(), blood);
    }
}

public abstract partial class MonoTemperatureEffect : MonoServerChemicalEffect
{
    protected abstract float Direction { get; }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.EntityManager.TryGetComponent<TemperatureComponent>(args.TargetEntity, out var temperature))
            args.EntityManager.System<TemperatureSystem>()
                .ChangeHeat(args.TargetEntity, Direction * potency.Float() * 100, true, temperature);
    }
}

public sealed partial class Hyperthermic : MonoTemperatureEffect
{
    protected override float Direction => 1;
}

public sealed partial class Hypothermic : MonoTemperatureEffect
{
    protected override float Direction => -1;
}

public abstract partial class MonoHungerEffect : MonoServerChemicalEffect
{
    protected abstract float Factor { get; }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.EntityManager.TryGetComponent<HungerComponent>(args.TargetEntity, out var hunger))
            args.EntityManager.System<HungerSystem>().ModifyHunger(args.TargetEntity, potency.Float() * Factor, hunger);
    }
}

public sealed partial class Nutritious : MonoHungerEffect
{
    protected override float Factor => 5;
}

public sealed partial class Ketogenic : MonoHungerEffect
{
    protected override float Factor => 2;
}

public sealed partial class Ravenous : MonoHungerEffect
{
    protected override float Factor => -5;
}

public sealed partial class Alcoholic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<SharedDrunkSystem>()
            .TryApplyDrunkenness(args.TargetEntity, potency.Float() * 3, true);
}

public sealed partial class Hallucinogenic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<StatusEffectsSystem>().TryAddStatusEffect(
            args.TargetEntity,
            "SeeingRainbows",
            TimeSpan.FromSeconds(potency.Float() * 5),
            false,
            "SeeingRainbows");
}

public sealed partial class Antihallucinogenic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<StatusEffectsSystem>()
            .TryRemoveStatusEffect(args.TargetEntity, "SeeingRainbows");
}

public sealed partial class Psychostimulating : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<SharedJitteringSystem>().DoJitter(
            args.TargetEntity,
            TimeSpan.FromSeconds(potency.Float() * 2),
            false,
            10,
            4);
}

public sealed partial class Hypnotic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<StatusEffectsSystem>().TryAddStatusEffect(
            args.TargetEntity,
            "Drowsiness",
            TimeSpan.FromSeconds(potency.Float() * 2),
            false,
            "Drowsiness");
}

public sealed partial class Neuroinhibiting : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<StunSystem>().TryParalyze(
            args.TargetEntity,
            TimeSpan.FromSeconds(potency.Float() * 0.5f),
            false);
}

public sealed partial class Emetic : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<VomitSystem>().Vomit(args.TargetEntity, -8, -8);
}

public sealed partial class Excreting : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.Reagent is not { } reagent)
            return;
        args.EntityManager.System<BloodstreamSystem>()
            .FlushChemicals(args.TargetEntity, reagent.ID, potency.Float() * 3);
    }
}

public sealed partial class Curing : MonoServerChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (potency >= 1)
            args.EntityManager.RemoveComponent<AbominationInfectionComponent>(args.TargetEntity);
    }
}
