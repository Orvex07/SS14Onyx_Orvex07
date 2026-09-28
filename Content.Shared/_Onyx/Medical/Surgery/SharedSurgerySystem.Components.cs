using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void OnComponentEffect(Entity<SurgeryComponentEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer)
            return;

        foreach (var target in GetEffectTargets(ent.Comp.Target, args))
        {
            EntityManager.RemoveComponents(target, ent.Comp.Remove);
            EntityManager.AddComponents(target, ent.Comp.Add, removeExisting: false);
        }
    }

    private void OnComponentEffectCheck(Entity<SurgeryComponentEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (ent.Comp.Target is SurgeryEntityTarget.User or SurgeryEntityTarget.Tool)
            return;

        var target = ent.Comp.Target switch
        {
            SurgeryEntityTarget.Body => args.Body,
            SurgeryEntityTarget.Part => args.Part,
            _ => EntityUid.Invalid,
        };
        if (!ComponentsMatch(target, ent.Comp.Add, ent.Comp.Remove))
            args.Cancelled = true;
    }

    private bool ComponentsMatch(EntityUid target, ComponentRegistry required, ComponentRegistry forbidden)
    {
        return required.Values.All(component => HasComp(target, component.Component.GetType())) &&
               forbidden.Values.All(component => !HasComp(target, component.Component.GetType()));
    }

    private static IEnumerable<EntityUid> GetEffectTargets(SurgeryEntityTarget target, SurgeryStepEvent args)
    {
        switch (target)
        {
            case SurgeryEntityTarget.Body:
                yield return args.Body;
                break;
            case SurgeryEntityTarget.Part:
                yield return args.Part;
                break;
            case SurgeryEntityTarget.User:
                yield return args.User;
                break;
            case SurgeryEntityTarget.Tool:
                foreach (var tool in args.Tools)
                    yield return tool;
                break;
        }
    }
}
