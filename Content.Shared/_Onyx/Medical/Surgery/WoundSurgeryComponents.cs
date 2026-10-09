using Content.Shared._Onyx.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

/// <summary>Requires a matching wound on the operated part (prototype, state, bleeding).</summary>
[RegisterComponent]
public sealed partial class SurgeryHasWoundConditionComponent : Component
{
    [DataField]
    public ProtoId<WoundPrototype>? WoundPrototype;

    [DataField]
    public WoundVisibility? Visibility;

    [DataField]
    public WoundState? State;

    [DataField]
    public bool Bleeding;

    [DataField]
    public bool InternalBleeding;
}

/// <summary>Reduces bleeding severity of a matching wound.</summary>
[RegisterComponent]
public sealed partial class SurgeryClampBleedingEffectComponent : Component
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField]
    public ProtoId<WoundPrototype>? WoundPrototype;
}

/// <summary>Requires a fracture of at least the given grade and treatment stage.</summary>
[RegisterComponent]
public sealed partial class SurgeryFractureGradeConditionComponent : Component
{
    [DataField]
    public FractureGrade MinGrade = FractureGrade.Hairline;

    [DataField]
    public FractureGrade? Grade;

    [DataField]
    public FractureTreatment? Treatment;
}

/// <summary>Fully mends the fracture on the operated part.</summary>
[RegisterComponent]
public sealed partial class SurgeryMendFractureEffectComponent : Component;

/// <summary>Reduces a displaced fracture so it can be mended.</summary>
[RegisterComponent]
public sealed partial class SurgeryReduceFractureEffectComponent : Component;

/// <summary>Requires nerve damage above the threshold on the operated part.</summary>
[RegisterComponent]
public sealed partial class SurgeryNerveDamageConditionComponent : Component
{
    [DataField]
    public FixedPoint2 MinimumDamage = FixedPoint2.New(0.01f);
}

/// <summary>Repairs nerve damage on the operated part.</summary>
[RegisterComponent]
public sealed partial class SurgeryRepairNerveEffectComponent : Component
{
    [DataField]
    public FixedPoint2 Amount = FixedPoint2.MaxValue;
}

/// <summary>Treats a matching wound by the given severity amount.</summary>
[RegisterComponent]
public sealed partial class SurgeryTreatWoundEffectComponent : Component
{
    [DataField]
    public ProtoId<WoundPrototype>? WoundPrototype;

    [DataField]
    public ProtoId<DamageGroupPrototype>? DamageGroup;

    [DataField]
    public bool InternalBleeding;

    [DataField]
    public FixedPoint2 Amount = FixedPoint2.MaxValue;

    [DataField]
    public DamageSpecifier Damage = new();
}

/// <summary>Requires wound severity in the given damage group within the band.</summary>
[RegisterComponent]
public sealed partial class SurgeryWoundedConditionComponent : Component
{
    [DataField]
    public ProtoId<DamageGroupPrototype> DamageGroup = "Brute";

    [DataField]
    public FixedPoint2 MinSeverity = FixedPoint2.Zero;

    [DataField]
    public FixedPoint2 MaxSeverity = FixedPoint2.MaxValue;
}

/// <summary>Tends all wounds of a damage group plus a bonus heal scaled by severity.</summary>
[RegisterComponent]
public sealed partial class SurgeryTendWoundsEffectComponent : Component
{
    [DataField]
    public ProtoId<DamageGroupPrototype> DamageGroup = "Brute";

    [DataField(required: true)]
    public SurgeryTendWoundsDamage Damage = new();

    [DataField]
    public float HealMultiplier = 0.07f;

    [DataField]
    public bool HealDamage = true;

    [DataField]
    public bool HealWounds = true;
}

/// <summary>Healing payload for a tend-wounds step: per-type and per-group amounts.</summary>
[DataDefinition]
public sealed partial class SurgeryTendWoundsDamage
{
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Types = new();

    [DataField]
    public Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> Groups = new();
}
