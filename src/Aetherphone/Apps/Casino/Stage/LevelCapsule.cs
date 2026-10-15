using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal struct LevelCapsule
{
    public const float Ring = 56f;
    public const float RingThickness = 4f;
    public const float BarHeight = 6f;
    public const float Gap = 10f;
    public const float LineGap = 5f;

    private static readonly TextStyle LineStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle TierStyle = TextStyles.Footnote;

    private LabelSlot levelLabel;
    private string? capLabel;
    private long capValue;
    private LanguageInfo? capLanguage;

    public static float Height(float scale) => MathF.Max(Ring * scale, TextBlock(BalanceTitle.Vip, scale));

    public static float TextBlock(BalanceTitle title, float scale)
    {
        var barBlock = BarHeight * scale + LineGap * scale + Typography.LineHeight(LineStyle);
        return title == BalanceTitle.None ? barBlock : TierLine(title, scale) + LineGap * scale + barBlock;
    }

    private static float TierLine(BalanceTitle title, float scale) =>
        MathF.Max(Typography.LineHeight(TierStyle), StatusTitle.PillHeight(title, scale));

    public float Draw(ImDrawListPtr drawList, Rect rect, int level, float progress, long cap, BalanceTitle title,
        Vector4 accent, float scale)
    {
        var ring = Ring * scale;
        var radius = ring * 0.5f;
        var ringCenter = new Vector2(rect.Min.X + radius, rect.Center.Y);
        var fraction = Math.Clamp(progress, 0f, 1f);
        var thickness = RingThickness * scale;
        ProgressRing.Track(drawList, ringCenter, radius - thickness, thickness,
            CasinoColors.InkMuted with { W = 0.35f });
        if (fraction > 0f)
        {
            ProgressRing.Fill(drawList, ringCenter, radius - thickness, thickness, fraction, accent);
        }

        var number = GameNumber.Label(level);
        var numberStyle = TextStyles.Title3;
        var numberFit = Typography.FitScale(number, ring - thickness * 4f, numberStyle.Scale, TextStyles.Subheadline.Scale,
            numberStyle.Weight);
        Typography.DrawCentered(drawList, ringCenter, Typography.FitText(number, ring - thickness * 4f, numberFit,
            numberStyle.Weight), CasinoColors.InkTitle, numberFit, numberStyle.Weight);

        var textLeft = ringCenter.X + radius + Gap * scale;
        var textWidth = MathF.Max(1f, rect.Max.X - textLeft);
        var block = TextBlock(title, scale);
        var top = rect.Center.Y - block * 0.5f;
        if (title != BalanceTitle.None)
        {
            var tierLine = TierLine(title, scale);
            var caption = Loc.T(L.Strip.CurrentTier);
            var pillMax = MathF.Max(1f, textWidth * 0.5f);
            var pillWidth = StatusTitle.Width(title, pillMax, scale);
            var captionWidth = MathF.Max(1f, textWidth - pillWidth - Metrics.Space.Xs * scale);
            var fitted = Typography.FitText(caption, captionWidth, TierStyle);
            var captionSize = Typography.Measure(fitted, TierStyle);
            var lineCenter = top + tierLine * 0.5f;
            Typography.Draw(drawList, new Vector2(textLeft, lineCenter - captionSize.Y * 0.5f), fitted,
                CasinoColors.InkBody, TierStyle);
            StatusTitle.Draw(drawList,
                new Vector2(textLeft + captionSize.X + Metrics.Space.Xs * scale + pillWidth * 0.5f, lineCenter), title,
                pillMax, scale);
            top += tierLine + LineGap * scale;
        }

        var barMin = new Vector2(textLeft, top);
        var barMax = new Vector2(rect.Max.X, top + BarHeight * scale);
        Squircle.Fill(drawList, barMin, barMax, BarHeight * 0.5f * scale,
            ImGui.GetColorU32(CasinoColors.InkMuted with { W = 0.25f }));
        if (fraction > 0f)
        {
            var filled = MathF.Max(barMin.X + BarHeight * scale, barMin.X + (barMax.X - barMin.X) * fraction);
            Squircle.Fill(drawList, barMin, new Vector2(filled, barMax.Y), BarHeight * 0.5f * scale,
                ImGui.GetColorU32(accent));
        }

        top = barMax.Y + LineGap * scale;
        var capText = cap > 0 ? CapText(cap) : string.Empty;
        var capSize = capText.Length > 0 ? Typography.Measure(capText, LineStyle) : Vector2.Zero;
        var levelWidth = MathF.Max(1f, textWidth - (capText.Length > 0 ? capSize.X + Metrics.Space.Sm * scale : 0f));
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(levelLabel.Get(L.Strip.Level, level), levelWidth, LineStyle), CasinoColors.InkTitle,
            LineStyle);
        if (capText.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(rect.Max.X - capSize.X, top), capText, CasinoColors.Money, LineStyle);
        }

        return rect.Min.Y + MathF.Max(ring, block);
    }

    private string CapText(long cap)
    {
        if (capLabel is not null && cap == capValue && ReferenceEquals(capLanguage, Loc.Current))
        {
            return capLabel;
        }

        capValue = cap;
        capLanguage = Loc.Current;
        capLabel = Loc.T(L.Strip.Cap, NumberText.Compact(cap));
        return capLabel;
    }
}
