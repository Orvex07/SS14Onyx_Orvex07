// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Onyx.Speech;

[RegisterComponent]
public sealed partial class SpeechTextRevealComponent : Component
{
    /// <summary>
    /// Number of text runes revealed per second.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Speed = 20f;
}
