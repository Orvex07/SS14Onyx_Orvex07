using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Medical.Surgery;

public sealed partial class SurgeryInfectionProtectionExamineSystem : EntitySystem
{
    private static readonly SpriteSpecifier InfectionIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/Clothing/Mask/sterile.rsi"),
        "icon");

    [Dependency] private ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryInfectionProtectionComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(Entity<SurgeryInfectionProtectionComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var soiled = HasComp<SurgerySoiledComponent>(ent);
        var protection = MathF.Round(ent.Comp.Protection);
        var tier = protection switch
        {
            >= 100f => "full",
            >= 40f => "high",
            >= 25f => "medium",
            _ => "low",
        };

        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString("surgery-infection-protection-examine",
            ("tier", tier), ("protection", protection)));
        if (soiled)
        {
            message.PushNewline();
            message.AddMarkupOrThrow(Loc.GetString("surgery-infection-protection-examine-soiled",
                ("penalty", MathF.Round(SurgerySterility.SoiledPenalty))));
        }
        var user = args.User;
        var target = args.Target;

        args.Verbs.Add(new ExamineVerb
        {
            Act = () => _examine.SendExamineTooltip(user, target, message, false, false),
            Text = Loc.GetString("surgery-infection-protection-examine-verb-text"),
            Message = Loc.GetString("surgery-infection-protection-examine-verb-message"),
            Category = VerbCategory.Examine,
            Icon = InfectionIcon,
        });
    }
}
