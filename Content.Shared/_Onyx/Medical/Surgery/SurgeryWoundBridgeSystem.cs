// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Medical.Surgery;

/// <summary>
/// Bridge between the surgery domain and the wound domain.
/// Surgery never touches wound internals directly; all wound-aware
/// surgery damage goes through here. Without <see cref="WoundHostComponent"/>
/// the vanilla systemic path is used, so surgery keeps working on bodies
/// that opt out of localized damage.
/// </summary>
public sealed partial class SurgeryWoundBridgeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundDamageRoutingSystem _woundRouting = default!;
    [Dependency] private INetManager _net = default!;

    public void ApplySurgeryDamage(
        EntityUid body,
        EntityUid part,
        EntityUid user,
        SurgeryEntityTarget target,
        DamageSpecifier damage,
        bool healWounds)
    {
        if (!_net.IsServer || damage.Empty)
            return;

        var change = new DamageSpecifier(damage);
        switch (target)
        {
            case SurgeryEntityTarget.Body:
                if (!_woundRouting.TryApplyDamage(body, change, user, healWounds: healWounds))
                    _damageable.TryChangeDamage(body, change, origin: user);
                break;
            case SurgeryEntityTarget.Part:
                if (!_woundRouting.TryApplyPartDamage(body, part, change, user, healWounds: healWounds))
                    _damageable.TryChangeDamage(part, change, origin: user);
                break;
            case SurgeryEntityTarget.User:
                _damageable.TryChangeDamage(user, change, origin: body);
                break;
            default:
                break;
        }
    }
}
