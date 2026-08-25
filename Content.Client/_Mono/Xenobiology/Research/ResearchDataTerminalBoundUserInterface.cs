using Content.Shared._Mono.Xenobiology.Research;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Xenobiology.Research;

public sealed class ResearchDataTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ResearchDataTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ResearchDataTerminalWindow>();
        _window.ContractSelected += id => SendMessage(new ResearchDataTerminalSelectMessage(id));
        _window.UpgradeRequested += () => SendMessage(new ResearchDataTerminalUpgradeMessage());
        _window.ReprintRequested += () => SendMessage(new ResearchDataTerminalReprintMessage());
        _window.ReportRequested += index => SendMessage(new ResearchDataTerminalPrintReportMessage(index));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ResearchDataTerminalBuiState terminalState)
            _window?.Update(terminalState);
    }
}
