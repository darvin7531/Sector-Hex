#nullable enable

using Content.Shared._Mono.Xenobiology.Chemistry.Effects;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed partial class MonoChemicalEffectTest
{
    private const string TestEntity = "MonoChemicalEffectTestEntity";
    private const string PlainReagent = "MonoChemicalEffectPlainReagent";
    private const string BoostedReagent = "MonoChemicalEffectBoostedReagent";

    [TestPrototypes]
    private const string Prototypes = """
- type: damageContainer
  id: MonoChemicalEffectDamageContainer
  supportedTypes:
  - Blunt
  - Poison

- type: entity
  id: MonoChemicalEffectTestEntity
  name: mono chemical effect test entity
  components:
  - type: Damageable
    damageContainer: MonoChemicalEffectDamageContainer

- type: reagent
  id: MonoChemicalEffectPlainReagent
  name: mono chemical effect plain reagent
  group: Medicine
  desc: mono chemical effect plain reagent
  physicalDesc: reagent-physical-desc-opaque
  flavor: bitter
  color: "#ffffff"
  overdose: 5
  criticalOverdose: 10

- type: reagent
  id: MonoChemicalEffectBoostedReagent
  name: mono chemical effect boosted reagent
  group: Medicine
  desc: mono chemical effect boosted reagent
  physicalDesc: reagent-physical-desc-opaque
  flavor: bitter
  color: "#ffffff"
  metabolisms:
    Medicine:
      effects:
      - !type:Boosting
        potency: 4
""";

    [Test]
    public async Task PositiveAndNegativeEffectsScaleWithPotency()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var damageable = server.System<DamageableSystem>();
        var target = entMan.SpawnEntity(TestEntity, MapCoordinates.Nullspace);
        var component = entMan.GetComponent<DamageableComponent>(target);
        var blunt = prototypes.Index<DamageTypePrototype>("Blunt");
        var reagent = prototypes.Index<ReagentPrototype>(PlainReagent);
        var args = ReagentArgs(target, entMan, reagent, 1);

        damageable.TryChangeDamage(target, new DamageSpecifier(blunt, 10), true);
        new Neogenetic { Potency = 8 }.Effect(args);
        new Toxic { Potency = 8 }.Effect(args);

        Assert.Multiple(() =>
        {
            Assert.That(component.Damage.DamageDict["Blunt"], Is.EqualTo((FixedPoint2) 8));
            Assert.That(component.Damage.DamageDict["Poison"], Is.EqualTo((FixedPoint2) 2));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BoostingEffectInMetabolismRaisesOtherEffectPotency()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var target = entMan.SpawnEntity(TestEntity, MapCoordinates.Nullspace);
        var component = entMan.GetComponent<DamageableComponent>(target);
        var reagent = prototypes.Index<ReagentPrototype>(BoostedReagent);

        new Toxic { Potency = 8 }.Effect(ReagentArgs(target, entMan, reagent, 1));

        Assert.That(component.Damage.DamageDict["Poison"], Is.EqualTo(FixedPoint2.New(2.5f)));
        await pair.CleanReturnAsync();
    }

    [TestCase(4, 1, 0, 0)]
    [TestCase(5, 1, 1, 0)]
    [TestCase(10, 1, 1, 1)]
    public async Task DispatchesOverdoseHooksAtReagentThresholds(
        int quantity,
        int regularTicks,
        int overdoseTicks,
        int criticalTicks)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var reagent = server.ResolveDependency<IPrototypeManager>().Index<ReagentPrototype>(PlainReagent);
        var effect = new ProbeChemicalEffect { Potency = 8 };

        effect.Effect(ReagentArgs(entMan.SpawnEntity(TestEntity, MapCoordinates.Nullspace), entMan, reagent, quantity));

        Assert.Multiple(() =>
        {
            Assert.That(effect.RegularTicks, Is.EqualTo(regularTicks));
            Assert.That(effect.OverdoseTicks, Is.EqualTo(overdoseTicks));
            Assert.That(effect.CriticalTicks, Is.EqualTo(criticalTicks));
            Assert.That(effect.LastPotency, Is.EqualTo((FixedPoint2) 2));
        });

        await pair.CleanReturnAsync();
    }

    private static EntityEffectReagentArgs ReagentArgs(
        EntityUid target,
        IEntityManager entMan,
        ReagentPrototype reagent,
        FixedPoint2 quantity)
    {
        return new EntityEffectReagentArgs(
            target,
            entMan,
            null,
            new Solution(reagent.ID, quantity),
            quantity,
            reagent,
            null,
            1);
    }

    private sealed partial class ProbeChemicalEffect : MonoChemicalEffect
    {
        public int RegularTicks;
        public int OverdoseTicks;
        public int CriticalTicks;
        public FixedPoint2 LastPotency;

        protected override string? ReagentEffectGuidebookText(
            IPrototypeManager prototype,
            IEntitySystemManager entSys)
        {
            return null;
        }

        protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        {
            RegularTicks++;
            LastPotency = potency;
        }

        protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        {
            OverdoseTicks++;
        }

        protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        {
            CriticalTicks++;
        }
    }
}
