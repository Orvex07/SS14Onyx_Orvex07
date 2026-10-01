// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Construction;

public sealed partial class TieredMachinePartExamineSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly SpriteSpecifier UpgradeIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/Objects/Misc/stock_parts.rsi"),
        "micro_mani");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TieredMachinePartExamineComponent, GetVerbsEvent<ExamineVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<TieredMachinePartExamineComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!_net.IsClient || !args.CanAccess || !args.CanInteract || ent.Comp.Parts.Count == 0)
            return;

        var message = GetExamineMessage(ent.Comp);
        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new ExamineVerb
        {
            Act = () => _examine.SendExamineTooltip(user, target, message, false, false),
            Text = Loc.GetString("machine-upgrade-examinable-verb-text"),
            Message = Loc.GetString("machine-upgrade-examinable-verb-message"),
            Category = VerbCategory.Examine,
            Icon = UpgradeIcon,
            ClientExclusive = true,
        });
    }

    private FormattedMessage GetExamineMessage(TieredMachinePartExamineComponent component)
    {
        var message = new FormattedMessage();
        foreach (var part in component.Parts)
        {
            message.AddMarkupOrThrow(Loc.GetString("tiered-machine-part-installed-examine",
                ("kind", Loc.GetString(TieredMachinePartRequirements.GetName(part.Kind))),
                ("tier", part.Tier),
                ("amount", part.Amount)));
            message.PushNewline();
        }

        foreach (var upgrade in component.Upgrades)
        {
            if (!upgrade.Percentage)
            {
                message.AddMarkupOrThrow(Loc.GetString("machine-upgrade-value",
                    ("upgraded", Loc.GetString(upgrade.Name)),
                    ("value", upgrade.Value)));
                message.PushNewline();
                continue;
            }

            var percent = Math.Round(MathF.Abs(upgrade.Value - 1f) * 100f, 2);
            var localization = upgrade.Value switch
            {
                < 1f => "machine-upgrade-decreased-by-percentage",
                > 1f => "machine-upgrade-increased-by-percentage",
                _ => "machine-upgrade-not-upgraded",
            };

            message.AddMarkupOrThrow(Loc.GetString(localization,
                ("upgraded", Loc.GetString(upgrade.Name)),
                ("percent", percent)));
            message.PushNewline();
        }

        message.TrimEnd();
        return message;
    }
}
