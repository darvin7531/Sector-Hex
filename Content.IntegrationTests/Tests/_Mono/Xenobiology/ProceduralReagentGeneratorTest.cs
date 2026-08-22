using Content.Shared._Mono.Xenobiology.Chemistry;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed class ProceduralReagentGeneratorTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: reagentProperty
  id: MonoTestToxic
  name: mono-test-toxic
  description: mono-test-toxic-desc
  effectName: MonoTestToxic
  category: Toxicant
  rarity: Common
  hint: Negative

- type: reagentProperty
  id: MonoTestAntitoxic
  name: mono-test-antitoxic
  description: mono-test-antitoxic-desc
  effectName: MonoTestAntitoxic
  category: Medicine
  rarity: Common
  hint: Positive

- type: reagentProperty
  id: MonoTestMuscleStimulating
  name: mono-test-muscle-stimulating
  description: mono-test-muscle-stimulating-desc
  effectName: MonoTestMuscleStimulating
  category: Medicine
  rarity: Common
  hint: Positive

- type: reagentProperty
  id: MonoTestCardiopeutic
  name: mono-test-cardiopeutic
  description: mono-test-cardiopeutic-desc
  effectName: MonoTestCardiopeutic
  category: Medicine
  rarity: Common
  hint: Positive

- type: reagentProperty
  id: MonoTestDefibrillating
  name: mono-test-defibrillating
  description: mono-test-defibrillating-desc
  effectName: MonoTestDefibrillating
  category: Medicine
  rarity: Legendary
  hint: Legendary
  maxLevel: 4

- type: dataset
  id: MonoReagentConflictingProperties
  values:
  - MonoTestToxic,MonoTestAntitoxic

- type: dataset
  id: MonoReagentCombiningProperties
  values:
  - MonoTestDefibrillating,MonoTestMuscleStimulating,MonoTestCardiopeutic
""";

    [Test]
    public async Task WeakerConflictingPropertyReducesExistingProperty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();
        generator.ReloadRules();
        var reagent = new GeneratedReagentData();
        reagent.Effects["MonoTestToxic"] = 3;

        var inserted = generator.InsertProperty(ref reagent, "MonoTestAntitoxic", 1);

        Assert.Multiple(() =>
        {
            Assert.That(inserted, Is.False);
            Assert.That(reagent.Effects["MonoTestToxic"], Is.EqualTo(2));
            Assert.That(reagent.Effects.ContainsKey("MonoTestAntitoxic"), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
