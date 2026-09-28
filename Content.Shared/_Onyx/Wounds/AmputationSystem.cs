using System.Numerics;
using Content.Shared.Armor;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Throwing;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class AmputationSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private WoundDamageRoutingSystem _routing = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private TraumaProtectionSystem _traumaProtection = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;

    private const float ExplosionIntegrityExponent = 1.3f;
    private const float ExplosionDismembermentSeverityGate = 10f;
    private const float PiercingDismembermentMultiplier = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundableComponent, PartDamageOverflowedEvent>(OnPartDamageOverflowed);
    }

    private void OnPartDamageOverflowed(Entity<WoundableComponent> part, ref PartDamageOverflowedEvent args)
    {
        if (!_net.IsServer ||
            !TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            bodyPart.PartType == BodyPartType.Chest ||
            bodyPart.MaxDamage <= FixedPoint2.Zero)
            return;

        var protection = _traumaProtection.GetProtection(args.Body, bodyPart, TraumaType.Dismemberment);
        if (args.IsExplosion && args.ExplosionAmputationCandidate &&
            TryExplosionAmputate(args.Body, part, bodyPart, args.Damage,
                args.WoundSeverityMultiplier, protection: protection))
            return;

        if (!part.Comp.Severable)
        {
            if (part.Comp.AmputationOverflow >= bodyPart.MaxDamage)
            {
                if (_random.Prob(Math.Clamp(1f - protection, 0f, 1f)))
                    SetSeverable(part, true);
            }
            return;
        }

        if (IsFinishingHit(args.Body, bodyPart, args.Damage))
            TryAmputate(args.Body, part.Owner);
    }

    public void ApplyAmputationConsequences(EntityUid body, EntityUid parent)
    {
        if (!_net.IsServer || !HasComp<WoundableComponent>(parent) ||
            !TryComp(parent, out BodyPartComponent? parentPart))
            return;

        if (!TryComp(body, out WoundHostComponent? host))
            return;

        _wounds.CreateOrMergeWound(parent, host.AmputationConsequenceWound, parentPart.AmputationConsequenceSeverity);
    }

    public void HandlePartDamageApplied(Entity<WoundableComponent> part, ref PartDamageAppliedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            bodyPart.PartType is BodyPartType.Chest || bodyPart.Parent == null ||
            bodyPart.AmputationThresholds.Count == 0 ||
            !TryComp(part, out DamageableComponent? damageable))
            return;

        var protection = _traumaProtection.GetProtection(args.Body, bodyPart, TraumaType.Dismemberment);
        var damage = _damageable.GetAllDamage((part.Owner, damageable));
        if (args.IsExplosion && args.ExplosionAmputationCandidate &&
            TryExplosionAmputate(args.Body, part, bodyPart, args.Damage,
                args.WoundSeverityMultiplier, protection))
            return;

        if (!part.Comp.Severable)
        {
            if (ReachedThreshold(damage, bodyPart.AmputationThresholds))
            {
                if (_random.Prob(Math.Clamp(1f - protection, 0f, 1f)))
                    SetSeverable(part, true);
            }
            return;
        }

        if (GetThresholdProgress(damage, bodyPart.AmputationThresholds) < GetResetRatio(args.Body))
        {
            SetSeverable(part, false);
            return;
        }

        var damageBeforeHit = damage.Clone();
        foreach (var (type, amount) in args.Damage.DamageDict)
            if (amount > FixedPoint2.Zero)
                damageBeforeHit.DamageDict[type] = FixedPoint2.Max(FixedPoint2.Zero,
                    damageBeforeHit.DamageDict.GetValueOrDefault(type) - amount);

        if (ReachedThreshold(damageBeforeHit, bodyPart.AmputationThresholds) &&
            IsFinishingHit(args.Body, bodyPart, args.Damage))
            TryAmputate(args.Body, part.Owner);
    }

    /// <summary>
    /// Detaches the part deterministically and applies amputation consequences.
    /// </summary>
    public bool TryAmputate(EntityUid body, EntityUid part)
    {
        if (!_net.IsServer || !TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            bodyPart.PartType == BodyPartType.Chest)
            return false;

        var parent = bodyPart.Parent ?? part;
        if (!_body.TryDetachPart(part))
            return false;

        var beforeDamage = new BeforeAmputationDamageEvent();
        RaiseLocalEvent(body, ref beforeDamage);
        if (!beforeDamage.Cancelled && bodyPart.DamageOnAmputate is { } damage)
            _routing.TryApplyAmputationDamage(body, parent, damage);

        if (TryComp(body, out WoundHostComponent? host))
            _wounds.CreateOrMergeWound(parent, host.DismembermentWound,
                bodyPart.DismembermentSeverity ?? GetDismembermentSeverity(host, bodyPart.PartType));
        ApplyAmputationConsequences(body, parent);
        _throwing.TryThrow(part, Vector2.UnitY, baseThrowSpeed: 3f,
            pushbackRatio: 0f, doSpin: true);
        return true;
    }

    private static bool ReachedThreshold(
        DamageSpecifier damage,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> thresholds)
    {
        return GetThresholdProgress(damage, thresholds) >= 1f;
    }

    private static float GetThresholdProgress(
        DamageSpecifier damage,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> thresholds)
    {
        var progress = 0f;
        foreach (var (type, threshold) in thresholds)
        {
            if (threshold <= FixedPoint2.Zero)
                continue;
            progress += (float) damage.DamageDict.GetValueOrDefault(type) / (float) threshold;
        }
        return progress;
    }

    private bool IsFinishingHit(EntityUid body, BodyPartComponent part, DamageSpecifier damage)
    {
        if (!TryComp(body, out WoundHostComponent? host))
            return false;

        foreach (var (type, amount) in damage.DamageDict)
        {
            if (amount <= FixedPoint2.Zero || !part.AmputationThresholds.ContainsKey(type))
                continue;

            var minimum = part.DismembermentFinishingDamage.GetValueOrDefault(type,
                host.DefaultDismembermentFinishingDamage.GetValueOrDefault(type));
            if (minimum > FixedPoint2.Zero && amount >= minimum)
                return true;
        }

        return false;
    }

    private bool TryExplosionAmputate(
        EntityUid body,
        Entity<WoundableComponent> part,
        BodyPartComponent bodyPart,
        DamageSpecifier hit,
        float woundSeverityMultiplier,
        float protection = 0f)
    {
        if (!TryComp(part, out DamageableComponent? damageable))
            return false;

        var integrityCap = GetIntegrityCap(bodyPart);
        if (integrityCap <= 0f)
            return false;

        var currentDamage = _damageable.GetPositiveDamage((part.Owner, damageable)).GetTotal().Float();
        var integrity = Math.Max(0f, integrityCap - currentDamage);
        var fractureMultiplier = GetFractureMultiplier(part);
        foreach (var (type, amount) in hit.DamageDict)
        {
            if (type != "Blunt" && type != "Piercing" && type != "Slash" ||
                amount.Float() * woundSeverityMultiplier < ExplosionDismembermentSeverityGate)
                continue;

            var damageTypeMultiplier = type == "Piercing" ? PiercingDismembermentMultiplier : 1f;
            var chance = Math.Clamp(
                (1f - (MathF.Pow(integrity, ExplosionIntegrityExponent) / integrityCap - 1f) * fractureMultiplier)
                * damageTypeMultiplier - protection,
                0f,
                1f);
            if (chance > 0f && _random.Prob(chance) && TryAmputate(body, part.Owner))
                return true;
        }

        return false;
    }

    private static float GetIntegrityCap(BodyPartComponent part)
    {
        if (part.MaxDamage > FixedPoint2.Zero)
            return part.MaxDamage.Float();

        var cap = float.MaxValue;
        foreach (var threshold in part.AmputationThresholds.Values)
            if (threshold > FixedPoint2.Zero)
                cap = Math.Min(cap, threshold.Float());

        return cap == float.MaxValue ? 0f : cap;
    }

    private float GetFractureMultiplier(EntityUid part)
    {
        return _fractures.GetFracture(part)?.Comp2.Grade switch
        {
            FractureGrade.Hairline => 0.6f,
            FractureGrade.Simple => 1f,
            FractureGrade.Displaced => 1.2f,
            FractureGrade.Comminuted => 1.2f,
            _ => 0.3f,
        };
    }

    private float GetResetRatio(EntityUid body)
    {
        return TryComp(body, out WoundHostComponent? host)
            ? Math.Clamp(host.SeverableResetRatio, 0f, 1f)
            : 0.8f;
    }

    private void SetSeverable(Entity<WoundableComponent> part, bool value)
    {
        if (part.Comp.Severable == value)
            return;

        part.Comp.Severable = value;
        Dirty(part);
    }

    private static FixedPoint2 GetDismembermentSeverity(WoundHostComponent host, BodyPartType type)
    {
        return host.DismembermentSeverities.GetValueOrDefault(type, host.DefaultDismembermentSeverity);
    }

}
