// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void InitializeDamage()
    {
        SubscribeLocalEvent<SurgeryDamageEffectComponent, SurgeryStepEvent>(OnDamageEffect);
        SubscribeLocalEvent<SurgeryFailureDamageComponent, SurgeryStepFailedEvent>(OnFailureDamage);
        SubscribeLocalEvent<SurgeryBloodFilterEffectComponent, SurgeryStepEvent>(OnBloodFilter);
        SubscribeLocalEvent<SurgeryBloodFilterEffectComponent, SurgeryStepCompleteCheckEvent>(OnBloodFilterCheck);
    }

    private void OnDamageEffect(Entity<SurgeryDamageEffectComponent> effect, ref SurgeryStepEvent args)
    {
        ApplySurgeryDamage(args.Body, args.Part, args.User, effect.Comp.Target, effect.Comp.Damage,
            effect.Comp.HealWounds);
    }

    private void OnFailureDamage(Entity<SurgeryFailureDamageComponent> effect, ref SurgeryStepFailedEvent args)
    {
        ApplySurgeryDamage(args.Body, args.Part, args.User, effect.Comp.Target, effect.Comp.Damage, false);
    }

    private void OnBloodFilter(Entity<SurgeryBloodFilterEffectComponent> effect, ref SurgeryStepEvent args)
    {
        if (_net.IsServer)
            _bloodstream.FilterChemicals(args.Body, effect.Comp.Proportion, effect.Comp.Minimum, effect.Comp.Maximum);
    }

    private void OnBloodFilterCheck(Entity<SurgeryBloodFilterEffectComponent> effect,
        ref SurgeryStepCompleteCheckEvent args)
    {
        if (_bloodstream.HasFilterableChemicals(args.Body))
            args.Cancelled = true;
    }

    private void ApplySurgeryDamage(EntityUid body, EntityUid part, EntityUid user, SurgeryEntityTarget target,
        Content.Shared.Damage.DamageSpecifier damage, bool healWounds)
    {
        _bridge.ApplySurgeryDamage(body, part, user, target, damage, healWounds);
    }
}
