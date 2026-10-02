// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Server.SurveillanceCamera.Systems;
using Content.Shared.Chat;

namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraSpeakerSystem
{
    private void RelaySpeech(EntityUid relay, SurveillanceCameraSpeechSendEvent args, string name)
    {
        _chatSystem.SendRelayedSpeech(
            relay,
            args.Speaker,
            args.Message,
            InGameICChatType.Speak,
            ChatTransmitRange.GhostRangeLimit,
            name,
            relayBarks: true);
    }
}
