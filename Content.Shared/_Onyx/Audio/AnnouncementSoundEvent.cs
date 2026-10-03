// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Audio;

[Serializable, NetSerializable]
public sealed class AnnouncementSoundEvent(ResolvedSoundSpecifier specifier, AudioParams audioParams) : EntityEventArgs
{
    public ResolvedSoundSpecifier Specifier = specifier;
    public AudioParams AudioParams = audioParams;
}
