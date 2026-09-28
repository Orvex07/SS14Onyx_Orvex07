// Content taken from Goob Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Localizations;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

public abstract partial class SharedArmorSystem
{
    private FormattedMessage GetArmorExamine(ArmorComponent component)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("armor-examine"));

        if (!component.CoverageHidden)
        {
            IEnumerable<BodyPartType> coverage = component.Coverage.Count == 0
                ? Enum.GetValues<BodyPartType>()
                : component.Coverage;
            var parts = coverage
                .Where(part => part != BodyPartType.Other)
                .Select(part => Loc.GetString($"armor-coverage-type-{part.ToString().ToLowerInvariant()}"))
                .ToList();

            if (parts.Count > 0)
            {
                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("armor-coverage-value",
                    ("type", ContentLocalizationManager.FormatListLocalized(parts, "generic-and"))));
            }
        }

        if (!component.ModifiersHidden)
            AppendArmorModifiers(msg, component.Modifiers);

        return msg;
    }

    private static bool HasVisibleArmorInformation(ArmorComponent component)
    {
        return !component.CoverageHidden && component.Coverage.Count > 0 ||
               !component.ModifiersHidden &&
               (component.Modifiers.Coefficients.Count > 0 || component.Modifiers.FlatReductions.Count > 0);
    }

    private void AppendArmorModifiers(FormattedMessage msg, DamageModifierSet armorModifiers)
    {
        foreach (var coefficientArmor in armorModifiers.Coefficients)
        {
            msg.PushNewline();
            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.Id.ToLowerInvariant());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                ("type", armorType),
                ("value", MathF.Round((1f - coefficientArmor.Value) * 100, 1))));
        }

        foreach (var flatArmor in armorModifiers.FlatReductions)
        {
            msg.PushNewline();
            var armorType = Loc.GetString("armor-damage-type-" + flatArmor.Key.Id.ToLowerInvariant());
            msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value",
                ("type", armorType),
                ("value", flatArmor.Value)));
        }
    }
}
