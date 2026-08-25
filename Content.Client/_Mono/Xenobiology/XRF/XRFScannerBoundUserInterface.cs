using Content.Shared._Mono.Xenobiology.XRF;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Xenobiology.XRF;

public sealed class XRFScannerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private XRFScannerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<XRFScannerWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is XRFScannerBuiState scannerState)
            _window?.Update(scannerState);
    }
}
