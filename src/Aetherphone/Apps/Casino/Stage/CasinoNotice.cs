using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal enum CasinoNoticeKind : byte
{
    Reason,
    Info,
    Card,
}

internal static class CasinoNotice
{
    private const float TintedPad = 12f;
    private const float CardPad = 14f;
    private const float TintedTitleGap = 4f;
    private const float CardTitleGap = 6f;
    private const float TintFill = 0.10f;
    private const float TintStroke = 0.35f;
    private const float ActionWidthShare = 0.6f;

    public static float Height(CasinoNoticeKind kind, string title, string body, float width, float scale)
    {
        var pad = Pad(kind) * scale;
        var inner = width - pad * 2f;
        var titleHeight = title.Length > 0 ? Typography.LineHeight(TitleStyle(kind)) + TitleGap(kind) * scale : 0f;
        var bodyHeight = body.Length > 0 ? Typography.MeasureWrappedBlock(body, BodyStyle(kind), inner).Y : 0f;
        return titleHeight + bodyHeight + pad * 2f;
    }

    public static float Draw(ImDrawListPtr drawList, AppSkin ui, CasinoNoticeKind kind, string title, string body,
        float left, float y, float width, float scale)
    {
        var height = Height(kind, title, body, width, scale);
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        var rounding = Metrics.Radius.Grouped * scale;
        if (kind == CasinoNoticeKind.Card)
        {
            ui.Card(drawList, min, max, rounding);
        }
        else
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, TintFill)));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, TintStroke)),
                MathF.Max(1f, scale));
        }

        var pad = Pad(kind) * scale;
        var inner = width - pad * 2f;
        var textTop = min.Y + pad;
        if (title.Length > 0)
        {
            var titleStyle = TitleStyle(kind);
            Typography.Draw(drawList, new Vector2(min.X + pad, textTop), Typography.FitText(title, inner, titleStyle),
                ui.TitleInk, titleStyle);
            textTop += Typography.LineHeight(titleStyle) + TitleGap(kind) * scale;
        }

        if (body.Length > 0)
        {
            var bodyInk = kind == CasinoNoticeKind.Reason ? ui.TitleInk : ui.MutedInk;
            Typography.DrawWrappedLeft(new Vector2(min.X + pad, textTop), body, bodyInk, BodyStyle(kind), inner);
        }

        return max.Y;
    }

    public static float DrawWithAction(ImDrawListPtr drawList, AppSkin ui, CasinoNoticeKind kind, string title,
        string body, string action, float left, float y, float width, float scale, out bool pressed)
    {
        var bottom = Draw(drawList, ui, kind, title, body, left, y, width, scale);
        var pillTop = bottom + Metrics.Space.Md * scale;
        var inset = width * (1f - ActionWidthShare) * 0.5f;
        var pill = new Rect(new Vector2(left + inset, pillTop),
            new Vector2(left + width - inset, pillTop + Button.LargeHeight * scale));
        pressed = Button.Draw(drawList, pill, action, ui.Ink);
        return pill.Max.Y;
    }

    private static float Pad(CasinoNoticeKind kind) => kind == CasinoNoticeKind.Card ? CardPad : TintedPad;

    private static float TitleGap(CasinoNoticeKind kind) => kind == CasinoNoticeKind.Card ? CardTitleGap : TintedTitleGap;

    private static TextStyle TitleStyle(CasinoNoticeKind kind) =>
        kind == CasinoNoticeKind.Card ? TextStyles.SubheadlineEmphasized : TextStyles.FootnoteEmphasized;

    private static TextStyle BodyStyle(CasinoNoticeKind kind) => kind switch
    {
        CasinoNoticeKind.Info => TextStyles.Caption1,
        _ => TextStyles.Footnote,
    };
}
