// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Wounds;
using Content.Shared.Changeling.Components;

namespace Content.Shared._Onyx.Changeling;

public sealed partial class ChangelingAmputationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChangelingIdentityComponent, BeforeAmputationDamageEvent>(OnBeforeAmputationDamage);
    }

    private void OnBeforeAmputationDamage(Entity<ChangelingIdentityComponent> entity,
        ref BeforeAmputationDamageEvent args)
    {
        args.Cancelled = true;
    }
}
