using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class ClubSheet
{
    private const float HeightShare = 0.78f;
    private const float RowHeight = 72f;
    private const float Tile = 36f;

    private readonly SheetSurface sheet = new("casino.club");
    private readonly Action<Rect> drawBody;
    private readonly CasinoTextCache texts = new();
    private readonly string[] perks = new string[CasinoClubTiers.Count];
    private LanguageInfo? perksLanguage;
    private AppSkin skin = null!;
    private int currentTier;

    public ClubSheet()
    {
        drawBody = DrawBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open(int tier)
    {
        currentTier = CasinoClubTiers.Clamp(tier);
        sheet.Open();
    }

    public void Close() => sheet.Close();

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        skin = ui;
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(L.Club.TiersTitle), HeightShare, drawBody);
    }

    internal static string PerkLine(int tier)
    {
        var multiplier = (CasinoClubTiers.MultiplierPercents[tier] / 100m).ToString("0.##", Loc.Culture);
        var rebate = CasinoClubTiers.RebateBasisPoints[tier];
        var line = rebate <= 0
            ? Loc.T(L.Strip.ClubPerksNoRebate, multiplier)
            : Loc.T(L.Strip.ClubPerks, multiplier, (rebate / 100m).ToString("0.##", Loc.Culture));
        return tier >= CasinoClubTiers.ReloadFloor ? Loc.T(L.Club.PerksWithReload, line) : line;
    }

    private void DrawBody(Rect content)
    {
        var scale = UiScale.Current;
        if (!ReferenceEquals(perksLanguage, Loc.Current))
        {
            perksLanguage = Loc.Current;
            for (var tier = 0; tier < perks.Length; tier++)
            {
                perks[tier] = PerkLine(tier);
            }
        }

        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##casinoClubTiers", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.NativeScrollContentWidth();
            var origin = ImGui.GetCursorScreenPos();
            var intro = Typography.DrawWrappedLeft(origin, Loc.T(L.Club.TiersIntro), skin.BodyInk, TextStyles.Subheadline,
                width);
            var top = origin.Y + intro + Metrics.Space.Md * scale;
            var perkWidth = PerkWidth(width, scale);
            for (var tier = 0; tier < CasinoClubTiers.Count; tier++)
            {
                var lines = Typography.WrapText(perks[tier], TextStyles.Footnote, perkWidth).Length;
                var rowHeight = RowHeightFor(lines, Typography.LineHeight(TextStyles.Headline),
                    Typography.LineHeight(TextStyles.Footnote), scale);
                var row = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + rowHeight));
                DrawTier(drawList, row, tier, scale);
                top += rowHeight + Metrics.Space.Xs * scale;
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
            ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
        }
    }

    private void DrawTier(ImDrawListPtr drawList, Rect row, int tier, float scale)
    {
        var tint = ClubTierArt.TierTint(tier);
        var radius = Metrics.Radius.Grouped * scale;
        var current = tier == currentTier;
        skin.Card(drawList, row.Min, row.Max, radius);
        if (current)
        {
            Squircle.Stroke(drawList, row.Min, row.Max, radius, ImGui.GetColorU32(tint), MathF.Max(1.5f, 2f * scale));
        }

        var pad = Metrics.Space.Lg * scale;
        var tile = Tile * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, tier <= currentTier ? tint : AccentRing.Slate,
            FontAwesomeIcon.Crown);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var badge = current ? Loc.T(L.Club.YouAreHere) : string.Empty;
        var badgeWidth = badge.Length > 0 ? Typography.Measure(badge, TextStyles.FootnoteEmphasized).X : 0f;
        var textWidth = MathF.Max(1f, row.Max.X - pad - textLeft - (badgeWidth > 0f ? badgeWidth + Metrics.Space.Sm * scale : 0f));
        var headline = Typography.LineHeight(TextStyles.Headline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var perkLines = Typography.WrapText(perks[tier], TextStyles.Footnote, PerkWidth(row.Width, scale));
        var top = row.Center.Y - (headline + footnote * (1 + perkLines.Length)) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(ClubTierArt.TierName(tier)), textWidth, TextStyles.Headline), skin.TitleInk,
            TextStyles.Headline);
        if (badgeWidth > 0f)
        {
            Typography.Draw(drawList, new Vector2(row.Max.X - pad - badgeWidth, top), badge, tint,
                TextStyles.FootnoteEmphasized);
        }

        top += headline;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(texts.Compact(L.Club.TierFloor, CasinoClubTiers.Floors[tier]), textWidth,
                TextStyles.Footnote), skin.BodyInk, TextStyles.Footnote);
        top += footnote;
        for (var line = 0; line < perkLines.Length; line++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top), perkLines[line], skin.BodyInk, TextStyles.Footnote);
            top += footnote;
        }
    }

    internal static float PerkWidth(float rowWidth, float scale) =>
        MathF.Max(1f, rowWidth - Metrics.Space.Lg * scale * 2f - Tile * scale - CoinArt.TextGap * scale);

    internal static float RowHeightFor(int perkLines, float headline, float footnote, float scale) =>
        MathF.Max(RowHeight * scale,
            headline + footnote * (1 + Math.Max(1, perkLines)) + Metrics.Space.Md * scale * 2f);
}
