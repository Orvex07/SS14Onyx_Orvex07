using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Medical.Surgery;

/// <summary>
/// Shared sterility tuning: protection points stack across worn gear,
/// reaching <see cref="FullProtection"/> or more rules out infection entirely.
/// </summary>
public static class SurgerySterility
{
    public const float FullProtection = 100f;

    /// <summary>
    /// Sterility points lost per soiled protection item worn by the surgeon.
    /// </summary>
    public const float SoiledPenalty = 25f;

    /// <summary>
    /// Sterility points lost when operating bare-handed (no gloves equipped).
    /// Bare hands carry the surgeon's own flora, so they can never be sterile.
    /// </summary>
    public const float BareHandsPenalty = 25f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryInfectionProtectionComponent : Component
{
    /// <summary>
    /// Sterility points this item provides while clean.
    /// Points from all worn items stack; 100 or more means full protection.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Protection;
}

/// <summary>
/// Marks sterile gear stained with blood. The owner's own blood is harmless to them;
/// it threatens other patients (cross-contamination): grants no protection
/// and lowers total sterility when operating on anyone else.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgerySoiledComponent : Component
{
    /// <summary>
    /// Patient whose blood stained this item.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? Source;
}

[RegisterComponent]
public sealed partial class SurgicalSiteInfectionComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryInfectionCooldownComponent : Component
{
    [ViewVariables]
    public TimeSpan NextAttempt;
}
