using Aetherphone.Core;
using Aetherphone.Core.Video;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class VideoStageOverlay
{
    private const float BubbleMaxWidth = 320f;
    private const float BubbleWidthShare = 0.62f;
    private const float BubbleWidthStep = 32f;
    private const float BubblePad = 8f;
    private const float BubbleGap = 5f;
    private const float BubbleRadius = 12f;
    private const float BubbleInset = 12f;
    private const float BubbleFadeIn = 0.05f;
    private const float BubbleFadeOut = 0.86f;
    private const float BubbleBackdrop = 0.64f;
    private const float MinimumStageWidth = 200f;
    private const float ReactionSizeShare = 0.13f;
    private const float ReactionMinSize = 18f;
    private const float ReactionMaxSize = 46f;
    private const float ReactionRise = 0.72f;
    private const float ReactionFadeStart = 0.7f;

    private static readonly TextStyle SenderStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle LineStyle = TextStyles.Footnote;
    private static readonly Vector4 SenderInk = new(1f, 0.84f, 0.45f, 1f);
    private static readonly Vector4 LineInk = new(1f, 1f, 1f, 1f);

    public static void DrawReactions(ImDrawListPtr drawList, Rect stage, PartyReactions reactions, float scale)
    {
        var now = Environment.TickCount64;
        var active = reactions.Active;
        var baseSize = Math.Clamp(stage.Height * ReactionSizeShare, ReactionMinSize * scale, ReactionMaxSize * scale);
        for (var index = 0; index < active.Length; index++)
        {
            ref readonly var reaction = ref active[index];
            if (!PartyReactions.TryProgress(reaction, now, out var progress))
            {
                continue;
            }

            var rise = 1f - (1f - progress) * (1f - progress);
            var pop = 0.7f + 0.3f * MathF.Min(1f, progress * 8f);
            var size = baseSize * pop;
            var sway = MathF.Sin(progress * MathF.Tau + reaction.Lane * 12f) * size * 0.3f;
            var center = new Vector2(
                stage.Min.X + stage.Width * (0.58f + 0.34f * reaction.Lane) + sway,
                stage.Max.Y - size - stage.Height * ReactionRise * rise);
            var alpha = progress < ReactionFadeStart
                ? 1f
                : 1f - (progress - ReactionFadeStart) / (1f - ReactionFadeStart);
            ReactionArt.Draw(drawList, PartyReactions.Tokens[reaction.Kind], center, size, alpha, 1f);
        }
    }

    public static void DrawBubbles(ImDrawListPtr drawList, Rect stage, ScreenChatFeed feed, float scale)
    {
        if (!feed.Enabled || stage.Width < MinimumStageWidth * scale)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lines = feed.Lines;
        var newest = feed.Newest;
        var pad = BubblePad * scale;
        var widthStep = BubbleWidthStep * scale;
        var maxWidth = MathF.Min(MathF.Floor(stage.Width * BubbleWidthShare / widthStep) * widthStep,
            BubbleMaxWidth * scale);
        var textWidth = maxWidth - pad * 2f;
        var senderHeight = Typography.LineHeight(SenderStyle);
        var lineHeight = Typography.LineHeight(LineStyle);
        var left = stage.Min.X + BubbleInset * scale;
        var bottom = stage.Max.Y - BubbleInset * scale;
        for (var offset = 0; offset < lines.Length; offset++)
        {
            ref readonly var line = ref lines[(newest - offset + lines.Length) % lines.Length];
            if (!ScreenChatFeed.TryAge(line, now, out var age))
            {
                continue;
            }

            var wrapped = Typography.WrapText(line.Text, LineStyle, textWidth);
            var sender = Typography.FitText(line.Sender, textWidth, SenderStyle);
            var widest = Typography.Measure(sender, SenderStyle).X;
            for (var lineIndex = 0; lineIndex < wrapped.Length; lineIndex++)
            {
                widest = MathF.Max(widest, Typography.Measure(wrapped[lineIndex], LineStyle).X);
            }

            var height = pad * 2f + senderHeight + wrapped.Length * lineHeight;
            var top = bottom - height;
            if (top < stage.Min.Y + BubbleInset * scale)
            {
                return;
            }

            var alpha = age < BubbleFadeIn
                ? age / BubbleFadeIn
                : age > BubbleFadeOut ? (1f - age) / (1f - BubbleFadeOut) : 1f;
            var min = new Vector2(left, top);
            var max = new Vector2(left + MathF.Min(maxWidth, widest + pad * 2f), bottom);
            Squircle.Fill(drawList, min, max, BubbleRadius * scale,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, BubbleBackdrop * alpha)));
            Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), sender,
                new Vector4(SenderInk.X, SenderInk.Y, SenderInk.Z, alpha), SenderStyle);
            var textY = min.Y + pad + senderHeight;
            for (var lineIndex = 0; lineIndex < wrapped.Length; lineIndex++)
            {
                Typography.Draw(drawList, new Vector2(min.X + pad, textY), wrapped[lineIndex],
                    new Vector4(LineInk.X, LineInk.Y, LineInk.Z, alpha), LineStyle);
                textY += lineHeight;
            }

            bottom = top - BubbleGap * scale;
        }
    }
}
