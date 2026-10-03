// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Clothing;
using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Body.Systems;

public sealed partial class BloodstreamSystem
{
    private void DirtyFromBleeding(Entity<BloodstreamComponent> entity, float bleed)
    {
        var amount = FixedPoint2.Min(FixedPoint2.New(bleed), FixedPoint2.New(1));
        if (amount <= 0)
            return;

        var blood = entity.Comp.BloodReferenceSolution.Clone();
        blood.SetReagentData(GetEntityBloodData(entity.AsNullable()));
        blood.ScaleTo(amount);
        var dirtEvent = new BleedingDirtEvent(blood, amount);
        RaiseLocalEvent(entity, ref dirtEvent);
    }
}
