using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Body.Part;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Medical.Surgery;

/// <summary>
/// Admits a body into the surgery framework. Bodies without it cannot be operated on.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryTargetComponent : Component
{
    /// <summary>
    /// Policy idea borrowed from Goob-Station's surgery target (own implementation):
    /// the marker admits a body into the surgery framework, and every surgery
    /// entry point additionally honors this switch. Bodies that must never be
    /// operated simply omit the marker instead.
    /// </summary>
    [DataField]
    public bool CanOperate = true;

    /// <summary>
    /// Policy idea borrowed from Goob-Station's sepsis immunity (own implementation):
    /// mechanical bodies cannot develop surgical site infections, so unsanitary
    /// surgery neither infects nor punishes them.
    /// </summary>
    [DataField]
    public bool InfectionImmune;
}

/// <summary>
/// Policy idea borrowed from Goob-Station (own implementation): its bearer
/// operates through patient clothing. Checked on the surgeon and on the
/// active tool; clothing rules are otherwise unchanged.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryClothingBypassComponent : Component;

/// <summary>
/// Surgery procedure definition: ordered step sequences keyed by context variant.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryComponent : Component
{
    [DataField, AutoNetworkedField] public int Priority;
    [DataField, AutoNetworkedField] public SpriteSpecifier? Icon;
    [DataField, AutoNetworkedField] public bool UseTargetPartIcon;
    /// <summary>Step whose sequence context selects the active step variant.</summary>
    [DataField] public EntProtoId? SequenceContextStep;
    [DataField(required: true)] public Dictionary<string, SurgeryStepSequence> Steps = new();
}

/// <summary>
/// Single surgery step: duration, tool and consumable requirements, marker transitions.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryStepComponent : Component
{
    [DataField] public float Duration = 2f;
    [DataField] public ComponentRegistry? Tool;
    [DataField] public ProtoId<ToolQualityPrototype>? ToolQuality;
    [DataField] public ProtoId<StackPrototype>? ConsumedStackType;
    [DataField] public EntProtoId? ConsumedPrototype;
    [DataField] public int ConsumedAmount = 1;
    [DataField] public HashSet<string> AddMarkers = new();
    [DataField] public HashSet<string> RemoveMarkers = new();
    [DataField] public HashSet<string> ParentRemoveMarkers = new();
    [DataField] public BodyPartType? ParentRemoveMarkersPart;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryMarkerComponent : Component
{
    [DataField, AutoNetworkedField] public HashSet<string> Markers = new();
}

[RegisterComponent] public sealed partial class MechanicalSurgeryStepComponent : Component;

/// <summary>Repeats this step until all of its completion bricks report success.</summary>
[RegisterComponent] public sealed partial class RepeatSurgeryStepComponent : Component;

[Serializable]
public enum SurgeryEntityTarget : byte
{
    Body,
    Part,
    User,
    Tool,
}

[RegisterComponent] public sealed partial class MechanicalOrganComponent : Component;

[RegisterComponent] public sealed partial class SlimeCoreComponent : Component;

[RegisterComponent] public sealed partial class TorsoOrganComponent : Component;
