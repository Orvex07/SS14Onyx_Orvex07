// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Onyx.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130
namespace Content.Shared.Construction;

public sealed partial class MachinePartSystem
{
    private static bool ReplacesLegacyRequirement(MachineBoardComponent board, ProtoId<StackPrototype> stack)
    {
        return stack == TieredMachinePartRequirements.LegacyManipulator
            && TieredMachinePartRequirements.ReplacesManipulators(board);
    }

    private void AppendTieredPartRequirements(MachineBoardComponent board, ExaminedEvent args)
    {
        var requirements = new Dictionary<MachinePartKind, int>();
        TieredMachinePartRequirements.CopyFromBoard(board, requirements);

        foreach (var (kind, amount) in requirements)
        {
            args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                ("amount", amount),
                ("requiredElement", Loc.GetString(TieredMachinePartRequirements.GetName(kind)))));
        }
    }

    private bool TryAddTieredPartMaterialCost(
        MachineBoardComponent board,
        Dictionary<string, int> materials,
        int coefficient)
    {
        var requirements = new Dictionary<MachinePartKind, int>();
        TieredMachinePartRequirements.CopyFromBoard(board, requirements);

        foreach (var (kind, amount) in requirements)
        {
            if (!_lathe.TryGetRecipesFromEntity(TieredMachinePartRequirements.GetDefaultPrototype(kind), out var recipes))
                return false;

            var recipe = recipes.MinBy(candidate => candidate.Materials.Values.Sum())!;
            foreach (var (material, materialAmount) in recipe.Materials)
            {
                materials.TryAdd(material, 0);
                materials[material] += materialAmount * amount * coefficient;
            }
        }

        return true;
    }
}
