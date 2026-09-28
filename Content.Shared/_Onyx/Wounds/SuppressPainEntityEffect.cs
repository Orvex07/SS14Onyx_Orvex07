using Content.Shared._Onyx.Consciousness;
using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class SuppressPainEntityEffectSystem : EntityEffectSystem<BodyComponent, SuppressPain>
{
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private PainSystem _pain = default!;

    protected override void Effect(Entity<BodyComponent> entity, ref EntityEffectEvent<SuppressPain> args)
    {
        if (!_consciousness.TryGetNervousSystem(entity.Owner, out var hub))
            return;

        var identifier = args.Effect.Identifier;
        var amount = args.Effect.Amount * args.Scale;

        _pain.SuppressPain(hub.Owner, identifier, amount, args.Effect.DecayDuration,
            args.Effect.RecoveryMultiplier, hub.Comp);
    }
}

public sealed partial class SuppressPain : EntityEffectBase<SuppressPain>
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField(required: true)]
    public TimeSpan DecayDuration;

    [DataField]
    public string Identifier = "PainSuppressant";

    [DataField]
    public float RecoveryMultiplier = 1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-suppress-pain",
            ("chance", Probability),
            ("amount", Amount.Float()),
            ("duration", DecayDuration.TotalSeconds),
            ("recoveryMultiplier", RecoveryMultiplier));
}
