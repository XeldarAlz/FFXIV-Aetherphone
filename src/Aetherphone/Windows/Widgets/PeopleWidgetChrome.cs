using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Widgets;

internal static class PeopleWidgetChrome
{
    public const float RowHeight = 46f;
    public const float RowAvatarRadius = 16f;
    public const float RowTextGap = 10f;
    public const float BadgeHeight = 18f;
    public const float CrossfadeSeconds = 0.25f;

    private const float MonogramLineFraction = 1.15f;
    private const float GroupGlyphFraction = 0.95f;
    private const float TileRounding = 0.32f;
    private const float TileFillAlpha = 0.22f;
    private const float TileGlyphFraction = 0.52f;
    private const float DotRadius = 4f;
    private const float TrailingGap = 6f;
    private const int MaximumCountLabel = 99;
    private const int Segments = 40;
    private const string LineProbe = "A";

    public static readonly Vector4 BadgeRed = new(1f, 59f / 255f, 48f / 255f, 1f);

    private static readonly string[] CountLabels = BuildCountLabels();
    private static readonly string OverflowLabel = string.Concat(MaximumCountLabel.ToString(), "+");

    public static string CountLabel(int count)
    {
        if (count <= 0)
        {
            return string.Empty;
        }

        return count > MaximumCountLabel ? OverflowLabel : CountLabels[count];
    }

    public static float Step(float value, float delta) =>
        MathF.Min(1f, value + MathF.Max(0f, delta) / CrossfadeSeconds);

    public static void Avatar(in WidgetContext context, in WidgetInk ink, Vector2 center, float radius, string name,
        IDalamudTextureWrap? texture, Vector4 accent, float alpha = 1f)
    {
        var drawList = context.DrawList;
        if (texture is not null)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ink.Fade(ink.Fill, alpha)), Segments);
            var corner = new Vector2(radius, radius);
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, center - corner, center + corner, uv0, uv1,
                ImGui.GetColorU32(ink.ImageTint with { W = ink.ImageTint.W * alpha }), radius,
                ImDrawFlags.RoundCornersAll);
            return;
        }

        var fill = ink.Accent(accent);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill with { W = fill.W * alpha }), Segments);
        var lineHeight = Typography.Measure("M", 1f, FontWeight.SemiBold).Y;
        var monogramScale = lineHeight > 0f ? radius * MonogramLineFraction / lineHeight : 1f;
        Typography.DrawCentered(drawList, center, Initials.Of(name), ink.OnAccent with { W = ink.OnAccent.W * alpha },
            monogramScale, FontWeight.SemiBold);
    }

    public static void GroupAvatar(in WidgetContext context, in WidgetInk ink, Vector2 center, float radius)
    {
        context.DrawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ink.Fill), Segments);
        PhoneIcon.Draw(context.DrawList, center, PhoneIcons.Users, ink.Secondary, radius * GroupGlyphFraction);
    }

    public static void Tile(in WidgetContext context, in WidgetInk ink, Vector2 center, float radius, Vector4 tint,
        string glyph)
    {
        var mapped = ink.Accent(tint);
        var corner = new Vector2(radius, radius);
        Squircle.Fill(context.DrawList, center - corner, center + corner, radius * 2f * TileRounding,
            ImGui.GetColorU32(mapped with { W = mapped.W * TileFillAlpha }));
        PhoneIcon.Draw(context.DrawList, center, glyph, mapped, radius * 2f * TileGlyphFraction);
    }

    public static float Badge(in WidgetContext context, in WidgetInk ink, float right, float centerY, string label,
        bool muted)
    {
        return WidgetChrome.Pill(context.DrawList, right, centerY, label, muted ? ink.Fill : ink.Accent(BadgeRed),
            muted ? ink.Secondary : ink.OnAccent, WidgetType.Caption, context.Scale);
    }

    public static void Dot(in WidgetContext context, in WidgetInk ink, Vector2 center, bool muted)
    {
        var fill = muted ? ink.Tertiary : ink.Accent(BadgeRed);
        context.DrawList.AddCircleFilled(center, DotRadius * context.Scale, ImGui.GetColorU32(fill), Segments);
    }

    public static void Row(in WidgetContext context, in WidgetInk ink, Rect row, float textLeft, string title,
        string subtitle, string trailing, string badge, bool muted, bool emphasized)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var titleHeight = Typography.Measure(LineProbe, WidgetType.Headline).Y;
        var subtitleHeight = subtitle.Length > 0 || badge.Length > 0
            ? Typography.Measure(LineProbe, WidgetType.Body).Y
            : 0f;
        var gap = subtitleHeight > 0f ? WidgetMetrics.RowGap * scale : 0f;
        var top = row.Center.Y - (titleHeight + gap + subtitleHeight) * 0.5f;
        var titleRight = row.Max.X;
        if (trailing.Length > 0)
        {
            var trailingSize = Typography.Measure(trailing, WidgetType.Caption);
            Typography.Draw(drawList,
                new Vector2(row.Max.X - trailingSize.X, top + (titleHeight - trailingSize.Y) * 0.5f), trailing,
                emphasized && !muted ? ink.Accent(BadgeRed) : ink.Secondary, WidgetType.Caption);
            titleRight -= trailingSize.X + TrailingGap * scale;
        }

        var titleInk = muted ? ink.Secondary : ink.Primary;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(title, MathF.Max(1f, titleRight - textLeft), WidgetType.Headline), titleInk,
            WidgetType.Headline);
        if (subtitleHeight <= 0f)
        {
            return;
        }

        var subtitleTop = top + titleHeight + gap;
        var subtitleRight = Badge(context, ink, row.Max.X, subtitleTop + subtitleHeight * 0.5f, badge, muted);
        if (badge.Length > 0)
        {
            subtitleRight -= TrailingGap * scale;
        }

        if (subtitle.Length == 0)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(textLeft, subtitleTop),
            Typography.FitText(subtitle, MathF.Max(1f, subtitleRight - textLeft), WidgetType.Body),
            muted ? ink.Tertiary : ink.Secondary, WidgetType.Body);
    }

    public static int RowCount(float available, float scale, int maximum) =>
        Math.Clamp((int)(available / (RowHeight * scale)), 1, Math.Max(1, maximum));

    private static string[] BuildCountLabels()
    {
        var labels = new string[MaximumCountLabel + 1];
        labels[0] = string.Empty;
        for (var count = 1; count <= MaximumCountLabel; count++)
        {
            labels[count] = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return labels;
    }
}
