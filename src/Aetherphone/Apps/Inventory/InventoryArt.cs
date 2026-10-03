using Aetherphone.Core;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Inventory;

internal static class InventoryArt
{
    public const float MeterHeight = 6f;
    public const float NearlyFull = 0.9f;
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 GoldTint = new(0.95f, 0.74f, 0.30f, 1f);
    public static readonly Vector4 WarningTint = new(1f, 0.62f, 0.04f, 1f);
    private static readonly Vector4 MutedTile = new(0.52f, 0.54f, 0.62f, 1f);

    private const float NativeIconPixels = 40f;
    private const float TileGlyphFraction = 0.46f;
    private const float MutedTileAlpha = 0.16f;
    private const float MeterTrackAlpha = 0.10f;
    private const float BadgePadX = 5f;
    private const float BadgeHeight = 16f;
    private const float StateTileSize = 72f;
    private const float StateGlyphFraction = 0.44f;
    private const float StateTitleGap = 18f;
    private const float StateHintGap = 6f;
    private const float StateActionGap = 20f;
    private const float StateActionHeight = 40f;
    private const float StateActionPadding = 40f;
    private const float StateActionMinWidth = 140f;
    private const float StateMaxTextWidth = 280f;
    private const float StateTextInset = 48f;
    private const float StateLift = 36f;
    private const float ChevronArm = 4f;
    private const float ChevronStroke = 1.8f;
    private const float SkeletonPulseRate = 2.4f;
    private const float SkeletonBaseAlpha = 0.07f;
    private const float SkeletonPulseAlpha = 0.04f;

    public static FontAwesomeIcon IconFor(InventorySourceKind kind) =>
        kind switch
        {
            InventorySourceKind.Inventory => FontAwesomeIcon.Briefcase,
            InventorySourceKind.Armoury => FontAwesomeIcon.ShieldAlt,
            InventorySourceKind.Crystals => FontAwesomeIcon.Gem,
            InventorySourceKind.Saddlebag => FontAwesomeIcon.Paw,
            InventorySourceKind.Equipped => FontAwesomeIcon.Tshirt,
            InventorySourceKind.Retainer => FontAwesomeIcon.UserTie,
            InventorySourceKind.FreeCompany => FontAwesomeIcon.Users,
            _ => FontAwesomeIcon.BoxOpen,
        };

    public static Vector4 AccentFor(InventorySourceKind kind) =>
        kind switch
        {
            InventorySourceKind.Inventory => new Vector4(0.98f, 0.60f, 0.23f, 1f),
            InventorySourceKind.Armoury => new Vector4(0.28f, 0.56f, 0.96f, 1f),
            InventorySourceKind.Crystals => new Vector4(0.62f, 0.44f, 0.96f, 1f),
            InventorySourceKind.Saddlebag => new Vector4(0.90f, 0.55f, 0.33f, 1f),
            InventorySourceKind.Equipped => new Vector4(0.28f, 0.78f, 0.52f, 1f),
            InventorySourceKind.Retainer => new Vector4(0.40f, 0.47f, 0.92f, 1f),
            InventorySourceKind.FreeCompany => new Vector4(0.93f, 0.72f, 0.30f, 1f),
            _ => new Vector4(0.55f, 0.55f, 0.60f, 1f),
        };

    public static void Tile(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, FontAwesomeIcon icon,
        float scale, bool enabled)
    {
        var half = size * 0.5f;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var radius = size * Metrics.Radius.TileFactor;
        if (!enabled)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(MutedTile with { W = MutedTileAlpha }));
            Material.EdgeSquircle(drawList, min, max, radius, scale, 0.5f);
            ProgressRing.CenterIcon(drawList, center, icon, MutedTile, size * TileGlyphFraction);
            return;
        }

        IconTile.FillShaded(drawList, min, max, radius, IconTile.Surface(color));
        Material.EdgeSquircle(drawList, min, max, radius, scale, 0.9f);
        ProgressRing.CenterIcon(drawList, center, icon, White, size * TileGlyphFraction);
    }

    public static void ItemIcon(ImDrawListPtr drawList, ITextureProvider textures, uint iconId, bool highQuality,
        Vector2 min, Vector2 max, float scale, Vector4 well)
    {
        var radius = (max.X - min.X) * Metrics.Radius.TileFactor;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(well));
        if (iconId != 0)
        {
            var hiRes = max.X - min.X > NativeIconPixels;
            var texture = textures.GetFromGameIcon(new GameIconLookup(iconId, highQuality, hiRes)).GetWrapOrEmpty();
            if (texture.Handle != 0)
            {
                drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, radius);
            }
        }

        Material.EdgeSquircle(drawList, min, max, radius, scale, 0.5f);
    }

    public static void Meter(ImDrawListPtr drawList, Vector2 min, float width, float fill, Vector4 accent,
        Vector4 track, PhoneTheme theme, float scale)
    {
        var height = MeterHeight * scale;
        var max = new Vector2(min.X + width, min.Y + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(track with { W = MeterTrackAlpha }), height * 0.5f);
        var clamped = Math.Clamp(fill, 0f, 1f);
        if (clamped <= 0.001f)
        {
            return;
        }

        var ink = MeterInk(clamped, accent, theme);
        var fillWidth = MathF.Max(height, width * clamped);
        drawList.AddRectFilled(min, new Vector2(min.X + fillWidth, max.Y), ImGui.GetColorU32(ink), height * 0.5f);
    }

    public static Vector4 MeterInk(float fill, Vector4 accent, PhoneTheme theme)
    {
        if (fill >= 0.999f)
        {
            return theme.Danger;
        }

        return fill >= NearlyFull ? WarningTint : accent;
    }

    public static float HqBadgeWidth(string label, float scale) =>
        Typography.Measure(label, TextStyles.Caption2).X + BadgePadX * 2f * scale;

    public static float HqBadge(ImDrawListPtr drawList, Vector2 leftCenter, string label, Vector4 accent,
        float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.Caption2);
        var width = HqBadgeWidth(label, scale);
        var height = BadgeHeight * scale;
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(min.X + width, min.Y + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(accent), height * 0.5f);
        Typography.Draw(drawList, new Vector2(min.X + BadgePadX * scale, leftCenter.Y - textSize.Y * 0.5f), label,
            White, TextStyles.Caption2);
        return width;
    }

    public static void Chevron(ImDrawListPtr drawList, Vector2 tip, Vector4 ink, float scale)
    {
        var arm = ChevronArm * scale;
        var color = ImGui.GetColorU32(ink);
        var stroke = ChevronStroke * scale;
        drawList.AddLine(new Vector2(tip.X - arm, tip.Y - arm * 1.4f), tip, color, stroke);
        drawList.AddLine(tip, new Vector2(tip.X - arm, tip.Y + arm * 1.4f), color, stroke);
    }

    public static bool StateScreen(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint,
        string actionLabel)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var centerX = body.Center.X;
        var tileSize = StateTileSize * scale;
        var tileTop = body.Center.Y - StateLift * scale - tileSize;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink,
            tileSize * StateGlyphFraction);
        var maxWidth = MathF.Min(body.Width - StateTextInset * scale, StateMaxTextWidth * scale);
        var bottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTitleGap * scale), maxWidth);
        if (hint.Length > 0)
        {
            bottom = Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, bottom + StateHintGap * scale), maxWidth);
        }

        if (actionLabel.Length == 0)
        {
            return false;
        }

        var natural = Typography.Measure(actionLabel, TextStyles.Headline).X + StateActionPadding * scale;
        var width = Math.Clamp(natural, StateActionMinWidth * scale,
            MathF.Max(StateActionMinWidth * scale, body.Width - StateTextInset * scale));
        var top = bottom + StateActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top),
            new Vector2(centerX + width * 0.5f, top + StateActionHeight * scale));
        return ui.AccentPill(rect, actionLabel, true, TextStyles.Headline);
    }

    public static uint SkeletonInk(float phase)
    {
        var pulse = SkeletonBaseAlpha + SkeletonPulseAlpha * (0.5f + 0.5f * MathF.Sin(phase * SkeletonPulseRate));
        return ImGui.GetColorU32(White with { W = pulse });
    }
}
