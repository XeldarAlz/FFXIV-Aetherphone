using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hub;

internal static class GameTileView
{
    private const float PressedScale = Motion.PressScaleControl;
    private const float PressDim = 0.14f;
    private const float DimFloor = 0.001f;
    private const float RankOverhang = 4f;
    private const float RankHeight = 18f;
    private const float RankPad = 6f;
    private const float OnlineBadgeSize = 20f;
    private const float OnlineBadgeOverhang = 3f;
    private const float OnlineRing = 2f;
    private const float OnlineGlyph = 11f;
    private const float StarSize = 9f;
    private const float EmptyStarAlpha = 0.35f;
    private const int BadgeSegments = 32;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 RankFill = new(0f, 0f, 0f, 0.55f);

    public static float Height(float side, float scale) =>
        side + HubMetrics.CaptionGap * scale + Typography.LineHeight(TextStyles.FootnoteEmphasized)
        + Typography.LineHeight(TextStyles.Footnote);

    public static Rect IconRect(Vector2 topLeft, float side) =>
        new(topLeft, new Vector2(topLeft.X + side, topLeft.Y + side));

    public static bool Draw(ImDrawListPtr drawList, AppSkin ui, GamesLibrary library, int entryIndex,
        Vector2 topLeft, float side, bool interactive)
    {
        var scale = UiScale.Current;
        var tileId = library.TileIds[entryIndex];
        var tileMax = new Vector2(topLeft.X + side, topLeft.Y + Height(side, scale));
        var hovered = interactive && UiInteract.Hover(topLeft, tileMax);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var hover = HoverFx.Amount(tileId, hovered);
        var press = PressFx.Scale(tileId, pressed, PressedScale);
        var rest = side * 0.5f;
        var center = new Vector2(topLeft.X + rest, topLeft.Y + rest);
        var half = rest * press * (1f + Motion.HoverLiftIcon * hover);
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        DrawIcon(drawList, library, entryIndex, center, half, rest, hover, press, scale);
        ref readonly var entry = ref library.Entries[entryIndex];
        var rank = library.RankLabel(entryIndex);
        if (rank.Length > 0)
        {
            DrawRank(drawList, new Vector2(max.X + RankOverhang * scale, min.Y - RankOverhang * scale), rank, scale);
        }

        if (entry.Online)
        {
            DrawOnline(drawList, ui, entry.OnlineKind, max, scale);
        }

        DrawCaption(drawList, ui, library, entryIndex, tileId, topLeft, side, hovered, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(topLeft, tileMax, hovered);
    }

    private static void DrawIcon(ImDrawListPtr drawList, GamesLibrary library, int entryIndex, Vector2 center,
        float half, float rest, float hover, float press, float scale)
    {
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        var radius = GameIconArt.Radius(half * 2f);
        var tilt = Tilt(ImGui.GetMousePos(), center, rest) * hover;
        var dim = PressDim * (1f - press) / (1f - PressedScale);
        var firstVertex = drawList.VtxBuffer.Size;
        Material.PointerHalo(drawList, min, max, radius, hover, scale);
        GameIconArt.Draw(drawList, library.IconIds[entryIndex], library.Accent(entryIndex), min, max, null, true);
        Material.PointerSpecular(drawList, min, max, radius, tilt, hover, scale);
        if (dim > DimFloor)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)));
        }

        VertexWarp.Tilt(drawList, firstVertex, center, half, -tilt, HubMetrics.TiltDepth);
    }

    private static Vector2 Tilt(Vector2 pointer, Vector2 center, float half)
    {
        var offset = (pointer - center) / MathF.Max(half, 1f);
        return new Vector2(Math.Clamp(offset.X, -1f, 1f), Math.Clamp(offset.Y, -1f, 1f));
    }

    private static void DrawRank(ImDrawListPtr drawList, Vector2 topRight, string label, float scale)
    {
        var height = RankHeight * scale;
        var width = Typography.Measure(label, TextStyles.FootnoteEmphasized).X + RankPad * 2f * scale;
        var min = new Vector2(topRight.X - width, topRight.Y);
        var max = new Vector2(topRight.X, topRight.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(RankFill));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, White, TextStyles.FootnoteEmphasized);
    }

    private static void DrawOnline(ImDrawListPtr drawList, AppSkin ui, string kind, Vector2 iconMax, float scale)
    {
        var radius = OnlineBadgeSize * 0.5f * scale;
        var center = new Vector2(iconMax.X + OnlineBadgeOverhang * scale - radius,
            iconMax.Y + OnlineBadgeOverhang * scale - radius);
        drawList.AddCircleFilled(center, radius + OnlineRing * scale, ImGui.GetColorU32(ui.Palette.BackdropBottom),
            BadgeSegments);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(OnlineGameArt.Accent(kind)), BadgeSegments);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Users, White, OnlineGlyph * scale);
    }

    private static void DrawCaption(ImDrawListPtr drawList, AppSkin ui, GamesLibrary library, int entryIndex,
        string tileId, Vector2 topLeft, float side, bool hovered, float scale)
    {
        var centerX = topLeft.X + side * 0.5f;
        var titleTop = topLeft.Y + side + HubMetrics.CaptionGap * scale;
        Marquee.DrawCentered(drawList, tileId, library.Title(entryIndex), centerX, titleTop, side,
            TextStyles.FootnoteEmphasized, ui.TitleInk, hovered);
        var metaTop = titleTop + Typography.LineHeight(TextStyles.FootnoteEmphasized);
        if (library.IsNew(entryIndex))
        {
            DrawMeta(drawList, library.Meta(entryIndex), centerX, metaTop, side, library.Accent(entryIndex),
                TextStyles.FootnoteEmphasized);
            return;
        }

        if (library.StarMax(entryIndex) > 0)
        {
            var starCenter = new Vector2(centerX, metaTop + Typography.LineHeight(TextStyles.Footnote) * 0.5f);
            StarRow.Draw(drawList, starCenter, StarSize * scale, library.StarTier(entryIndex),
                ui.MutedInk with { W = EmptyStarAlpha }, 1f);
            return;
        }

        DrawMeta(drawList, library.Meta(entryIndex), centerX, metaTop, side, ui.MutedInk, TextStyles.Footnote);
    }

    private static void DrawMeta(ImDrawListPtr drawList, string text, float centerX, float top, float width,
        Vector4 ink, in TextStyle style)
    {
        var fitted = Typography.FitText(text, width, style);
        var size = Typography.Measure(fitted, style);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top), fitted, ink, style);
    }
}
