// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Paper;

public sealed partial class StampComponent
{
    /// <summary>
    /// Texture displayed as the stamp impression instead of the generated text stamp.
    /// </summary>
    [DataField]
    public string? StampLargeIcon;
}
