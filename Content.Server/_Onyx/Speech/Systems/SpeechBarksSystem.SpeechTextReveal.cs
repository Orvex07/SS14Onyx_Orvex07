// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Speech;
using Content.Shared.CCVar;
using Content.Shared.Radio;

namespace Content.Server._Onyx.SpeechBarks;

public sealed partial class SpeechBarksSystem
{
    private void ApplyRadioSpeechTextReveal(RadioReceiveEvent args)
    {
        if (args.ChatMsg.Message.RevealSpeed != null ||
            !_cfg.GetCVar(CCVars.SpeechTextRevealEnabled) ||
            !TryComp<SpeechTextRevealComponent>(args.MessageSource, out var reveal))
            return;

        args.ChatMsg.Message.RevealSpeed = Math.Clamp(
            reveal.Speed,
            _cfg.GetCVar(CCVars.SpeechTextRevealMinSpeed),
            _cfg.GetCVar(CCVars.SpeechTextRevealMaxSpeed));
    }
}
