using Content.Shared._Mono.Xenobiology.XRF;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Mono.Xenobiology.XRF;

public sealed class XRFScannerWindow : DefaultWindow
{
    private readonly Label _status = new();
    private readonly Label _report = new();

    public XRFScannerWindow()
    {
        Title = Loc.GetString("mono-xrf-scanner-title");
        MinSize = new System.Numerics.Vector2(420, 240);
        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children = { _status, _report },
        });
    }

    public void Update(XRFScannerBuiState state)
    {
        _status.Text = state.Processing
            ? Loc.GetString("mono-xrf-scanner-scanning")
            : Loc.GetString("mono-xrf-scanner-status", ("status", state.Report.Status));
        _report.Text = !state.HasReport
            ? Loc.GetString("mono-xrf-scanner-instructions")
            : Loc.GetString("mono-xrf-scanner-report",
                ("name", state.Report.Name ?? Loc.GetString("mono-xrf-scanner-unknown")),
                ("class", state.Report.Class),
                ("reward", state.Report.Reward),
                ("first", Loc.GetString(state.Report.RewardGranted ? "mono-xrf-scanner-first-yes" : "mono-xrf-scanner-first-no")));
    }
}
