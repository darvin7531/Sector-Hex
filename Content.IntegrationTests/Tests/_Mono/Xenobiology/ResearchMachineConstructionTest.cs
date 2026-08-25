using Content.Server.Construction.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mono.Xenobiology;

[TestFixture]
[NonParallelizable]
public sealed class ResearchMachineConstructionTest
{
    [Test]
    public async Task ResearchMachinesReferenceConstructibleBoards()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var terminal = pair.Server.EntMan.SpawnEntity("MonoResearchDataTerminal", MapCoordinates.Nullspace);
            var xrf = pair.Server.EntMan.SpawnEntity("MonoXRFScanner", MapCoordinates.Nullspace);
            var simulator = pair.Server.EntMan.SpawnEntity("MonoSynthesisSimulator", MapCoordinates.Nullspace);

            Assert.Multiple(() =>
            {
                Assert.That(pair.Server.EntMan.GetComponent<ComputerComponent>(terminal).BoardPrototype,
                    Is.EqualTo("MonoResearchDataTerminalCircuitboard"));
                Assert.That(pair.Server.EntMan.GetComponent<MachineComponent>(xrf).Board?.Id,
                    Is.EqualTo("MonoXRFScannerCircuitboard"));
                Assert.That(pair.Server.EntMan.GetComponent<MachineComponent>(simulator).Board?.Id,
                    Is.EqualTo("MonoSynthesisSimulatorCircuitboard"));
                Assert.That(prototypes.HasIndex<EntityPrototype>("MonoResearchDataTerminalCircuitboard"), Is.True);
                Assert.That(prototypes.HasIndex<EntityPrototype>("MonoXRFScannerCircuitboard"), Is.True);
                Assert.That(prototypes.HasIndex<EntityPrototype>("MonoSynthesisSimulatorCircuitboard"), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
