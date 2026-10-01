// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Power.Components;

#pragma warning disable IDE0130
namespace Content.Shared.Power.EntitySystems;

public sealed partial class ChargerSystem
{
    public void RefreshTieredPartEffects(Entity<ChargerComponent> ent)
    {
        RefreshAllBatteries(ent);
        UpdateStatus(ent);
    }
}
