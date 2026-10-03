// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Onyx.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    [Dependency] private AnnouncementSoundSystem _announcementSound = default!;

    private void SendAnnouncementSound(SoundSpecifier sound, Filter filter)
    {
        _announcementSound.Play(sound, filter, sound.Params.WithVolume(-2f));
    }
}
