// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared.Armor;

public sealed partial class ArmorComponent
{
    /// <summary>Trauma protection applied after existing part and symmetry coverage is resolved.</summary>
    [DataField]
    public Dictionary<TraumaType, float> TraumaProtection = new();
}

public sealed partial class ArmorPartModifier
{
    /// <summary>Location-specific trauma protection. Empty falls back to armor-wide values.</summary>
    [DataField]
    public Dictionary<TraumaType, float> TraumaProtection = new();
}

[Serializable, NetSerializable]
public enum TraumaType : byte
{
    Fracture,
    Dismemberment,
    OrganDamage,
    NerveDamage,
}

public sealed class TraumaProtectionQueryEvent(
    BodyPartType partType,
    BodyPartSymmetry symmetry,
    TraumaType trauma) : EntityEventArgs, IInventoryRelayEvent
{
    public BodyPartType PartType = partType;
    public BodyPartSymmetry Symmetry = symmetry;
    public TraumaType Trauma = trauma;
    public float Protection;
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}
