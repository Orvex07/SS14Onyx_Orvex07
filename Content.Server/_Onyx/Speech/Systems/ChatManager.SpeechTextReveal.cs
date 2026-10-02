// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Content.Shared.Chat;

namespace Content.Server.Chat.Managers;

internal sealed partial class ChatManager
{
    private void ApplySpeechTextReveal(ChatMessage message, ChatChannel channel, EntityUid source)
    {
        if (!_configurationManager.GetCVar(CCVars.SpeechTextRevealEnabled) ||
            channel is not (ChatChannel.Local or ChatChannel.Whisper or ChatChannel.Radio or ChatChannel.CollectiveMind))
            return;

        if (_chatSystem.GetSpeechTextRevealSpeed(source) is not { } revealSpeed)
            return;

        message.RevealSpeed = Math.Clamp(
            revealSpeed,
            _configurationManager.GetCVar(CCVars.SpeechTextRevealMinSpeed),
            _configurationManager.GetCVar(CCVars.SpeechTextRevealMaxSpeed));
    }
}
