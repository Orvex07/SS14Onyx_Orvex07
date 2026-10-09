// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;

namespace Content.Shared._Onyx.Wounds;

/// <summary>
/// Armor handling for wound hosts. Vanilla armor keeps the systemic path;
/// this system owns the localized path and never runs for non-hosts.
/// </summary>
public sealed partial class WoundArmorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>(OnPartDamageModify);
    }

    /// <summary>
    /// Applies armor modifiers to a wound host's systemic damage only. Localized part
    /// damage is left untouched so it can be armored after the struck part is resolved.
    /// </summary>
    public static DamageSpecifier ApplyWoundSystemicArmor(
        DamageSpecifier damage,
        DamageModifierSet modifiers,
        WoundHostComponent host)
    {
        var systemic = new DamageSpecifier(damage);
        var hasSystemic = false;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (host.LocalizedDamageTypes.Contains(type))
                systemic.DamageDict.Remove(type);
            else if (value != 0)
                hasSystemic = true;
        }

        if (!hasSystemic)
            return damage;

        var reduced = DamageSpecifier.ApplyModifierSet(systemic, modifiers);

        var result = damage.Clone();
        foreach (var (type, _) in systemic.DamageDict)
        {
            if (reduced.DamageDict.TryGetValue(type, out var value))
                result.DamageDict[type] = value;
            else
                result.DamageDict.Remove(type);
        }

        return result;
    }

    private void OnPartDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<PartDamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        foreach (var profile in component.PartModifiers)
        {
            if (profile.Parts.Count != 0 && !profile.Parts.Contains(args.Args.PartType) ||
                profile.Symmetry.Count != 0 && !profile.Symmetry.Contains(args.Args.Symmetry))
                continue;

            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, profile.Modifiers);
            return;
        }

        // The armor's global modifiers protect the whole body; they are never skipped
        // for an individual body part that is not listed in coverage.
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
    }
}
