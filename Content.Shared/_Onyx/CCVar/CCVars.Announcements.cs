// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> AnnouncementsVolume =
        CVarDef.Create("audio.announcements_volume", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
