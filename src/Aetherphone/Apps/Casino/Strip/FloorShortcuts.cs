using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Strip;

internal enum FloorShortcutKind : byte
{
    Missions,
    Club,
    Fame,
    History,
}

internal readonly record struct FloorShortcut(FloorShortcutKind Kind, FontAwesomeIcon Icon, Vector4 Tint,
    string Label, string Badge, Vector4 BadgeFill);

internal readonly record struct MissionsTally(int Done, int Total, int Ready);

internal sealed class FloorShortcuts
{
    public const int Capacity = 4;
    public const float TileHeight = 84f;
    public const float Icon = 36f;
    public const float Pad = 6f;
    public const float IconGap = 7f;
    public const float LabelPad = 2f;
    public const float BadgeInset = 4f;
    public const float BadgePadX = 4f;
    public const float BadgePadY = 0.5f;
    public const float BadgeGap = 3f;
    public const int LabelLines = 2;

    private const string AnchorKey = "casino.shortcuts";
    private const string PressId = "shortcut";
    private const float HoverFloor = 0.001f;
    private const float WashAlpha = 0.22f;
    private const float RimAlpha = 0.75f;
    private const float RimWidth = 1.2f;

    private static readonly TextStyle LabelStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle BadgeStyle = TextStyles.Caption1;

    private readonly TileLabelFit[] fits = new TileLabelFit[Capacity];

    public static MissionsTally Tally(CasinoMissionsDto? missions)
    {
        var total = MissionsCard.Count(missions);
        var done = 0;
        var ready = 0;
        for (var index = 0; index < total; index++)
        {
            var mission = missions!.Missions![index];
            if (!mission.Complete)
            {
                continue;
            }

            done++;
            ready += mission.Claimed ? 0 : 1;
        }

        return new MissionsTally(done, total, ready);
    }

    public static float Height(float labelBlock, float band, float scale) =>
        MathF.Max(TileHeight * scale, BlockTop(band, scale) + (Icon + IconGap + Pad) * scale + labelBlock);

    public static float BlockTop(float band, float scale) => MathF.Max(Pad * scale, band);

    public static float BadgeBand(float badgeHeight, float scale) =>
        badgeHeight <= 0f ? 0f : (BadgeInset + BadgeGap) * scale + badgeHeight;

    public static float BadgeHeight(float textHeight, float scale) => textHeight + BadgePadY * 2f * scale;

    public static float LabelWidth(float tileWidth, float scale) => MathF.Max(1f, tileWidth - LabelPad * 2f * scale);

    public int Draw(ImDrawListPtr drawList, AppSkin ui, ReadOnlySpan<FloorShortcut> items, Vector2 origin,
        float width, float scale, out float bottom)
    {
        var count = Math.Min(items.Length, Capacity);
        bottom = origin.Y;
        if (count == 0)
        {
            return -1;
        }

        var gap = StripShelves.GridGap * scale;
        var tileWidth = StripGrid.CellWidth(width, count, gap);
        var labelWidth = LabelWidth(tileWidth, scale);
        var labelBlock = 0f;
        var badges = false;
        for (var index = 0; index < count; index++)
        {
            fits[index] = TileLabel.Fit(items[index].Label, labelWidth, LabelLines, LabelStyle, LabelStyle);
            labelBlock = MathF.Max(labelBlock, TileLabel.Height(fits[index]));
            badges |= items[index].Badge.Length > 0;
        }

        var band = badges ? BadgeBand(BadgeHeight(Typography.LineHeight(BadgeStyle), scale), scale) : 0f;
        var height = Height(labelBlock, band, scale);
        bottom = origin.Y + height;
        UiAnchors.Report(AnchorKey, new Rect(origin, new Vector2(origin.X + width, bottom)));
        var tapped = -1;
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + index * (tileWidth + gap);
            var tile = new Rect(new Vector2(left, origin.Y), new Vector2(left + tileWidth, bottom));
            using (ImRaii.PushId(index))
            {
                if (DrawTile(drawList, ui, items[index], fits[index], tile, labelBlock, band, scale))
                {
                    tapped = index;
                }
            }
        }

        return tapped;
    }

    private static bool DrawTile(ImDrawListPtr drawList, AppSkin ui, in FloorShortcut item, in TileLabelFit fit,
        Rect tile, float labelBlock, float band, float scale)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID(PressId), tile.Min, tile.Max, out var pressedMin,
            out var pressedMax);
        var hover = HoverFx.Amount(PressId, hovered);
        var center = (pressedMin + pressedMax) * 0.5f;
        var half = (pressedMax - pressedMin) * 0.5f * (1f + Motion.HoverLiftCard * hover);
        var min = center - half;
        var max = center + half;
        var radius = Metrics.Radius.Lg * scale;
        ui.Card(drawList, min, max, radius);
        DrawHover(drawList, item.Tint, min, max, radius, hover, scale);
        var icon = Icon * scale;
        var regionTop = min.Y + BlockTop(band, scale);
        var regionBottom = max.Y - Pad * scale;
        var block = icon + IconGap * scale + labelBlock;
        var blockTop = regionTop + MathF.Max(0f, (regionBottom - regionTop - block) * 0.5f);
        var iconCenter = new Vector2((min.X + max.X) * 0.5f, blockTop + icon * 0.5f);
        CasinoArt.IconTileAt(drawList, iconCenter, icon * (1f + Motion.HoverLiftIcon * hover), item.Tint,
            item.Icon);
        var lineHeight = Typography.LineHeight(fit.Style);
        var top = blockTop + icon + IconGap * scale;
        for (var line = 0; line < fit.Lines.Length; line++)
        {
            var text = fit.Lines[line];
            var size = Typography.Measure(text, fit.Style);
            Typography.Draw(drawList, new Vector2(iconCenter.X - size.X * 0.5f, top), text, ui.TitleInk, fit.Style);
            top += lineHeight;
        }

        DrawBadge(drawList, item, new Rect(min, max), scale);
        return UiInteract.Click(tile.Min, tile.Max, hovered);
    }

    private static void DrawHover(ImDrawListPtr drawList, Vector4 tint, Vector2 min, Vector2 max, float radius,
        float hover, float scale)
    {
        if (hover <= HoverFloor)
        {
            return;
        }

        Squircle.FillVerticalGradient(drawList, min, max, radius,
            ImGui.GetColorU32(tint with { W = WashAlpha * hover }), ImGui.GetColorU32(tint with { W = 0f }));
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(tint with { W = RimAlpha * hover }),
            MathF.Max(1f, RimWidth * scale));
    }

    private static void DrawBadge(ImDrawListPtr drawList, in FloorShortcut item, Rect tile, float scale)
    {
        if (item.Badge.Length == 0)
        {
            return;
        }

        var inset = BadgeInset * scale;
        var padX = BadgePadX * scale;
        var label = Typography.FitText(item.Badge, MathF.Max(1f, tile.Width - inset * 2f - padX * 2f), BadgeStyle);
        var size = Typography.Measure(label, BadgeStyle);
        var height = BadgeHeight(Typography.LineHeight(BadgeStyle), scale);
        var max = new Vector2(tile.Max.X - inset, tile.Min.Y + inset + height);
        var min = new Vector2(max.X - size.X - padX * 2f, tile.Min.Y + inset);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(item.BadgeFill));
        Typography.Draw(drawList, (min + max) * 0.5f - size * 0.5f, label, CasinoColors.InkOnLight, BadgeStyle);
    }
}
