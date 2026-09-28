// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Consciousness;

[Serializable, NetSerializable]
public enum ConsciousnessModType : byte
{
    Generic,
    Pain,
}

[Serializable, NetSerializable]
public partial record struct ConsciousnessModifier(FixedPoint2 Change, TimeSpan? Time, ConsciousnessModType Type = ConsciousnessModType.Generic);

[Serializable, NetSerializable]
public partial record struct ConsciousnessMultiplier(FixedPoint2 Change, TimeSpan? Time, ConsciousnessModType Type = ConsciousnessModType.Generic);

/// <summary>
/// Raised on a body when its consciousness flips.
/// </summary>
[ByRefEvent]
public record struct ConsciousnessUpdatedEvent(bool IsConscious);

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsciousnessComponent : Component
{
    /// <summary>
    /// Consciousness at or below which the entity falls unconscious.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Threshold = 95;

    /// <summary>
    /// Base consciousness before modifiers. Negative means "use Cap on init".
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 RawConsciousness = -1;

    /// <summary>
    /// Consciousness after the multiplier, clamped between 0 and Cap.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 Consciousness => FixedPoint2.Clamp(RawConsciousness * Multiplier, 0, Cap);

    [DataField, AutoNetworkedField]
    public FixedPoint2 Multiplier = FixedPoint2.New(1f);

    /// <summary>
    /// Maximum consciousness. Also the default RawConsciousness.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Cap = 190;

    /// <summary>
    /// Server-side modifiers. Not networked; clients see the resulting values.
    /// </summary>
    [ViewVariables]
    public Dictionary<(EntityUid, string), ConsciousnessModifier> Modifiers = new();

    [ViewVariables]
    public Dictionary<(EntityUid, string), ConsciousnessMultiplier> Multipliers = new();

    /// <summary>
    /// Required parts/organs: identifier to (entity, causesDeath, isLost).
    /// </summary>
    [ViewVariables]
    public Dictionary<string, (EntityUid?, bool, bool)> RequiredConsciousnessParts = new();

    [DataField]
    public TimeSpan ConsciousnessUpdateTime = TimeSpan.FromSeconds(0.8f);

    [ViewVariables]
    public TimeSpan NextConsciousnessUpdate;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool PassedOut;

    [ViewVariables]
    public TimeSpan PassedOutTime = TimeSpan.Zero;

    [ViewVariables]
    public TimeSpan ForceConsciousnessTime = TimeSpan.Zero;

    [ViewVariables]
    public bool ForceDead;

    [ViewVariables]
    public bool ForceUnconscious;

    [ViewVariables]
    public bool ForceConscious;

    [ViewVariables, AutoNetworkedField]
    public bool IsConscious = true;

    [DataField, AutoNetworkedField]
    public bool HasPainScreams = true;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsciousnessRequiredComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Identifier = "requiredConsciousnessPart";

    /// <summary>
    /// Losing this part means death, otherwise only unconsciousness.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CausesDeath = true;
}
