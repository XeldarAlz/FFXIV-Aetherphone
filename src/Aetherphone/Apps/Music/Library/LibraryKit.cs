using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Music.Library;

internal static class LibraryKit
{
    public const float ButtonHeight = 44f;
    public const float ButtonRadius = 11f;
    public const float ActionRowHeight = 58f;
    public const float FieldHeight = 40f;
    private const float ButtonGap = 14f;
    private const float ButtonGlyphScale = 0.75f;
    private const float ButtonGlyphGap = 8f;
    private const float ButtonFillAlpha = 0.10f;
    private const float ButtonHoverAlpha = 0.16f;
    private const float ButtonPressedAlpha = 0.22f;
    private const float TileFillAlpha = 0.12f;
    private const float TileGlyphScale = 0.9f;
    private const float FieldPadX = 12f;

    private const double LongPressSeconds = 0.5d;
    private const float LongPressSlop = 6f;
    private static int pressKey = -1;
    private static double pressStart;
    private static Vector2 pressOrigin;

    public static bool LongPressed(int key, bool hovered)
    {
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressKey = key;
            pressStart = ImGui.GetTime();
            pressOrigin = ImGui.GetMousePos();
            return false;
        }

        if (pressKey != key)
        {
            return false;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) ||
            Vector2.Distance(ImGui.GetMousePos(), pressOrigin) > LongPressSlop * UiScale.Current)
        {
            pressKey = -1;
            return false;
        }

        if (ImGui.GetTime() - pressStart < LongPressSeconds)
        {
            return false;
        }

        pressKey = -1;
        UiInteract.CancelPendingTap();
        return true;
    }

    public static bool Secondary(int key, bool hovered)
    {
        return LongPressed(key, hovered) || (hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right));
    }

    public static void Gap(float units)
    {
        ImGui.Dummy(new Vector2(0f, units * UiScale.Current));
    }

    public static bool ActionButton(Rect rect, string glyph, string label, AppSkin ui, bool enabled = true)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var alpha = !hovered ? ButtonFillAlpha :
            ImGui.IsMouseDown(ImGuiMouseButton.Left) ? ButtonPressedAlpha : ButtonHoverAlpha;
        var radius = ButtonRadius * UiScale.Current;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, alpha)));
        var ink = enabled ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.4f);
        var glyphBox = Typography.LineHeight(TextStyles.Headline);
        var gap = ButtonGlyphGap * UiScale.Current;
        var available = MathF.Max(1f, rect.Width - glyphBox - gap - radius * 2f);
        var fitted = Typography.FitText(label, available, TextStyles.Headline);
        var labelSize = Typography.Measure(fitted, TextStyles.Headline);
        var left = rect.Center.X - (glyphBox + gap + labelSize.X) * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + glyphBox * 0.5f, rect.Center.Y), glyph, ink, ButtonGlyphScale);
        Typography.Draw(drawList, new Vector2(left + glyphBox + gap, rect.Center.Y - labelSize.Y * 0.5f), fitted, ink,
            TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static (bool Play, bool Shuffle) PlayShuffleRow(AppSkin ui, string playLabel, string shuffleLabel,
        bool enabled)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var gap = ButtonGap * scale;
        var height = ButtonHeight * scale;
        var buttonWidth = MathF.Max(1f, (width - inset * 2f - gap) * 0.5f);
        var playRect = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + inset + buttonWidth, origin.Y + height));
        var shuffleRect = new Rect(new Vector2(playRect.Max.X + gap, origin.Y),
            new Vector2(playRect.Max.X + gap + buttonWidth, origin.Y + height));
        var play = ActionButton(playRect, IconGlyph.Of(FontAwesomeIcon.Play), playLabel, ui, enabled);
        var shuffle = ActionButton(shuffleRect, IconGlyph.Of(FontAwesomeIcon.Random), shuffleLabel, ui, enabled);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return (play, shuffle);
    }

    public static bool ActionRow(AppSkin ui, string glyph, string label, bool interactive = true)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, ActionRowHeight * scale, ui.HoverWash, interactive);
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(cell.Bounds.Min.X + MusicUi.Inset * scale,
            cell.Bounds.Min.Y + (cell.Bounds.Height - side) * 0.5f);
        var artMax = artMin + new Vector2(side, side);
        Squircle.Fill(drawList, artMin, artMax, side * ArtworkTile.TileRadiusFraction,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, TileFillAlpha)));
        AppSkin.Icon(drawList, (artMin + artMax) * 0.5f, glyph, ui.Accent, TileGlyphScale);
        var textLeft = artMax.X + Metrics.Space.Md * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        var fitted = Typography.FitText(label, MathF.Max(1f, cell.Bounds.Max.X - MusicUi.Inset * scale - textLeft),
            TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Bounds.Min.Y + (cell.Bounds.Height - labelHeight) * 0.5f),
            fitted, ui.Accent, TextStyles.Body);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        return cell.Tapped;
    }

    public static bool TextField(Rect field, string id, string hint, ref string text, int maxLength, bool focus,
        AppSkin ui, out bool active)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, field.Min, field.Max, ButtonRadius * scale, ImGui.GetColorU32(ui.FieldSurface));
        var padX = FieldPadX * scale;
        ImGui.SetCursorScreenPos(new Vector2(field.Min.X + padX, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, field.Width - padX * 2f));
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(text);
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
        }

        bool submitted;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgActive, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (ImRaii.PushColor(ImGuiCol.TextDisabled, ui.MutedInk))
        {
            submitted = ImGui.InputTextWithHint(id, hint, ref text, maxLength, ImGuiInputTextFlags.EnterReturnsTrue);
        }

        active = ImGui.IsItemActive();
        return submitted;
    }

    public static float CenteredText(string text, in TextStyle style, Vector4 color, float maxWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y), text, color, style,
            maxWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return height;
    }

    public static string MegabytesText(long bytes)
    {
        const double BytesPerMegabyte = 1024d * 1024d;
        var megabytes = bytes / BytesPerMegabyte;
        return megabytes < 10d
            ? megabytes.ToString("0.0", Core.Localization.Loc.Culture)
            : Math.Round(megabytes).ToString("N0", Core.Localization.Loc.Culture);
    }
}
