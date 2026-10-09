using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

/// <summary>Inflicts surgery pain on step completion. Skipped on mechanical steps.</summary>
[RegisterComponent]
public sealed partial class SurgeryStepPainInflicterComponent : Component
{
    [DataField] public FixedPoint2 Amount = 5;
    [DataField] public FixedPoint2 SleepModifier = 1;

    /// <summary>
    /// How long the inflicted surgery pain lasts on the nervous hub.
    /// </summary>
    [DataField] public TimeSpan PainDuration = TimeSpan.FromSeconds(30);
}

[RegisterComponent, NetworkedComponent] public sealed partial class BodyPartReattachedComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class BodyPartMendedComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class BodyPartSuturedComponent : Component;

/// <summary>Opens a surgical incision wound dealing the given damage.</summary>
[RegisterComponent]
public sealed partial class SurgeryStepBleedEffectComponent : Component
{
    [DataField] public int Damage;
}

/// <summary>Clamps incision bleeding on the operated part.</summary>
[RegisterComponent] public sealed partial class SurgeryClampBleedEffectComponent : Component;

/// <summary>Closes, scars and removes the surgical incision wound.</summary>
[RegisterComponent] public sealed partial class SurgeryCloseIncisionEffectComponent : Component;

/// <summary>Detaches the operated part from the body.</summary>
[RegisterComponent] public sealed partial class SurgeryDetachPartEffectComponent : Component;

/// <summary>Attaches a held part of the given type to the operated part.</summary>
[RegisterComponent]
public sealed partial class SurgeryAttachPartEffectComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

/// <summary>Advances a reattached part to the mended stage.</summary>
[RegisterComponent]
public sealed partial class SurgeryMendAttachedPartEffectComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

/// <summary>Advances a mended part to the sutured stage.</summary>
[RegisterComponent]
public sealed partial class SurgerySutureAttachedPartEffectComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

/// <summary>Heals an organ in the given slot by the given amount.</summary>
[RegisterComponent]
public sealed partial class SurgeryOrganHealEffectComponent : Component
{
    [DataField(required: true)] public ProtoId<OrganCategoryPrototype> Slot;
    [DataField(required: true)] public FixedPoint2 Amount;
}

/// <summary>Removes a matching organ from the operated part into the surgeon's hands.</summary>
[RegisterComponent]
public sealed partial class SurgeryRemoveOrganEffectComponent : Component
{
    [DataField] public ProtoId<OrganCategoryPrototype>? Slot;
    [DataField] public ComponentRegistry Required = new();
}

/// <summary>Inserts a held organ into the given slot of the operated part.</summary>
[RegisterComponent]
public sealed partial class SurgeryInsertOrganEffectComponent : Component
{
    [DataField(required: true)] public ProtoId<OrganCategoryPrototype> Slot;
    [DataField] public bool RequireMechanical;
    [DataField] public ComponentRegistry? Required;
}

/// <summary>Stores a held small item into the part cavity.</summary>
[RegisterComponent] public sealed partial class SurgeryInsertCavityItemEffectComponent : Component;
/// <summary>Returns the cavity item into the surgeon's hands.</summary>
[RegisterComponent] public sealed partial class SurgeryRemoveCavityItemEffectComponent : Component;

/// <summary>Adds and removes components on the patient or operated body part.</summary>
[RegisterComponent]
public sealed partial class SurgeryComponentEffectComponent : Component
{
    [DataField]
    public SurgeryEntityTarget Target;

    [DataField]
    public ComponentRegistry Add = new();

    [DataField]
    public ComponentRegistry Remove = new();
}

[RegisterComponent] public sealed partial class SurgicallyPacifiedComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryMutingEffectComponent : Component
{
    [DataField] public bool Remove;
}

[RegisterComponent]
public sealed partial class SurgeryStepEmoteEffectComponent : Component
{
    [DataField] public string Emote = "Scream";
}

[RegisterComponent]
public sealed partial class SurgeryDamageEffectComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public SurgeryEntityTarget Target = SurgeryEntityTarget.Part;

    [DataField]
    public bool HealWounds;
}

[RegisterComponent]
public sealed partial class SurgeryFailureDamageComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public SurgeryEntityTarget Target = SurgeryEntityTarget.Part;
}

/// <summary>Removes a bounded proportion of each non-blood reagent per surgery cycle.</summary>
[RegisterComponent]
public sealed partial class SurgeryBloodFilterEffectComponent : Component
{
    [DataField] public float Proportion = 0.22f;
    [DataField] public FixedPoint2 Minimum = 0.4;
    [DataField] public FixedPoint2 Maximum = 10;
}
