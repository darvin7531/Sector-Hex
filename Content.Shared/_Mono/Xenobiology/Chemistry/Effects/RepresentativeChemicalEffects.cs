// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Xenobiology.Chemistry.Effects;

public sealed partial class Neogenetic : MonoChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        damageable.TryChangeDamage(
            args.TargetEntity,
            Damage(BluntType, -potency),
            true,
            interruptsDoAfters: false,
            canSever: false);
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        damageable.TryChangeDamage(
            args.TargetEntity,
            Damage(HeatType, potency),
            true,
            interruptsDoAfters: false,
            canSever: false);
    }

    protected override void TickCriticalOverdose(
        DamageableSystem damageable,
        FixedPoint2 potency,
        EntityEffectReagentArgs args)
    {
        var damage = Damage(HeatType, potency * 5);
        damage.DamageDict[PoisonType] = potency * 2;
        damageable.TryChangeDamage(
            args.TargetEntity,
            damage,
            true,
            interruptsDoAfters: false,
            canSever: false);
    }

    private static DamageSpecifier Damage(ProtoId<DamageTypePrototype> type, FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        return damage;
    }
}

public sealed partial class Toxic : MonoChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        ApplyPoison(damageable, potency, args);
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        ApplyPoison(damageable, potency * 2, args);
    }

    protected override void TickCriticalOverdose(
        DamageableSystem damageable,
        FixedPoint2 potency,
        EntityEffectReagentArgs args)
    {
        ApplyPoison(damageable, potency * 5, args);
    }

    private static void ApplyPoison(
        DamageableSystem damageable,
        FixedPoint2 amount,
        EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonType] = amount;
        damageable.TryChangeDamage(
            args.TargetEntity,
            damage,
            true,
            interruptsDoAfters: false,
            canSever: false);
    }
}

public sealed partial class Boosting : MonoChemicalEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    protected override void ReagentBoost(EntityEffectReagentArgs args, ref float boost)
    {
        boost += Potency * 0.5f;
    }
}
