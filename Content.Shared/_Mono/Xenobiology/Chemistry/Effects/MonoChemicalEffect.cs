// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;

namespace Content.Shared._Mono.Xenobiology.Chemistry.Effects;

public abstract partial class MonoChemicalEffect : EntityEffect
{
    [DataField]
    public float Potency;

    public float ActualPotency => Potency * 0.5f;

    public float PotencyPerSecond => ActualPotency * 0.5f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs { Reagent: { } reagent } reagentArgs)
            return;

        var potency = FixedPoint2.New((Potency + CalculateReagentBoost(reagentArgs)) * 0.25f) * reagentArgs.Scale;
        var damageable = args.EntityManager.System<DamageableSystem>();
        Tick(damageable, potency, reagentArgs);

        var totalQuantity = reagentArgs.Source?.GetTotalPrototypeQuantity(reagent.ID) ?? FixedPoint2.Zero;
        if (reagent.Overdose is { } overdose && totalQuantity >= overdose)
            TickOverdose(damageable, potency, reagentArgs);

        if (reagent.CriticalOverdose is { } criticalOverdose && totalQuantity >= criticalOverdose)
            TickCriticalOverdose(damageable, potency, reagentArgs);
    }

    private static float CalculateReagentBoost(EntityEffectReagentArgs args)
    {
        var boost = 0f;
        if (args.Reagent?.Metabolisms is null)
            return boost;

        foreach (var entry in args.Reagent.Metabolisms.Values)
        {
            foreach (var effect in entry.Effects)
            {
                if (effect is MonoChemicalEffect chemicalEffect)
                    chemicalEffect.ReagentBoost(args, ref boost);
            }
        }

        return boost;
    }

    protected virtual void ReagentBoost(EntityEffectReagentArgs args, ref float boost)
    {
    }

    protected virtual void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
    }

    protected virtual void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
    }

    protected virtual void TickCriticalOverdose(
        DamageableSystem damageable,
        FixedPoint2 potency,
        EntityEffectReagentArgs args)
    {
    }
}
