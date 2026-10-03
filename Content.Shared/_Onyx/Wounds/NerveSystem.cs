// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Wounds;

[RegisterComponent, NetworkedComponent]
public sealed partial class NerveComponent : Component
{
    /// <summary>
    /// Physical nerve damage. While above zero the nerve conducts pain worse (see feels).
    /// </summary>
    [DataField]
    public FixedPoint2 Damage;

    [DataField]
    public FixedPoint2 MaxDamage = 100;

    [DataField]
    public float DamageMultiplier = 1f;

    [DataField]
    public FixedPoint2 MinimumHitDamage = 5;

    [DataField]
    public FixedPoint2 ChanceDivisor = 20;

    /// <summary>
    /// Multiplier applied to every pain modifier anchored to this nerve.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainMultiplier = 1f;

    /// <summary>
    /// How feelable pain through this nerve is. Lowered by nerve damage and suppressants.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public FixedPoint2 PainFeels
    {
        get
        {
            var painFeels = 1f;
            foreach (var modifier in PainFeelingModifiers.Values)
                painFeels += modifier.Change.Float();

            return FixedPoint2.New(painFeels);
        }
    }

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<(EntityUid, string), PainFeelingModifier> PainFeelingModifiers = new();

    /// <summary>
    /// Nerve system hub (brain organ) this nerve is parented to.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid ParentedNerveSystem;
}

public sealed partial class NerveSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TraumaProtectionSystem _traumaProtection = default!;

    private const float MinPainFeels = 0.2f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NerveComponent, PartDamageAppliedEvent>(OnPartDamageApplied);
        SubscribeLocalEvent<NerveComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<NerveComponent, ComponentGetState>(OnComponentGet);
        SubscribeLocalEvent<NerveComponent, ComponentHandleState>(OnComponentHandleState);
    }

    private void OnComponentGet(Entity<NerveComponent> nerve, ref ComponentGetState args)
    {
        var state = new NerveComponentState
        {
            PainMultiplier = nerve.Comp.PainMultiplier,
            Damage = nerve.Comp.Damage,
        };

        if (!TerminatingOrDeleted(nerve.Comp.ParentedNerveSystem))
            state.ParentedNerveSystem = GetNetEntity(nerve.Comp.ParentedNerveSystem);

        foreach (var ((modEntity, id), modifier) in nerve.Comp.PainFeelingModifiers)
        {
            if (!TerminatingOrDeleted(modEntity))
                state.PainFeelingModifiers.Add((GetNetEntity(modEntity), id), modifier);
        }

        args.State = state;
    }

    private void OnComponentHandleState(Entity<NerveComponent> nerve, ref ComponentHandleState args)
    {
        if (args.Current is not NerveComponentState state)
            return;

        var parentEntity = GetEntity(state.ParentedNerveSystem);
        nerve.Comp.ParentedNerveSystem = !TerminatingOrDeleted(parentEntity) ? parentEntity : EntityUid.Invalid;
        nerve.Comp.PainMultiplier = state.PainMultiplier;
        nerve.Comp.Damage = state.Damage;

        nerve.Comp.PainFeelingModifiers.Clear();
        foreach (var ((modEntity, id), modifier) in state.PainFeelingModifiers)
        {
            var entity = GetEntity(modEntity);
            if (!TerminatingOrDeleted(entity) && !nerve.Comp.PainFeelingModifiers.ContainsKey((entity, id)))
                nerve.Comp.PainFeelingModifiers.Add((entity, id), modifier);
        }
    }

    private void OnPartDamageApplied(Entity<NerveComponent> nerve, ref PartDamageAppliedEvent args)
    {
        if (!_net.IsServer || !TryComp(nerve, out BodyPartComponent? part))
            return;

        var severity = PositiveDamage(args.Damage);
        if (severity < nerve.Comp.MinimumHitDamage || nerve.Comp.MaxDamage <= FixedPoint2.Zero)
            return;

        if (nerve.Comp.PainFeels < FixedPoint2.New(MinPainFeels))
            return;

        var protection = _traumaProtection.GetProtection(args.Body, part, TraumaType.NerveDamage);
        var chance = Math.Clamp(severity.Float() / Math.Max(0.01f, nerve.Comp.ChanceDivisor.Float()) - protection, 0f, 1f);
        if (_random.Prob(chance))
            ChangeDamage(nerve, severity * nerve.Comp.DamageMultiplier);
    }

    private void OnRejuvenate(Entity<NerveComponent> nerve, ref RejuvenateEvent args)
    {
        SetDamage(nerve, FixedPoint2.Zero);
        if (nerve.Comp.PainFeelingModifiers.Count > 0)
        {
            nerve.Comp.PainFeelingModifiers.Clear();
            Dirty(nerve);
        }
    }

    public bool Repair(Entity<NerveComponent?> nerve, FixedPoint2 amount)
    {
        return amount > FixedPoint2.Zero && Resolve(nerve, ref nerve.Comp, false) &&
               ChangeDamage((nerve.Owner, nerve.Comp), -amount);
    }

    private bool ChangeDamage(Entity<NerveComponent> nerve, FixedPoint2 amount)
    {
        return SetDamage(nerve, nerve.Comp.Damage + amount);
    }

    private bool SetDamage(Entity<NerveComponent> nerve, FixedPoint2 damage)
    {
        damage = FixedPoint2.Clamp(damage, FixedPoint2.Zero,
            FixedPoint2.Max(FixedPoint2.Zero, nerve.Comp.MaxDamage));
        if (nerve.Comp.Damage == damage)
            return false;

        var oldDamage = nerve.Comp.Damage;
        nerve.Comp.Damage = damage;
        Dirty(nerve);
        var changed = new NerveDamageChangedEvent(nerve, oldDamage, damage);
        RaiseLocalEvent(nerve, ref changed);
        return true;
    }

    private static FixedPoint2 PositiveDamage(DamageSpecifier damage)
    {
        var result = FixedPoint2.Zero;
        foreach (var amount in damage.DamageDict.Values)
            if (amount > FixedPoint2.Zero)
                result += amount;
        return result;
    }
}

[ByRefEvent]
public readonly record struct NerveDamageChangedEvent(EntityUid Part, FixedPoint2 OldDamage, FixedPoint2 Damage);
