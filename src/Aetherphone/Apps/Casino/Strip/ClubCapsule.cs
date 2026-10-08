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

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class ClubCapsule
{
    public const float Height = 64f;
    public const float Pad = 14f;
    public const float Tile = 36f;
    public const float BarHeight = 5f;

    private readonly CasinoTextCache texts = new();

    public bool Draw(ImDrawListPtr drawList, AppSkin ui, CasinoClubDto club, Vector2 origin, float width, float scale,
        out float bottom)
    {
        var height = Height * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        bottom = max.Y;
        var hovered = CasinoArt.PressCard(ImGui.GetID("casino.club.capsule"), origin, max, out var min, out var shown);
        var tier = CasinoClubTiers.Clamp(club.Tier);
        var tint = CashierClubCard.TierTint(tier);
        var radius = height * 0.5f;
        Squircle.Fill(drawList, min, shown, radius, ImGui.GetColorU32(Palette.Mix(ui.Palette.BackdropTop, tint, 0.16f)));
        Squircle.Stroke(drawList, min, shown, radius, ImGui.GetColorU32(tint with { W = 0.55f }), MathF.Max(1f, scale));
        var pad = Pad * scale;
        var tile = Tile * scale;
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, (min.Y + shown.Y) * 0.5f);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, tint, FontAwesomeIcon.Crown);
        var chevron = new Vector2(shown.X - pad - 4f * scale, tileCenter.Y);
        CasinoArt.Chevron(drawList, chevron, ui.BodyInk);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textWidth = MathF.Max(1f, chevron.X - CoinArt.ValueGap * scale - textLeft);
        var title = texts.Named(L.Strip.ClubTierTitle, Loc.T(CashierClubCard.TierName(tier)));
        var headline = Typography.LineHeight(TextStyles.Headline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var top = tileCenter.Y - (headline + BarHeight * scale + Metrics.Space.Xs * scale + footnote) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(title, textWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        top += headline;
        CoinArt.Bar(drawList, new Vector2(textLeft, top), new Vector2(textLeft + textWidth, top + BarHeight * scale),
            CasinoClubTiers.Progress(club), Palette.WithAlpha(ui.MutedInk, 0.25f), tint);
        top += BarHeight * scale + Metrics.Space.Xs * scale;
        var line = tier >= CasinoClubTiers.Obsidian || club.NextTierPoints <= club.Points
            ? Loc.T(L.Strip.ClubTop)
            : texts.NamedNumber(L.Strip.ClubPointsTo, Loc.T(CashierClubCard.TierName(tier + 1)),
                Math.Max(0, club.NextTierPoints - club.Points));
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(line, textWidth, TextStyles.Footnote),
            ui.BodyInk, TextStyles.Footnote);
        return UiInteract.Click(origin, max, hovered);
    }
}
