using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Wounds;

/// <summary>
/// Marks a body whose damage is tracked per body part.
/// Without it all damage and healing use the vanilla systemic path.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WoundHostComponent : Component
{
    /// <summary>
    /// Marks a body whose damage is tracked per body part.
    /// This component never grants surgical interaction; that is the separate SurgeryTarget role.
    /// Without this component all damage and healing use the vanilla systemic path.
    /// </summary>
    [DataField]
    public Dictionary<BodyPartType, float> TargetWeights = new()
    {
        [BodyPartType.Chest] = 2.5f,
        [BodyPartType.Groin] = 1.5f,
        [BodyPartType.Head] = 1f,
        [BodyPartType.Arm] = 2f,
        [BodyPartType.Hand] = 1f,
        [BodyPartType.Leg] = 2f,
        [BodyPartType.Foot] = 1f,
        [BodyPartType.Tail] = 1f,
        [BodyPartType.Other] = 1f,
    };

    /// <summary>
    /// Damage types routed to body parts. Other types remain systemic on the wound host.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageTypePrototype>> LocalizedDamageTypes =
    [
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Cold",
        "Shock",
        "Caustic",
    ];

    [DataField]
    public Dictionary<BodyPartType, FixedPoint2> DismembermentSeverities = new()
    {
        [BodyPartType.Head] = 200,
        [BodyPartType.Groin] = 160,
        [BodyPartType.Arm] = 120,
        [BodyPartType.Leg] = 120,
        [BodyPartType.Hand] = 80,
        [BodyPartType.Foot] = 80,
    };

    [DataField]
    public FixedPoint2 DefaultDismembermentSeverity = 100;

    [DataField]
    public ProtoId<WoundPrototype> DismembermentWound = "DismembermentWound";

    [DataField]
    public ProtoId<WoundPrototype> AmputationConsequenceWound = "AmputationConsequenceWound";

    [DataField]
    public float SeverableResetRatio = 0.8f;

    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> DefaultDismembermentFinishingDamage = new()
    {
        ["Slash"] = 15,
        ["Piercing"] = 40,
        ["Blunt"] = 50,
    };

    [DataField]
    public HashSet<BodyPartType> MobilityParts = [BodyPartType.Leg, BodyPartType.Foot];

    [DataField]
    public HashSet<BodyPartType> ManipulationParts = [BodyPartType.Arm, BodyPartType.Hand];

    [DataField]
    public Dictionary<BodyPartType, float> PartEffectScales = new()
    {
        [BodyPartType.Leg] = 0.5f,
        [BodyPartType.Foot] = 0.5f,
        [BodyPartType.Hand] = 0.75f,
    };
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PartDamageVisualsComponent : Component
{
    /// <summary>Per-layer damage projection of the owning body. Rebuilt by the projection system.</summary>
    [AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, DamageSpecifier> Damage = new();
}

/// <summary>
/// Marks a body part that can hold wounds in its wounds container.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundableComponent : Component
{
    /// <summary>Child container holding wound entities.</summary>
    public const string ContainerId = "wounds";

    /// <summary>Body-part profile gating accepted damage and supported wounds.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<BodyPartProfilePrototype> Profile = "OrganicBodyPartProfile";

    /// <summary>Resolved wounds container. Assigned on init; runtime only.</summary>
    [ViewVariables]
    public Container WoundsContainer = default!;

    /// <summary>Excess damage kept toward future dismemberment. Runtime only.</summary>
    [ViewVariables]
    public FixedPoint2 AmputationOverflow;

    /// <summary>Whether the part is currently severable by damage.</summary>
    [AutoNetworkedField]
    public bool Severable;
}

/// <summary>
/// Aggregated usability of a body part, recomputed from wounds and fractures.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyPartFunctionalityComponent : Component
{
    [AutoNetworkedField]
    public BodyPartFunctionalityState State = BodyPartFunctionalityState.Functional;
}

/// <summary>
/// A single wound instance held by a body part. Severity is the single source of truth;
/// symptoms (bleeding, fracture, functionality) are derived by <see cref="WoundSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundComponent : Component
{
    /// <summary>Part holding this wound.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid HoldingPart;

    [DataField(required: true), AutoNetworkedField]
    public ProtoId<WoundPrototype> Prototype;

    /// <summary>Current severity. Zero means healed and removed.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Severity;

    /// <summary>Highest severity reached. Used for scarring thresholds.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 PeakSeverity;

    [DataField, AutoNetworkedField]
    public WoundState State = WoundState.Open;

    /// <summary>Guards against double scar rolls for the current closure. Runtime only.</summary>
    [ViewVariables]
    public bool ScarCreatedForCurrentClosure;
}

/// <summary>Derived usability contribution of a single wound.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundFunctionalityComponent : Component
{
    [DataField, AutoNetworkedField]
    public BodyPartFunctionalityState State;
}

/// <summary>External bleeding state of a wound. Rates are recomputed from severity and treatment.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundBleedingComponent : Component
{
    /// <summary>Base bleeding rate before multipliers.</summary>
    [DataField, AutoNetworkedField]
    public float BaseRate;

    /// <summary>Effective rate after treatment, sleep and clotting. Drives blood loss.</summary>
    [DataField, AutoNetworkedField]
    public float CurrentRate;

    /// <summary>Severity slice that bleeds. Grows on worsening, shrinks on treatment.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 BleedingSeverity;

    [DataField, AutoNetworkedField]
    public BleedingTreatment Treatment;

    /// <summary>Passive clotting progress. Runtime only.</summary>
    [DataField, AutoNetworkedField]
    public float NaturalClotting;

    [ViewVariables]
    public TimeSpan? AutomaticClottingStartedAt;

    [ViewVariables]
    public TimeSpan? AutomaticClottingAt;

}

/// <summary>Internal bleeding state: blood lost directly from the bloodstream.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundInternalBleedingComponent : Component
{
    /// <summary>Blood units lost per severity per second.</summary>
    [DataField, AutoNetworkedField]
    public float Rate;

    /// <summary>Currently bleeding severity. Zero while the wound is not open.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Severity;
}

/// <summary>Bone fracture state attached to a fracture wound.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundFractureComponent : Component
{
    /// <summary>
    /// Cached severity grade. Bone damage is the wound's <see cref="WoundComponent.Severity"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FractureGrade Grade;

    [DataField, AutoNetworkedField]
    public FractureTreatment Treatment;
}

/// <summary>Healed wound converted into a permanent scar. Blocks treatment and severity changes.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WoundScarComponent : Component;

/// <summary>Lifecycle state of a wound.</summary>
[Serializable, NetSerializable]
public enum WoundState : byte
{
    Open,
    Stabilized,
    Closed,
    Healed,
    Scarred,
}

/// <summary>Treatment applied to a bleeding wound. Scales the effective rate.</summary>
[Serializable, NetSerializable]
public enum BleedingTreatment : byte
{
    None,
    Bandaged,
    Clamped,
    Sutured,
    Cauterized,
}

/// <summary>Bone damage grade derived from fracture severity.</summary>
[Serializable, NetSerializable]
public enum FractureGrade : byte
{
    None,
    Hairline,
    Simple,
    Displaced,
    Comminuted,
}

/// <summary>Fracture care stage.</summary>
[Serializable, NetSerializable]
public enum FractureTreatment : byte
{
    None,
    Reduced,
    Mended,
}

/// <summary>Damage kept systemically on the wound host (non-localized types).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SystemicDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();
}

