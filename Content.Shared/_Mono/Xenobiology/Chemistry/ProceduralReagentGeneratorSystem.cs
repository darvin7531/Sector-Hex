// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using System.Linq;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Xenobiology.Chemistry;

public sealed partial class ProceduralReagentGeneratorSystem : EntitySystem
{
    private static readonly ProtoId<DatasetPrototype> ConflictsDataset = "MonoReagentConflictingProperties";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly List<(string First, string Second)> _conflicts = [];

    public void ReloadRules()
    {
        _conflicts.Clear();

        if (_prototypes.TryIndex(ConflictsDataset, out var conflicts))
        {
            foreach (var value in conflicts.Values)
            {
                var pair = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (pair.Length == 2)
                    _conflicts.Add((pair[0], pair[1]));
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
