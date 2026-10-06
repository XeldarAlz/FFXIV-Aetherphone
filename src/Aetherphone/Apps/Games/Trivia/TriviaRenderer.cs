using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Trivia;

internal readonly struct TriviaLayout
{
    public readonly Rect Prompt;
    public readonly Rect Subject;
    public readonly Rect Options;

    public TriviaLayout(Rect prompt, Rect subject, Rect options)
    {
        Prompt = prompt;
        Subject = subject;
        Options = options;
    }
}

internal static class TriviaRenderer
{
    public const float UrgentFraction = 0.3f;
    private const float PromptHeight = 22f;
    private const float PromptGap = 8f;
    private const float SubjectHeightFraction = 0.30f;
    private const float SubjectGap = 18f;
    private const float OptionGap = 9f;
    private const float OptionRadius = 16f;
    private const float RingThickness = 3.5f;
    private const float RingGap = 8f;
    private const float BadgeRadius = 13f;
    private const float BadgeInset = 16f;
    private const float IconHeightFraction = 0.56f;
    private const float PulseGrow = 0.04f;
    private const int OptionColumns = 2;
    private static readonly Vector4 Right = new(0.34f, 0.82f, 0.50f, 1f);
    private static readonly Vector4 Wrong = new(0.92f, 0.30f, 0.34f, 1f);
    private static readonly Vector4 PaperCell = new(0.99f, 0.98f, 0.96f, 1f);
    private static readonly Vector4 PaperTrack = new(0f, 0f, 0f, 0.08f);
    private static readonly Vector4 LightTrack = new(1f, 1f, 1f, 0.12f);

    public static TriviaLayout Layout(Rect area, float scale)
    {
        var promptHeight = PromptHeight * scale;
        var prompt = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + promptHeight));
        var subjectTop = prompt.Max.Y + PromptGap * scale;
        var subject = new Rect(new Vector2(area.Min.X, subjectTop),
            new Vector2(area.Max.X, subjectTop + area.Height * SubjectHeightFraction));
        var options = new Rect(new Vector2(area.Min.X, subject.Max.Y + SubjectGap * scale), area.Max);
        return new TriviaLayout(prompt, subject, options);
    }

    public static Rect OptionRect(Rect options, int index, float scale)
    {
        var gap = OptionGap * scale;
        var cellWidth = (options.Width - gap) * 0.5f;
        var cellHeight = (options.Height - gap) * 0.5f;
        var column = index % OptionColumns;
        var row = index / OptionColumns;
        var min = new Vector2(options.Min.X + column * (cellWidth + gap), options.Min.Y + row * (cellHeight + gap));
        return new Rect(min, new Vector2(min.X + cellWidth, min.Y + cellHeight));
    }

    public static Vector4 StrongInk(StageInk ink, PhoneTheme theme) =>
        ink == StageInk.Dark ? GamePalette.InkDark : theme.TextStrong;

    public static Vector4 MutedInk(StageInk ink, PhoneTheme theme) =>
        ink == StageInk.Dark ? GamePalette.InkDark with { W = 0.62f } : theme.TextMuted;

    public static void DrawPrompt(ImDrawListPtr drawList, Rect prompt, string text, Vector4 ink)
    {
        Typography.DrawCentered(drawList, prompt.Center, text, ink, TextStyles.FootnoteEmphasized.Scale,
            TextStyles.FootnoteEmphasized.Weight);
    }

    public static void DrawSubject(ImDrawListPtr drawList, TriviaBoard board, Rect subject, bool showQuestion,
        ITextureProvider textures, Vector4 accent, StageInk ink, PhoneTheme theme, float scale)
    {
        BoardPlate.Draw(drawList, subject, BoardPlate.Radius * scale, scale, accent, ink);
        if (!showQuestion || !board.HasQuestion)
        {
            return;
        }

        var fraction = Math.Clamp(board.TimeLeft / TriviaBoard.QuestionSeconds, 0f, 1f);
        var track = ink == StageInk.Dark ? PaperTrack : LightTrack;
        if (board.Kind == TriviaKind.IconToName)
        {
            var size = MathF.Min(subject.Height * IconHeightFraction, subject.Width * 0.4f);
            var center = subject.Center;
            DrawIcon(drawList, textures, board.CorrectEntry.IconId, center, size, scale);
            DrawRing(drawList, center, size * 0.5f + RingGap * scale, fraction, accent, track, scale);
            return;
        }

        Typography.DrawWrappedCentered(drawList, subject.Center, board.CorrectEntry.Name, StrongInk(ink, theme),
            TextStyles.Title2, subject.Width - 2f * (BadgeInset + BadgeRadius * 2f) * scale);
        var badge = new Vector2(subject.Max.X - BadgeInset * scale, subject.Min.Y + BadgeInset * scale);
        ProgressRing.CenterIcon(drawList, badge, FontAwesomeIcon.Clock, MutedInk(ink, theme), BadgeRadius * scale);
        DrawRing(drawList, badge, BadgeRadius * scale + RingGap * 0.5f * scale, fraction, accent, track, scale);
    }

    public static void DrawOption(ImDrawListPtr drawList, TriviaBoard board, Rect cell, int index, bool showQuestion,
        ITextureProvider textures, Vector4 accent, StageInk ink, PhoneTheme theme, bool hovered, float scale)
    {
        var revealing = showQuestion && board.State == TriviaState.Revealing;
        var correct = revealing && index == board.CorrectIndex;
        var wrong = revealing && index == board.PickedIndex && board.PickedIndex != board.CorrectIndex;
        var baseFill = ink == StageInk.Dark ? PaperCell : GamePalette.Cell;
        var fill = correct ? Vector4.Lerp(baseFill, Right, 0.38f) :
            wrong ? Vector4.Lerp(baseFill, Wrong, 0.38f) :
            hovered ? Vector4.Lerp(baseFill, accent, 0.14f) : baseFill;
        var rect = cell;
        if (correct)
        {
            var pulse = MathF.Sin(board.RevealProgress * MathF.PI);
            var half = cell.Size * 0.5f * (1f + PulseGrow * pulse);
            rect = new Rect(cell.Center - half, cell.Center + half);
            ProgressRing.Glow(cell.Center, cell.Height * 0.5f, Right, 0.3f + 0.3f * pulse);
        }

        var radius = OptionRadius * scale;
        StageCell.Draw(drawList, rect, fill, CellDepth.Raised, radius, scale);
        if (correct || wrong)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32((correct ? Right : Wrong) with { W = 0.9f }), 2f * scale);
        }
        else if (hovered)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(accent with { W = 0.5f }),
                1.2f * scale);
        }

        if (!showQuestion || !board.HasQuestion)
        {
            return;
        }

        var entry = board.Option(index);
        if (board.Kind == TriviaKind.IconToName)
        {
            Typography.DrawWrappedCentered(drawList, rect.Center, entry.Name, GamePalette.InkOn(fill),
                TextStyles.SubheadlineEmphasized, rect.Width - 16f * scale);
            return;
        }

        var size = MathF.Min(rect.Height * 0.72f, rect.Width * 0.62f);
        DrawIcon(drawList, textures, entry.IconId, rect.Center, size, scale);
    }

    private static void DrawRing(ImDrawListPtr drawList, Vector2 center, float radius, float fraction, Vector4 accent,
        Vector4 track, float scale)
    {
        var thickness = RingThickness * scale;
        ProgressRing.Track(drawList, center, radius, thickness, track);
        if (fraction <= 0f)
        {
            return;
        }

        var urgent = fraction < UrgentFraction;
        var tone = urgent ? Wrong : accent;
        var alpha = urgent ? 0.7f + 0.3f * Pulse.Wave(Pulse.Fast) : 1f;
        ProgressRing.Fill(drawList, center, radius, thickness, fraction, tone with { W = alpha });
    }

    private static void DrawIcon(ImDrawListPtr drawList, ITextureProvider textures, uint iconId, Vector2 center,
        float size, float scale)
    {
        if (iconId == 0)
        {
            return;
        }

        var half = new Vector2(size * 0.5f, size * 0.5f);
        GameIconTile.Draw(drawList, textures, iconId, center - half, center + half, size * 0.22f, scale,
            flatShadow: true, requireIcon: true);
    }
}
