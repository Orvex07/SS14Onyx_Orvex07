// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Speech;

public static class SpeechTextReveal
{
    private const string ContentTag = "BubbleContent";

    public static List<Rune> GetRunes(FormattedMessage message)
    {
        var runes = new List<Rune>();

        foreach (var node in message.Nodes)
        {
            if (node.Value.StringValue is not { } text)
                continue;

            foreach (var rune in text.EnumerateRunes())
                runes.Add(rune);
        }

        return runes;
    }

    public static FormattedMessage Create(FormattedMessage message, int visibleRunes)
    {
        var result = new FormattedMessage(message.Count);

        foreach (var node in message.Nodes)
        {
            if (node.Name != null)
            {
                if (node.Closing)
                    result.Pop();
                else
                    result.PushTag(node);

                continue;
            }

            var text = node.Value.StringValue;
            if (text == null)
                continue;

            var visible = new StringBuilder(text.Length);
            foreach (var rune in text.EnumerateRunes())
            {
                if (visibleRunes-- > 0)
                    visible.Append(rune);
            }

            result.AddText(visible.ToString());
        }

        return result;
    }

    public static TextRange FindContentRange(FormattedMessage message)
    {
        var content = new List<Rune>();
        var runeIndex = 0;
        var start = -1;
        var inContent = false;

        foreach (var node in message.Nodes)
        {
            if (node.Name == ContentTag)
            {
                if (!node.Closing && start < 0)
                {
                    start = runeIndex;
                    inContent = true;
                }
                else if (node.Closing)
                {
                    inContent = false;
                }

                continue;
            }

            if (node.Name != null || node.Value.StringValue is not { } text)
                continue;

            foreach (var rune in text.EnumerateRunes())
            {
                if (inContent)
                    content.Add(rune);

                runeIndex++;
            }
        }

        if (content.Count == 0)
            start = -1;

        return new TextRange(content, start);
    }

    public static FormattedMessage Create(FormattedMessage message, int visibleRunes, TextRange range)
    {
        var result = new FormattedMessage(message.Count);
        var runeIndex = 0;

        foreach (var node in message.Nodes)
        {
            if (node.Name != null)
            {
                if (node.Closing)
                    result.Pop();
                else
                    result.PushTag(node);

                continue;
            }

            var text = node.Value.StringValue;
            if (text == null)
                continue;

            var visible = new StringBuilder(text.Length);
            foreach (var rune in text.EnumerateRunes())
            {
                var rangeIndex = runeIndex - range.Start;
                if (rangeIndex < 0 || rangeIndex >= range.Runes.Count || rangeIndex < visibleRunes)
                    visible.Append(rune);

                runeIndex++;
            }

            result.AddText(visible.ToString());
        }

        return result;
    }

    public static float[] BuildTimeline(
        IReadOnlyList<Rune> runes,
        float speed,
        float commaPause,
        float sentencePause,
        float ellipsisPause)
    {
        var timeline = new float[runes.Count];
        var elapsed = 0f;
        var runeDuration = 1f / speed;
        for (var i = 0; i < runes.Count; i++)
        {
            elapsed += runeDuration;
            timeline[i] = elapsed;

            if (!IsPausePunctuation(runes[i]))
                continue;

            var pause = runes[i].Value is ',' or ';' or ':' ? commaPause : sentencePause;
            var consecutiveDots = runes[i].Value == '.' ? 1 : 0;
            var maxConsecutiveDots = consecutiveDots;
            while (i + 1 < runes.Count && IsPausePunctuation(runes[i + 1]))
            {
                i++;
                elapsed += runeDuration;
                timeline[i] = elapsed;

                if (runes[i].Value is '.' or '?' or '!')
                    pause = Math.Max(pause, sentencePause);

                if (runes[i].Value == '.')
                {
                    consecutiveDots++;
                    maxConsecutiveDots = Math.Max(maxConsecutiveDots, consecutiveDots);
                }
                else
                {
                    consecutiveDots = 0;
                }
            }

            if (maxConsecutiveDots >= 3)
                pause = ellipsisPause;

            elapsed += pause;
        }

        return timeline;
    }

    public static int GetVisibleRunes(float[] timeline, float elapsed, int visibleRunes)
    {
        while (visibleRunes < timeline.Length && timeline[visibleRunes] <= elapsed)
            visibleRunes++;

        return visibleRunes;
    }

    private static bool IsPausePunctuation(Rune rune)
    {
        return rune.Value is ',' or ';' or ':' or '.' or '?' or '!';
    }

    public readonly record struct TextRange(List<Rune> Runes, int Start);
}
