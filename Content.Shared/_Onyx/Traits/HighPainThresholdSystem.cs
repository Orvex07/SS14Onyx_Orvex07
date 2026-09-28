// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Consciousness;
using Content.Shared._Onyx.Wounds;
using Content.Shared.FixedPoint;

namespace Content.Shared._Onyx.Traits;

public sealed partial class HighPainThresholdSystem : EntitySystem
{
    [Dependency] private ConsciousnessSystem _consciousness = default!;
    [Dependency] private PainSystem _pain = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HighPainThresholdComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HighPainThresholdComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<HighPainThresholdComponent> ent, ref ComponentStartup args)
    {
        ApplyMultiplier(ent);
    }

    private void OnShutdown(Entity<HighPainThresholdComponent> ent, ref ComponentShutdown args)
    {
        if (_consciousness.TryGetNervousSystem(ent.Owner, out var hub))
            _pain.TryRemovePainMultiplier(hub.Owner, nameof(HighPainThresholdComponent), hub.Comp);
    }

    private void ApplyMultiplier(Entity<HighPainThresholdComponent> ent)
    {
        if (!_consciousness.TryGetNervousSystem(ent.Owner, out var hub))
            return;

        if (!_pain.TryAddPainMultiplier(hub.Owner, nameof(HighPainThresholdComponent),
                FixedPoint2.New(ent.Comp.PainMultiplier), PainDamageTypes.WoundPain, hub.Comp))
        {
            _pain.TryChangePainMultiplier(hub.Owner, nameof(HighPainThresholdComponent),
                FixedPoint2.New(ent.Comp.PainMultiplier), null, PainDamageTypes.WoundPain, hub.Comp);
        }
    }
}
