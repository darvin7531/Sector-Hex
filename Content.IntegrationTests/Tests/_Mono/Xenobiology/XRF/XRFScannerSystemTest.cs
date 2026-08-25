using Content.IntegrationTests.Pair;
using Content.Server._Mono.Xenobiology.Research;
using Content.Server._Mono.Xenobiology.XRF;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared._Mono.Xenobiology.XRF;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology.XRF;

[TestFixture]
[NonParallelizable]
[TestOf(typeof(XRFScannerSystem))]
public sealed class XRFScannerSystemTest
{
    private const string Reagent = "MonoXRFTestReagent";
    private const string Contaminant = "MonoXRFTestContaminant";

    [TestPrototypes]
    private const string Prototypes = """
- type: reagent
  id: MonoXRFTestReagent
  name: mono-xrf-test-reagent
  group: Medicine
  desc: mono-xrf-test-reagent-desc
  physicalDesc: reagent-physical-desc-opaque
  flavor: bitter
  color: "#123456"
  class: Uncommon
  flags: Scannable
  generated: true
  reward: 5

- type: reagent
  id: MonoXRFTestContaminant
  name: mono-xrf-test-contaminant
  group: Medicine
  desc: mono-xrf-test-contaminant-desc
  physicalDesc: reagent-physical-desc-opaque
  flavor: bitter
  color: "#654321"

- type: entity
  id: MonoXRFTestScanner
  components:
  - type: XRFScanner

- type: entity
  id: MonoXRFTestVial
  components:
  - type: Tag
    tags:
    - CentrifugeCompatible
  - type: SolutionContainerManager
    solutions:
      beaker:
        maxVol: 60
""";

    [Test]
    public async Task MissingVialReportsMissingSample()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var scannerSystem = server.System<XRFScannerSystem>();
        EntityUid scanner = default;

        await server.WaitAssertion(() =>
        {
            scanner = server.EntMan.SpawnEntity("MonoXRFTestScanner", map.GridCoords);
            Assert.That(scannerSystem.FinishScan(scanner).Status, Is.EqualTo(XRFScanStatus.Missing));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LessThanThirtyUnitsReportsInsufficientSample()
    {
        await using var pair = await PoolManager.GetServerClient();
        var (scanner, vial) = await SpawnScannerAndVial(pair);
        await Fill(pair, vial, (Reagent, 29));

        await pair.Server.WaitAssertion(() =>
        {
            Insert(pair, scanner, vial);
            var component = pair.Server.EntMan.GetComponent<XRFScannerComponent>(scanner);
            Assert.That(component.NextScan - pair.Server.ResolveDependency<IGameTiming>().CurTime,
                Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(pair.Server.System<XRFScannerSystem>().FinishScan(scanner).Status,
                Is.EqualTo(XRFScanStatus.Insufficient));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultipleReagentsReportContaminatedSample()
    {
        await using var pair = await PoolManager.GetServerClient();
        var (scanner, vial) = await SpawnScannerAndVial(pair);
        await Fill(pair, vial, (Reagent, 30), (Contaminant, 1));

        await pair.Server.WaitAssertion(() =>
        {
            Insert(pair, scanner, vial);
            Assert.That(pair.Server.System<XRFScannerSystem>().FinishScan(scanner).Status,
                Is.EqualTo(XRFScanStatus.Contaminated));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PureThirtyUnitSampleReportsDataAndRewardsResearch()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (scanner, vial) = await SpawnScannerAndVial(pair);
        await Fill(pair, vial, (Reagent, 30));

        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Insert(pair, scanner, vial);
            var report = server.System<XRFScannerSystem>().FinishScan(scanner);

            Assert.Multiple(() =>
            {
                Assert.That(report.Status, Is.EqualTo(XRFScanStatus.Valid));
                Assert.That(report.ReagentId, Is.EqualTo(Reagent));
                Assert.That(report.Name, Is.EqualTo("mono-xrf-test-reagent"));
                Assert.That(report.Class, Is.EqualTo(ProceduralReagentClass.Uncommon));
                Assert.That(report.Reward, Is.EqualTo(5));
                Assert.That(report.RewardGranted, Is.True);
                Assert.That(server.System<ResearchDataTerminalSystem>().Credits, Is.EqualTo(5));
                Assert.That(server.System<ResearchDataTerminalSystem>().KnownScans, Has.Count.EqualTo(1));
                Assert.That(server.System<ResearchDataTerminalSystem>().KnownScans[0].Name,
                    Is.EqualTo("mono-xrf-test-reagent"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DuplicateScanReportsDataWithoutRewardingAgain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (scanner, vial) = await SpawnScannerAndVial(pair);
        await Fill(pair, vial, (Reagent, 30));

        await server.WaitAssertion(() =>
        {
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Insert(pair, scanner, vial);
            Assert.That(server.System<XRFScannerSystem>().FinishScan(scanner).RewardGranted, Is.True);
            Assert.That(server.System<XRFScannerSystem>().TryStartScan(scanner), Is.True);
            var duplicate = server.System<XRFScannerSystem>().FinishScan(scanner);

            Assert.Multiple(() =>
            {
                Assert.That(duplicate.Status, Is.EqualTo(XRFScanStatus.Valid));
                Assert.That(duplicate.ReagentId, Is.EqualTo(Reagent));
                Assert.That(duplicate.RewardGranted, Is.False);
                Assert.That(server.System<ResearchDataTerminalSystem>().Credits, Is.EqualTo(5));
                Assert.That(server.System<ResearchDataTerminalSystem>().CompletedResearchCount, Is.EqualTo(1));
                Assert.That(server.System<ResearchDataTerminalSystem>().KnownScans, Has.Count.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static async Task<(EntityUid Scanner, EntityUid Vial)> SpawnScannerAndVial(TestPair pair)
    {
        var map = await pair.CreateTestMap();
        EntityUid scanner = default;
        EntityUid vial = default;
        await pair.Server.WaitAssertion(() =>
        {
            scanner = pair.Server.EntMan.SpawnEntity("MonoXRFTestScanner", map.GridCoords);
            vial = pair.Server.EntMan.SpawnEntity("MonoXRFTestVial", map.GridCoords);
        });
        return (scanner, vial);
    }

    private static async Task Fill(TestPair pair, EntityUid vial, params (string Id, int Amount)[] reagents)
    {
        await pair.Server.WaitAssertion(() =>
        {
            var solutions = pair.Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(vial, "beaker", out var solution));
            foreach (var reagent in reagents)
                Assert.That(solutions.TryAddReagent(solution.Value, new ReagentId(reagent.Id, null), reagent.Amount, out _));
        });
    }

    private static void Insert(TestPair pair, EntityUid scanner, EntityUid vial)
    {
        Assert.That(pair.Server.System<ItemSlotsSystem>()
            .TryInsert(scanner, XRFScannerComponent.SampleSlotId, vial, null), Is.True);
    }
}
