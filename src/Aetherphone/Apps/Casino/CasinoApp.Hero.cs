using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float HeroOverlayPad = 16f;
    private const float HeroScrimFrom = 0.38f;
    private const float JackpotSignHeight = 24f;

    private static readonly Vector4 JackpotCrown = new(0.36f, 0.06f, 0.16f, 1f);
    private static readonly Vector4 JackpotBed = new(0.07f, 0.02f, 0.05f, 1f);
    private static readonly Vector4 HeroScrim = new(0.02f, 0.015f, 0.04f, 0.92f);

    private readonly CabinetPreview[] heroPreviews = { new(), new(), new(), new() };
    private RollingAmount jackpotRoll;
    private CasinoTableRowDto? hotTable;
    private const int NamedTableSlots = 16;
    private readonly CasinoTableRowDto?[] namedTables = new CasinoTableRowDto?[NamedTableSlots];
    private readonly string[] namedTableTexts = new string[NamedTableSlots];
    private LanguageInfo? namedLanguage;
    private int namedNext;

    private float DrawCarousel(ImDrawListPtr drawList, Vector2 origin, float width, float delta, float scale)
    {
        BuildHeroPages();
        if (heroCards.Count == 0)
        {
            return origin.Y;
        }

        var cardHeight = StripCarousel.CardHeight(width, scale);
        var stride = StripCarousel.Stride(width, scale);
        var rail = new Rect(origin, new Vector2(origin.X + width, origin.Y + cardHeight));
        heroCards.Update(rail, stride, router.IsTransitioning || LaunchActive || cashier.IsOpen, delta);
        drawList.PushClipRect(new Vector2(rail.Min.X - CardGap * scale, rail.Min.Y),
            new Vector2(rail.Max.X + CardGap * scale, rail.Max.Y + 2f * scale), true);
        var live = !router.IsTransitioning && !LaunchActive;
        for (var index = 0; index < heroCards.Count; index++)
        {
            var left = heroCards.CardLeft(index, origin.X, stride);
            if (left > rail.Max.X || left + width < rail.Min.X)
            {
                continue;
            }

            var card = new Rect(new Vector2(left, rail.Min.Y), new Vector2(left + width, rail.Max.Y));
            using (Dalamud.Interface.Utility.Raii.ImRaii.PushId(index))
            {
                DrawHeroPage(drawList, heroCards.PageAt(index), card, live, delta, scale);
            }
        }

        drawList.PopClipRect();
        var dotsCenter = new Vector2(rail.Center.X, rail.Max.Y + (StripCarousel.DotsGap + StripCarousel.DotsRow * 0.5f) * scale);
        if (heroCards.Count > 1)
        {
            heroCards.DrawDots(drawList, dotsCenter, width, ui.TitleInk);
        }

        return origin.Y + StripCarousel.BlockHeight(width, heroCards.Count, scale) + CardGap * scale;
    }

    private void BuildHeroPages()
    {
        heroCards.Clear();
        var features = casino.Features;
        if (casino.Jackpot > 0)
        {
            heroCards.Add(StripPage.Jackpot);
        }

        if (CasinoGameGate.IsOpen(features, CasinoGames.SlotsBird))
        {
            heroCards.Add(StripPage.Machine);
        }

        if (CasinoGameGate.IsOpen(features, CasinoGames.Race))
        {
            heroCards.Add(StripPage.Race);
        }

        hotTable = HottestTable();
        if (hotTable is not null)
        {
            heroCards.Add(StripPage.Table);
        }

        if (CasinoGameGate.IsOpen(features, CasinoGames.DealerHoldem))
        {
            heroCards.Add(StripPage.Dealer);
        }

        if (casino.HasFeature(CasinoFeatures.Challenges) && floor.LiveChallenge() is not null)
        {
            heroCards.Add(StripPage.Challenge);
        }

        heroCards.Settle();
    }

    private CasinoTableRowDto? HottestTable()
    {
        CasinoTableRowDto? best = null;
        best = Hotter(casinoTables.Tables, best);
        best = Hotter(casinoTables.Listed, best);
        return best;
    }

    private CasinoTableRowDto? Hotter(CasinoTableRowDto[] rows, CasinoTableRowDto? best)
    {
        var features = casino.Features;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (row.SeatedCount <= 0 || !CasinoTableFilters.HasOpenSeat(row) || row.Paused
                || VenueKinds.IsVenue(row.GameKind) || !CasinoGameGate.RoomOpen(features, row.GameKind)
                || CasinoCurrencies.Of(row) != CasinoCurrencies.Chips)
            {
                continue;
            }

            if (best is null || row.SeatedCount > best.SeatedCount
                || (row.SeatedCount == best.SeatedCount && row.Occupancy > best.Occupancy))
            {
                best = row;
            }
        }

        return best;
    }

    private static string MachineOfTheNight()
    {
        var ids = Machines.MachineCabinet.MachineIds;
        var day = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86_400 % ids.Length);
        return ids[day];
    }

    private void DrawHeroPage(ImDrawListPtr drawList, StripPage page, Rect card, bool live, float delta, float scale)
    {
        var interactive = heroCards.TapAllowed;
        var hovered = CasinoArt.PressCard(ImGui.GetID("hero"), card.Min, card.Max, out var pressedMin,
            out var pressedMax, interactive);
        var shown = new Rect(pressedMin, pressedMax);
        var radius = Metrics.Radius.Widget * scale;
        switch (page)
        {
            case StripPage.Jackpot:
                DrawJackpotPage(drawList, shown, radius, delta, scale);
                break;
            case StripPage.Machine:
                DrawPreviewPage(drawList, shown, radius, 0, machines.IdleFor(MachineOfTheNight()),
                    CasinoArt.TintOf(MachineOfTheNight()), live, hovered, delta, scale);
                DrawHeroOverlay(drawList, shown, Loc.T(L.Strip.HeroMachineEyebrow),
                    Loc.T(CasinoGameNames.Of(MachineOfTheNight())), MinimumStakeLine(MachineOfTheNight()), scale);
                break;
            case StripPage.Race:
                DrawPreviewPage(drawList, shown, radius, 1, race, CasinoArt.TintOf(CasinoGames.Race), live, hovered,
                    delta, scale);
                DrawHeroOverlay(drawList, shown, Loc.T(L.Strip.HeroRaceEyebrow), Loc.T(L.Race.Title),
                    RaceLine(), scale);
                break;
            case StripPage.Table when hotTable is not null:
                var holdemTable = string.Equals(hotTable.GameKind, HoldemRules.Kind, StringComparison.Ordinal);
                DrawPreviewPage(drawList, shown, radius, 2, holdemTable ? holdem : blackjack,
                    CasinoArt.TintOf(holdemTable ? CasinoGames.Holdem : CasinoGames.Blackjack), live, hovered, delta,
                    scale);
                DrawHeroOverlay(drawList, shown, Loc.T(L.Strip.HeroTableEyebrow), TableName(hotTable),
                    texts.Counts(L.Casino.TableSeats, hotTable.SeatedCount, hotTable.MaxSeats), scale);
                break;
            case StripPage.Dealer:
                DrawPreviewPage(drawList, shown, radius, 3, dealerHoldem, CasinoArt.TintOf(CasinoGames.DealerHoldem),
                    live, hovered, delta, scale);
                DrawHeroOverlay(drawList, shown, Loc.T(L.Strip.HeroDealerEyebrow), Loc.T(L.DealerHoldem.Game),
                    MinimumStakeLine(CasinoGames.DealerHoldem), scale);
                break;
            case StripPage.Challenge:
                DrawChallengePage(drawList, shown, radius, scale);
                break;
        }

        if (!UiInteract.Click(card.Min, card.Max, hovered) || !interactive)
        {
            return;
        }

        switch (page)
        {
            case StripPage.Jackpot:
            case StripPage.Machine:
                OpenGame(page == StripPage.Jackpot ? CasinoGames.SlotsBird : MachineOfTheNight(), shown);
                break;
            case StripPage.Race:
                OpenGame(CasinoGames.Race, shown);
                break;
            case StripPage.Table when hotTable is not null:
                OpenTable(hotTable.TableId);
                break;
            case StripPage.Dealer:
                OpenGame(CasinoGames.DealerHoldem, shown);
                break;
            case StripPage.Challenge:
                OpenChallenge(shown);
                break;
        }
    }

    private void DrawPreviewPage(ImDrawListPtr drawList, Rect card, float radius, int slot, ICabinetIdle? idle,
        Vector4 tint, bool live, bool hovered, float delta, float scale)
    {
        if (idle is null)
        {
            Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius,
                ImGui.GetColorU32(Palette.ShadeToLuminance(tint with { W = 1f }, 0.2f)), ImGui.GetColorU32(JackpotBed));
            return;
        }

        var preview = heroPreviews[slot];
        preview.Prepare(idle);
        preview.Draw(drawList, card, radius, tint, live, hovered, delta, scale);
    }

    private void DrawHeroOverlay(ImDrawListPtr drawList, Rect card, string eyebrow, string title, string line,
        float scale)
    {
        var pad = HeroOverlayPad * scale;
        var radius = Metrics.Radius.Widget * scale;
        var width = card.Width - pad * 2f;
        var left = card.Min.X + pad;
        var block = Tables.PitText.Height(eyebrow, TextStyles.FootnoteEmphasized, width)
                    + Tables.PitText.Height(title, TextStyles.Title2, width)
                    + Tables.PitText.Height(line, TextStyles.Subheadline, width);
        var top = card.Max.Y - pad - block;
        var scrimTop = MathF.Max(card.Min.Y, MathF.Min(card.Min.Y + card.Height * HeroScrimFrom, top - pad));
        drawList.PushClipRect(new Vector2(card.Min.X, scrimTop), card.Max, true);
        Squircle.FillVerticalGradient(drawList, new Vector2(card.Min.X, scrimTop), card.Max, radius,
            ImGui.GetColorU32(HeroScrim with { W = 0f }), ImGui.GetColorU32(HeroScrim));
        drawList.PopClipRect();
        top = Tables.PitText.Draw(drawList, eyebrow, TextStyles.FootnoteEmphasized, CasinoColors.Money, left, top,
            width, false);
        top = Tables.PitText.Draw(drawList, title, TextStyles.Title2, CasinoColors.InkTitle, left, top, width, false);
        Tables.PitText.Draw(drawList, line, TextStyles.Subheadline, CasinoColors.InkBody, left, top, width, false);
        LivePreview.Rim(drawList, card, radius, scale);
    }

    private void DrawJackpotPage(ImDrawListPtr drawList, Rect card, float radius, float delta, float scale)
    {
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(JackpotCrown),
            ImGui.GetColorU32(JackpotBed));
        var inset = card.Inset(6f * scale);
        CasinoLights.BulbChase(drawList, inset, radius - 6f * scale, scale, stage.Phase, CasinoLights.BulbPitch,
            CasinoColors.Money, CasinoColors.LightA, 1f);
        var signHeight = CasinoSigns.HeightToFit(CasinoSign.Jackpot, card.Width * 0.7f, JackpotSignHeight * scale);
        var signCenter = new Vector2(card.Center.X, card.Min.Y + card.Height * 0.24f);
        CasinoSigns.Draw(drawList, CasinoSign.Jackpot, signCenter, signHeight, CasinoColors.LightA,
            0.85f + 0.15f * MathF.Sin(stage.Phase * 3f));
        jackpotRoll.Update(casino.Jackpot, delta);
        var amount = NumberText.Compact(jackpotRoll.Display);
        var style = TextStyles.LargeTitle;
        var available = card.Width * 0.8f;
        var amountScale = Typography.FitScale(amount, available - CurrencyGlyph.Reserve(Typography.LineHeight(style)),
            style.Scale * jackpotRoll.PopScale, TextStyles.Title2.Scale, style.Weight);
        var size = Typography.Measure(amount, amountScale, style.Weight);
        var glyph = size.Y * CurrencyGlyph.GlyphFraction;
        var total = CurrencyGlyph.Reserve(size.Y) + size.X;
        var hint = Loc.T(L.Strip.HeroJackpotHint);
        var hintWidth = card.Width - HeroOverlayPad * 2f * scale;
        var hintHeight = Tables.PitText.Height(hint, TextStyles.Subheadline, hintWidth);
        var amountSlot = Typography.LineHeight(style);
        var gap = Metrics.Space.Sm * scale;
        var signBottom = signCenter.Y + signHeight * 0.5f;
        var floorY = card.Max.Y - HeroOverlayPad * scale;
        var stackTop = MathF.Max(signBottom, (signBottom + floorY - amountSlot - gap - hintHeight) * 0.5f);
        var amountTop = stackTop + (amountSlot - size.Y) * 0.5f;
        var amountLeft = card.Center.X - total * 0.5f;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, new Vector2(amountLeft + glyph * 0.5f, amountTop + size.Y * 0.5f),
            glyph);
        Typography.Draw(drawList, new Vector2(amountLeft + CurrencyGlyph.Reserve(size.Y), amountTop), amount,
            CasinoColors.Money, amountScale, style.Weight);
        Tables.PitText.Draw(drawList, hint, TextStyles.Subheadline, CasinoColors.InkTitle,
            card.Center.X - hintWidth * 0.5f, stackTop + amountSlot + gap, hintWidth, true);
    }

    private string RaceLine()
    {
        var line = RoomPhaseLine(CasinoGames.Race, CasinoRoomIds.RaceTrack, out _);
        if (line.Length > 0)
        {
            return line;
        }

        var crowd = casinoRooms.OccupancyOf(CasinoRoomIds.RaceTrack);
        return crowd > 0 ? texts.Count(L.Casino.LivePlayers, crowd) : Loc.T(L.Strip.HeroRaceIdle);
    }

    private string TableName(CasinoTableRowDto row)
    {
        if (!ReferenceEquals(namedLanguage, Loc.Current))
        {
            namedLanguage = Loc.Current;
            Array.Clear(namedTables);
        }

        for (var slot = 0; slot < NamedTableSlots; slot++)
        {
            if (ReferenceEquals(namedTables[slot], row))
            {
                return namedTableTexts[slot];
            }
        }

        var next = namedNext;
        namedNext = (namedNext + 1) % NamedTableSlots;
        namedTables[next] = row;
        namedTableTexts[next] = Tables.TableBrowser.NameOf(row);
        return namedTableTexts[next];
    }
}
