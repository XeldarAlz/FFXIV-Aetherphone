using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal readonly record struct VFact(string Glyph, Vector4 Tone, string Label, string Value);

internal readonly struct VCardScope
{
    public readonly Vector2 Origin;
    public readonly float Width;
    public readonly float Height;
    public readonly Vector2 ContentOrigin;
    public readonly float ContentWidth;

    public VCardScope(Vector2 origin, float width, float height, Vector2 contentOrigin, float contentWidth)
    {
        Origin = origin;
        Width = width;
        Height = height;
        ContentOrigin = contentOrigin;
        ContentWidth = contentWidth;
    }
}

internal static class VCard
{
    public const float Gap = 12f;
    public const float Pad = 14f;
    public const float HeaderTile = 26f;
    public const float HeaderGap = 10f;
    public const float HeaderBlock = HeaderTile + HeaderGap;
    public const float RowHeight = Metrics.Size.Row;

    public const float HeaderGlyph = 15f;

    private const float Radius = Metrics.Radius.Card;
    private const float ShadowOpacity = 0.5f;
    private const float TileAlpha = 0.18f;
    private const float HeaderTitleGap = 10f;
    private const float RowTile = 26f;
    private const float RowGlyph = 15f;
    private const float RowLabelGap = 12f;
    private const float RowValueGap = 16f;
    private const float StackedPadY = 10f;
    private const float StackedGap = 2f;

    private static readonly TextStyle TrailingStyle = TextStyles.Footnote;
    private static readonly TextStyle FactLabelStyle = TextStyles.Body;
    private static readonly TextStyle FactValueStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle StackedLabelStyle = TextStyles.Footnote;
    private static readonly TextStyle StackedValueStyle = TextStyles.Body;

    public static VCardScope Begin(ImDrawListPtr drawList, float width, float contentHeight, float scale,
        float padY = Pad)
    {
        var origin = ImGui.GetCursorScreenPos();
        var padX = Pad * scale;
        var height = contentHeight + padY * 2f * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Paint(drawList, origin, max, scale);
        var contentOrigin = new Vector2(origin.X + padX, origin.Y + padY * scale);
        return new VCardScope(origin, width, height, contentOrigin, MathF.Max(1f, width - padX * 2f));
    }

    public static void End(in VCardScope card)
    {
        ImGui.SetCursorScreenPos(card.Origin);
        ImGui.Dummy(new Vector2(card.Width, card.Height));
    }

    public static void Paint(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale)
    {
        var radius = Radius * scale;
        Elevation.Card(drawList, min, max, radius, scale, ShadowOpacity);
        Squircle.FillVerticalGradient(drawList, min, max, radius, VelvetTheme.CardHi.Packed(),
            VelvetTheme.Card.Packed());
        Squircle.Stroke(drawList, min, max, radius, VelvetTheme.CardStroke.Packed(), Metrics.Stroke.Hairline * scale);
    }

    public static void Header(ImDrawListPtr drawList, Vector2 origin, float width, string glyph, Vector4 tone,
        string title, float scale, string trailing = "")
    {
        var tile = HeaderTile * scale;
        var tileMax = new Vector2(origin.X + tile, origin.Y + tile);
        var centerY = origin.Y + tile * 0.5f;
        Tile(drawList, origin, tileMax, glyph, tone, HeaderGlyph * scale, scale);
        var titleLeft = tileMax.X + HeaderTitleGap * scale;
        var titleWidth = MathF.Max(1f, origin.X + width - titleLeft);
        if (trailing.Length > 0)
        {
            var trailingSize = Typography.Measure(trailing, TrailingStyle);
            titleWidth = MathF.Max(1f, titleWidth - trailingSize.X - HeaderTitleGap * scale);
            Typography.Draw(drawList, new Vector2(origin.X + width - trailingSize.X, centerY - trailingSize.Y * 0.5f),
                trailing, VelvetTheme.MutedInk, TrailingStyle);
        }

        Typography.Draw(drawList, new Vector2(titleLeft, centerY - Typography.LineHeight(TextStyles.Headline) * 0.5f),
            Typography.FitText(title, titleWidth, TextStyles.Headline), VelvetTheme.TitleInk, TextStyles.Headline);
    }

    public static float RowTextLeft(float left, float scale) => left + (HeaderTile + HeaderGap) * scale;

    public static void RowLabel(ImDrawListPtr drawList, Vector2 origin, float rowHeight, string glyph, Vector4 tone,
        string label, float width, float scale)
    {
        var tile = HeaderTile * scale;
        var centerY = origin.Y + rowHeight * 0.5f;
        Tile(drawList, new Vector2(origin.X, centerY - tile * 0.5f), new Vector2(origin.X + tile, centerY + tile * 0.5f),
            glyph, tone, HeaderGlyph * scale, scale);
        var textLeft = RowTextLeft(origin.X, scale);
        var textWidth = MathF.Max(1f, origin.X + width - textLeft);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - Typography.LineHeight(TextStyles.Body) * 0.5f),
            Typography.FitText(label, textWidth, TextStyles.Body), VelvetTheme.TitleInk, TextStyles.Body);
    }

    public static float FactHeight(in VFact fact, float width, float scale)
    {
        if (Inline(fact, width, scale, out _))
        {
            return RowHeight * scale;
        }

        var wrapped = Typography.MeasureWrappedBlock(fact.Value, StackedValueStyle, StackedWidth(width, scale)).Y;
        return (StackedPadY * 2f + StackedGap) * scale + Typography.LineHeight(StackedLabelStyle) + wrapped;
    }

    public static void Fact(ImDrawListPtr drawList, Vector2 origin, float width, in VFact fact, float height,
        bool separator, float scale)
    {
        var tile = RowTile * scale;
        var centerY = origin.Y + height * 0.5f;
        var tileMin = new Vector2(origin.X, centerY - tile * 0.5f);
        Tile(drawList, tileMin, new Vector2(origin.X + tile, centerY + tile * 0.5f), fact.Glyph, fact.Tone,
            RowGlyph * scale, scale);
        var textLeft = origin.X + tile + RowLabelGap * scale;
        var right = origin.X + width;
        if (Inline(fact, width, scale, out var valueSize))
        {
            Typography.Draw(drawList, new Vector2(textLeft, centerY - Typography.LineHeight(FactLabelStyle) * 0.5f),
                fact.Label, VelvetTheme.BodyInk, FactLabelStyle);
            Typography.Draw(drawList, new Vector2(right - valueSize.X, centerY - valueSize.Y * 0.5f), fact.Value,
                VelvetTheme.TitleInk, FactValueStyle);
        }
        else
        {
            var labelTop = origin.Y + StackedPadY * scale;
            Typography.Draw(drawList, new Vector2(textLeft, labelTop), fact.Label, VelvetTheme.MutedInk,
                StackedLabelStyle);
            var valueTop = labelTop + Typography.LineHeight(StackedLabelStyle) + StackedGap * scale;
            Typography.DrawWrappedLeft(new Vector2(textLeft, valueTop), fact.Value, VelvetTheme.TitleInk,
                StackedValueStyle, StackedWidth(width, scale));
        }

        if (separator)
        {
            FeedCell.Hairline(drawList, textLeft, right, origin.Y + height, VelvetTheme.Hairline);
        }
    }

    public static void Tile(ImDrawListPtr drawList, Vector2 min, Vector2 max, string glyph, Vector4 tone,
        float glyphSize, float scale)
    {
        Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale, VelvetTheme.Alpha(tone, TileAlpha).Packed());
        PhoneIcon.Draw(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), glyph,
            VelvetTheme.ToneInk(tone), glyphSize);
    }

    private static bool Inline(in VFact fact, float width, float scale, out Vector2 valueSize)
    {
        valueSize = Typography.Measure(fact.Value, FactValueStyle);
        var labelWidth = Typography.Measure(fact.Label, FactLabelStyle).X;
        var available = StackedWidth(width, scale) - labelWidth - RowValueGap * scale;
        return valueSize.X <= available;
    }

    private static float StackedWidth(float width, float scale) =>
        MathF.Max(1f, width - (RowTile + RowLabelGap) * scale);
}
