// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Construction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TieredMachinePartExamineComponent : Component
{
    /// <summary>
    /// Installed machine parts grouped by kind and tier.
    /// </summary>
    [AutoNetworkedField]
    public List<MachinePartExamineEntry> Parts = [];

    /// <summary>
    /// Effective machine parameter multipliers produced by installed parts.
    /// </summary>
    [AutoNetworkedField]
    public List<MachineUpgradeExamineEntry> Upgrades = [];
}

[Serializable, NetSerializable]
public readonly record struct MachinePartExamineEntry(MachinePartKind Kind, int Tier, int Amount);

[Serializable, NetSerializable]
public readonly record struct MachineUpgradeExamineEntry(LocId Name, float Value, bool Percentage);
