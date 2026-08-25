using Content.Shared._Mono.Xenobiology.Research;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Mono.Xenobiology.Research;

public sealed class ResearchDataTerminalWindow : DefaultWindow
{
    public event Action<string>? ContractSelected;
    public event Action? UpgradeRequested;
    public event Action? ReprintRequested;
    public event Action<int>? ReportRequested;

    private readonly Label _progress;
    private readonly Button _upgrade;
    private readonly Button _reprint;
    private readonly BoxContainer _contracts;
    private readonly BoxContainer _reports;

    public ResearchDataTerminalWindow()
    {
        Title = "Research Data Terminal";
        MinSize = new System.Numerics.Vector2(620, 480);

        _progress = new Label();
        _upgrade = new Button { Text = "Upgrade clearance" };
        _reprint = new Button { Text = "Reprint selected contract" };
        _contracts = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        _reports = new BoxContainer { Orientation = LayoutOrientation.Vertical };

        _upgrade.OnPressed += _ => UpgradeRequested?.Invoke();
        _reprint.OnPressed += _ => ReprintRequested?.Invoke();

        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                _progress,
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children = { _upgrade, _reprint },
                },
                new Label { Text = "Available contracts" },
                new ScrollContainer { VerticalExpand = true, Children = { _contracts } },
                new Label { Text = "Known scans" },
                new ScrollContainer { VerticalExpand = true, Children = { _reports } },
            },
        });
    }

    public void Update(ResearchDataTerminalBuiState state)
    {
        _progress.Text = $"Credits: {state.Credits}    Clearance: {state.Clearance}    Upgrade: {state.UpgradeCost?.ToString() ?? "maximum"}";
        _upgrade.Disabled = !state.CanUpgrade;
        _reprint.Disabled = !state.Picked;

        _contracts.RemoveAllChildren();
        foreach (var contract in state.Contracts)
        {
            var button = new Button
            {
                Text = $"{contract.Name} — {contract.ScanPointYield} credits",
                Disabled = !state.CanSelect,
            };
            var id = contract.ID;
            button.OnPressed += _ => ContractSelected?.Invoke(id);
            _contracts.AddChild(button);
        }

        _reports.RemoveAllChildren();
        for (var index = 0; index < state.KnownScans.Count; index++)
        {
            var report = state.KnownScans[index];
            var button = new Button { Text = report.Name };
            var reportIndex = index;
            button.OnPressed += _ => ReportRequested?.Invoke(reportIndex);
            _reports.AddChild(button);
        }
    }
}
