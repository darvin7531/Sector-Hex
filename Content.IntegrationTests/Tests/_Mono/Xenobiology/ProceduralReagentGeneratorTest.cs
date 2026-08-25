using System.Collections.Generic;
using System.Linq;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Robust.Shared.Random;

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
  effectName: Toxic
  category: Toxicant
  rarity: Common
  hint: Negative

- type: reagentProperty
  id: MonoTestAntitoxic
  name: mono-test-antitoxic
  description: mono-test-antitoxic-desc
  effectName: Neogenetic
  category: Medicine
  rarity: Common
  hint: Positive

- type: reagentProperty
  id: MonoTestMuscleStimulating
  name: mono-test-muscle-stimulating
  description: mono-test-muscle-stimulating-desc
  effectName: Boosting
  category: Medicine
  rarity: Common
  hint: Positive

- type: reagentProperty
  id: MonoTestCardiopeutic
  name: mono-test-cardiopeutic
  description: mono-test-cardiopeutic-desc
  effectName: Neogenetic
  category: Medicine
  rarity: Common
  hint: Positive

- type: reagentProperty
  id: MonoTestDefibrillating
  name: mono-test-defibrillating
  description: mono-test-defibrillating-desc
  effectName: Neogenetic
  category: Medicine
  rarity: Legendary
  hint: Legendary
  maxLevel: 4

- type: reagentProperty
  id: MonoTestNeutral
  name: mono-test-neutral
  description: mono-test-neutral-desc
  effectName: Boosting
  category: Reactant
  rarity: Common
  hint: Neutral

- type: reagentProperty
  id: MonoTestRare
  name: mono-test-rare
  description: mono-test-rare-desc
  effectName: Neogenetic
  category: Medicine
  rarity: Rare
  hint: Rare

- type: reagentProperty
  id: MonoTestDisabled
  name: mono-test-disabled
  description: mono-test-disabled-desc
  effectName: MonoTestDisabled
  category: Medicine
  rarity: Disabled
  hint: Positive

- type: reagentProperty
  id: MonoTestGenerationDisabled
  name: mono-test-generation-disabled
  description: mono-test-generation-disabled-desc
  effectName: MonoTestGenerationDisabled
  category: Medicine
  rarity: Common
  hint: Positive
  generationDisabled: true

- type: reagentProperty
  id: MonoTestGenerationDisabledResult
  name: mono-test-generation-disabled-result
  description: mono-test-generation-disabled-result-desc
  category: Medicine
  rarity: Rare
  hint: Rare
  generationDisabled: true

- type: reagent
  id: MonoTestBasicReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  class: Basic

- type: reagent
  id: MonoTestBasicReagentTwo
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  class: Basic

- type: reagent
  id: MonoTestBasicReagentThree
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  class: Basic

- type: reagent
  id: MonoTestCommonReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  class: Common

- type: reagent
  id: MonoTestNoGenerationReagent
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
  class: Rare
  flags: NoGeneration

- type: dataset
  id: MonoTestReagentConflictingProperties
  values:
  - MonoTestToxic,MonoTestAntitoxic

- type: dataset
  id: MonoTestReagentCombiningProperties
  values:
  - MonoTestDefibrillating,MonoTestMuscleStimulating,MonoTestCardiopeutic
  - MonoTestGenerationDisabledResult,MonoTestNeutral,MonoTestAntitoxic
""";

    [Test]
    public async Task WeakerConflictingPropertyReducesExistingProperty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();
        generator.ReloadRules("MonoTestReagentConflictingProperties", "MonoTestReagentCombiningProperties");
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

    [Test]
    public async Task CompletePropertyCombinationCreatesResultWithPotencyDifference()
    {
        await using var pair = await PoolManager.GetServerClient();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();
        generator.ReloadRules("MonoTestReagentConflictingProperties", "MonoTestReagentCombiningProperties");
        var reagent = new GeneratedReagentData();
        reagent.Effects["MonoTestMuscleStimulating"] = 2;

        var inserted = generator.InsertProperty(ref reagent, "MonoTestCardiopeutic", 3);

        Assert.Multiple(() =>
        {
            Assert.That(inserted, Is.True);
            Assert.That(reagent.Effects["MonoTestMuscleStimulating"], Is.EqualTo(1));
            Assert.That(reagent.Effects["MonoTestDefibrillating"], Is.EqualTo(1));
            Assert.That(reagent.Effects.ContainsKey("MonoTestCardiopeutic"), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GenerationDisabledPropertiesCannotBeInsertedOrProducedByCombination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();
        generator.ReloadRules("MonoTestReagentConflictingProperties", "MonoTestReagentCombiningProperties");
        var reagent = new GeneratedReagentData();

        Assert.That(generator.InsertProperty(ref reagent, "MonoTestGenerationDisabled", 1), Is.False);

        reagent.Effects["MonoTestNeutral"] = 1;
        Assert.That(generator.InsertProperty(ref reagent, "MonoTestAntitoxic", 1), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reagent.Effects, Does.ContainKey("MonoTestAntitoxic"));
            Assert.That(reagent.Effects, Does.Not.ContainKey("MonoTestGenerationDisabledResult"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GeneratedCipheringCombinationAlwaysContainsEncrypted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();

        generator.PreparePools();

        Assert.That(generator.Combinations["Ciphering"], Does.Contain("Encrypted"));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreparePoolsSortsPropertiesAndReagentsForGeneration()
    {
        await using var pair = await PoolManager.GetServerClient();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();

        generator.PreparePools();

        Assert.Multiple(() =>
        {
            Assert.That(generator.PropertyPools["negative"], Does.Contain("MonoTestToxic"));
            Assert.That(generator.PropertyPools["neutral"], Does.Contain("MonoTestNeutral"));
            Assert.That(generator.PropertyPools["positive"], Does.Contain("MonoTestAntitoxic"));
            Assert.That(generator.PropertyPools["rare"], Does.Contain("MonoTestRare"));
            Assert.That(generator.PropertyPools.SelectMany(pool => pool.Value), Does.Not.Contain("MonoTestDisabled"));
            Assert.That(generator.PropertyPools.SelectMany(pool => pool.Value), Does.Not.Contain("MonoTestGenerationDisabled"));
            Assert.That(generator.ReagentClassPools["C1"], Does.Contain("MonoTestBasicReagent"));
            Assert.That(generator.ReagentClassPools["C2"], Does.Contain("MonoTestCommonReagent"));
            Assert.That(generator.ReagentClassPools["C"], Does.Contain("MonoTestBasicReagent"));
            Assert.That(generator.ReagentClassPools.SelectMany(pool => pool.Value), Does.Not.Contain("MonoTestNoGenerationReagent"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GenerateNameUsesDatasetsAndTauPoolCount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();
        generator.PreparePools();
        var reagent = new GeneratedReagentData();

        generator.GenerateName(ref reagent);

        Assert.Multiple(() =>
        {
            Assert.That(reagent.Name, Is.Not.Empty);
            Assert.That(reagent.ID, Is.EqualTo($"TAU-0-{reagent.Name}"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GenerateStatsIsDeterministicForFixedSeed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var random = pair.Server.ResolveDependency<IRobustRandom>();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();
        var first = new GeneratedReagentData { GenTier = 3 };
        var second = new GeneratedReagentData { GenTier = 3 };

        random.SetSeed(8675309);
        generator.GenerateStats(ref first, noProperties: true);
        random.SetSeed(8675309);
        generator.GenerateStats(ref second, noProperties: true);

        Assert.Multiple(() =>
        {
            Assert.That(first.Overdose, Is.EqualTo(second.Overdose));
            Assert.That(first.CriticalOverdose, Is.EqualTo(second.CriticalOverdose));
            Assert.That(first.Color, Is.EqualTo(second.Color));
            Assert.That(first.CriticalOverdose, Is.GreaterThanOrEqualTo(first.Overdose + 5));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AddChemicalUsesExplicitClassAndRejectsDuplicate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();
        generator.PreparePools();
        var reagent = new GeneratedReagentData { GenTier = 1 };

        var selected = generator.AddChemical(ref reagent, cClass: "1");
        var duplicate = generator.AddChemical(ref reagent, chem: selected);

        Assert.Multiple(() =>
        {
            Assert.That(generator.ReagentClassPools["C1"], Does.Contain(selected));
            Assert.That(reagent.Recipe[selected], Is.EqualTo((1, false)));
            Assert.That(duplicate, Is.EqualTo(bool.FalseString));
            Assert.That(reagent.Recipe, Has.Count.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GenerateRecipeIncludesRequiredIngredient()
    {
        await using var pair = await PoolManager.GetServerClient();
        var random = pair.Server.ResolveDependency<IRobustRandom>();
        var generator = pair.Server.System<ProceduralReagentGeneratorSystem>();
        generator.PreparePools();
        var reagent = new GeneratedReagentData { GenTier = 1 };
        HashSet<string> required = ["MonoTestBasicReagent"];
        random.SetSeed(12345);

        var generated = generator.GenerateRecipe(ref reagent, required);

        Assert.Multiple(() =>
        {
            Assert.That(generated, Is.True);
            Assert.That(reagent.Recipe, Has.Count.EqualTo(3));
            Assert.That(reagent.Recipe, Does.ContainKey("MonoTestBasicReagent"));
            Assert.That(reagent.Recipe.Values.Skip(1).All(value => value.Amount == 1), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
