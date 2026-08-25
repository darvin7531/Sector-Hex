using System.Linq;
using Content.Shared._Mono.Xenobiology.Chemistry;
using Content.Shared._Mono.Xenobiology.Simulator;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Mono.Xenobiology.Simulator;

public sealed class SynthesisSimulatorWindow : DefaultWindow
{
    public event Action<SynthesisSimulatorRunMessage>? RunRequested;

    private readonly OptionButton _target = new();
    private readonly OptionButton _reference = new();
    private readonly OptionButton _mode = new();
    private readonly OptionButton _targetProperty = new();
    private readonly OptionButton _referenceProperty = new();
    private readonly CheckBox _override = new() { Text = "Override conflicts" };
    private readonly Button _run = new() { Text = "Simulate" };
    private readonly Label _result = new();
    private List<GeneratedReagentData> _available = [];

    public SynthesisSimulatorWindow()
    {
        Title = "Synthesis Simulator";
        MinSize = new System.Numerics.Vector2(560, 420);

        foreach (var mode in Enum.GetValues<SynthesisSimulatorMode>())
            _mode.AddItem(mode.ToString(), (int) mode);
        _mode.SelectId((int) SynthesisSimulatorMode.Amplify);

        _target.OnItemSelected += _ => RefreshProperties();
        _reference.OnItemSelected += _ => RefreshProperties();
        _run.OnPressed += _ => SendRun();

        Contents.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new Label { Text = "Target reagent" }, _target,
                new Label { Text = "Target property" }, _targetProperty,
                new Label { Text = "Operation" }, _mode,
                new Label { Text = "Reference reagent" }, _reference,
                new Label { Text = "Reference property" }, _referenceProperty,
                _override, _run, _result,
            },
        });
    }

    public void Update(SynthesisSimulatorBuiState state)
    {
        _available = state.Available;
        FillReagents(_target, state.TargetId);
        FillReagents(_reference, state.ReferenceId);
        _mode.SelectId((int) state.Mode);
        _override.Pressed = state.OverrideConflicts;
        RefreshProperties(state.TargetProperty, state.ReferenceProperty);
        _run.Disabled = _available.Count == 0;
        _result.Text = state.Error ?? (state.Result is { } result ? $"Created: {result.Name}" : string.Empty);
    }

    private void FillReagents(OptionButton button, string? selected)
    {
        button.Clear();
        button.AddItem("None", -1);
        for (var index = 0; index < _available.Count; index++)
            button.AddItem(_available[index].Name, index);
        button.SelectId(IndexOf(selected));
    }

    private void RefreshProperties(string? targetSelected = null, string? referenceSelected = null)
    {
        FillProperties(_targetProperty, Selected(_target), targetSelected);
        FillProperties(_referenceProperty, Selected(_reference), referenceSelected);
    }

    private static void FillProperties(OptionButton button, GeneratedReagentData? reagent, string? selected)
    {
        button.Clear();
        button.AddItem("None", -1);
        if (reagent is not { } data)
        {
            button.SelectId(-1);
            return;
        }

        var properties = data.Effects.Keys.Order().ToArray();
        for (var index = 0; index < properties.Length; index++)
            button.AddItem(properties[index], index);
        button.SelectId(Array.IndexOf(properties, selected));
    }

    private void SendRun()
    {
        var target = Selected(_target);
        if (target is not { } targetData)
            return;
        var reference = Selected(_reference);
        RunRequested?.Invoke(new SynthesisSimulatorRunMessage(
            targetData.ID,
            reference?.ID,
            (SynthesisSimulatorMode) _mode.SelectedId,
            SelectedProperty(_targetProperty, targetData),
            reference is { } referenceData ? SelectedProperty(_referenceProperty, referenceData) : null,
            _override.Pressed));
    }

    private GeneratedReagentData? Selected(OptionButton button)
        => button.SelectedId >= 0 && button.SelectedId < _available.Count ? _available[button.SelectedId] : null;

    private int IndexOf(string? id)
    {
        if (id == null)
            return -1;
        return _available.FindIndex(data => data.ID == id);
    }

    private static string? SelectedProperty(OptionButton button, GeneratedReagentData reagent)
    {
        var properties = reagent.Effects.Keys.Order().ToArray();
        return button.SelectedId >= 0 && button.SelectedId < properties.Length ? properties[button.SelectedId] : null;
    }
}
