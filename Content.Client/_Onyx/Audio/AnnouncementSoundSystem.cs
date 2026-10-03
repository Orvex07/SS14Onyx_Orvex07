// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Audio;
using Content.Shared.CCVar;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Onyx.Audio;

public sealed partial class AnnouncementSoundSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AnnouncementSoundEvent>(OnAnnouncementSound);
    }

    private void OnAnnouncementSound(AnnouncementSoundEvent ev)
    {
        var volume = SharedAudioSystem.GainToVolume(_cfg.GetCVar(CCVars.AnnouncementsVolume));
        _audio.PlayGlobal(ev.Specifier, Filter.Local(), false, ev.AudioParams.AddVolume(volume));
    }
}
