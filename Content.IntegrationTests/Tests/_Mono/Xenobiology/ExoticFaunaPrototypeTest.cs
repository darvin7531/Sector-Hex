using System.Linq;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed class ExoticFaunaPrototypeTest
{
    [TestCase("MonoYirenCube", "MobMonkey")]
    [TestCase("MonoFarwaCube", "MobCat")]
    [TestCase("MonoStokCube", "MobLizard")]
    [TestCase("MonoNeaeraCube", "MobParrot")]
    public async Task ExoticCubeUsesExpectedMonolithFauna(string cubePrototype, string mobPrototype)
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var cube = pair.Server.EntMan.SpawnEntity(cubePrototype, MapCoordinates.Nullspace);
            var rehydratable = pair.Server.EntMan.GetComponent<RehydratableComponent>(cube);
            Assert.That(rehydratable.PossibleSpawns.Single().Id, Is.EqualTo(mobPrototype));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExoticFaunaCratePrototypeLoads()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        Assert.That(prototypes.HasIndex<EntityPrototype>("MonoExoticFaunaCrate"), Is.True);
        await pair.CleanReturnAsync();
    }
}
