// THIS FILE IS LICENSED UNDER THE MIT LICENSE.
// Adapted from Ciphering by MACMAN2003 in RussianCM commit 0540a61b873bb3e08a40ba75404a1eb2fb21da27.

using Content.Shared._Mono.Xenobiology.Xeno;
using Content.Shared.EntityEffects;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Xenobiology.Chemistry.Effects;

public sealed partial class CipheringEffect : EntityEffect
{
    private static readonly ProtoId<NpcFactionPrototype>[] Factions =
    [
        "MonoXenoLaboratoryPrime",
        "MonoXenoLaboratoryCorrupted",
        "MonoXenoLaboratoryAlpha",
        "MonoXenoLaboratoryBravo",
        "MonoXenoLaboratoryCharlie",
        "MonoXenoLaboratoryDelta",
    ];

    [DataField]
    public float Potency = 1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent<XenoInfectionComponent>(args.TargetEntity, out var infection))
            return;

        var index = Math.Clamp((int) MathF.Round(Potency), 1, Factions.Length) - 1;
        infection.LarvaFaction = Factions[index];
        args.EntityManager.Dirty(args.TargetEntity, infection);
    }
}
