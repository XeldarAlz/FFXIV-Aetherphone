using Aetherphone.Core;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NameplateStage
{
    private const float Height = 132f;
    private const float Inset = 18f;
    private const float NameTitleGap = 4f;
    private const float HaloRadius = 1.4f;
    private const string OpenQuote = "《";
    private const string CloseQuote = "》";

    private static readonly Vector4 Top = new(0.05f, 0.09f, 0.13f, 1f);
    private static readonly Vector4 Bottom = new(0.08f, 0.19f, 0.18f, 1f);
    private static readonly Vector4 NameInk = new(0.95f, 0.96f, 1f, 1f);
    private static readonly Vector4 NameHalo = new(0.04f, 0.16f, 0.29f, 1f);

    private string quotedSource = string.Empty;
    private string quotedTitle = string.Empty;

    public float DrawPinned(Rect body, PhoneTheme theme, float scale, string name, in NameplateTitle title,
        string caption)
    {
        var stage = Paint(body, theme, scale);
        DrawTitle(stage, scale, name, title);
        return DrawCaption(stage, caption, theme, scale);
    }

    public static float DrawPinnedMessage(Rect body, PhoneTheme theme, float scale, string message)
    {
        var stage = Paint(body, theme, scale);
        Typography.DrawCenteredHalo(stage.Center, message, NameInk, NameHalo, HaloRadius * scale,
            stage.Width - Inset * 2f * scale, TextStyles.Headline);
        return stage.Max.Y;
    }

    public static string NameOf(NameplateTitleService titles) =>
        titles.CharacterName.Length > 0 ? titles.CharacterName : Loc.T(L.Nameplate.Example);

    public static string CaptionOf(NameplateTitleService titles)
    {
        if (!titles.Current.IsNone)
        {
            return Loc.T(L.Nameplate.Live);
        }

        return titles.Settings.Enabled ? Loc.T(L.Nameplate.NothingActive) : Loc.T(L.Nameplate.Example);
    }

    private void DrawTitle(Rect stage, float scale, string name, in NameplateTitle title)
    {
        var maxWidth = stage.Width - Inset * 2f * scale;
        var nameHeight = Typography.LineHeight(TextStyles.Title3);
        var titleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var gap = NameTitleGap * scale;
        var blockTop = stage.Center.Y - (nameHeight + gap + titleHeight) * 0.5f;
        var nameCenterY = title.Prefix ? blockTop + titleHeight + gap + nameHeight * 0.5f : blockTop + nameHeight * 0.5f;
        var titleCenterY = title.Prefix
            ? blockTop + titleHeight * 0.5f
            : blockTop + nameHeight + gap + titleHeight * 0.5f;
        Typography.DrawCenteredHalo(new Vector2(stage.Center.X, nameCenterY), name, NameInk, NameHalo,
            HaloRadius * scale, maxWidth, TextStyles.Title3);
        if (title.IsNone)
        {
            return;
        }

        var look = title.Look;
        var center = new Vector2(stage.Center.X, titleCenterY);
        var quoted = Quoted(title.Text);
        var ink = new Vector4(look.Color, 1f);
        Span<Vector4> halos = stackalloc Vector4[quoted.Length];
        if (TryGradientHalos(halos, look, title.Text.Length))
        {
            Typography.DrawCenteredHalo(center, quoted, ink, halos, HaloRadius * scale, maxWidth,
                TextStyles.SubheadlineEmphasized);
            return;
        }

        var glow = look.Glow ?? look.Color3 ?? NameplatePalette.DarkGlow;
        Typography.DrawCenteredHalo(center, quoted, ink, new Vector4(glow, 1f), HaloRadius * scale, maxWidth,
            TextStyles.SubheadlineEmphasized);
    }

    private static float DrawCaption(Rect stage, string caption, PhoneTheme theme, float scale)
    {
        if (caption.Length == 0)
        {
            return stage.Max.Y;
        }

        var inset = Metrics.Space.Lg * scale;
        var top = stage.Max.Y + Metrics.Space.Sm * scale;
        return top + Typography.DrawWrappedLeft(new Vector2(stage.Min.X + inset, top), caption, theme.TextMuted,
            TextStyles.Footnote, stage.Width - inset * 2f);
    }

    private static bool TryGradientHalos(Span<Vector4> halos, in TitleLook look, int length)
    {
        var tick = NameplateGradients.Tick(Environment.TickCount64);
        var edge = new Vector4(NameplatePalette.DarkGlow, 1f);
        halos.Fill(edge);
        for (var glyph = 0; glyph < length; glyph++)
        {
            if (!NameplateGradients.TryGlyphGlow(look, glyph, length, tick, out var glow))
            {
                return false;
            }

            halos[OpenQuote.Length + glyph] = new Vector4(glow, 1f);
        }

        return length > 0;
    }

    private static Rect Paint(Rect body, PhoneTheme theme, float scale)
    {
        var inset = AppSurface.SidePadding * scale;
        var min = new Vector2(body.Min.X + inset, body.Min.Y + Metrics.Space.Md * scale);
        var max = new Vector2(body.Max.X - inset, min.Y + Height * scale);
        var drawList = ImGui.GetWindowDrawList();
        GroupCard.DrawSurface(drawList, min, max, theme, scale);
        Squircle.FillVerticalGradient(drawList, min, max, Metrics.Radius.Grouped * scale, ImGui.GetColorU32(Top),
            ImGui.GetColorU32(Bottom));
        return new Rect(min, max);
    }

    private string Quoted(string text)
    {
        if (!ReferenceEquals(text, quotedSource))
        {
            quotedSource = text;
            quotedTitle = string.Concat(OpenQuote, text, CloseQuote);
        }

        return quotedTitle;
    }
}
