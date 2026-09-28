// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Body;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public abstract partial class SharedVisualBodySystem
{
    [Dependency] private INetManager _net = default!;

    private void SyncConditionalMarkingOrgans(
        EntityUid body,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings)
    {
        if (_net.IsClient && !IsClientSide(body) ||
            !TryComp(body, out ConditionalMarkingOrgansComponent? conditional))
            return;

        foreach (var (category, data) in conditional.Organs)
        {
            var required = false;
            if (markings.TryGetValue(category, out var organMarkings))
            {
                foreach (var (layer, applied) in organMarkings)
                {
                    if (!data.Layers.Contains(layer) || applied.Count == 0)
                        continue;

                    required = true;
                    break;
                }
            }

            if (_bodySystem.TryGetOrgan(body, category.Id, out var existing))
            {
                SetMarkingOrganData(existing, body, data);

                if (!required && TryFindOrganParent(body, category.Id, out var parent))
                {
                    if (_bodySystem.TryRemoveOrgan(parent, category.Id, out var removed))
                        QueueDel(removed);
                }

                continue;
            }

            var parentPart = EntityUid.Invalid;
            foreach (var (part, _) in _bodySystem.GetBodyChildrenOfType(body, data.Parent))
            {
                parentPart = part;
                break;
            }

            if (!parentPart.IsValid())
                continue;

            _bodySystem.TryCreateOrganSlot(parentPart, category.Id);

            if (!required)
                continue;

            var organ = Spawn(data.Prototype, Transform(body).Coordinates);
            if (!_bodySystem.TryInsertOrganIgnoringCompatibility(parentPart, organ, category.Id))
            {
                QueueDel(organ);
                continue;
            }

            SetMarkingOrganData(organ, body, data);
        }
    }

    private void SetMarkingOrganData(EntityUid organ, EntityUid body, ConditionalMarkingOrganData data)
    {
        if (!TryComp(organ, out VisualOrganMarkingsComponent? visual) || !TryFindMarkingGroup(body, data.Layers, out var group))
            return;

        visual.MarkingData = visual.MarkingData with { Group = group, Layers = [..data.Layers] };
        Dirty(organ, visual);
    }

    private bool TryFindOrganParent(EntityUid body, string category, out EntityUid parent)
    {
        foreach (var (part, component) in _bodySystem.GetBodyChildren(body))
        {
            if (!component.Organs.Contains(category))
                continue;

            parent = part;
            return true;
        }

        parent = default;
        return false;
    }

    private bool TryFindMarkingGroup(EntityUid body, HashSet<HumanoidVisualLayers> layers, out ProtoId<MarkingsGroupPrototype> group)
    {
        foreach (var (part, _) in _bodySystem.GetBodyChildren(body))
        {
            if (!TryComp(part, out VisualOrganMarkingsComponent? visual) || !visual.MarkingData.Layers.Overlaps(layers))
                continue;

            group = visual.MarkingData.Group;
            return true;
        }

        group = default;
        return false;
    }
}
