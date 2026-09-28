// Content taken from Goob Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Armor;

public sealed partial class ArmorComponent
{
    [DataField]
    public bool CoverageHidden;

    [DataField]
    public bool ModifiersHidden;
}
