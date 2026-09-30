using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared.Body.Part;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Client._Onyx.Medical.Surgery;

public sealed partial class SurgeryBoundUserInterface : BoundUserInterface
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly SurgerySystem _system;
    private readonly SpriteSystem _sprites;
    private SurgeryWindow? _window;
    private EntityUid? _part;
    private EntProtoId? _surgery;
    private uint _stepsRequestId;
    private bool _stepsRequestPending;
    private readonly List<SurgeryUiItem> _protocolItems = new();
    private readonly List<SurgeryChoiceControl> _protocolControls = new();
    private string? _viewHint;
    private bool? _patientReady;

    public SurgeryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = EntMan.System<SurgerySystem>();
        _sprites = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SurgeryWindow>();
        _system.OnRefresh += Refresh;
        _window.OnClose += () => _system.OnRefresh -= Refresh;
        _window.BackButton.OnPressed += _ => GoBack();
        if (State is SurgeryBuiState state)
            Update(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is SurgeryBuiState surgery)
            Update(surgery);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is SurgeryStepsStateResponse state)
            RefreshSteps(state);
    }

    private void Update(SurgeryBuiState state)
    {
        if (_window == null)
            return;

        if (_part is { } selectedPart && !state.Choices.ContainsKey(EntMan.GetNetEntity(selectedPart)))
        {
            _part = null;
            _surgery = null;
            _stepsRequestPending = false;
        }
        else if (_part is { } currentPart && _surgery is { } currentSurgery &&
                 !state.Choices[EntMan.GetNetEntity(currentPart)].Contains(currentSurgery))
        {
            _surgery = null;
            _stepsRequestPending = false;
        }

        if (_part != null && _surgery != null)
        {
            ShowView(ViewType.Protocol);
            RequestStepsState();
        }
        else if (_part != null)
        {
            RebuildSurgeries(state);
            ShowView(ViewType.Procedures);
        }
        else
        {
            RebuildParts(state);
            ShowView(ViewType.Parts);
        }

        UpdateContext();
        UpdateDisabledPanel();
    }

    private void Refresh()
    {
        if (_window == null)
            return;

        if (_part != null && _surgery != null && !_stepsRequestPending)
            RequestStepsState();
        UpdateDisabledPanel();
    }

    private void RebuildParts(SurgeryBuiState state)
    {
        if (_window == null)
            return;

        _window.Parts.RemoveAllChildren();
        var parts = new List<(EntityUid Entity, BodyPartComponent Part, string Name)>();
        foreach (var netPart in state.Choices.Keys)
        {
            var entity = EntMan.GetEntity(netPart);
            if (EntMan.TryGetComponent(entity, out BodyPartComponent? part))
                parts.Add((entity, part, EntMan.GetComponent<MetaDataComponent>(entity).EntityName));
        }

        foreach (var part in parts.OrderBy(part => PartOrder(part.Part.PartType)).ThenBy(part => part.Name))
        {
            var button = Choice(Capitalize(part.Name), part.Entity);
            button.SetSelected(_part == part.Entity);
            button.Button.OnPressed += _ => SelectPart(part.Entity);
            _window.Parts.AddChild(button);
        }
    }

    private void SelectPart(EntityUid part)
    {
        if (_part == part)
            return;

        _part = part;
        _surgery = null;
        _stepsRequestPending = false;
        if (State is SurgeryBuiState state)
        {
            RebuildParts(state);
            RebuildSurgeries(state);
        }
        ShowView(ViewType.Procedures);
        UpdateContext();
    }

    private void RebuildSurgeries(SurgeryBuiState state)
    {
        if (_window == null)
            return;

        _window.Surgeries.RemoveAllChildren();
        if (_part is not { } part || !state.Choices.TryGetValue(EntMan.GetNetEntity(part), out var surgeryIds))
            return;

        var completed = state.Completed.GetValueOrDefault(EntMan.GetNetEntity(part));
        var surgeries = new List<(EntProtoId Id, EntityPrototype Proto, SurgeryComponent Component)>();
        foreach (var id in surgeryIds)
        {
            if (_prototypes.TryIndex<EntityPrototype>(id, out var proto) &&
                proto.TryComp(out SurgeryComponent? component, EntMan.ComponentFactory))
                surgeries.Add((id, proto, component));
        }

        foreach (var surgery in surgeries.OrderBy(surgery => surgery.Component.Priority).ThenBy(surgery => surgery.Proto.Name))
        {
            var button = ChoiceWithTexture(surgery.Proto.Name, SurgeryIcon(surgery.Component));
            button.SetSelected(_surgery == surgery.Id);
            if (completed?.Contains(surgery.Id) == true)
                button.SetStatus(Loc.GetString("surgery-ui-status-completed"), Color.FromHex("#79C99E"));
            button.Button.OnPressed += _ => SelectSurgery(surgery.Id);
            _window.Surgeries.AddChild(button);
        }
    }

    private void SelectSurgery(EntProtoId surgery)
    {
        if (_part == null)
            return;

        _surgery = surgery;
        _stepsRequestPending = false;
        ClearProtocol();
        ShowView(ViewType.Protocol);
        UpdateContext();
        RequestStepsState();
    }

    private SurgeryChoiceControl Choice(string text, EntityUid? icon = null)
    {
        var control = new SurgeryChoiceControl();
        control.Set(text, EntityIcon(icon));
        return control;
    }

    private static SurgeryChoiceControl ChoiceWithTexture(string text, Texture? texture)
    {
        var control = new SurgeryChoiceControl();
        control.Set(text, texture);
        return control;
    }

    private Texture? SurgeryIcon(SurgeryComponent surgery)
    {
        if (surgery.UseTargetPartIcon && _part is { } part &&
            EntMan.TryGetComponent(part, out SpriteComponent? partSprite))
            return partSprite.Icon?.Default;

        return surgery.Icon is { } icon ? _sprites.Frame0(icon) : null;
    }

    private Texture? EntityIcon(EntityUid? icon)
    {
        return icon is { } entity && EntMan.TryGetComponent(entity, out SpriteComponent? sprite)
            ? sprite.Icon?.Default
            : null;
    }

    private void GoBack()
    {
        if (_surgery != null)
        {
            _surgery = null;
            _stepsRequestPending = false;
            ClearProtocol();
            if (State is SurgeryBuiState state)
                RebuildSurgeries(state);
            ShowView(ViewType.Procedures);
        }
        else if (_part != null)
        {
            _part = null;
            if (State is SurgeryBuiState state)
                RebuildParts(state);
            ShowView(ViewType.Parts);
        }

        UpdateContext();
    }

    private void ShowView(ViewType view, string? hint = null)
    {
        if (_window == null)
            return;

        _window.Parts.Visible = view == ViewType.Parts;
        _window.Surgeries.Visible = view == ViewType.Procedures;
        _window.Steps.Visible = view == ViewType.Protocol;
        _window.BackButton.Visible = view != ViewType.Parts;
        _window.SectionTitle.Text = Loc.GetString(view switch
        {
            ViewType.Parts => "surgery-ui-section-parts",
            ViewType.Procedures => "surgery-ui-section-surgeries",
            _ => "surgery-ui-section-steps",
        });
        SetViewHint(hint);
    }

    private void SetViewHint(string? hint)
    {
        if (_window == null || _viewHint == hint)
            return;

        _viewHint = hint;
        _window.ViewHint.Visible = hint != null;
        _window.ViewHint.Text = hint ?? string.Empty;
    }

    private void UpdateContext()
    {
        if (_window == null)
            return;

        var partName = _part is { } part ? Capitalize(EntMan.GetComponent<MetaDataComponent>(part).EntityName) : null;
        var surgeryName = _surgery is { } surgery && _prototypes.TryIndex<EntityPrototype>(surgery, out var proto)
            ? proto.Name
            : null;
        _window.ContextLabel.Text = surgeryName != null
            ? Loc.GetString("surgery-ui-context-full", ("part", partName!), ("surgery", surgeryName))
            : partName != null
                ? Loc.GetString("surgery-ui-context-part", ("part", partName))
                : Loc.GetString("surgery-ui-context-none");
    }

    private void UpdateDisabledPanel()
    {
        if (_window == null)
            return;

        var ready = _system.IsReadyForSurgery(Owner);
        if (_patientReady == ready)
            return;

        _patientReady = ready;
        _window.DisabledPanel.Visible = !ready;
        _window.DisabledPanel.MouseFilter = !ready
            ? Control.MouseFilterMode.Stop
            : Control.MouseFilterMode.Ignore;
        if (_window.DisabledPanel.Visible)
        {
            var message = new FormattedMessage();
            message.AddMarkupOrThrow(Loc.GetString("surgery-ui-patient-must-lie"));
            _window.DisabledLabel.SetMessage(message);
        }
    }

    private void RequestStepsState()
    {
        if (_part == null || _surgery == null || _stepsRequestPending)
            return;

        _stepsRequestId++;
        _stepsRequestPending = true;
        SendMessage(new SurgeryStepsStateRequest(EntMan.GetNetEntity(_part.Value), _surgery.Value, _stepsRequestId));
    }

    private void RefreshSteps(SurgeryStepsStateResponse state)
    {
        if (_window == null || _part == null || _surgery == null ||
            state.Part != EntMan.GetNetEntity(_part.Value) || state.Surgery != _surgery ||
            state.RequestId != _stepsRequestId)
            return;

        _stepsRequestPending = false;
        if (state.SelectionState == SurgerySelectionState.Invalid)
        {
            _surgery = null;
            if (State is SurgeryBuiState buiState)
                RebuildSurgeries(buiState);
            ShowView(ViewType.Procedures, Loc.GetString("surgery-ui-protocol-unavailable"));
            UpdateContext();
            return;
        }

        if (!ProtocolShapeMatches(state.Items))
            RebuildProtocol(state.Items);

        for (var index = 0; index < state.Items.Count; index++)
        {
            var item = state.Items[index];
            var control = _protocolControls[index];
            string? tooltip = null;

            if (item.Kind == SurgeryItemKind.Surgery)
            {
                if (!control.Button.Disabled)
                    control.Button.Disabled = true;
                control.SetStatus(item.Completed
                    ? Loc.GetString("surgery-ui-status-completed")
                    : Loc.GetString("surgery-ui-status-required"),
                    item.Completed ? Color.FromHex("#79C99E") : Color.FromHex("#D5B56E"));
                continue;
            }

            var isNext = state.NextStep == index;
            var disabled = !isNext || !state.Available;
            if (control.Button.Disabled != disabled)
                control.Button.Disabled = disabled;
            if (item.Completed)
                control.SetStatus(Loc.GetString("surgery-ui-status-completed"), Color.FromHex("#79C99E"));
            else if (isNext && state.Available)
                control.SetStatus(Loc.GetString("surgery-ui-status-ready"), Color.FromHex("#73C5B5"));
            else if (isNext)
            {
                control.SetStatus(Loc.GetString("surgery-ui-status-blocked"), Color.FromHex("#E28A8F"));
                tooltip = state.Popup ?? InvalidReason(state.Reason);
            }
            else
                control.SetStatus(Loc.GetString("surgery-ui-status-pending"), Color.FromHex("#82918E"));

            if (control.ToolTip != tooltip)
                control.ToolTip = tooltip;
        }

        if (state.SelectionState == SurgerySelectionState.Completed)
            SetViewHint(Loc.GetString("surgery-ui-protocol-completed"));
        else if (state.NextStep >= 0 && !state.Available)
            SetViewHint(state.Popup ?? InvalidReason(state.Reason));
        else
            SetViewHint(null);

    }

    private bool ProtocolShapeMatches(IReadOnlyList<SurgeryUiItem> items)
    {
        if (_protocolItems.Count != items.Count)
            return false;

        for (var i = 0; i < items.Count; i++)
        {
            var current = _protocolItems[i];
            var next = items[i];
            if (current.Surgery != next.Surgery || current.Id != next.Id || current.Kind != next.Kind ||
                current.Depth != next.Depth)
                return false;
        }

        return true;
    }

    private void RebuildProtocol(IReadOnlyList<SurgeryUiItem> items)
    {
        if (_window == null || _part == null || _surgery == null)
            return;

        ClearProtocol();
        var number = 0;
        foreach (var item in items)
        {
            if (!_prototypes.TryIndex<EntityPrototype>(item.Id, out var prototype))
                continue;

            SurgeryChoiceControl control;
            if (item.Kind == SurgeryItemKind.Surgery)
            {
                control = new SurgeryChoiceControl { Margin = new Thickness(item.Depth * 12, 6, 0, 0) };
                control.Set(Loc.GetString("surgery-ui-protocol-section", ("surgery", prototype.Name)), null);
                control.Button.Disabled = true;
            }
            else
            {
                number++;
                var surgeryId = item.Surgery;
                var stepId = item.Id;
                var procedureId = _surgery.Value;
                var step = new SurgeryStepButton
                {
                    StepId = stepId,
                    Margin = new Thickness(item.Depth * 12, 0, 0, 0),
                };
                step.Set(Loc.GetString("surgery-ui-step-numbered", ("number", number), ("step", prototype.Name)),
                    EntityIcon(_system.GetSurgeryStepEntity(stepId)));
                step.Button.OnPressed += _ => SendMessage(new SurgeryStepChosenBuiMsg(
                    EntMan.GetNetEntity(_part.Value), procedureId, surgeryId, stepId));
                control = step;
            }

            _protocolItems.Add(item);
            _protocolControls.Add(control);
            _window.Steps.AddChild(control);
        }
    }

    private void ClearProtocol()
    {
        _window?.Steps.RemoveAllChildren();
        _protocolItems.Clear();
        _protocolControls.Clear();
    }

    private static int PartOrder(BodyPartType part) => part switch
    {
        BodyPartType.Head => 1,
        BodyPartType.Chest => 2,
        BodyPartType.Groin => 3,
        BodyPartType.Arm => 4,
        BodyPartType.Hand => 5,
        BodyPartType.Leg => 6,
        BodyPartType.Foot => 7,
        BodyPartType.Tail => 8,
        _ => 9,
    };

    private static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : OopsConcat(char.ToUpper(text[0]).ToString(), text.Remove(0, 1));

    private static string OopsConcat(string a, string b)
    {
        // Prevent Roslyn from emitting string span code forbidden by content sandbox.
        return a + b;
    }

    private static string InvalidReason(StepInvalidReason reason) => Loc.GetString(reason switch
    {
        StepInvalidReason.OutOfRange => "surgery-ui-reason-out-of-range",
        StepInvalidReason.NeedsOperatingTable => "surgery-ui-reason-operating-table",
        StepInvalidReason.Clothing => "surgery-ui-reason-clothing",
        StepInvalidReason.MissingTool => "surgery-ui-reason-tool",
        StepInvalidReason.MissingMaterial => "surgery-ui-reason-material",
        StepInvalidReason.SurgerySiteBusy => "surgery-ui-reason-site-busy",
        StepInvalidReason.IncompatibleTransplant => "surgery-ui-reason-incompatible-transplant",
        StepInvalidReason.IncompatibleTransplantType => "surgery-ui-reason-incompatible-transplant-type",
        StepInvalidReason.AmputationConsequence => "surgery-ui-reason-amputation-consequence",
        _ => "surgery-ui-reason-unavailable",
    });

    private enum ViewType : byte
    {
        Parts,
        Procedures,
        Protocol,
    }
}
