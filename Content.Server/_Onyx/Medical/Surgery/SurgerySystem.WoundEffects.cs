using System.Linq;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Wounds;
using Content.Shared.CCVar;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Medical.Surgery;

/// <summary>
/// Incision part of the server surgery system: bleed, clamp, close and emote steps.
/// </summary>
public sealed partial class SurgerySystem
{
    /// <summary>Wound prototype opened by incision steps.</summary>
    private static readonly ProtoId<WoundPrototype> SurgicalIncision = "SurgicalIncisionWound";

    /// <summary>Opens the surgical incision wound on step completion.</summary>
    private void OnStepBleedComplete(Entity<SurgeryStepBleedEffectComponent> ent, ref SurgeryStepEvent args)
    {
        _wounds.CreateOrMergeWound(args.Part, SurgicalIncision, ent.Comp.Damage);
    }

    /// <summary>Clamps incision bleeding on step completion.</summary>
    private void OnStepClampBleedComplete(Entity<SurgeryClampBleedEffectComponent> ent, ref SurgeryStepEvent args)
    {
        _bleeding.TreatPart(args.Part, BleedingTreatment.Clamped, SurgicalIncision);
    }

    /// <summary>Closes open incisions, optionally scarring, then removes the wound.</summary>
    private void OnCloseIncisionComplete(Entity<SurgeryCloseIncisionEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var chance = Math.Clamp(_configuration.GetCVar(CCVars.SurgeryScarChance), 0f, 1f);
        if (!TryComp(args.Part, out WoundableComponent? woundable))
            return;

        foreach (var wound in _wounds.GetWounds((args.Part, woundable)).ToArray())
        {
            if (wound.Comp.Prototype != SurgicalIncision ||
                wound.Comp.State is not WoundState.Open and not WoundState.Stabilized)
                continue;

            _bleeding.SetTreatment(wound.Owner, BleedingTreatment.Cauterized);
            _wounds.CloseWound(wound.Owner);
            if (chance > 0f && _random.Prob(chance))
                _scars.CreateScar(wound.Owner);
            _wounds.RemoveWound(wound.Owner);
        }
    }

    /// <summary>Plays the configured emote on the patient on step completion.</summary>
    private void OnStepEmoteComplete(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {
        _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
    }
}
