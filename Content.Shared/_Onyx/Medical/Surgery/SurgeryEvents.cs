using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Medical.Surgery;

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : SimpleDoAfterEvent
{
    public readonly NetEntity Part;
    public readonly EntProtoId Procedure;
    public readonly EntProtoId Surgery;
    public readonly EntProtoId Step;
    public readonly uint Token;
    public readonly float SuccessRate;

    public SurgeryDoAfterEvent(NetEntity part, EntProtoId procedure, EntProtoId surgery, EntProtoId step, uint token,
        float successRate)
    {
        Part = part;
        Procedure = procedure;
        Surgery = surgery;
        Step = step;
        Token = token;
        SuccessRate = successRate;
    }
}

/// <summary>Why a step cannot be performed right now. None means the step is available.</summary>
public enum StepInvalidReason
{
    None,
    OutOfRange,
    NeedsOperatingTable,
    Clothing,
    MissingTool,
    MissingMaterial,
    SurgerySiteBusy,
    IncompatibleTransplant,
    AmputationConsequence,
    IncompatibleTransplantType,
}

/// <summary>Raised on step and surgery singletons to veto a procedure. Cancel to reject.</summary>
[ByRefEvent]
public record struct SurgeryValidEvent(
    EntityUid Body,
    EntityUid Part,
    EntityUid? User = null,
    IReadOnlyList<EntityUid>? Tools = null,
    bool Cancelled = false);
[ByRefEvent] public record struct SurgeryStepEvent(EntityUid User, EntityUid Body, EntityUid Part, List<EntityUid> Tools);
/// <summary>Raised when a step do-after fails its success roll.</summary>
[ByRefEvent] public readonly record struct SurgeryStepFailedEvent(EntityUid User, EntityUid Body, EntityUid Part);
/// <summary>Raised on step singletons to report completion. Cancel to mark incomplete.</summary>
[ByRefEvent] public record struct SurgeryStepCompleteCheckEvent(EntityUid Body, EntityUid Part, bool Cancelled = false);
[ByRefEvent] public record struct SurgeryOrganInsertedEvent(EntityUid User, EntityUid Body, EntityUid Part);
[ByRefEvent] public record struct SurgeryGetStepSequenceContextEvent(EntityUid Body, EntityUid Part, List<EntityUid> Tools, EntityUid? Context = null);

[ByRefEvent]
public record struct SurgeryCanPerformStepEvent(
    EntityUid User,
    EntityUid Body,
    EntityUid Part,
    List<EntityUid> Tools,
    SlotFlags TargetSlots,
    string? Popup = null,
    StepInvalidReason Invalid = StepInvalidReason.None,
    HashSet<EntityUid>? ValidTools = null
) : IInventoryRelayEvent;
