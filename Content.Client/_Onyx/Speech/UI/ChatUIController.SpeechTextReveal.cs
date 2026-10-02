// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Chat.UI;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController
{
    private void SpeechBubbleHeightChanged(EntityUid entity, SpeechBubble bubble, float delta)
    {
        if (!_activeSpeechBubbles.TryGetValue(entity, out var bubbles))
            return;

        var index = bubbles.IndexOf(bubble);
        for (var i = 0; i < index; i++)
            bubbles[i].VerticalOffset += delta;
    }
}
