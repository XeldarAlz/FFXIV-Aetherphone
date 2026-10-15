using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HostCards
{
    private const float SelectedWashAlpha = 0.26f;
    private const float SelectedRimWidth = 2f;
    private const float FooterHairlineAlpha = 0.10f;
    private const float ReasonWashAlpha = 0.14f;
    private const float ReasonPad = 10f;
    private const float CheckGlyphFactor = 0.5f;

    public static void Gap(float units, float scale) => ImGui.Dummy(new Vector2(0f, units * scale));

    public static void Heading(AppSkin ui, string title, string aside, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var titleText = Typography.FitText(title, aside.Length > 0 ? width * 0.7f : width, TextStyles.Headline);
        Typography.Draw(drawList, origin, titleText, ui.TitleInk, TextStyles.Headline);
        if (aside.Length > 0)
        {
            var asideLeft = origin.X + Typography.Measure(titleText, TextStyles.Headline).X + Metrics.Space.Xs * scale;
            var asideHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(asideLeft, origin.Y + titleHeight - asideHeight),
                Typography.FitText(aside, MathF.Max(1f, origin.X + width - asideLeft), TextStyles.Footnote),
                ui.MutedInk, TextStyles.Footnote);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, titleHeight + HostFlowLayout.HeadingGap * scale));
    }

    public static void Hint(AppSkin ui, string text, float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(origin, text, ui.BodyInk, TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    public static void Note(AppSkin ui, string text, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var inset = HostFlowLayout.LineGap * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + inset, origin.Y + inset), text, ui.MutedInk,
            TextStyles.Footnote, MathF.Max(1f, width - inset * 2f));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + inset));
    }

    public static void InfoCard(AppSkin ui, string text, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pad = HostFlowLayout.CardPad * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var height = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, inner).Y + pad * 2f;
        ui.Card(ImGui.GetWindowDrawList(), origin, new Vector2(origin.X + width, origin.Y + height),
            Metrics.Radius.Grouped * scale);
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), text, ui.BodyInk,
            TextStyles.Subheadline, inner);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    public static void Ladder(AppSkin ui, string id, string label, string value, string hint, LadderSpan span,
        ref int index, float width, float scale)
    {
        var low = span.First;
        LadderCard(ui, id, label, value, hint, span, ref low, ref index, false, width, scale);
    }

    public static void Range(AppSkin ui, string id, string label, string value, string hint, LadderSpan span,
        ref int low, ref int high, float width, float scale) =>
        LadderCard(ui, id, label, value, hint, span, ref low, ref high, true, width, scale);

    public static float LadderBlockHeight(string hint, float inner, in TextStyle valueStyle, float scale)
    {
        var hintHeight = hint.Length > 0 ? Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, inner).Y : 0f;
        return HostFlowLayout.LadderBlockHeight(scale, Typography.LineHeight(TextStyles.FootnoteEmphasized),
            Typography.LineHeight(valueStyle), hintHeight);
    }

    public static void LadderBlock(ImDrawListPtr drawList, AppSkin ui, string id, Vector2 topLeft, float inner,
        string label, string value, string hint, LadderSpan span, ref int low, ref int high, bool range,
        in TextStyle valueStyle, float scale)
    {
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var valueHeight = Typography.LineHeight(valueStyle);
        Typography.Draw(drawList, topLeft, Typography.FitText(label, inner, TextStyles.FootnoteEmphasized),
            ui.MutedInk, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(topLeft.X, topLeft.Y + labelHeight),
            Typography.FitText(value, inner, valueStyle), CasinoColors.Money, valueStyle);
        var row = HostFlowLayout.LadderRow(topLeft.X, topLeft.Y, inner, scale, labelHeight, valueHeight);
        var rail = Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary);
        if (range)
        {
            LadderSlider.Range(drawList, id, row, span, ref low, ref high, ui.Accent, rail, scale);
        }
        else
        {
            LadderSlider.Single(drawList, id, row, span, ref high, ui.Accent, rail, scale);
        }

        if (hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(topLeft.X, row.Max.Y), hint, ui.MutedInk, TextStyles.Footnote,
                inner);
        }
    }

    public static void ChoiceSurface(ImDrawListPtr drawList, AppSkin ui, in Rect face, float radius, Vector4 tint,
        bool selected, bool hovered, float scale)
    {
        ui.Card(drawList, face.Min, face.Max, radius);
        if (selected)
        {
            Squircle.FillVerticalGradient(drawList, face.Min, face.Max, radius,
                ImGui.GetColorU32(tint with { W = SelectedWashAlpha }), ImGui.GetColorU32(tint with { W = 0f }));
            Squircle.Stroke(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(tint), SelectedRimWidth * scale);
            return;
        }

        if (hovered)
        {
            Squircle.Fill(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(ui.HoverTint));
        }
    }

    public static void Check(ImDrawListPtr drawList, Vector2 center, float size, Vector4 fill)
    {
        drawList.AddCircleFilled(center, size * 0.5f, ImGui.GetColorU32(fill), 24);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Check, CasinoArt.White, size * CheckGlyphFactor);
    }

    public static float FooterBlock(string reason, string line, string note, float width, float scale)
    {
        var height = Typography.MeasureWrappedBlock(line, TextStyles.Subheadline, width).Y;
        if (reason.Length > 0)
        {
            height += ReasonHeight(reason, width, scale) + HostFlowLayout.CardGap * scale;
        }

        if (note.Length > 0)
        {
            height += HostFlowLayout.LineGap * scale
                + Typography.MeasureWrappedBlock(note, TextStyles.Footnote, width).Y;
        }

        return height;
    }

    public static bool Footer(AppSkin ui, Rect footer, string reason, string line, string note, string label,
        bool enabled, string id, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bandMin = new Vector2(footer.Min.X - AppSurface.SidePadding * scale, footer.Min.Y);
        var bandMax = new Vector2(footer.Max.X + AppSurface.SidePadding * scale, footer.Max.Y);
        Material.ThemedGlass(drawList, bandMin, bandMax, 0f, scale, ui.BackdropColor, TabBar.GlassOpacity);
        drawList.AddLine(bandMin, new Vector2(bandMax.X, bandMin.Y),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, FooterHairlineAlpha)), MathF.Max(1f, 0.5f * scale));
        var top = footer.Min.Y + HostFlowLayout.FooterPad * scale;
        if (reason.Length > 0)
        {
            top = DrawReason(drawList, ui, new Vector2(footer.Min.X, top), reason, footer.Width, scale)
                + HostFlowLayout.CardGap * scale;
        }

        var lineHeight = Typography.DrawWrappedLeft(new Vector2(footer.Min.X, top), line, ui.BodyInk,
            TextStyles.Subheadline, footer.Width);
        if (note.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(footer.Min.X, top + lineHeight + HostFlowLayout.LineGap * scale),
                note, ui.BodyInk, TextStyles.Footnote, footer.Width);
        }

        return Button.Draw(drawList, HostFlowLayout.FooterButton(footer, scale), label, ui.Ink,
            ButtonStyle.Prominent, enabled: enabled, id: id);
    }

    private static float ReasonHeight(string reason, float width, float scale)
    {
        var pad = ReasonPad * scale;
        return Typography.MeasureWrappedBlock(reason, TextStyles.Subheadline, MathF.Max(1f, width - pad * 2f)).Y
            + pad * 2f;
    }

    private static float DrawReason(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, string reason, float width,
        float scale)
    {
        var pad = ReasonPad * scale;
        var max = new Vector2(origin.X + width, origin.Y + ReasonHeight(reason, width, scale));
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, ReasonWashAlpha)));
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), reason, ui.TitleInk,
            TextStyles.Subheadline, MathF.Max(1f, width - pad * 2f));
        return max.Y;
    }

    private static void LadderCard(AppSkin ui, string id, string label, string value, string hint, LadderSpan span,
        ref int low, ref int high, bool range, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = HostFlowLayout.CardPad * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var height = HostFlowLayout.LadderCardHeight(scale, LadderBlockHeight(hint, inner, TextStyles.Title2, scale));
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Grouped * scale);
        LadderBlock(drawList, ui, id, new Vector2(origin.X + pad, origin.Y + HostFlowLayout.CardPadTop * scale), inner,
            label, value, hint, span, ref low, ref high, range, TextStyles.Title2, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }
}
