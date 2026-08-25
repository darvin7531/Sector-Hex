using System.Linq;
using Content.Server._Mono.Xenobiology.Research;
using Content.Shared._Mono.Xenobiology.Research;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology.Research;

[TestFixture]
[NonParallelizable]
[TestOf(typeof(ResearchDataTerminalSystem))]
public sealed class ResearchDataTerminalSystemTest
{
    [Test]
    public async Task ExactThirtyNineCreditsBuysEveryClearanceUpgrade()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var system = server.System<ResearchDataTerminalSystem>();

        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            for (var i = 0; i < 13; i++)
                Assert.That(system.TryCompleteResearch($"chemical-{i}", 3), Is.True);

            Assert.That(system.Credits, Is.EqualTo(39));
            Assert.That(system.UpgradeCost, Is.EqualTo(4));

            foreach (var (cost, clearance) in new[] { (4, 2), (7, 3), (10, 4), (13, 5), (5, 6) })
            {
                Assert.That(system.UpgradeCost, Is.EqualTo(cost));
                Assert.That(system.TryUpgradeClearance(), Is.True);
                Assert.That(system.Clearance, Is.EqualTo(clearance));
            }

            Assert.Multiple(() =>
            {
                Assert.That(system.Credits, Is.Zero);
                Assert.That(system.UpgradeCost, Is.Null);
                Assert.That(system.TryUpgradeClearance(), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InsufficientCreditsDoNotChangeProgress()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var system = server.System<ResearchDataTerminalSystem>();

        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(system.TryCompleteResearch("chemical", 3), Is.True);
            Assert.That(system.TryUpgradeClearance(), Is.False);

            Assert.Multiple(() =>
            {
                Assert.That(system.Credits, Is.EqualTo(3));
                Assert.That(system.Clearance, Is.EqualTo(1));
                Assert.That(system.UpgradeCost, Is.EqualTo(4));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DuplicateIdentificationOnlyRewardsTheFirstCompletion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var system = server.System<ResearchDataTerminalSystem>();

        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            Assert.Multiple(() =>
            {
                Assert.That(system.TryCompleteResearch("chemical", 7), Is.True);
                Assert.That(system.TryCompleteResearch("chemical", 7), Is.False);
                Assert.That(system.Credits, Is.EqualTo(7));
                Assert.That(system.IsResearchCompleted("chemical"), Is.True);
                Assert.That(system.CompletedResearchCount, Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundCleanupResetsCreditsClearanceAndCompletions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var system = server.System<ResearchDataTerminalSystem>();

        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(system.TryCompleteResearch("chemical", 7), Is.True);
            Assert.That(system.TryUpgradeClearance(), Is.True);

            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            Assert.Multiple(() =>
            {
                Assert.That(system.Credits, Is.Zero);
                Assert.That(system.Clearance, Is.EqualTo(1));
                Assert.That(system.UpgradeCost, Is.EqualTo(4));
                Assert.That(system.IsResearchCompleted("chemical"), Is.False);
                Assert.That(system.CompletedResearchCount, Is.Zero);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RefreshOffersSixContractsForThreeMinutes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var system = server.System<ResearchDataTerminalSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            system.RefreshContracts();

            Assert.Multiple(() =>
            {
                Assert.That(system.Contracts, Has.Count.EqualTo(6));
                Assert.That(system.Contracts.Select(contract => contract.ID), Is.Unique);
                Assert.That(system.NextRefresh, Is.EqualTo(timing.CurTime + TimeSpan.FromSeconds(180)));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SelectingContractRegistersItAndStartsSixMinuteCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var system = server.System<ResearchDataTerminalSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            system.RefreshContracts();
            var selected = system.Contracts[0];

            Assert.That(system.TrySelectContract(selected.ID, out var contract), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(contract.ID, Is.EqualTo(selected.ID));
                Assert.That(contract.Recipe, Is.Not.Empty);
                Assert.That(system.Contracts, Has.None.Matches<Content.Shared._Mono.Xenobiology.Chemistry.GeneratedReagentData>(entry => entry.ID == selected.ID));
                Assert.That(system.NextRefresh, Is.EqualTo(timing.CurTime + TimeSpan.FromSeconds(360)));
                Assert.That(system.TrySelectContract(system.Contracts[0].ID, out _), Is.False);
                Assert.That(system.TrySelectContract("missing", out _), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void BuiStateDerivesAvailableActions()
    {
        var ready = new ResearchDataTerminalBuiState([], [], TimeSpan.Zero, 7, 2, 7, false);
        var coolingDown = new ResearchDataTerminalBuiState([], [], TimeSpan.FromSeconds(1), 6, 2, 7, true);

        Assert.Multiple(() =>
        {
            Assert.That(ready.CanUpgrade, Is.True);
            Assert.That(ready.CanSelect, Is.True);
            Assert.That(coolingDown.CanUpgrade, Is.False);
            Assert.That(coolingDown.CanSelect, Is.False);
        });
    }
}
