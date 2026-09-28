// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

[RegisterComponent]
public sealed partial class ConditionalMarkingOrgansComponent : Component
{
    [DataField(required: true)]
    public Dictionary<ProtoId<OrganCategoryPrototype>, ConditionalMarkingOrganData> Organs = new();
}

[DataDefinition]
public partial record struct ConditionalMarkingOrganData
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField(required: true)]
    public BodyPartType Parent;

    [DataField(required: true)]
    public HashSet<HumanoidVisualLayers> Layers = new();
}
