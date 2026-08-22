// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using System.Linq;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Xenobiology.Chemistry;

public sealed partial class ProceduralReagentGeneratorSystem : EntitySystem
{
    private static readonly ProtoId<DatasetPrototype> ConflictsDataset = "MonoReagentConflictingProperties";
    private static readonly ProtoId<DatasetPrototype> CombinationsDataset = "MonoReagentCombiningProperties";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly List<(string First, string Second)> _conflicts = [];
    private readonly Dictionary<string, HashSet<string>> _combinations = [];

    public void ReloadRules()
    {
        _conflicts.Clear();
        _combinations.Clear();

        if (_prototypes.TryIndex(ConflictsDataset, out var conflicts))
        {
            foreach (var value in conflicts.Values)
            {
                var pair = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (pair.Length == 2)
                    _conflicts.Add((pair[0], pair[1]));
            }
        }

        if (_prototypes.TryIndex(CombinationsDataset, out var combinations))
        {
            foreach (var value in combinations.Values)
            {
                var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                    _combinations[parts[0]] = parts[1..].ToHashSet();
            }
        }
    }

    public bool InsertProperty(ref GeneratedReagentData data, string property, int level)
    {
        if (!_prototypes.TryIndex<ReagentPropertyPrototype>(property, out var incoming))
            throw new ArgumentException($"Unknown reagent property '{property}'.", nameof(property));

        if (level <= 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Property level must be positive.");

        var propertyToAdd = property;
        var levelToAdd = level;
        var effects = data.Effects;

        foreach (var (result, ingredients) in _combinations)
        {
            if (!ingredients.Contains(property))
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
}
