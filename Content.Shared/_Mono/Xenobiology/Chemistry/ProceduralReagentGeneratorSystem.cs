// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using System.Linq;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Mono.Xenobiology.Chemistry;

public sealed partial class ProceduralReagentGeneratorSystem : EntitySystem
{
    private static readonly ProtoId<DatasetPrototype> ConflictsDataset = "MonoReagentConflictingProperties";
    private static readonly ProtoId<DatasetPrototype> CombinationsDataset = "MonoReagentCombiningProperties";
    private static readonly ProtoId<DatasetPrototype> NamePrefixesDataset = "MonoRandChemPrefix";
    private static readonly ProtoId<DatasetPrototype> NameMiddlesDataset = "MonoRandChemWordroot";
    private static readonly ProtoId<DatasetPrototype> NameSuffixesDataset = "MonoRandChemSuffix";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly List<(string First, string Second)> _conflicts = [];
    private readonly Dictionary<string, HashSet<string>> _combinations = [];
    public IReadOnlyDictionary<string, HashSet<string>> Combinations => _combinations;
    public Dictionary<string, HashSet<string>> PropertyPools { get; } = [];
    public Dictionary<string, HashSet<string>> GeneratedPropertyPools { get; } = [];
    public Dictionary<string, HashSet<string>> ReagentClassPools { get; } = [];

    public void ReloadRules(
        ProtoId<DatasetPrototype>? conflictsDataset = null,
        ProtoId<DatasetPrototype>? combinationsDataset = null)
    {
        conflictsDataset ??= ConflictsDataset;
        combinationsDataset ??= CombinationsDataset;
        _conflicts.Clear();
        _combinations.Clear();

        if (_prototypes.TryIndex(conflictsDataset.Value, out var conflicts))
        {
            foreach (var value in conflicts.Values)
            {
                var pair = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (pair.Length == 2)
                    _conflicts.Add((pair[0], pair[1]));
            }
        }

        if (_prototypes.TryIndex(combinationsDataset.Value, out var combinations))
        {
            foreach (var value in combinations.Values)
            {
                var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                    _combinations[parts[0]] = parts[1..].ToHashSet();
            }
        }
    }

    public void PreparePools()
    {
        ReloadRules();
        PropertyPools.Clear();
        GeneratedPropertyPools.Clear();
        ReagentClassPools.Clear();

        foreach (var pool in new[] { "negative", "neutral", "positive", "rare" })
            PropertyPools[pool] = [];
        foreach (var pool in new[] { "negative", "neutral", "positive" })
            GeneratedPropertyPools[pool] = [];
        foreach (var pool in new[] { "C", "C1", "C2", "C3", "C4", "C5", "C6", "H1", "TAU" })
            ReagentClassPools[pool] = [];

        foreach (var property in _prototypes.EnumeratePrototypes<ReagentPropertyPrototype>())
        {
            if (property.GenerationDisabled || property.Rarity == ReagentPropertyRarity.Disabled)
                continue;

            if (property.Rarity == ReagentPropertyRarity.Rare)
                PropertyPools["rare"].Add(property.ID);
            else if (property.Hint == ReagentPropertyHint.Negative)
                PropertyPools["negative"].Add(property.ID);
            else if (property.Hint == ReagentPropertyHint.Neutral)
                PropertyPools["neutral"].Add(property.ID);
            else if (property.Hint == ReagentPropertyHint.Positive)
                PropertyPools["positive"].Add(property.ID);
        }

        foreach (var property in _prototypes.EnumeratePrototypes<ReagentPropertyPrototype>())
        {
            if (property.Hint != ReagentPropertyHint.Legendary ||
                property.Rarity != ReagentPropertyRarity.Legendary ||
                property.Category.HasFlag(ReagentPropertyType.Anomalous) ||
                _combinations.ContainsKey(property.ID) ||
                PropertyPools["negative"].Count == 0 ||
                PropertyPools["neutral"].Count == 0 ||
                PropertyPools["positive"].Count == 0)
                continue;

            HashSet<string> ingredients =
            [
                _random.Pick(PropertyPools[_random.Pick(new[] { "neutral", "positive", "negative" })]),
                _random.Pick(PropertyPools[_random.Pick(new[] { "neutral", "positive", "negative" })]),
                _random.Pick(PropertyPools[_random.Pick(new[] { "neutral", "positive", "negative" })]),
            ];
            if (property.ID == "Ciphering")
            {
                ingredients.Remove(ingredients.Last());
                ingredients.Add("Encrypted");
            }
            _combinations.TryAdd(property.ID, ingredients);
        }

        foreach (var reagent in _prototypes.EnumeratePrototypes<ReagentPrototype>())
        {
            if (reagent.Flags.HasFlag(ProceduralReagentFlag.NoGeneration))
                continue;

            var pool = reagent.Class switch
            {
                ProceduralReagentClass.Basic => "C1",
                ProceduralReagentClass.Common => "C2",
                ProceduralReagentClass.Uncommon => "C3",
                ProceduralReagentClass.Rare => "C4",
                ProceduralReagentClass.Special => "C5",
                ProceduralReagentClass.Ultra => "C6",
                ProceduralReagentClass.Hydro => "H1",
                _ => null,
            };
            if (pool == null)
                continue;

            ReagentClassPools[pool].Add(reagent.ID);
            ReagentClassPools["C"].Add(reagent.ID);
        }
    }

    public void GenerateName(ref GeneratedReagentData data)
    {
        if (!_prototypes.TryIndex(NamePrefixesDataset, out var prefixes) ||
            !_prototypes.TryIndex(NameMiddlesDataset, out var middles) ||
            !_prototypes.TryIndex(NameSuffixesDataset, out var suffixes))
            return;

        string name;
        do
        {
            name = _random.Pick(prefixes.Values) +
                _random.Pick(middles.Values) +
                _random.Pick(suffixes.Values);
        } while (_prototypes.HasIndex<ReagentPrototype>(name));

        var sequence = ReagentClassPools.TryGetValue("TAU", out var generated)
            ? generated.Count.ToString()
            : "ERROR";
        data.ID = $"TAU-{sequence}-{name}";
        data.Name = name;
    }

    public bool GenerateStats(ref GeneratedReagentData data, bool noProperties = false)
    {
        if (PropertyPools.Count == 0 || ReagentClassPools.Count == 0)
            PreparePools();

        if (!noProperties)
            GenerateProperties(ref data);

        data.Overdose = 5;
        var overdoseMult = 2;
        if (data.GenTier == 1)
            overdoseMult = _random.Next(data.GenTier, overdoseMult + 1);
        if (data.GenTier == 2)
            overdoseMult = _random.Next(data.GenTier + 2, 7);
        else if (data.GenTier >= 3)
            overdoseMult = _random.Next(data.GenTier + 3, 10);

        for (var i = 1; i <= overdoseMult; i++)
            data.Overdose += 5;

        data.CriticalOverdose = data.Overdose + 5;
        for (var i = 1; i <= _random.Next(1, 4); i++)
        {
            if (_random.Prob((20 + 2 * data.GenTier) / 100))
                data.CriticalOverdose += 5;
        }

        var red = (byte) _random.Next(0, 256);
        var green = (byte) _random.Next(0, 256);
        var blue = (byte) _random.Next(0, 256);
        data.Color = Color.FromHex($"#{red:x2}{green:x2}{blue:x2}");
        return true;
    }

    private void GenerateProperties(ref GeneratedReagentData data)
    {
        var generatedValue = 0;
        var propertiesBuff = _random.Next(3, 5);
        if (data.GenTier == 2)
            propertiesBuff -= 2;
        var specificProperty = "none";

        for (var i = 1; i <= data.GenTier + propertiesBuff; i++)
        {
            if (i == 1)
            {
                if (data.GenTier > 2)
                    generatedValue = AddProperty(ref data, typeToAdd: "rare");
                else if (data.GenTier > 1 && _random.Prob(20 / 100))
                {
                    generatedValue = AddProperty(ref data, typeToAdd: "rare", track: true);
                    specificProperty = "negative";
                }
                else
                    generatedValue = AddProperty(ref data, track: true);
            }
            else if (generatedValue == data.GenTier * 2 + 2)
                break;
            else if (data.GenTier < 3)
                generatedValue += AddProperty(ref data,
                    valueOffset: data.GenTier - generatedValue - 1,
                    typeToAdd: specificProperty,
                    track: true);
            else
                generatedValue += AddProperty(ref data,
                    valueOffset: data.GenTier - generatedValue - 1,
                    typeToAdd: specificProperty);
        }

        while (data.Effects.Count < data.GenTier + 1)
            AddProperty(ref data);
    }

    private int AddProperty(ref GeneratedReagentData data,
        string? property = null,
        int? propertyLevel = null,
        int valueOffset = 0,
        string typeToAdd = "none",
        bool track = false,
        int depth = 0)
    {
        if (depth > 5)
            return 0;

        var level = propertyLevel ?? (_random.Next(0, 101) switch
        {
            <= 20 => 1,
            <= 40 => 2,
            <= 60 => 3,
            <= 75 => 4,
            <= 80 => 5,
            <= 90 => 6,
            <= 95 => 7,
            _ => 8,
        });
        level = Math.Min(level, data.GenTier + 3);

        if (property != null)
            return InsertProperty(ref data, property, level) ? 1 : 0;

        var roll = _random.Next(1, 101);
        string pool;
        if (typeToAdd != "none")
            pool = typeToAdd;
        else if (valueOffset > 0)
            pool = "positive";
        else if (valueOffset < 0)
            pool = roll <= data.GenTier * 10 ? "negative" : "neutral";
        else
        {
            pool = data.GenTier switch
            {
                1 => roll <= 40 ? "negative" : roll <= 50 ? "neutral" : "positive",
                2 => roll <= 35 ? "negative" : roll <= 45 ? "neutral" : "positive",
                3 => roll <= 15 ? "negative" : roll <= 25 ? "neutral" : "positive",
                _ => roll <= 10 ? "negative" : roll <= 15 ? "neutral" : "positive",
            };
        }

        property = _random.Pick(PropertyPools[pool]);
        if (track)
        {
            var checks = 0;
            while (!CheckGeneratedProperty(property) && checks < 4)
            {
                property = _random.Pick(PropertyPools[pool]);
                checks++;
            }
        }

        var prototype = _prototypes.Index<ReagentPropertyPrototype>(property);
        if (prototype.GenerationDisabled || prototype.Rarity is ReagentPropertyRarity.Disabled or ReagentPropertyRarity.Admin)
            return AddProperty(ref data, valueOffset: valueOffset, typeToAdd: typeToAdd, track: track, depth: depth + 1);

        level = Math.Min(level, prototype.MaxLevel);
        InsertProperty(ref data, property, level);
        return prototype.Hint switch
        {
            ReagentPropertyHint.Negative => -level,
            ReagentPropertyHint.Neutral => (int)Math.Floor(-level / 2f),
            _ => level,
        };
    }

    public bool CheckGeneratedProperty(string property)
    {
        foreach (var pool in new[] { "positive", "negative", "neutral" })
        {
            if (!PropertyPools[pool].Contains(property))
                continue;
            if (GeneratedPropertyPools[pool].Contains(property) &&
                GeneratedPropertyPools[pool].Count < PropertyPools[pool].Count)
                return false;
            GeneratedPropertyPools[pool].Add(property);
            break;
        }
        return true;
    }

    public bool GenerateRecipe(ref GeneratedReagentData data, HashSet<string> requiredReagents)
    {
        var modifier = _random.Next(0, 101) switch
        {
            <= 60 => 1,
            <= 75 => 2,
            <= 85 => 3,
            <= 92 => 4,
            <= 97 => 5,
            _ => 6,
        };
        var desiredChems = _random.Next(3, Math.Max(Math.Min(data.GenTier * 2, 4), 3) + 1);
        var failedAttempts = 0;

        while (true)
        {
            var required = requiredReagents.ToList();
            for (var i = 1; i <= desiredChems; i++)
            {
                if (required.Count > 0)
                {
                    foreach (var reagent in required)
                        AddChemical(ref data, reagent, i == 1 ? modifier : 1);
                    required.Clear();
                }
                else
                    AddChemical(ref data, modifier: i == 1 ? modifier : 1);
            }

            if (!IsDuplicateRecipe(data) && !IsAllMedicine(data))
                break;

            data.Recipe.Clear();
            if (failedAttempts++ > 10)
                return false;
        }

        if (_random.Prob(0.2f) && data.GenTier >= 2)
            AddChemical(ref data, modifier: 5, catalyst: true);
        return true;
    }

    private bool IsDuplicateRecipe(GeneratedReagentData data)
    {
        foreach (var reaction in _prototypes.EnumeratePrototypes<ReactionPrototype>())
        {
            if (reaction.Reactants.Keys.All(data.Recipe.ContainsKey))
                return true;
        }
        return false;
    }

    private bool IsAllMedicine(GeneratedReagentData data)
    {
        return data.Recipe.Keys.All(id =>
            _prototypes.Index<ReagentPrototype>(id).Flags.HasFlag(ProceduralReagentFlag.Medical));
    }

    public string AddChemical(ref GeneratedReagentData data,
        string chem = "",
        int modifier = 1,
        int? tier = null,
        bool catalyst = false,
        string cClass = "")
    {
        var amount = modifier == 0 ? 1 : modifier;
        var useTier = tier ?? data.GenTier;
        string selected;

        while (true)
        {
            selected = chem != ""
                ? chem
                : cClass != ""
                    ? _random.Pick(ReagentClassPools["C" + cClass])
                    : PickChemicalForTier(useTier, data.Recipe.Count == 0 || catalyst);

            if (!data.Recipe.ContainsKey(selected))
                break;
            if (chem != "")
                return bool.FalseString;
        }

        data.Recipe.Add(selected, (amount, catalyst));
        return selected;
    }

    private string PickChemicalForTier(int tier, bool firstOrCatalyst)
    {
        var roll = _random.Next(0, 101);
        var pool = tier switch
        {
            0 => "C",
            1 => roll <= 60 ? "C1" : roll <= 80 ? "C2" : "C1",
            2 => roll <= 50 ? "C2" : roll <= 75 ? "C3" : "C4",
            3 => roll <= 80 ? _random.Pick(new[] { "C1", "C2" }) : "H1",
            _ when firstOrCatalyst => _random.Prob(0.5f) ? "C5" : "C4",
            _ => roll <= 25 ? "C2" : roll <= 45 ? "C3" : roll <= 65 ? "C4" : "C5",
        };
        return _random.Pick(ReagentClassPools[pool]);
    }

    public bool InsertProperty(ref GeneratedReagentData data, string property, int level)
    {
        if (!_prototypes.TryIndex<ReagentPropertyPrototype>(property, out var incoming))
            throw new ArgumentException($"Unknown reagent property '{property}'.", nameof(property));

        if (level <= 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Property level must be positive.");

        if (incoming.GenerationDisabled)
            return false;

        var propertyToAdd = property;
        var levelToAdd = level;
        var effects = data.Effects;

        foreach (var (result, ingredients) in _combinations)
        {
            if (!ingredients.Contains(property) ||
                _prototypes.Index<ReagentPropertyPrototype>(result).GenerationDisabled)
                continue;

            var existingIngredients = ingredients
                .Where(id => id != property && effects.ContainsKey(id))
                .ToList();
            if (existingIngredients.Count != ingredients.Count - 1)
                continue;

            propertyToAdd = result;
            foreach (var existing in existingIngredients)
            {
                levelToAdd = Math.Max(Math.Abs(levelToAdd - effects[existing]), 1);
                if (!_prototypes.Index<ReagentPropertyPrototype>(existing).Category.HasFlag(ReagentPropertyType.Catalyst))
                {
                    effects[existing] -= levelToAdd;
                    if (effects[existing] <= 0)
                        effects.Remove(existing);
                }
            }
            break;
        }

        var conflict = _conflicts.FirstOrDefault(pair =>
            pair.First == propertyToAdd && effects.ContainsKey(pair.Second) ||
            pair.Second == propertyToAdd && effects.ContainsKey(pair.First));
        var existingConflict = conflict.First == propertyToAdd ? conflict.Second : conflict.First;

        if (!string.IsNullOrEmpty(existingConflict) && effects.TryGetValue(existingConflict, out var existingLevel))
        {
            if (existingLevel > levelToAdd)
            {
                effects[existingConflict] -= levelToAdd;
                return false;
            }

            effects.Remove(existingConflict);
            if (existingLevel == levelToAdd)
                return false;

            levelToAdd -= existingLevel;
        }

        var resultProperty = _prototypes.Index<ReagentPropertyPrototype>(propertyToAdd);
        levelToAdd = Math.Min(resultProperty.MaxLevel, levelToAdd);
        effects.TryAdd(propertyToAdd, levelToAdd);
        return true;
    }

    public bool HasConflict(GeneratedReagentData data, string property)
    {
        ReloadRules();
        return _conflicts.Any(pair =>
            pair.First == property && data.Effects.ContainsKey(pair.Second) ||
            pair.Second == property && data.Effects.ContainsKey(pair.First));
    }
}
