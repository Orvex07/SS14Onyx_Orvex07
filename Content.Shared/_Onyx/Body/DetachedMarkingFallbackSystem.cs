// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

/// <summary>
/// Ensures a detached marking organ keeps showing a marking on its own sprite.
/// Organs that opted in via <see cref="VisualOrganMarkingsComponent.ShowOnDetached"/>
/// but carry no markings (e.g. a freshly transplanted blank tail) fall back to
/// their marking group's layer defaults so the item is never an invisible blank.
/// </summary>
public sealed partial class DetachedMarkingFallbackSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (_net.IsClient)
            return;

        SubscribeLocalEvent<VisualOrganMarkingsComponent, OrganGotRemovedEvent>(OnDetached);
    }

    private void OnDetached(Entity<VisualOrganMarkingsComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!ent.Comp.ShowOnDetached)
            return;

        foreach (var markings in ent.Comp.Markings.Values)
        {
            if (markings.Count != 0)
                return;
        }

        if (!_proto.TryIndex(ent.Comp.MarkingData.Group, out var group))
            return;

        var changed = false;
        foreach (var layer in ent.Comp.MarkingData.Layers)
        {
            if (ent.Comp.Markings.TryGetValue(layer, out var existing) && existing.Count != 0)
                continue;

            if (!group.Limits.TryGetValue(layer, out var limits) || limits.Default.Count == 0)
                continue;

            var fallback = new List<Marking>();
            foreach (var id in limits.Default)
            {
                if (!_proto.TryIndex(id, out MarkingPrototype? proto))
                    continue;

                fallback.Add(proto.AsMarking());
            }

            if (fallback.Count == 0)
                continue;

            ent.Comp.Markings[layer] = fallback;
            changed = true;
        }

        if (changed)
            Dirty(ent);
    }
}
