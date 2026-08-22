// Adapted from RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.
// Licensed under AGPL-3.0 under the RussianCM repository-wide license.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Xenobiology.Chemistry;

[Serializable, NetSerializable]
public struct GeneratedReagentData
{
    public string ID;
    public string Name;
    public Dictionary<string, int> Effects;
    public Dictionary<string, (int Amount, bool Catalyst)> Recipe;
    public int RecipeYield;
    public int ScanPointYield;
    public Color Color;
    public FixedPoint2 Overdose;
    public FixedPoint2 CriticalOverdose;
    public FixedPoint2 MetabolismRate;
    public int GenTier;
    public string RecipeHint;
    public string PropertyHint;
    public string OriginalID;
    public HashSet<string> ModifiedChems;
    public ProceduralReagentClass Class;

    public GeneratedReagentData()
    {
        ID = string.Empty;
        Name = string.Empty;
        Effects = [];
        Recipe = [];
        RecipeHint = string.Empty;
        RecipeYield = 1;
        ScanPointYield = 2;
        Color = Color.Black;
        Overdose = 30;
        CriticalOverdose = 50;
        MetabolismRate = 0.1;
        GenTier = 1;
        Class = ProceduralReagentClass.None;
        PropertyHint = string.Empty;
        OriginalID = string.Empty;
        ModifiedChems = [];
    }
}

[Serializable, NetSerializable]
public struct ResearchReportData
{
    public string Name;
    public string Info;
    public bool Completed;
    public bool Valid;
    public ResearchReportIcon Icon;
}

[Serializable, NetSerializable]
public enum ResearchReportIcon
{
    None,
    Full,
    Partial,
    Synthesis,
}
