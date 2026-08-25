using System.Collections.Generic;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared._Mono.Xenobiology.Simulator;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed class SynthesisSimulatorTest
{
    [Test]
    public async Task AmplifyRaisesPropertyAndAppliesOverdosePenalty()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var simulator = pair.Server.System<SynthesisSimulatorSystem>();
            var target = Reagent("MonoSimulatorAmplifyTarget", ("MonoTestToxic", 2));
            target.Overdose = 20;
            var result = simulator.Simulate(new SynthesisSimulationRequest(
                target,
                SynthesisSimulatorMode.Amplify,
                targetProperty: "MonoTestToxic"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Effects["MonoTestToxic"], Is.EqualTo(3));
                Assert.That(result.Overdose, Is.EqualTo((FixedPoint2) 15));
                Assert.That(result.CriticalOverdose, Is.EqualTo((FixedPoint2) 30));
                Assert.That(result.OriginalID, Is.EqualTo(target.ID));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SuppressLowersPropertyAndAppliesLowOverdosePenalty()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var simulator = pair.Server.System<SynthesisSimulatorSystem>();
            var target = Reagent("MonoSimulatorSuppressTarget", ("MonoTestToxic", 2));
            target.Overdose = 5;
            var result = simulator.Simulate(new SynthesisSimulationRequest(
                target,
                SynthesisSimulatorMode.Suppress,
                targetProperty: "MonoTestToxic"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Effects["MonoTestToxic"], Is.EqualTo(1));
                Assert.That(result.Overdose, Is.EqualTo((FixedPoint2) 4));
                Assert.That(result.CriticalOverdose, Is.EqualTo((FixedPoint2) 10));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RelateReplacesEqualLevelProperty()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var simulator = pair.Server.System<SynthesisSimulatorSystem>();
            var target = Reagent("MonoSimulatorRelateTarget", ("MonoTestNeutral", 2), ("MonoTestToxic", 1));
            var reference = Reagent("MonoSimulatorRelateReference", ("MonoTestCardiopeutic", 2));
            var result = simulator.Simulate(new SynthesisSimulationRequest(
                target,
                SynthesisSimulatorMode.Relate,
                reference,
                "MonoTestNeutral",
                "MonoTestCardiopeutic"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Effects, Does.Not.ContainKey("MonoTestNeutral"));
                Assert.That(result.Effects["MonoTestCardiopeutic"], Is.EqualTo(2));
                Assert.That(result.Effects["MonoTestToxic"], Is.EqualTo(1));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AddCopiesPropertyAndLocksReferenceLineage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var simulator = pair.Server.System<SynthesisSimulatorSystem>();
            var registry = pair.Server.System<ProceduralReagentRegistrySystem>();
            var target = Reagent("MonoSimulatorAddTarget", ("MonoTestNeutral", 1), ("MonoTestToxic", 1));
            var referenceRoot = Reagent("MonoSimulatorReferenceRoot", ("MonoTestCardiopeutic", 2));
            var referenceChild = Reagent("MonoSimulatorReferenceChild", ("MonoTestCardiopeutic", 2));
            referenceChild.OriginalID = referenceRoot.ID;
            registry.Track(referenceRoot);
            registry.Track(referenceChild);
            var result = simulator.Simulate(new SynthesisSimulationRequest(
                target,
                SynthesisSimulatorMode.Add,
                referenceChild,
                referenceProperty: "MonoTestCardiopeutic"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Effects["MonoTestCardiopeutic"], Is.EqualTo(2));
                Assert.That(result.Overdose, Is.EqualTo(target.Overdose));
                Assert.That(registry.IsLockedDown(referenceRoot.ID), Is.True);
                Assert.That(registry.IsLockedDown(referenceChild.ID), Is.True);
                Assert.That(registry.IsLockedDown(target.ID), Is.False);
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OverrideAllowsRelateConflictSubtraction()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var simulator = pair.Server.System<SynthesisSimulatorSystem>();
            var target = Reagent("MonoSimulatorOverrideTarget", ("MonoTestNeutral", 1), ("MonoTestToxic", 3));
            var reference = Reagent("MonoSimulatorOverrideReference", ("MonoTestAntitoxic", 1));
            var protectedRequest = new SynthesisSimulationRequest(
                target,
                SynthesisSimulatorMode.Relate,
                reference,
                "MonoTestNeutral",
                "MonoTestAntitoxic");

            Assert.Throws<InvalidOperationException>(() => simulator.Simulate(protectedRequest));
            var result = simulator.Simulate(protectedRequest with { OverrideConflicts = true });
            Assert.Multiple(() =>
            {
                Assert.That(result.Effects["MonoTestToxic"], Is.EqualTo(2));
                Assert.That(result.Effects, Does.Not.ContainKey("MonoTestAntitoxic"));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SimulatorPrototypeProvidesComponentApi()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entMan = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid uid = default;

        await pair.Server.WaitAssertion(() =>
        {
            uid = entMan.SpawnEntity("MonoSynthesisSimulator", MapCoordinates.Nullspace);
            Assert.That(entMan.HasComponent<SynthesisSimulatorComponent>(uid), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    private static GeneratedReagentData Reagent(string id, params (string Property, int Level)[] effects)
    {
        var data = new GeneratedReagentData
        {
            ID = id,
            Name = id,
            Recipe = new Dictionary<string, (int Amount, bool Catalyst)> { ["Water"] = (1, false) },
        };
        foreach (var (property, level) in effects)
            data.Effects[property] = level;
        return data;
    }
}
