using Content.Shared._Mono.Xenobiology.Simulator;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Xenobiology.Simulator;

public sealed class SynthesisSimulatorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SynthesisSimulatorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SynthesisSimulatorWindow>();
        _window.RunRequested += request => SendMessage(request);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is SynthesisSimulatorBuiState simulatorState)
            _window?.Update(simulatorState);
    }
}
