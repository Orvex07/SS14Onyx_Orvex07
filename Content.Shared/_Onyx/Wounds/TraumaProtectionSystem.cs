// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using Content.Shared.Armor;
using Content.Shared.Inventory;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class TraumaProtectionSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<TraumaProtectionQueryEvent>>(OnQuery);
    }

    private void OnQuery(Entity<ArmorComponent> entity,
        ref InventoryRelayedEvent<TraumaProtectionQueryEvent> args)
    {
        var protection = entity.Comp.TraumaProtection;
        var locationMatched = false;
        foreach (var profile in entity.Comp.PartModifiers)
        {
            if (profile.Parts.Count != 0 && !profile.Parts.Contains(args.Args.PartType) ||
                profile.Symmetry.Count != 0 && !profile.Symmetry.Contains(args.Args.Symmetry))
                continue;

            if (profile.TraumaProtection.Count != 0)
                protection = profile.TraumaProtection;
            locationMatched = true;
            break;
        }

        if (!locationMatched &&
            ((entity.Comp.Coverage.Count != 0 && !entity.Comp.Coverage.Contains(args.Args.PartType)) ||
             (entity.Comp.CoverageSymmetry.Count != 0 && !entity.Comp.CoverageSymmetry.Contains(args.Args.Symmetry))) ||
            !protection.TryGetValue(args.Args.Trauma, out var value))
            return;

        args.Args.Protection += value;
    }

    public float GetProtection(EntityUid body, BodyPartComponent part, TraumaType trauma)
    {
        var protection = part.TraumaProtection.GetValueOrDefault(trauma);

        if (!TryComp(body, out InventoryComponent? inventory))
            return protection;

        var query = new TraumaProtectionQueryEvent(part.PartType, part.Symmetry, trauma);
        query.Protection = protection;
        _inventory.RelayEvent((body, inventory), query);
        return Math.Clamp(query.Protection, -1f, 1f);
    }
}
