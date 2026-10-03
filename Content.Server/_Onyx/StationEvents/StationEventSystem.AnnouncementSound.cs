// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Onyx.Audio;

namespace Content.Server.StationEvents.Events;

public abstract partial class StationEventSystem<T> where T : IComponent
{
    [Dependency] protected AnnouncementSoundSystem AnnouncementSound = default!;
}
