using Content.Server._Mono.Xenobiology.Xeno;
using Content.Shared._Mono.Xenobiology.Xeno;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed class XenoLifecycleTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: MonoTestXenoEgg
  parent: MonoXenoEgg
  components:
  - type: XenoEgg
    placementDelay: 0
    growthDelay: 0
    openingDelay: 0

- type: entity
  id: MonoTestXenoHost
  components:
  - type: InfectableHost
    incubationDelay: 0
    larvaPrototype: MonoXenoLarva

- type: entity
  id: MonoTestXenoLarva
  parent: MonoXenoLarva
""";

    [Test]
    public async Task EggCompletesPlacedGrowthAndOpeningStates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        EntityUid egg = default;

        await server.WaitAssertion(() =>
        {
            egg = entMan.SpawnEntity("MonoTestXenoEgg", map.GridCoords);
            var component = entMan.GetComponent<XenoEggComponent>(egg);
            var appearance = server.System<SharedAppearanceSystem>();
            Assert.That(component.State, Is.EqualTo(XenoEggState.Item));
            Assert.That(appearance.TryGetData<XenoEggState>(egg, XenoEggVisuals.State, out var visual), Is.True);
            Assert.That(visual, Is.EqualTo(XenoEggState.Item));
            Assert.That(entMan.System<XenoLifecycleSystem>().TryPlace((egg, component)), Is.True);
            Assert.That(component.State, Is.EqualTo(XenoEggState.Item));
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var appearance = server.System<SharedAppearanceSystem>();
            Assert.That(entMan.GetComponent<XenoEggComponent>(egg).State, Is.EqualTo(XenoEggState.Growing));
            Assert.That(appearance.TryGetData<XenoEggState>(egg, XenoEggVisuals.State, out var visual), Is.True);
            Assert.That(visual, Is.EqualTo(XenoEggState.Growing));
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var component = entMan.GetComponent<XenoEggComponent>(egg);
            Assert.That(component.State, Is.EqualTo(XenoEggState.Grown));
            Assert.That(entMan.System<XenoLifecycleSystem>().TryOpen((egg, component)), Is.True);
            Assert.That(component.State, Is.EqualTo(XenoEggState.Opening));
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var component = entMan.GetComponent<XenoEggComponent>(egg);
            Assert.That(component.State, Is.EqualTo(XenoEggState.Opened));
            Assert.That(component.SpawnedParasite, Is.Not.Null);
            Assert.That(entMan.GetComponent<MetaDataComponent>(component.SpawnedParasite!.Value).EntityPrototype?.ID,
                Is.EqualTo("MonoXenoParasite"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InfectionSpawnsExactlyOneLarva()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        EntityUid host = default;
        EntityUid spawnedLarva = default;

        await server.WaitAssertion(() =>
        {
            host = entMan.SpawnEntity("MonoTestXenoHost", map.GridCoords);
            var parasite = entMan.SpawnEntity("MonoXenoParasite", map.GridCoords);
            var system = entMan.System<XenoLifecycleSystem>();
            Assert.That(system.TryInfect(parasite, host), Is.True);
            Assert.That(system.TryInfect(parasite, host), Is.False);
            Assert.That(entMan.HasComponent<XenoInfectionComponent>(host), Is.True);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var infection = entMan.GetComponent<XenoInfectionComponent>(host);
            Assert.That(infection.SpawnedLarva, Is.Not.Null);
            spawnedLarva = infection.SpawnedLarva!.Value;
        });

        await server.WaitRunTicks(5);
        await server.WaitAssertion(() =>
        {
            var infection = entMan.GetComponent<XenoInfectionComponent>(host);
            Assert.That(infection.SpawnedLarva, Is.EqualTo(spawnedLarva));

            var larvaCount = 0;
            var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == "MonoXenoLarva")
                    larvaCount++;
            }

            Assert.That(larvaCount, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ParasiteIsNonCombatAndConsumedOnInfection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var parasite = server.EntMan.SpawnEntity("MonoXenoParasite", map.GridCoords);
            var host = server.EntMan.SpawnEntity("MonoTestXenoHost", map.GridCoords);
            var lifecycle = server.EntMan.System<XenoLifecycleSystem>();

            Assert.That(server.EntMan.HasComponent<MeleeWeaponComponent>(parasite), Is.False);
            Assert.That(lifecycle.TryInfect(parasite, host), Is.True);
            Assert.That(server.EntMan.Deleted(parasite), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StandardHumanoidAndMonkeyAreValidHosts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            var monkey = server.EntMan.SpawnEntity("MobMonkey", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.HasComponent<InfectableHostComponent>(human), Is.True);
                Assert.That(server.EntMan.HasComponent<InfectableHostComponent>(monkey), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LarvaEvolvesIntoAdultXeno()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var larva = server.EntMan.SpawnEntity("MonoTestXenoLarva", map.GridCoords);
            var adult = server.System<XenoLifecycleSystem>().TryEvolveLarva(larva);

            Assert.That(adult, Is.Not.Null);
            Assert.That(server.EntMan.GetComponent<MetaDataComponent>(adult!.Value).EntityPrototype?.ID,
                Is.EqualTo("MobXenoDrone"));
        });

        await pair.CleanReturnAsync();
    }
}
