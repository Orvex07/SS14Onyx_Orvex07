using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

/// <summary>Requires the patient to be buckled to an operating table.</summary>
[RegisterComponent] public sealed partial class SurgeryOperatingTableConditionComponent : Component;
[RegisterComponent]
public sealed partial class OperatingTableComponent : Component
{
    [ViewVariables]
    public EntityUid? Scanner;
}

/// <summary>Gates a step on the operated part type and symmetry.</summary>
[RegisterComponent]
public sealed partial class SurgeryPartConditionComponent : Component
{
    [DataField] public BodyPartType? Part;
    [DataField] public HashSet<BodyPartType> Parts = new();
    [DataField] public BodyPartSymmetry? Symmetry;
    [DataField] public bool Inverse;
}

/// <summary>Gates a step on completed-step markers of the operated part.</summary>
[RegisterComponent]
public sealed partial class SurgeryMarkerConditionComponent : Component
{
    [DataField] public HashSet<string> All = new();
    [DataField] public HashSet<string> Any = new();
    [DataField] public HashSet<string> None = new();
    [DataField] public HashSet<string> MissingAny = new();
}

/// <summary>Gates a step on patient or part species.</summary>
[RegisterComponent]
public sealed partial class SurgerySpeciesConditionComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<SpeciesPrototype>> Species = new();

    [DataField]
    public bool Inverse;
}

/// <summary>
/// Gates surgery on an organ tag. Without inversion requires the operated part to contain an
/// organ with the tag (removal). With inversion requires the organ being implanted to have the
/// tag (insertion, placed on the insert step).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganTagConditionComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TagPrototype> Tag;

    [DataField]
    public bool Inverse;
}

[RegisterComponent]
public sealed partial class SurgeryOrganConditionComponent : Component
{
    [DataField] public ProtoId<OrganCategoryPrototype>? Slot;
    [DataField] public ComponentRegistry Required = new();
    [DataField] public bool Inverse;
    [DataField] public bool Damaged;
    [DataField] public BodyPartType? Part;
}

/// <summary>Requires a missing (detached) body part of the given type.</summary>
[RegisterComponent]
public sealed partial class SurgeryMissingPartConditionComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

/// <summary>Requires the operated part to still be attached to its parent.</summary>
[RegisterComponent] public sealed partial class SurgeryDetachablePartConditionComponent : Component;

/// <summary>Checks component presence on the patient or operated body part.</summary>
[RegisterComponent]
public sealed partial class SurgeryComponentConditionComponent : Component
{
    [DataField]
    public SurgeryEntityTarget Target;

    [DataField]
    public ComponentRegistry All = new();

    [DataField]
    public ComponentRegistry None = new();
}

/// <summary>Requires the body cavity to be occupied (or empty when not set).</summary>
[RegisterComponent]
public sealed partial class SurgeryCavityConditionComponent : Component
{
    [DataField] public bool Occupied;
}

[RegisterComponent]
public sealed partial class SurgeryMutingConditionComponent : Component
{
    [DataField] public bool Muted;
}

/// <summary>Requires the patient to have removable chemicals in their bloodstream.</summary>
[RegisterComponent]
public sealed partial class SurgeryBloodstreamConditionComponent : Component;
