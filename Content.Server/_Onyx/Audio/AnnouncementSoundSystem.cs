// Content adapted from Corvax Goob-Station (https://github.com/space-syndicate/Goob-Station), licensed under AGPL-3.0-or-later.
// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Onyx.Audio;

public sealed partial class AnnouncementSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    public void Play(
        SoundSpecifier? sound,
        Filter filter,
        AudioParams? audioParams = null,
        bool recordReplay = true)
    {
        if (sound == null)
            return;

        RaiseNetworkEvent(
            new AnnouncementSoundEvent(_audio.ResolveSound(sound), audioParams ?? sound.Params),
            filter,
            recordReplay);
    }

    public void Play(
        string sound,
        Filter filter,
        AudioParams? audioParams = null,
        bool recordReplay = true)
    {
        Play(new SoundPathSpecifier(sound), filter, audioParams, recordReplay);
    }
}
