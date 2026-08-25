using System.Collections.Generic;
using System.Linq;
using Content.Server.EntityEffects.Effects;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed class ReagentPropertyCatalogueTest
{
    private static readonly HashSet<string> ExpectedProperties =
    [
        "Negative", "Hypoxemic", "Toxic", "Corrosive", "Biocidic", "Neuropathic", "Hemolytic",
        "Hemorrhaging", "Carcinogenic", "Hepatotoxic", "Intravenous", "Nephrotoxic", "Pneumotoxic",
        "Oculotoxic", "Cardiotoxic", "Neurotoxic", "Hypermetabolic", "Addictive", "Hemositic", "Igniting",
        "Neutral", "Cryometabolizing", "Thanatometabolizing", "Excreting", "Nutritious", "Ketogenic",
        "Neuroinhibiting", "Alcoholic", "Hallucinogenic", "Antispasmodic", "Hyperthermic", "Hypothermic",
        "Atrichogenic", "Trichogenic", "Allergenic", "Euphoric", "Emetic", "Psychostimulating",
        "Antihallucinogenic", "Hypometabolic", "Hypnotic", "Hyperthrottling", "Viscous", "Thermostabilizing",
        "Focusing", "Transformative", "Unknown", "Positive", "Antitoxic", "Anticorrosive", "Neogenetic",
        "Repairing", "Hemogenic", "Yautjahemogenic", "Hemostatic", "Nervestimulating", "Musclestimulating",
        "Painkilling", "Hepatopeutic", "Nephropeutic", "Pneumopeutic", "Oculopeutic", "Cardiopeutic",
        "Neuropeutic", "Bonemending", "Fluxing", "Neurocryogenic", "Antiparasitic", "Electrogenetic",
        "Defibrillating", "Hyperdensificating", "Neuroshielding", "Antiaddictive", "PositiveFire", "Fueling",
        "Oxidizing", "Flowing", "Explosive", "Photosensitive", "Crystallization", "Disrupting", "Neutralizing",
        "Cardiostabilizing", "Aiding", "Oxygenating", "Anticarcinogenic", "Firepenetrating", "Special",
        "Boosting", "Optimized", "Hypergenetic", "Organhealing", "DNADisintegrating", "Regulating", "Ciphering",
        "Encrypted", "Crossciphering", "Crossmetabolizing", "Embryonic", "Transforming", "Ravenous", "Curing",
        "Omnipotent", "Radius", "Intensity", "Duration", "Encephaloprasive",
    ];

    [Test]
    public async Task CompleteRussianCmCatalogueAndDatasetsLoad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();

        var actual = prototypes.EnumeratePrototypes<ReagentPropertyPrototype>().Select(property => property.ID).ToHashSet();
        var conflicts = prototypes.Index<DatasetPrototype>("MonoReagentConflictingProperties");
        var combinations = prototypes.Index<DatasetPrototype>("MonoReagentCombiningProperties");

        Assert.Multiple(() =>
        {
            Assert.That(actual, Is.SupersetOf(ExpectedProperties));
            Assert.That(ExpectedProperties, Has.Count.EqualTo(107));
            Assert.That(conflicts.Values, Has.Count.EqualTo(42));
            Assert.That(combinations.Values, Has.Count.EqualTo(10));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryGeneratableEffectNameResolvesToAnEntityEffectType()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        var effectNames = typeof(EntityEffect).Assembly.GetTypes()
            .Concat(typeof(HealthChange).Assembly.GetTypes())
            .Where(type => !type.IsAbstract && typeof(EntityEffect).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToHashSet();

        var missing = prototypes.EnumeratePrototypes<ReagentPropertyPrototype>()
            .Where(property => !property.Abstract && !property.GenerationDisabled &&
                property.Rarity is not ReagentPropertyRarity.Disabled and not ReagentPropertyRarity.Admin &&
                !string.IsNullOrEmpty(property.EffectName) && !effectNames.Contains(property.EffectName))
            .Select(property => $"{property.ID}:{property.EffectName}")
            .ToArray();

        Assert.That(missing, Is.Empty);
        await pair.CleanReturnAsync();
    }
}