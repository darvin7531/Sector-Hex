// This file is licensed under the MIT license.
// Adapted from SharedReagentGeneratorSystem by MACMAN2003 in RussianCM.

using System.Globalization;
using System.Linq;
using System.Text;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Upload;

namespace Content.Shared._Mono.Xenobiology.Chemistry;

public sealed partial class ProceduralReagentRegistrySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGamePrototypeLoadManager _prototypeLoader = default!;

    public readonly HashSet<string> GeneratedReagents = [];
    public readonly HashSet<string> GeneratedRecipes = [];

    public void Register(GeneratedReagentData data)
    {
        if (string.IsNullOrWhiteSpace(data.ID))
            throw new ArgumentException("A generated reagent requires an ID.", nameof(data));

        if (GeneratedReagents.Contains(data.ID) || _prototypes.HasIndex<ReagentPrototype>(data.ID))
            throw new InvalidOperationException($"Reagent '{data.ID}' is already registered.");

        foreach (var ingredient in data.Recipe.Keys)
        {
            if (!_prototypes.HasIndex<ReagentPrototype>(ingredient))
                throw new ArgumentException($"Unknown recipe reagent '{ingredient}'.", nameof(data));
        }

        var reagent = BuildReagentPrototype(data);
        var reaction = BuildReactionPrototype(data);
        _prototypeLoader.SendGamePrototype($"{reagent}\n{reaction}");

        GeneratedReagents.Add(data.ID);
        GeneratedRecipes.Add(data.ID);
    }

    private static string BuildReagentPrototype(GeneratedReagentData data)
    {
        return $"""
- type: reagent
  id: {data.ID}
  name: {data.Name}
  desc: mono-generated-reagent-desc
  physicalDesc: reagent-physical-desc-unidentifiable
  flavor: bitter
  color: "{data.Color.ToHexNoAlpha()}"
  group: Generated
  class: {data.Class}
  flags: Scannable
  overdose: {Format(data.Overdose)}
  criticalOverdose: {Format(data.CriticalOverdose)}
  genTier: {data.GenTier}
  generated: true
  reward: {data.ScanPointYield}

""";
    }

    private int GetReactionPriority(GeneratedReagentData data)
    {
        var priority = 0;
        foreach (var reaction in _prototypes.EnumeratePrototypes<ReactionPrototype>())
        {
            if (reaction.Reactants.Keys.All(data.Recipe.ContainsKey))
                priority = Math.Max(priority, reaction.Priority + 1);
        }

        return priority;
    }

    private string BuildReactionPrototype(GeneratedReagentData data)
    {
        var yaml = new StringBuilder();
        yaml.AppendLine("- type: reaction");
        yaml.AppendLine($"  id: {data.ID}");
        yaml.AppendLine($"  priority: {GetReactionPriority(data)}");
        yaml.AppendLine("  reactants:");

        foreach (var (ingredient, requirement) in data.Recipe)
        {
            yaml.AppendLine($"    {ingredient}:");
            yaml.AppendLine($"      amount: {requirement.Amount}");
            if (requirement.Catalyst)
                yaml.AppendLine("      catalyst: true");
        }

        yaml.AppendLine("  products:");
        yaml.AppendLine($"    {data.ID}: {Math.Max(1, data.RecipeYield)}");
        return yaml.ToString();
    }

    private static string Format(IFormattable value)
    {
        return value.ToString(null, CultureInfo.InvariantCulture);
    }
}
