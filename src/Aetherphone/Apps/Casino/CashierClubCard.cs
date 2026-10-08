using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierClubCard
{
    private const float Pad = 16f;
    private const float TileSize = 40f;
    private const float PipRadius = 4.5f;
    private const float BarHeight = 6f;
    private const float RowGap = 10f;

    private static readonly LocString[] TierNames =
    {
        L.Strip.ClubBronze,
        L.Strip.ClubSilver,
        L.Strip.ClubGold,
        L.Strip.ClubPlatinum,
        L.Strip.ClubDiamond,
        L.Strip.ClubRoyal,
        L.Strip.ClubObsidian,
    };

    private static readonly Vector4[] TierTints =
    {
        new(0.80f, 0.50f, 0.28f, 1f),
        new(0.76f, 0.79f, 0.84f, 1f),
        CasinoColors.Money,
        new(0.62f, 0.86f, 0.92f, 1f),
        new(0.55f, 0.80f, 1f, 1f),
        CasinoColors.LightA,
        new(0.42f, 0.36f, 0.58f, 1f),
    };

    private readonly CasinoTextCache texts = new();
    private string? perks;
    private int perksMultiplier;
    private int perksRebate;
    private LanguageInfo? perksLanguage;

    public static LocString TierName(int tier) => TierNames[CasinoClubTiers.Clamp(tier)];

    public static Vector4 TierTint(int tier) => TierTints[CasinoClubTiers.Clamp(tier)];

    public float Height(float scale)
    {
        var title = Typography.LineHeight(TextStyles.Headline);
        var line = Typography.LineHeight(TextStyles.Footnote);
        return (Pad * 2f + RowGap * 2f + BarHeight + PipRadius * 2f) * scale + MathF.Max(title + line, TileSize * scale)
            + line;
    }

    public float Draw(ImDrawListPtr drawList, AppSkin ui, CasinoClubDto club, float left, float top, float width,
        float scale)
    {
        var height = Height(scale);
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var tier = CasinoClubTiers.Clamp(club.Tier);
        var tint = TierTints[tier];
        var pad = Pad * scale;
        var tile = TileSize * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var headHeight = MathF.Max(titleHeight + lineHeight, tile);
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, min.Y + pad + headHeight * 0.5f);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, tint, FontAwesomeIcon.Crown);

        var textLeft = tileCenter.X + tile * 0.5f + 12f * scale;
        var textWidth = max.X - pad - textLeft;
        var textTop = min.Y + pad + (headHeight - titleHeight - lineHeight) * 0.5f;
        var title = texts.Named(L.Strip.ClubTierTitle, Loc.T(TierNames[tier]));
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(title, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        var perks = PerksLine(club);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(perks, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        var y = min.Y + pad + headHeight + RowGap * scale;
        var barMin = new Vector2(min.X + pad, y);
        var barMax = new Vector2(max.X - pad, y + BarHeight * scale);
        CoinArt.Bar(drawList, barMin, barMax, CasinoClubTiers.Progress(club), Palette.WithAlpha(ui.MutedInk, 0.25f),
            tint);
        y = barMax.Y + RowGap * scale * 0.6f;
        var progress = tier >= CasinoClubTiers.Obsidian || club.NextTierPoints <= club.Points
            ? Loc.T(L.Strip.ClubTop)
            : texts.NamedNumber(L.Strip.ClubPointsTo, Loc.T(TierNames[Math.Min(tier + 1, CasinoClubTiers.Obsidian)]),
                Math.Max(0, club.NextTierPoints - club.Points));
        Typography.Draw(drawList, new Vector2(min.X + pad, y),
            Typography.FitText(progress, width - pad * 2f, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);

        var pipY = max.Y - pad - PipRadius * scale;
        var span = width - pad * 2f - PipRadius * 2f * scale;
        for (var index = 0; index < CasinoClubTiers.Count; index++)
        {
            var center = new Vector2(min.X + pad + PipRadius * scale + span * index / (CasinoClubTiers.Count - 1),
                pipY);
            if (index <= tier)
            {
                drawList.AddCircleFilled(center, PipRadius * scale, ImGui.GetColorU32(TierTints[index]), 16);
                continue;
            }

            drawList.AddCircle(center, PipRadius * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.55f)), 16,
                1f * scale);
        }

        return max.Y;
    }

    private string PerksLine(CasinoClubDto club)
    {
        var multiplier = Math.Max(100, club.MultiplierPercent);
        var rebate = Math.Max(0, club.RebateBasisPoints);
        if (perks is not null && multiplier == perksMultiplier && rebate == perksRebate
            && ReferenceEquals(perksLanguage, Loc.Current))
        {
            return perks;
        }

        perksMultiplier = multiplier;
        perksRebate = rebate;
        perksLanguage = Loc.Current;
        var factor = (multiplier / 100m).ToString("0.##", Loc.Culture);
        perks = rebate <= 0
            ? Loc.T(L.Strip.ClubPerksNoRebate, factor)
            : Loc.T(L.Strip.ClubPerks, factor, (rebate / 100m).ToString("0.##", Loc.Culture));
        return perks;
    }
}
