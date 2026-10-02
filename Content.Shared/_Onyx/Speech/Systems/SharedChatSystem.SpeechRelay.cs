// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Chat;

public abstract partial class SharedChatSystem
{
    public virtual EntityUid? GetSpeechBarkSource(EntityUid source)
    {
        return source;
    }

    public virtual float? GetSpeechTextRevealSpeed(EntityUid source)
    {
        return null;
    }

    public virtual void SendRelayedSpeech(
        EntityUid relay,
        EntityUid speaker,
        string message,
        InGameICChatType type,
        ChatTransmitRange range,
        string name,
        bool relayBarks)
    {
    }
}
