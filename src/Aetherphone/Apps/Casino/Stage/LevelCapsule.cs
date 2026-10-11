using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal struct LevelCapsule
{
    public const float Height = 40f;

    private const float RingThickness = 3f;
    private const float BarHeight = 5f;
    private const float Gap = 8f;

    private LabelSlot levelLabel;
    private string? capLabel;
    private long capValue;
    private LanguageInfo? capLanguage;

    public float Draw(ImDrawListPtr drawList, Rect rect, int level, float progress, long cap, Vector4 accent,
        float scale)
    {
        var height = MathF.Min(rect.Height, Height * scale);
        var radius = height * 0.5f;
        var ringCenter = new Vector2(rect.Min.X + radius, rect.Min.Y + radius);
        var fraction = Math.Clamp(progress, 0f, 1f);
        var thickness = RingThickness * scale;
        ProgressRing.Track(drawList, ringCenter, radius - thickness, thickness,
            CasinoColors.InkMuted with { W = 0.35f });
        if (fraction > 0f)
        {
            ProgressRing.Fill(drawList, ringCenter, radius - thickness, thickness, fraction, accent);
        }

        Typography.DrawCentered(drawList, ringCenter, GameNumber.Label(level), CasinoColors.InkTitle,
            TextStyles.SubheadlineEmphasized);

        var textLeft = ringCenter.X + radius + Gap * scale;
        var textWidth = MathF.Max(0f, rect.Max.X - textLeft);
        var title = Typography.FitText(levelLabel.Get(L.Strip.Level, level), textWidth, TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, rect.Min.Y), title, CasinoColors.InkTitle,
            TextStyles.FootnoteEmphasized);
        if (cap > 0)
        {
            var capText = Typography.FitText(CapText(cap), textWidth * 0.5f, TextStyles.FootnoteEmphasized);
            var capSize = Typography.Measure(capText, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(rect.Max.X - capSize.X, rect.Min.Y + (titleHeight - capSize.Y) * 0.5f),
                capText, CasinoColors.Money, TextStyles.FootnoteEmphasized);
        }

        var barTop = rect.Min.Y + titleHeight + Gap * 0.5f * scale;
        var barMin = new Vector2(textLeft, barTop);
        var barMax = new Vector2(rect.Max.X, barTop + BarHeight * scale);
        Squircle.Fill(drawList, barMin, barMax, BarHeight * 0.5f * scale,
            ImGui.GetColorU32(CasinoColors.InkMuted with { W = 0.25f }));
        if (fraction > 0f)
        {
            var filled = MathF.Max(barMin.X + BarHeight * scale, barMin.X + (barMax.X - barMin.X) * fraction);
            Squircle.Fill(drawList, barMin, new Vector2(filled, barMax.Y), BarHeight * 0.5f * scale,
                ImGui.GetColorU32(accent));
        }

        return rect.Min.Y + height;
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
