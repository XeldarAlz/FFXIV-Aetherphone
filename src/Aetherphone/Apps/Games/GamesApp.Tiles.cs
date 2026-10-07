using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const float TileArtAspect = 0.82f;
    private const float TileLabelBlock = 42f;
    private const float TileRoundingFactor = 0.22f;
    private const float TileEntranceLift = 14f;
    private const float BadgeHeight = 16f;
    private const float RankChipPadX = 5f;
    private const float RankChipFillAlpha = 0.20f;
    private const float OnlineBadgeRadius = 9f;
    private const float GridMinTileWidth = 104f;
    private const float TileGap = 10f;
    private const float EntranceSpeed = 1.6f;
    private const int MinColumns = 3;
    private const int MaxColumns = 6;

    private static readonly Vector4 BadgeFill = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 OnlineBadgeFill = new(0f, 0f, 0f, 0.38f);
    private static readonly Vector4 HeroInk = new(0.97f, 0.97f, 0.99f, 1f);
    private static readonly Vector4 StreakEmber = new(0.98f, 0.72f, 0.34f, 1f);

    private float entrance;

    private static float TileHeight(float tileWidth, float scale) => tileWidth * TileArtAspect + TileLabelBlock * scale;

    private bool DrawTile(Rect rect, int entryIndex, float appear, bool interactive)
    {
        if (appear <= 0f)
        {
            return false;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        ref var lift = ref library.Lift[entryIndex];
        ref readonly var entry = ref library.Entries[entryIndex];
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var target = pressed ? Motion.PressScaleControl : hovered ? 1f + Motion.HoverLiftIcon : 1f;
        var smoothTime = pressed ? Motion.PressIn : hovered ? Motion.HoverLift : Motion.Release;
        var grow = lift.Step(target, smoothTime, frameSeconds) * (0.90f + 0.10f * Easing.EaseOutBack(appear));
        var entranceLift = (1f - Easing.EaseOutCubic(appear)) * TileEntranceLift * scale;
        var artHeight = rect.Width * TileArtAspect;
        var artCenter = new Vector2(rect.Center.X, rect.Min.Y + artHeight * 0.5f + entranceLift);
        var half = new Vector2(rect.Width, artHeight) * 0.5f * grow;
        var min = artCenter - half;
        var max = artCenter + half;
        var rounding = rect.Width * TileRoundingFactor * grow;
        var accent = library.Accent(entryIndex);
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, hovered ? 0.26f : 0.18f)),
            ImGui.GetColorU32(GamePalette.Darken(accent, 0.34f)));
        drawList.PushClipRect(min, max, true);
        var size = max - min;
        drawList.AddCircleFilled(min + size * new Vector2(0.22f, 0.16f), size.X * 0.55f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.09f)), 40);
        drawList.AddCircleFilled(max - size * new Vector2(0.14f, 0.08f), size.X * 0.50f,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.55f) with { W = 0.14f }), 40);
        drawList.PopClipRect();
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.45f) with { W = hovered ? 0.65f : 0.32f }), 1f * scale);
        var iconSize = size.Y * 0.58f;
        if (entry.Online)
        {
            OnlineGameArt.Draw(drawList, entry.OnlineKind, artCenter, iconSize, scale);
        }
        else if (!DrawGameIcon(drawList, entry.Id, accent, artCenter, iconSize, scale))
        {
            Typography.DrawCentered(drawList, artCenter, library.Title(entryIndex), AccentRing.Ink, TextStyles.Caption2);
        }

        if (library.IsNew(entryIndex))
        {
            DrawNewBadge(drawList, new Vector2(min.X + 8f * scale, min.Y + 8f * scale), accent, scale);
        }

        if (entry.Online)
        {
            DrawOnlineBadge(drawList, new Vector2(max.X - 8f * scale - OnlineBadgeRadius * scale,
                min.Y + 8f * scale + OnlineBadgeRadius * scale), scale);
        }

        var textLeft = rect.Min.X + 2f * scale;
        var textWidth = MathF.Max(1f, rect.Width - 4f * scale);
        var titleY = rect.Min.Y + artHeight + 7f * scale + entranceLift;
        Marquee.DrawLeft(drawList, library.TileIds[entryIndex], library.Title(entryIndex), textLeft, titleY,
            textWidth, TextStyles.Headline, ui.TitleInk, hovered);
        var subtitleY = titleY + 18f * scale;
        var rankChip = library.RankLabel(entryIndex);
        var chipWidth = rankChip.Length > 0
            ? Typography.Measure(rankChip, TextStyles.Caption2).X + RankChipPadX * 2f * scale
            : 0f;
        var subtitleWidth = chipWidth > 0f ? MathF.Max(1f, textWidth - chipWidth - Metrics.Space.Xs * scale) : textWidth;
        var subtitle = Typography.FitText(library.Subtitle(entryIndex), subtitleWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, subtitleY), subtitle, ui.MutedInk, TextStyles.Footnote);
        if (chipWidth > 0f)
        {
            var chipLeft = textLeft + Typography.Measure(subtitle, TextStyles.Footnote).X + Metrics.Space.Xs * scale;
            DrawRankChip(drawList, new Vector2(chipLeft, subtitleY + Typography.LineHeight(TextStyles.Footnote) * 0.5f),
                rankChip, chipWidth, accent, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private float DrawGrid(ReadOnlySpan<int> entries, float left, float y, float width, float scale)
    {
        if (entries.Length == 0)
        {
            return y;
        }

        var gap = TileGap * scale;
        var columns = Math.Clamp((int)((width + gap) / (GridMinTileWidth * scale + gap)), MinColumns, MaxColumns);
        var tileWidth = (width - gap * (columns - 1)) / columns;
        var tileHeight = TileHeight(tileWidth, scale);
        var drawList = ImGui.GetWindowDrawList();
        var clipMin = drawList.GetClipRectMin();
        var clipMax = drawList.GetClipRectMax();
        var activate = -1;
        for (var index = 0; index < entries.Length; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var min = new Vector2(left + column * (tileWidth + gap), y + row * (tileHeight + gap));
            var rect = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            if (rect.Max.Y < clipMin.Y || rect.Min.Y > clipMax.Y)
            {
                continue;
            }

            if (DrawTile(rect, entries[index], GameJuice.Stagger(entrance, index, entries.Length), true))
            {
                activate = entries[index];
            }
        }

        if (activate >= 0)
        {
            Activate(activate);
        }

        var rows = (entries.Length + columns - 1) / columns;
        return y + rows * (tileHeight + gap) - gap;
    }

    private static bool DrawGameIcon(ImDrawListPtr drawList, string id, Vector4 accent, Vector2 center, float size,
        float scale)
    {
        var half = new Vector2(size, size) * 0.5f;
        if (AppIconTile.TryDraw(drawList, id, accent, center - half, center + half, size * Metrics.Radius.TileFactor,
                1f, false, scale))
        {
            return true;
        }

        return AppIconArt.TryDraw(drawList, id, center, size, AccentRing.Ink, GamePalette.Darken(accent, 0.16f));
    }

    private static void DrawEntryIcon(ImDrawListPtr drawList, in GameEntry entry, Vector4 accent, Vector2 center,
        float size, float scale)
    {
        if (!entry.Online)
        {
            DrawGameIcon(drawList, entry.Id, accent, center, size, scale);
            return;
        }

        var half = new Vector2(size, size) * 0.5f;
        Squircle.FillVerticalGradient(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.18f)), ImGui.GetColorU32(GamePalette.Darken(accent, 0.34f)));
        OnlineGameArt.Draw(drawList, entry.OnlineKind, center, size * 0.62f, scale);
    }

    private static void DrawNewBadge(ImDrawListPtr drawList, Vector2 topLeft, Vector4 accent, float scale)
    {
        var label = Loc.T(L.Games.BadgeNew);
        var textSize = Typography.Measure(label, TextStyles.Caption2);
        var height = BadgeHeight * scale;
        var max = new Vector2(topLeft.X + textSize.X + 12f * scale, topLeft.Y + height);
        Squircle.Fill(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(BadgeFill));
        Typography.DrawCentered(drawList, (topLeft + max) * 0.5f, label, GamePalette.Darken(accent, 0.30f),
            TextStyles.Caption2);
    }

    private void DrawRankChip(ImDrawListPtr drawList, Vector2 leftCenter, string label, float width, Vector4 accent,
        float scale)
    {
        var height = BadgeHeight * scale;
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(leftCenter.X + width, leftCenter.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(accent with { W = RankChipFillAlpha }));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, ui.TitleInk, TextStyles.Caption2);
    }

    private static void DrawOnlineBadge(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var radius = OnlineBadgeRadius * scale;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(OnlineBadgeFill), 24);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.UserFriends, AccentRing.Ink, radius * 0.95f);
    }
}
