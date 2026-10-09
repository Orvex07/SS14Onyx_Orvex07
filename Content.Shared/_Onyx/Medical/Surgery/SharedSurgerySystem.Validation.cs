using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Buckle.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    /// <summary>
    /// Checks that the surgery, step and part are valid and raises gates on step and surgery singletons.
    /// </summary>
    protected bool IsSurgeryValid(EntityUid user, EntityUid body, EntityUid targetPart, EntProtoId surgery, EntProtoId stepId, List<EntityUid> tools, out Entity<SurgeryComponent> surgeryEnt, out Entity<BodyPartComponent> part, out EntityUid step)
    {
        surgeryEnt = default;
        part = default;
        step = default;

        if (!TryComp(body, out SurgeryTargetComponent? surgeryTarget) || !surgeryTarget.CanOperate ||
            !IsReadyForSurgery(body) ||
            !TryComp(targetPart, out BodyPartComponent? partComp) || !IsPartOfTarget(body, targetPart) ||
            GetSurgeryEntity(surgery) is not { } surgeryEntId || !TryComp(surgeryEntId, out SurgeryComponent? surgeryComp) ||
            !GetSurgerySteps(body, targetPart, (surgeryEntId, surgeryComp), tools).Contains(stepId) ||
            GetSurgeryEntity(stepId) != null ||
            GetSurgeryStepEntity(stepId) is not { } stepEnt)
            return false;

        var ev = new SurgeryValidEvent(body, targetPart, user, tools);
        RaiseLocalEvent(stepEnt, ref ev);
        RaiseLocalEvent(surgeryEntId, ref ev);
        if (ev.Cancelled)
            return false;

        surgeryEnt = (surgeryEntId, surgeryComp);
        part = (targetPart, partComp);
        step = stepEnt;
        return true;
    }

    private bool TryValidateSurgeryStep(EntityUid user, Entity<SurgeryTargetComponent> target, EntityUid targetPart,
        EntProtoId surgeryId, EntProtoId stepId, List<EntityUid> tools, bool doPopup,
        out Entity<BodyPartComponent> part, out EntityUid step, out HashSet<EntityUid>? validTools)
    {
        part = default;
        step = default;
        validTools = null;
        return IsSurgeryValid(user, target, targetPart, surgeryId, stepId, tools, out var surgery, out part, out step) &&
               PreviousStepsComplete(target, part, surgery, stepId, tools) &&
               !IsStepComplete(target, part, stepId) &&
               CanPerformStep(user, target, part, part.Comp.PartType, step, doPopup,
                   out _, out _, out validTools);
    }

    private bool IsLyingDown(EntityUid entity)
    {
        if (_standing.IsDown(entity))
            return true;

        return TryComp(entity, out BuckleComponent? buckle) &&
               TryComp(buckle.BuckledTo, out StrapComponent? strap) &&
               strap.Position == StrapPosition.Down;
    }

    /// <summary>
    /// Returns true if the body can be operated on: a detached part, or a lying patient.
    /// </summary>
    public bool IsReadyForSurgery(EntityUid entity)
    {
        return TryComp(entity, out BodyPartComponent? part)
            ? part.Body == null && part.Parent == null
            : IsLyingDown(entity);
    }

    /// <summary>
    /// Returns true if the part belongs to the surgery target.
    /// </summary>
    protected bool IsPartOfTarget(EntityUid target, EntityUid part)
    {
        return TryComp(target, out BodyPartComponent? targetPart)
            ? targetPart.Body == null && targetPart.Parent == null && _body.GetBodyPartChildren(target).Any(child => child.Id == part)
            : _body.BodyHasChild(target, part);
    }

    private bool PreviousStepsComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId step, List<EntityUid> tools)
    {
        foreach (var surgeryStep in GetSurgerySteps(body, part, surgery, tools))
        {
            if (surgeryStep == step)
                break;
            if (!IsSurgeryItemComplete(body, part, surgeryStep, tools))
                return false;
        }

        return true;
    }

    protected IReadOnlyList<EntProtoId> GetSurgerySteps(EntityUid body, EntityUid part,
        Entity<SurgeryComponent> surgery, List<EntityUid> tools)
        => GetSurgerySequence(body, part, surgery, tools);

    /// <summary>
    /// Resolves the next incomplete step of a procedure, descending into nested surgeries.
    /// </summary>
    protected bool TryGetNextProcedureStep(EntityUid body, EntityUid part, EntProtoId surgeryId,
        EntityUid? user, List<EntityUid> tools, out EntProtoId owner, out EntProtoId step)
    {
        owner = default;
        step = default;
        if (GetSurgeryEntity(surgeryId) is not { } surgery || !TryComp(surgery, out SurgeryComponent? surgeryComp))
            return false;

        var valid = new SurgeryValidEvent(body, part, user, tools);
        RaiseLocalEvent(surgery, ref valid);
        if (valid.Cancelled)
            return false;

        foreach (var item in GetSurgerySteps(body, part, (surgery, surgeryComp), tools))
        {
            if (IsSurgeryItemComplete(body, part, item, tools))
                continue;

            if (GetSurgeryEntity(item) != null)
                return TryGetNextProcedureStep(body, part, item, user, tools, out owner, out step);

            owner = surgeryId;
            step = item;
            return true;
        }

        return false;
    }

    private IReadOnlyList<EntProtoId> GetSurgerySequence(EntityUid body, EntityUid part,
        Entity<SurgeryComponent> surgery, List<EntityUid> tools)
    {
        if (surgery.Comp.Steps.Count == 0)
            return [];

        var fallback = surgery.Comp.Steps.Values.FirstOrDefault(sequence => sequence.Required.Count == 0)?.Steps ?? [];
        if (surgery.Comp.Steps.Values.All(sequence => sequence.Required.Count == 0))
            return fallback;

        var context = part;
        if (surgery.Comp.SequenceContextStep is { } contextStep)
        {
            if (GetSurgeryStepEntity(contextStep) is not { } step)
                return fallback;

            var contextEvent = new SurgeryGetStepSequenceContextEvent(body, part, tools);
            RaiseLocalEvent(step, ref contextEvent);
            if (contextEvent.Context is not { } resolved)
                return fallback;
            context = resolved;
        }

        return surgery.Comp.Steps
            .Where(entry => entry.Value.Required.Count > 0 &&
                entry.Value.Required.Values.All(required => HasComp(context, required.Component.GetType())))
            .OrderByDescending(entry => entry.Value.Required.Count)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => (IReadOnlyList<EntProtoId>) entry.Value.Steps)
            .FirstOrDefault() ?? fallback;
    }

    /// <summary>
    /// Returns true if a surgery item (nested surgery or single step) is fully complete.
    /// </summary>
    protected bool IsSurgeryItemComplete(EntityUid body, EntityUid part, EntProtoId item, List<EntityUid> tools)
    {
        if (GetSurgeryEntity(item) is { } surgery && TryComp(surgery, out SurgeryComponent? surgeryComp))
            return GetSurgerySteps(body, part, (surgery, surgeryComp), tools)
                .All(child => IsSurgeryItemComplete(body, part, child, tools));

        return IsStepComplete(body, part, item);
    }

    /// <summary>
    /// Returns true if a single step reports completion.
    /// </summary>
    protected bool IsStepComplete(EntityUid body, EntityUid part, EntProtoId stepId)
    {
        if (GetSurgeryStepEntity(stepId) is not { } step)
            return false;

        var ev = new SurgeryStepCompleteCheckEvent(body, part);
        RaiseLocalEvent(step, ref ev);
        return !ev.Cancelled;
    }

    /// <summary>
    /// Checks range, clothing and per-step gates, reporting the failure reason.
    /// </summary>
    protected bool CanPerformStep(EntityUid user, EntityUid body, EntityUid targetPart, BodyPartType part, EntityUid step, bool doPopup, out string? popup, out StepInvalidReason reason, out HashSet<EntityUid>? validTools)
    {
        if (!_interaction.InRangeUnobstructed(user, body, popup: doPopup))
        {
            popup = Loc.GetString("surgery-ui-reason-out-of-range");
            reason = StepInvalidReason.OutOfRange;
            validTools = null;
            return false;
        }

        var slot = part switch
        {
            BodyPartType.Head => SlotFlags.HEAD,
            BodyPartType.Chest or BodyPartType.Groin or BodyPartType.Arm => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Hand => SlotFlags.GLOVES,
            BodyPartType.Leg => SlotFlags.OUTERCLOTHING | SlotFlags.LEGS,
            BodyPartType.Foot => SlotFlags.FEET,
            _ => SlotFlags.NONE,
        };

        if (slot != SlotFlags.NONE && !HasClothingBypass(user) && TryComp(body, out InventoryComponent? inventory))
        {
            var equipped = new InventorySystem.InventorySlotEnumerator(inventory, slot);
            if (equipped.NextItem(out _))
            {
                popup = Loc.GetString("surgery-ui-reason-clothing");
                validTools = null;
                if (doPopup)
                    _popup.PopupEntity(popup, user, user, PopupType.SmallCaution);
                reason = StepInvalidReason.Clothing;
                return false;
            }
        }

        var check = new SurgeryCanPerformStepEvent(user, body, targetPart, GetActiveTool(user), slot);
        RaiseLocalEvent(step, ref check);
        popup = check.Popup;
        validTools = check.ValidTools;

        if (check.Invalid == StepInvalidReason.None)
        {
            reason = default;
            return true;
        }

        if (doPopup && check.Popup != null)
            _popup.PopupEntity(check.Popup, user, user, PopupType.SmallCaution);

        reason = check.Invalid;
        return false;
    }

    protected virtual void RefreshUI(EntityUid body) { }

    private bool HasClothingBypass(EntityUid user)
    {
        if (HasComp<SurgeryClothingBypassComponent>(user))
            return true;

        return _hands.GetActiveItem(user) is { } tool && HasComp<SurgeryClothingBypassComponent>(tool);
    }
}
