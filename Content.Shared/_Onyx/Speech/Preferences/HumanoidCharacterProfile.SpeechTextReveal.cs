// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    private void EnsureSpeechTextRevealValid(IConfigurationManager configManager)
    {
        SpeechBubbleRevealSpeed = Math.Clamp(
            SpeechBubbleRevealSpeed,
            configManager.GetCVar(CCVars.SpeechTextRevealMinSpeed),
            configManager.GetCVar(CCVars.SpeechTextRevealMaxSpeed));
    }
}
