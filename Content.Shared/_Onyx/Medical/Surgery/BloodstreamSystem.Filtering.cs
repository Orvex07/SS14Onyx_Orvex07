// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;

#pragma warning disable IDE0130
namespace Content.Shared.Body.Systems;

public sealed partial class BloodstreamSystem
{
    /// <summary>Checks whether a bloodstream contains anything other than its reference blood reagents.</summary>
    public bool HasFilterableChemicals(Entity<BloodstreamComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, logMissing: false) ||
            !_solutionContainer.ResolveSolution(ent.Owner, ent.Comp.BloodSolutionName, ref ent.Comp.BloodSolution,
                out var bloodSolution))
            return false;

        foreach (var (reagent, _) in bloodSolution.Contents)
        {
            if (!ent.Comp.BloodReferenceSolution.ContainsPrototype(reagent.Prototype))
                return true;
        }

        return false;
    }

    /// <summary>Removes a bounded proportion of every non-blood reagent from a bloodstream.</summary>
    public bool FilterChemicals(Entity<BloodstreamComponent?> ent, float proportion, FixedPoint2 minimum,
        FixedPoint2 maximum)
    {
        if (!Resolve(ent, ref ent.Comp, logMissing: false) ||
            !_solutionContainer.ResolveSolution(ent.Owner, ent.Comp.BloodSolutionName, ref ent.Comp.BloodSolution,
                out var bloodSolution))
            return false;

        proportion = Math.Clamp(proportion, 0f, 1f);
        minimum = FixedPoint2.Max(FixedPoint2.Zero, minimum);
        maximum = FixedPoint2.Max(minimum, maximum);
        var removed = false;

        for (var i = bloodSolution.Contents.Count - 1; i >= 0; i--)
        {
            var reagent = bloodSolution.Contents[i];
            if (ent.Comp.BloodReferenceSolution.ContainsPrototype(reagent.Reagent.Prototype))
                continue;

            var quantity = FixedPoint2.Clamp(reagent.Quantity * proportion, minimum, maximum);
            quantity = FixedPoint2.Min(quantity, reagent.Quantity);
            removed |= _solutionContainer.RemoveReagent(ent.Comp.BloodSolution.Value, reagent.Reagent, quantity) > 0;
        }

        return removed;
    }
}
