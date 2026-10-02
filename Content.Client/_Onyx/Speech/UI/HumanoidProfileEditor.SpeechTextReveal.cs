// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private bool _updatingSpeechRevealSpeed;

    private void InitializeSpeechTextReveal()
    {
        SpeechRevealSpeedContainer.Visible = _cfgManager.GetCVar(CCVars.SpeechTextRevealEnabled);
        SpeechRevealSpeedSlider.MinValue = _cfgManager.GetCVar(CCVars.SpeechTextRevealMinSpeed);
        SpeechRevealSpeedSlider.MaxValue = _cfgManager.GetCVar(CCVars.SpeechTextRevealMaxSpeed);
        SpeechRevealSpeedSlider.OnValueChanged += _ =>
        {
            if (!_updatingSpeechRevealSpeed)
                SetSpeechBubbleRevealSpeed(SpeechRevealSpeedSlider.Value);
        };
        SpeechRevealSpeedEdit.OnTextChanged += args =>
        {
            if (!_updatingSpeechRevealSpeed && float.TryParse(args.Text, out var speed))
                SetSpeechBubbleRevealSpeed(speed, updateEdit: false);
        };
    }

    private void UpdateSpeechTextRevealControls()
    {
        if (Profile is null)
            return;

        _updatingSpeechRevealSpeed = true;
        SpeechRevealSpeedSlider.Value = Profile.SpeechBubbleRevealSpeed;
        SpeechRevealSpeedEdit.Text = MathF.Round(Profile.SpeechBubbleRevealSpeed).ToString();
        _updatingSpeechRevealSpeed = false;
    }

    private void SetSpeechBubbleRevealSpeed(float speed, bool updateEdit = true)
    {
        if (Profile is null)
            return;

        speed = MathF.Round(Math.Clamp(
            speed,
            _cfgManager.GetCVar(CCVars.SpeechTextRevealMinSpeed),
            _cfgManager.GetCVar(CCVars.SpeechTextRevealMaxSpeed)));
        if (Profile.SpeechBubbleRevealSpeed == speed)
            return;

        Profile = Profile.WithSpeechBubbleRevealSpeed(speed);
        _updatingSpeechRevealSpeed = true;
        SpeechRevealSpeedSlider.Value = speed;
        if (updateEdit)
            SpeechRevealSpeedEdit.Text = speed.ToString();
        _updatingSpeechRevealSpeed = false;
        SetDirty();
    }
}
