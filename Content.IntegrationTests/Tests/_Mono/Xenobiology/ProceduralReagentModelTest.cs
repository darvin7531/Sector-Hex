using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
public sealed class ProceduralReagentModelTest
{
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
            var reagent = prototypes.Index<ReagentPrototype>("MonoTestProceduralReagent");
            Assert.Multiple(() =>
            {
                Assert.That(reagent.Class, Is.EqualTo(ProceduralReagentClass.Rare));
                Assert.That(reagent.Flags, Does.Contain(ProceduralReagentFlag.Scannable));
                Assert.That(reagent.Flags, Does.Contain(ProceduralReagentFlag.NoGeneration));
                Assert.That(reagent.Overdose, Is.EqualTo(17));
                Assert.That(reagent.CriticalOverdose, Is.EqualTo(29));
                Assert.That(reagent.GenTier, Is.EqualTo(3));
                Assert.That(reagent.Generated, Is.True);
                Assert.That(reagent.Reward, Is.EqualTo(7));
            });

            var property = prototypes.Index<ReagentPropertyPrototype>("MonoTestReagentProperty");
            Assert.Multiple(() =>
            {
                Assert.That(property.Category, Does.Contain(ReagentPropertyType.Medicine));
                Assert.That(property.Category, Does.Contain(ReagentPropertyType.Stimulant));
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
            Assert.That(generated.Overdose, Is.EqualTo(30));
            Assert.That(generated.CriticalOverdose, Is.EqualTo(50));
            Assert.That(generated.MetabolismRate, Is.EqualTo(0.1));
            Assert.That(generated.GenTier, Is.EqualTo(1));
            Assert.That(generated.Class, Is.EqualTo(ProceduralReagentClass.None));
            Assert.That(generated.Effects, Is.Empty);
            Assert.That(generated.Recipe, Is.Empty);
            Assert.That(generated.ModifiedChems, Is.Empty);
        });

        await pair.CleanReturnAsync();
    }
}
