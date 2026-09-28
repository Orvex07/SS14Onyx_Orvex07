// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Body;
using Content.Shared.FixedPoint;

namespace Content.Shared._Onyx.Wounds;

/// <summary>
/// Wound-to-nerve pain affliction: wound severity is aggregated per part into
/// the <c>WoundPain</c> modifier of the body's nervous hub, mirroring Goob-Station.
/// Called explicitly by <see cref="WoundStatusEffectSystem"/>, the single subscriber
/// of the wound lifecycle events.
/// </summary>
public partial class PainSystem
{
    private const string WoundPainRecoveryIdentifier = "WoundPainRecovery";
    private const float PainRecoverySecondsPerPoint = 6f;

    private static string OneTimeIdentifier(EntityUid woundUid) => $"OneTime:{woundUid}";

    /// <summary>
    /// Recomputes the persistent wound pain floor of a part from its active wounds.
    /// </summary>
    public void RecomputeWoundPain(EntityUid part)
    {
        if (!_net.IsServer)
            return;

        if (!TryGetHub(part, out var hub, out _))
            return;

        EnsureNerveAttached(hub.Owner, part, hub.Comp);

        if (TerminatingOrDeleted(part) || !CanFeelPain(part))
        {
            TryRemovePainModifier(hub.Owner, part, WoundPainIdentifier, hub.Comp);
            return;
        }

        var floor = FixedPoint2.Zero;
        foreach (var wound in _wounds.GetWounds((part, CompOrNull<WoundableComponent>(part))))
        {
            if (wound.Comp.State is WoundState.Healed or WoundState.Scarred ||
                !_prototypes.TryIndex(wound.Comp.Prototype, out var prototype) ||
                !prototype.TryGetBehavior(wound.Comp.Severity, out WoundPainBehavior behavior))
                continue;

            if (behavior.MinSeverity is { } minimum && wound.Comp.Severity < minimum)
                continue;

            if (behavior.OneTime)
                continue;

            floor += wound.Comp.Severity * behavior.PainPerSeverity;
        }

        var recoveryPain = FixedPoint2.Zero;
        if (TryGetPainModifier(hub.Owner, part, WoundPainIdentifier, out var existing, hub.Comp))
            recoveryPain = FixedPoint2.Max(FixedPoint2.Zero, existing.Value.Change - floor);

        if (floor <= FixedPoint2.Zero)
            TryRemovePainModifier(hub.Owner, part, WoundPainIdentifier, hub.Comp);
        else if (!TryChangePainModifier(hub.Owner, part, WoundPainIdentifier, floor,
                     PainDamageTypes.WoundPain, hub.Comp))
            TryAddPainModifier(hub.Owner, part, WoundPainIdentifier, floor,
                PainDamageTypes.WoundPain, hub.Comp);

        if (recoveryPain > FixedPoint2.Zero)
            AddRecoveryPain(hub, part, recoveryPain);
    }

    /// <summary>
    /// One-time pain spike produced when a wound with a one-time pain behavior worsens.
    /// The spike decays linearly instead of vanishing when its timer expires.
    /// </summary>
    public void ApplyOneTimeSpike(EntityUid part, EntityUid woundUid, FixedPoint2 delta)
    {
        if (!_net.IsServer || delta <= FixedPoint2.Zero)
            return;

        if (!TryComp(woundUid, out WoundComponent? wound) ||
            !_prototypes.TryIndex(wound.Prototype, out var prototype) ||
            !prototype.TryGetBehavior(wound.Severity, out WoundPainBehavior behavior) ||
            !behavior.OneTime)
            return;

        if (behavior.MinSeverity is { } minimum && wound.Severity < minimum)
            return;

        if (!TryGetHub(part, out var hub, out _) || !CanFeelPain(part))
            return;

        var spike = delta * behavior.PainPerSeverity;
        if (spike <= FixedPoint2.Zero)
            return;

        var identifier = OneTimeIdentifier(woundUid);
        if (TryGetPainModifier(hub.Owner, part, identifier, out var existing, hub.Comp))
        {
            var total = existing.Value.Change + spike;
            SetDecayingPain(hub, part, identifier, total);
        }
        else
        {
            SetDecayingPain(hub, part, identifier, spike);
        }
    }

    private void AddRecoveryPain(Entity<NervousSystemComponent> hub, EntityUid part, FixedPoint2 amount)
    {
        if (TryGetPainModifier(hub.Owner, part, WoundPainRecoveryIdentifier, out var existing, hub.Comp))
            amount += existing.Value.Change;

        SetDecayingPain(hub, part, WoundPainRecoveryIdentifier, amount);
    }

    private void SetDecayingPain(
        Entity<NervousSystemComponent> hub,
        EntityUid part,
        string identifier,
        FixedPoint2 amount)
    {
        var decayPerSecond = FixedPoint2.New(1f / PainRecoverySecondsPerPoint);

        if (!TryChangePainModifier(hub.Owner, part, identifier, amount,
                PainDamageTypes.WoundPain, hub.Comp))
        {
            TryAddPainModifier(hub.Owner, part, identifier, amount,
                PainDamageTypes.WoundPain, hub.Comp);
        }

        if (hub.Comp.Modifiers.TryGetValue((part, identifier), out var modifier))
        {
            hub.Comp.Modifiers[(part, identifier)] = modifier with
            {
                Time = null,
                DecayPerSecond = decayPerSecond,
            };
        }

        Dirty(hub.Owner, hub.Comp);
    }
}
