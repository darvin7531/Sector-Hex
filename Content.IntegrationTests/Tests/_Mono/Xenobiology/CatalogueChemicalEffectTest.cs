#nullable enable

using Content.Server._Mono.Xenobiology.Chemistry.Effects;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Temperature.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Drunk;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed class CatalogueChemicalEffectTest
{
    private const string TestReagent = "MonoCatalogueEffectTestReagent";

    [TestPrototypes]
    private const string Prototypes = """
- type: reagent
  id: MonoCatalogueEffectTestReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  group: Medicine
  flavor: bitter
  color: "#ffffff"
""";

    [Test]
    public async Task DamageAndHealingPropertiesChangeMatchingDamagePools()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.ResolveDependency<IEntityManager>();
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var target = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var damageable = entMan.GetComponent<DamageableComponent>(target);
            var caustic = prototypes.Index<DamageTypePrototype>("Caustic");
            var reagent = prototypes.Index<ReagentPrototype>(TestReagent);

            entMan.System<DamageableSystem>().TryChangeDamage(target, new DamageSpecifier(caustic, 10), true);
            new Corrosive { Potency = 8 }.Effect(ReagentArgs(target, entMan, reagent));
            Assert.That(damageable.Damage.DamageDict["Caustic"], Is.GreaterThan((FixedPoint2) 10));

            new Anticorrosive { Potency = 8 }.Effect(ReagentArgs(target, entMan, reagent));
            Assert.That(damageable.Damage.DamageDict["Caustic"], Is.EqualTo((FixedPoint2) 10));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TemperatureHungerBloodAndStatusFamiliesUseNativeSystems()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.ResolveDependency<IEntityManager>();
            var reagent = pair.Server.ResolveDependency<IPrototypeManager>().Index<ReagentPrototype>(TestReagent);
            var target = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var args = ReagentArgs(target, entMan, reagent);

            var temperature = entMan.GetComponent<TemperatureComponent>(target);
            var startingTemperature = temperature.CurrentTemperature;
            new Hyperthermic { Potency = 8 }.Effect(args);
            Assert.That(temperature.CurrentTemperature, Is.GreaterThan(startingTemperature));
            new Hypothermic { Potency = 8 }.Effect(args);
            Assert.That(temperature.CurrentTemperature, Is.EqualTo(startingTemperature).Within(0.001f));

            var hunger = entMan.GetComponent<Content.Shared.Nutrition.Components.HungerComponent>(target);
            var hungerSystem = entMan.System<HungerSystem>();
            var startingHunger = hungerSystem.GetHunger(hunger);
            new Nutritious { Potency = 8 }.Effect(args);
            Assert.That(hungerSystem.GetHunger(hunger), Is.GreaterThan(startingHunger));

            var bloodstream = entMan.GetComponent<BloodstreamComponent>(target);
            entMan.System<BloodstreamSystem>().TryModifyBleedAmount(target, 5, bloodstream);
            new Hemostatic { Potency = 8 }.Effect(args);
            Assert.That(bloodstream.BleedAmount, Is.LessThan(5));

            new Alcoholic { Potency = 8 }.Effect(args);
            Assert.That(entMan.HasComponent<DrunkComponent>(target), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    private static EntityEffectReagentArgs ReagentArgs(
        EntityUid target,
        IEntityManager entMan,
        ReagentPrototype reagent)
    {
        return new EntityEffectReagentArgs(
            target,
            entMan,
            null,
            new Solution(reagent.ID, 1),
            1,
            reagent,
            null,
            1);
    }
}
