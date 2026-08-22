using System.Collections.Generic;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
public sealed class ProceduralReagentModelTest
{
    private static readonly ProtoId<ReagentPrototype> TestReagent = "MonoTestProceduralReagent";
    private static readonly ProtoId<ReagentPropertyPrototype> TestProperty = "MonoTestReagentProperty";

    [TestPrototypes]
    private const string Prototypes = """
- type: reagent
  id: MonoTestProceduralReagent
  name: mono-test-procedural-reagent
  group: Medicine
  desc: mono-test-procedural-reagent-desc
  physicalDesc: reagent-physical-desc-opaque
  flavor: bitter
  color: "#123456"
  class: Rare
  flags: Scannable, NoGeneration
  overdose: 17
  criticalOverdose: 29
  genTier: 3
  generated: true
  reward: 7

- type: reagentProperty
  id: MonoTestReagentProperty
  name: mono-test-reagent-property
  description: mono-test-reagent-property-desc
  effectName: HealthChange
  category: Medicine, Stimulant
  rarity: Rare
  hint: Positive
  level: 2
  maxLevel: 4
  value: 6
""";

    [Test]
    public async Task ExtendedReagentAndPropertyModelsLoadFromPrototypes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var reagent = prototypes.Index(TestReagent);
            Assert.Multiple(() =>
            {
                Assert.That(reagent.Class, Is.EqualTo(ProceduralReagentClass.Rare));
                Assert.That(reagent.Flags.HasFlag(ProceduralReagentFlag.Scannable), Is.True);
                Assert.That(reagent.Flags.HasFlag(ProceduralReagentFlag.NoGeneration), Is.True);
                Assert.That(reagent.Overdose, Is.EqualTo((FixedPoint2) 17));
                Assert.That(reagent.CriticalOverdose, Is.EqualTo((FixedPoint2) 29));
                Assert.That(reagent.GenTier, Is.EqualTo(3));
                Assert.That(reagent.Generated, Is.True);
                Assert.That(reagent.Reward, Is.EqualTo(7));
            });

            var property = prototypes.Index(TestProperty);
            Assert.Multiple(() =>
            {
                Assert.That(property.Category.HasFlag(ReagentPropertyType.Medicine), Is.True);
                Assert.That(property.Category.HasFlag(ReagentPropertyType.Stimulant), Is.True);
                Assert.That(property.Rarity, Is.EqualTo(ReagentPropertyRarity.Rare));
                Assert.That(property.Hint, Is.EqualTo(ReagentPropertyHint.Positive));
                Assert.That(property.Level, Is.EqualTo(2));
                Assert.That(property.MaxLevel, Is.EqualTo(4));
                Assert.That(property.Value, Is.EqualTo(6));
                Assert.That(property.EffectName, Is.EqualTo("HealthChange"));
            });
        });

        var generated = new GeneratedReagentData();
        Assert.Multiple(() =>
        {
            Assert.That(generated.RecipeYield, Is.EqualTo(1));
            Assert.That(generated.ScanPointYield, Is.EqualTo(2));
            Assert.That(generated.Overdose, Is.EqualTo((FixedPoint2) 30));
            Assert.That(generated.CriticalOverdose, Is.EqualTo((FixedPoint2) 50));
            Assert.That(generated.MetabolismRate, Is.EqualTo(FixedPoint2.New(0.1f)));
            Assert.That(generated.GenTier, Is.EqualTo(1));
            Assert.That(generated.Class, Is.EqualTo(ProceduralReagentClass.None));
            Assert.That(generated.Effects, Is.Empty);
            Assert.That(generated.Recipe, Is.Empty);
            Assert.That(generated.ModifiedChems, Is.Empty);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GeneratedReagentRegistersRuntimeReagentAndReaction()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var registry = server.System<ProceduralReagentRegistrySystem>();
        const string generatedId = "MonoRuntimeGeneratedReagent";

        var generated = new GeneratedReagentData
        {
            ID = generatedId,
            Name = "runtime generated reagent",
            Class = ProceduralReagentClass.Uncommon,
            Color = Color.FromHex("#654321"),
            Recipe = new Dictionary<string, (int Amount, bool Catalyst)>
            {
                ["Water"] = (2, false),
            },
            RecipeYield = 1,
            ScanPointYield = 5,
            GenTier = 2,
        };

        await server.WaitPost(() => registry.Register(generated));
        await server.WaitAssertion(() =>
        {
            var reagent = prototypes.Index<ReagentPrototype>(generatedId);
            Assert.Multiple(() =>
            {
                Assert.That(reagent.Generated, Is.True);
                Assert.That(reagent.Class, Is.EqualTo(ProceduralReagentClass.Uncommon));
                Assert.That(reagent.Flags.HasFlag(ProceduralReagentFlag.Scannable), Is.True);
                Assert.That(reagent.Reward, Is.EqualTo(5));
                Assert.That(reagent.GenTier, Is.EqualTo(2));
            });

            var reaction = prototypes.Index<ReactionPrototype>(generatedId);
            Assert.Multiple(() =>
            {
                Assert.That(reaction.Reactants["Water"].Amount, Is.EqualTo((FixedPoint2) 2));
                Assert.That(reaction.Reactants["Water"].Catalyst, Is.False);
                Assert.That(reaction.Products[generatedId], Is.EqualTo((FixedPoint2) 1));
            });
        });

        await pair.CleanReturnAsync();
    }
}
