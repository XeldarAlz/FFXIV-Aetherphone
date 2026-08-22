using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Trivia;

internal sealed class TriviaRenderer
{
    private const float SubjectTop = 34f;
    private const float SubjectHeightFactor = 0.30f;
    private const float OptionGap = 9f;
    private static readonly Vector4 Right = new(0.34f, 0.82f, 0.50f, 1f);

    public static Rect SubjectOf(Rect area, float scale)
    {
        var top = area.Min.Y + SubjectTop * scale;
        return new Rect(new Vector2(area.Min.X + 10f * scale, top),
            new Vector2(area.Max.X - 10f * scale, top + area.Height * SubjectHeightFactor));
    }

    public static Rect OptionsOf(Rect area, float scale)
    {
        var top = SubjectOf(area, scale).Max.Y + 20f * scale;
        return new Rect(new Vector2(area.Min.X + 8f * scale, top),
            new Vector2(area.Max.X - 8f * scale, area.Max.Y - 8f * scale));
    }

    public static Rect OptionRect(Rect area, int index, float scale)
    {
        var options = OptionsOf(area, scale);
        var gap = OptionGap * scale;
        var cellWidth = (options.Width - gap) * 0.5f;
        var cellHeight = (options.Height - gap) * 0.5f;
        var column = index % 2;
        var row = index / 2;
        var min = new Vector2(options.Min.X + column * (cellWidth + gap), options.Min.Y + row * (cellHeight + gap));
        return new Rect(min, new Vector2(min.X + cellWidth, min.Y + cellHeight));
    }

    public static Rect CategoryRect(Rect area, int index, float scale)
    {
        var count = TriviaBoard.PickableCount;
        var rowHeight = 50f * scale;
        var gap = 10f * scale;
        var total = count * rowHeight + (count - 1) * gap;
        var top = area.Center.Y - total * 0.5f + 14f * scale;
        var width = MathF.Min(area.Width - 44f * scale, 268f * scale);
        var left = area.Center.X - width * 0.5f;
        var y = top + index * (rowHeight + gap);
        return new Rect(new Vector2(left, y), new Vector2(left + width, y + rowHeight));
    }

    public static Vector4 TintOf(TriviaCategory category, Vector4 accent)
    {
        switch (category)
        {
            case TriviaCategory.Mounts:
                return new Vector4(0.98f, 0.72f, 0.38f, 1f);
            case TriviaCategory.Minions:
                return new Vector4(0.54f, 0.86f, 0.64f, 1f);
            case TriviaCategory.Actions:
                return new Vector4(0.96f, 0.46f, 0.58f, 1f);
            case TriviaCategory.Emotes:
                return new Vector4(0.74f, 0.54f, 0.96f, 1f);
            default:
                return accent;
        }
    }

    public void DrawPicker(TriviaBoard board, Rect area, PhoneTheme theme, Vector4 accent, int hovered, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var first = CategoryRect(area, 0, scale);
        Typography.DrawCentered(new Vector2(area.Center.X, first.Min.Y - 26f * scale),
            Loc.T(L.Games.ChooseCategory), theme.TextMuted, TextStyles.FootnoteEmphasized);
        for (var index = 0; index < TriviaBoard.PickableCount; index++)
        {
            var category = TriviaBoard.PickableAt(index);
            var row = CategoryRect(area, index, scale);
            var enabled = board.IsAvailable(category);
            var tint = TintOf(category, accent);
            var rounding = row.Height * 0.42f;
            var isHovered = enabled && hovered == index;
            var fillAlpha = !enabled ? 0.05f : isHovered ? 0.26f : 0.14f;
            if (isHovered)
            {
                ProgressRing.Glow(row.Center, row.Height * 0.6f, tint, 0.35f);
            }

            Squircle.FillVerticalGradient(drawList, row.Min, row.Max, rounding,
                ImGui.GetColorU32(GamePalette.Lighten(tint, 0.24f) with { W = fillAlpha }),
                ImGui.GetColorU32(GamePalette.Darken(tint, 0.28f) with { W = fillAlpha + 0.14f }));
            Squircle.Stroke(drawList, row.Min, row.Max, rounding,
                ImGui.GetColorU32(tint with { W = !enabled ? 0.12f : isHovered ? 0.75f : 0.32f }),
                (isHovered ? 1.6f : 1f) * scale);
            var dotCenter = new Vector2(row.Min.X + 22f * scale, row.Center.Y);
            drawList.AddCircleFilled(dotCenter, 5f * scale,
                ImGui.GetColorU32(tint with { W = enabled ? 0.95f : 0.25f }), 16);
            var label = Loc.T(TriviaBoard.LabelOf(category));
            var ink = enabled ? theme.TextStrong : theme.TextMuted;
            var maxWidth = row.Width - 56f * scale;
            Typography.Draw(new Vector2(dotCenter.X + 16f * scale, row.Center.Y - 9f * scale),
                Typography.FitText(label, maxWidth, TextStyles.Headline), ink, TextStyles.Headline);
        }
    }

    public void Draw(TriviaBoard board, Rect area, ITextureProvider textures, PhoneTheme theme, Vector4 accent,
        int hovered, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var subject = SubjectOf(area, scale);
        DrawSubject(drawList, board, subject, textures, theme, accent, scale);
        DrawTimer(drawList, board, subject, accent, scale);
        for (var index = 0; index < TriviaBoard.Options; index++)
        {
            DrawOption(drawList, board, area, index, textures, theme, accent, hovered == index, scale);
        }
    }

    private static void DrawSubject(ImDrawListPtr drawList, TriviaBoard board, Rect subject, ITextureProvider textures,
        PhoneTheme theme, Vector4 accent, float scale)
    {
        var rounding = 22f * scale;
        Elevation.Card(drawList, subject.Min, subject.Max, rounding, scale, 0.8f);
        Squircle.FillVerticalGradient(drawList, subject.Min, subject.Max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.10f) with { W = 0.20f }),
            ImGui.GetColorU32(GamePalette.Darken(accent, 0.45f) with { W = 0.34f }));
        Squircle.Stroke(drawList, subject.Min, subject.Max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = 0.28f }), 1f * scale);
        if (!board.HasQuestion)
        {
            return;
        }

        if (board.Kind == TriviaKind.IconToName)
        {
            var size = MathF.Min(subject.Height * 0.66f, subject.Width * 0.42f);
            var center = new Vector2(subject.Center.X, subject.Center.Y - 4f * scale);
            DrawIcon(drawList, textures, board.Correct.IconId, center, size, scale);
            return;
        }

        Typography.DrawWrappedCentered(drawList, new Vector2(subject.Center.X, subject.Center.Y - 4f * scale),
            board.Correct.Name, theme.TextStrong, TextStyles.Title2, subject.Width - 26f * scale);
    }

    private static void DrawTimer(ImDrawListPtr drawList, TriviaBoard board, Rect subject, Vector4 accent, float scale)
    {
        var fraction = Math.Clamp(board.TimeLeft / TriviaBoard.QuestionSeconds, 0f, 1f);
        var height = 4f * scale;
        var width = subject.Width - 40f * scale;
        var left = subject.Center.X - width * 0.5f;
        var top = subject.Max.Y - 16f * scale;
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        if (fraction <= 0f)
        {
            return;
        }

        var low = fraction < 0.3f;
        var tone = low ? new Vector4(0.96f, 0.42f, 0.42f, 1f) : GamePalette.Lighten(accent, 0.35f);
        var pulse = low ? 0.75f + 0.25f * Pulse.Wave(Pulse.Fast) : 1f;
        Squircle.Fill(drawList, min, new Vector2(left + width * fraction, max.Y), height * 0.5f,
            ImGui.GetColorU32(tone with { W = pulse }));
    }

    private static void DrawOption(ImDrawListPtr drawList, TriviaBoard board, Rect area, int index,
        ITextureProvider textures, PhoneTheme theme, Vector4 accent, bool hovered, float scale)
    {
        var cell = OptionRect(area, index, scale);
        var rounding = 18f * scale;
        var revealing = board.State == TriviaState.Revealing;
        var correct = revealing && index == board.CorrectIndex;
        var wrong = revealing && index == board.PickedIndex && board.PickedIndex != board.CorrectIndex;
        var tone = correct ? Right :
            wrong ? theme.Danger : accent;
        var fillAlpha = correct || wrong ? 0.30f : hovered ? 0.16f : 0.09f;
        Squircle.FillVerticalGradient(drawList, cell.Min, cell.Max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(tone, 0.24f) with { W = fillAlpha }),
            ImGui.GetColorU32(GamePalette.Darken(tone, 0.30f) with { W = fillAlpha + 0.12f }));
        Squircle.Stroke(drawList, cell.Min, cell.Max, rounding,
            ImGui.GetColorU32(tone with { W = correct || wrong ? 0.85f : hovered ? 0.5f : 0.22f }),
            (correct || wrong ? 1.8f : 1f) * scale);
        if (correct)
        {
            ProgressRing.Glow(cell.Center, cell.Height * 0.5f, Right, 0.35f);
        }

        if (!board.HasQuestion)
        {
            return;
        }

        var entry = board.Option(index);
        if (board.Kind == TriviaKind.IconToName)
        {
            Typography.DrawWrappedCentered(drawList, cell.Center, entry.Name, theme.TextStrong,
                TextStyles.SubheadlineEmphasized, cell.Width - 16f * scale);
            return;
        }

        var size = MathF.Min(cell.Height * 0.72f, cell.Width * 0.62f);
        DrawIcon(drawList, textures, entry.IconId, cell.Center, size, scale);
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
