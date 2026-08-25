using Content.Shared._Mono.Xenobiology.XRF;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Mono.Xenobiology.XRF;

public sealed class XRFScannerWindow : DefaultWindow
{
    private readonly Label _status = new();
    private readonly Label _report = new();

    public XRFScannerWindow()
    {
        Title = "XRF Scanner";
        MinSize = new System.Numerics.Vector2(420, 240);
        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children = { _status, _report },
        });
    }

    public void Update(XRFScannerBuiState state)
    {
        _status.Text = state.Processing ? "Scanning sample…" : $"Status: {state.Report.Status}";
        _report.Text = !state.HasReport
            ? "Insert a vial containing at least 30u of one pure reagent."
            : $"Reagent: {state.Report.Name ?? "unknown"}\n" +
              $"Class: {state.Report.Class}\n" +
              $"Research reward: {state.Report.Reward}\n" +
              $"First identification: {(state.Report.RewardGranted ? "yes" : "no")}";
    }
}
