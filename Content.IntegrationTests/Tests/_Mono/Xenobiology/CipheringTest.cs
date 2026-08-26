using Content.Shared._Mono.Xenobiology.Chemistry.Effects;
using Content.Shared._Mono.Xenobiology.Xeno;
using Content.Shared.EntityEffects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed class CipheringTest
{
    [TestCase(1, "MonoXenoLaboratoryPrime")]
    [TestCase(2, "MonoXenoLaboratoryCorrupted")]
    [TestCase(3, "MonoXenoLaboratoryAlpha")]
    [TestCase(4, "MonoXenoLaboratoryBravo")]
    [TestCase(5, "MonoXenoLaboratoryCharlie")]
    [TestCase(6, "MonoXenoLaboratoryDelta")]
    public async Task PotencyAssignsFutureLarvaFaction(int potency, string expectedFaction)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;

        await pair.Server.WaitAssertion(() =>
        {
            var host = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var infection = entities.AddComponent<XenoInfectionComponent>(host);

            new CipheringEffect { Potency = potency }.Effect(new EntityEffectBaseArgs(host, entities));

            Assert.That(infection.LarvaFaction.Id, Is.EqualTo(expectedFaction));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NonInfectedTargetIsUnchanged()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;

        await pair.Server.WaitAssertion(() =>
        {
            var target = entities.SpawnEntity(null, MapCoordinates.Nullspace);

            new CipheringEffect { Potency = 6 }.Effect(new EntityEffectBaseArgs(target, entities));

            Assert.That(entities.HasComponent<XenoInfectionComponent>(target), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}