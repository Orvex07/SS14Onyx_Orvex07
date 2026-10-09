using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Medical.Surgery;

/// <summary>
/// Tool quality profile: per-task speed and success modifiers plus examine hints and sounds.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSurgerySystem), typeof(SurgeryToolExamineSystem))]
public sealed partial class SurgeryToolComponent : Component
{
    [DataField, AutoNetworkedField] public Dictionary<string, float> SpeedModifiers = new();
    [DataField, AutoNetworkedField] public Dictionary<string, float> SuccessModifiers = new();
    [DataField, AutoNetworkedField] public List<LocId> CustomUses = new();
    /// <summary>Sound played when a step starts with this tool.</summary>
    [DataField, AutoNetworkedField] public SoundSpecifier? StartSound;
    /// <summary>Sound played when a step finishes with this tool.</summary>
    [DataField, AutoNetworkedField] public SoundSpecifier? EndSound;
}

[RegisterComponent] public sealed partial class ScalpelComponent : Component;
[RegisterComponent] public sealed partial class HemostatComponent : Component;
[RegisterComponent] public sealed partial class RetractorComponent : Component;
[RegisterComponent] public sealed partial class BoneSawComponent : Component;
[RegisterComponent] public sealed partial class CauteryComponent : Component;
[RegisterComponent] public sealed partial class BoneGelComponent : Component;
[RegisterComponent] public sealed partial class TweezersComponent : Component;
[RegisterComponent] public sealed partial class DrillComponent : Component;
[RegisterComponent] public sealed partial class StitchesComponent : Component;
[RegisterComponent] public sealed partial class TendingComponent : Component;

/// <summary>Marks a tool capable of filtering chemicals from a patient's bloodstream.</summary>
[RegisterComponent]
public sealed partial class BloodFilterComponent : Component;
