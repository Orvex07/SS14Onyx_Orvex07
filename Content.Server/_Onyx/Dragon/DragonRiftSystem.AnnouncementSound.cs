// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Onyx.Audio;

namespace Content.Server.Dragon;

public sealed partial class DragonRiftSystem
{
    [Dependency] private AnnouncementSoundSystem _announcementSound = default!;
}
