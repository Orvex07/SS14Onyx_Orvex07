// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos.Piping.Portable.Components;
using Content.Shared.Atmos.Piping.Unary.Components;

#pragma warning disable IDE0130
namespace Content.Server.Atmos.Portable;

public sealed partial class SpaceHeaterSystem
{
    public void RefreshTieredPartEffects(Entity<SpaceHeaterComponent> ent, GasThermoMachineComponent thermoMachine)
    {
        thermoMachine.HeatCapacity = ent.Comp.PowerLevel switch
        {
            SpaceHeaterPowerLevel.Low => ent.Comp.PowerConsumption / 2f,
            SpaceHeaterPowerLevel.High => ent.Comp.PowerConsumption * 2f,
            _ => ent.Comp.PowerConsumption,
        };
        DirtyUI(ent, ent.Comp);
    }
}
