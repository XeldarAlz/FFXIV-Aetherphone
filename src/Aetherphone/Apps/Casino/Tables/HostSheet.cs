using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Games;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly record struct HostSummaryKey(
    HostGame Game,
    int Currency,
    int Seats,
    long Low,
    long High,
    int Listing,
    LanguageInfo? Language);

internal sealed class HostSheet
{
    private const string RailId = "##casino.host.games";
    private const float RowUnits = 52f;
    private const float SegmentRowUnits = 80f;
    private const float SelectedRimAlpha = 0.95f;
    private const float RestRimAlpha = 0.30f;
    private const float DisabledAlpha = 0.42f;
    private const float ChoiceFillAlpha = 0.14f;
    private const float IconFillAlpha = 0.20f;
    private const float ScrimAlpha = 0.72f;
    private const float FooterHairlineAlpha = 0.10f;
    private const int NameMaxLength = CasinoHostingRules.NameMaxLength;

    private static readonly Vector4 Night = new(0.027f, 0.020f, 0.055f, 1f);

    private static readonly int[] Currencies =
        { CasinoCurrencies.Chips, CasinoCurrencies.Practice, CasinoCurrencies.Gil };

    private static readonly LocString[] CurrencyTitles =
        { L.Tables.PlayCoins, L.Tables.CurrencyPractice, L.Tables.CurrencyGil };

    private static readonly LocString[] CurrencyLines =
        { L.Tables.PlayCoinsLine, L.Tables.PlayPracticeLine, L.Tables.PlayGilLine };

    private static readonly FontAwesomeIcon[] CurrencyIcons =
        { FontAwesomeIcon.Coins, FontAwesomeIcon.GraduationCap, FontAwesomeIcon.Landmark };

    private static readonly LocString[] JoinTitles =
        { L.Tables.JoinInvite, L.Tables.JoinKnock, L.Tables.JoinOpen };

    private static readonly LocString[] JoinLines =
        { L.Tables.JoinInviteLine, L.Tables.JoinKnockLine, L.Tables.JoinOpenLine };

    private static readonly LocString[] SummaryListings =
        { L.Tables.SummaryInvite, L.Tables.SummaryKnock, L.Tables.SummaryOpen };

    private static readonly FontAwesomeIcon[] JoinIcons =
        { FontAwesomeIcon.Lock, FontAwesomeIcon.HandPaper, FontAwesomeIcon.DoorOpen };

    private static readonly LocString[] DealerLabels = { L.Tables.DealerHouse, L.Tables.DealerHost };

    private static readonly LocString[] PaysLabels = { L.Tables.Pays32, L.Tables.Pays21, L.Tables.Pays11 };

    private static readonly LocString[] SplitLabels = { L.Tables.OptionOff, L.Tables.SplitsOnce, L.Tables.SplitsFour };

    private static readonly LocString[] DoubleLabels =
        { L.Tables.DoublesAny, L.Tables.DoublesNineEleven, L.Tables.OptionOff };

    private readonly CasinoTablesStore tables;
    private readonly CasinoStore chips;
    private readonly Venue.VenueHostOptions venueOptions;
    private readonly HostDraft draft = new();
    private readonly CasinoTextCache texts = new();
    private readonly TileRail rail = new();
    private readonly string[] dealerOptions = new string[2];
    private readonly string[] paysOptions = new string[3];
    private readonly string[] splitOptions = new string[3];
    private readonly string[] doubleOptions = new string[3];
    private readonly string[] turnOptions = new string[CasinoHostingRules.TurnSeconds.Length];
    private readonly string[] deckOptions = new string[CasinoRuleSheet.Decks.Length];
    private readonly string[] roundOptions = new string[HostLadders.RoundSeconds.Length];
    private readonly string[] anteOptions = new string[HostLadders.AnteTenths.Length];
    private LanguageInfo? optionsLanguage;
    private long anteBigBlind = -1;
    private HostSummaryKey summaryKey;
    private string summary = string.Empty;
    private string inlineReason = string.Empty;
    private bool moreOpen;
    private bool revealGame;

    public HostSheet(CasinoTablesStore tables, CasinoStore chips, Venue.VenueHostOptions venueOptions)
    {
        this.tables = tables;
        this.chips = chips;
        this.venueOptions = venueOptions;
    }

    public void Enter() => Begin(HostGame.Blackjack);

    public void Enter(string gameKind) => Begin(HostGames.FromKind(gameKind));

    public void EnterVenue(VenueRoomKind kind) => Begin(HostGames.FromVenue(kind));

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        ConsumeOutcomes();
        RefreshOptions();
        var gilOpen = chips.HasFeature(CasinoFeatures.GilTables);
        draft.Normalize(gilOpen);
        var line = Summary();
        var footerWidth = body.Width;
        var summaryHeight = SummaryBlock(line, footerWidth, scale);
        var footerHeight = HostFlowLayout.FooterHeight(scale, summaryHeight);
        var scroll = new Rect(body.Min, new Vector2(body.Max.X, body.Max.Y - footerHeight));
        using (ImRaii.PushId("casino.host"))
        {
            using (AppSurface.Begin(scroll))
            {
                DrawSteps(ui, gilOpen, scale);
            }

            DrawFooter(ui, new Rect(new Vector2(body.Min.X, scroll.Max.Y), body.Max), line, summaryHeight, scale);
        }
    }

    private void Begin(HostGame game)
    {
        draft.Reset(game);
        venueOptions.Reset();
        inlineReason = string.Empty;
        moreOpen = false;
        rail.Reset();
        revealGame = game != HostGame.Blackjack;
    }

    private void DrawSteps(AppSkin ui, bool gilOpen, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        StepHeader(ui, 1, Loc.T(L.Tables.StepGame), width, scale);
        DrawGameRail(ui, gilOpen, width, scale);
        Gap(HostFlowLayout.SectionGap, scale);
        StepHeader(ui, 2, Loc.T(L.Tables.StepPlayFor), width, scale);
        DrawPlayFor(ui, gilOpen, width, scale);
        Gap(HostFlowLayout.SectionGap, scale);
        StepHeader(ui, 3, Loc.T(L.Tables.StepTable), width, scale);
        DrawEssentials(ui, width, scale);
        Gap(HostFlowLayout.SectionGap, scale);
        DrawMoreToggle(ui, width, scale);
        if (moreOpen)
        {
            DrawMore(ui, scale);
        }

        if (inlineReason.Length > 0)
        {
            Gap(HostFlowLayout.CardGap, scale);
            DrawReason(ui, width, scale);
        }

        Gap(HostFlowLayout.SectionGap, scale);
    }

    private static void Gap(float units, float scale) => ImGui.Dummy(new Vector2(0f, units * scale));

    private static void StepHeader(AppSkin ui, int number, string title, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var height = HostFlowLayout.StepHeight(scale, titleHeight);
        var disc = HostFlowLayout.StepDisc * scale;
        var center = new Vector2(origin.X + disc * 0.5f, origin.Y + height * 0.5f);
        drawList.AddCircleFilled(center, disc * 0.5f, ImGui.GetColorU32(ui.Accent), 32);
        Typography.DrawCentered(drawList, center, StepNumber(number), CasinoArt.White, TextStyles.FootnoteEmphasized);
        var left = origin.X + disc + Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(left, origin.Y + (height - titleHeight) * 0.5f),
            Typography.FitText(title, MathF.Max(1f, origin.X + width - left), TextStyles.Title3), ui.TitleInk,
            TextStyles.Title3);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + HostFlowLayout.StepGap * scale));
    }

    private static string StepNumber(int number) => number switch
    {
        1 => "1",
        2 => "2",
        _ => "3",
    };

    private void DrawGameRail(AppSkin ui, bool gilOpen, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var tileWidth = HostFlowLayout.TileWidth(width, scale);
        var textWidth = HostFlowLayout.TileTextWidth(tileWidth, scale);
        var lineBlock = 0f;
        for (var index = 0; index < HostGames.All.Length; index++)
        {
            lineBlock = MathF.Max(lineBlock,
                Typography.MeasureWrappedBlock(Loc.T(HostGames.All[index].Line), TextStyles.Footnote, textWidth).Y);
        }

        var headline = Typography.LineHeight(TextStyles.Headline);
        var tileHeight = HostFlowLayout.TileHeight(scale, headline, lineBlock);
        var pad = HostFlowLayout.RailPad * scale;
        var contentWidth = HostFlowLayout.RailWidth(HostGames.All.Length, tileWidth, scale);
        var row = new Rect(new Vector2(origin.X - pad, origin.Y - pad),
            new Vector2(origin.X + width + pad, origin.Y + tileHeight + pad));
        if (revealGame)
        {
            revealGame = false;
            var selected = HostFlowLayout.Tile(0f, 0f, (int)draft.Game, tileWidth, tileHeight, scale);
            rail.SettleTo(MathF.Max(0f, MathF.Min(selected.Max.X - width, contentWidth - width)));
        }

        rail.Begin(drawList, RailId, row, row, contentWidth);
        var interactive = rail.TapAllowed;
        var phase = (float)ImGui.GetTime();
        var tapped = -1;
        for (var index = 0; index < HostGames.All.Length; index++)
        {
            var tile = HostFlowLayout.Tile(origin.X - rail.Offset, origin.Y, index, tileWidth, tileHeight, scale);
            if (tile.Min.X > row.Max.X || tile.Max.X < row.Min.X)
            {
                continue;
            }

            using (ImRaii.PushId(index))
            {
                if (DrawGameTile(drawList, ui, HostGames.All[index], tile, textWidth, phase, interactive, scale))
                {
                    tapped = index;
                }
            }
        }

        rail.End(drawList, row, contentWidth, ui, tileWidth + HostFlowLayout.TileGap * scale);
        if (tapped >= 0)
        {
            inlineReason = string.Empty;
            draft.SelectGame(HostGames.All[tapped].Game, gilOpen);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tileHeight));
    }

    private bool DrawGameTile(ImDrawListPtr drawList, AppSkin ui, in HostGameInfo info, Rect tile, float textWidth,
        float phase, bool interactive, float scale)
    {
        var selected = info.Game == draft.Game;
        var hovered = CasinoArt.PressCard(ImGui.GetID("tile"), tile.Min, tile.Max, out var pressedMin,
            out var pressedMax, interactive);
        var face = new Rect(pressedMin, pressedMax);
        PosterTile.DrawFrame(drawList, face, info.Tint, scale);
        var art = new Rect(face.Min, new Vector2(face.Max.X, face.Min.Y + HostFlowLayout.TileArt * scale));
        PosterTile.DrawSign(drawList, face, info.Sign, phase, scale);
        var artCenter = new Vector2(art.Center.X, art.Center.Y + PosterTile.SignHeight * scale * 0.5f);
        if (info.HasGlyph)
        {
            CasinoGlyphs.Draw(drawList, info.GameId, artCenter, art.Height * 0.24f, ImGui.GetColorU32(CasinoArt.White),
                ImGui.GetColorU32(Night));
        }
        else
        {
            AppSkin.Icon(drawList, artCenter, IconGlyph.Of(info.Icon), CasinoArt.White, PosterTile.IconScale);
        }

        var radius = Metrics.Radius.Widget * scale;
        drawList.PushClipRect(new Vector2(face.Min.X, art.Max.Y), face.Max, true);
        Squircle.Fill(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(Night with { W = ScrimAlpha }));
        drawList.PopClipRect();
        var pad = HostFlowLayout.CardPad * scale;
        var top = art.Max.Y + pad;
        Typography.Draw(drawList, new Vector2(face.Min.X + pad, top),
            Typography.FitText(Loc.T(info.Title), textWidth, TextStyles.Headline), CasinoColors.InkTitle,
            TextStyles.Headline);
        top += Typography.LineHeight(TextStyles.Headline) + HostFlowLayout.LineGap * scale;
        Typography.DrawWrappedLeft(new Vector2(face.Min.X + pad, top), Loc.T(info.Line),
            CasinoColors.InkBody, TextStyles.Footnote, textWidth);
        var rim = selected ? ui.Accent with { W = SelectedRimAlpha } : info.Tint with { W = RestRimAlpha };
        Squircle.Stroke(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(rim),
            (selected ? 2.4f : 1.2f) * scale);
        if (selected)
        {
            var check = HostFlowLayout.CheckSize * scale;
            var center = new Vector2(face.Max.X - pad * 0.6f - check * 0.5f, face.Min.Y + pad * 0.6f + check * 0.5f);
            CheckBadge(drawList, center, check, ui.Accent);
        }

        return interactive && UiInteract.Click(tile.Min, tile.Max, hovered);
    }

    private static void CheckBadge(ImDrawListPtr drawList, Vector2 center, float size, Vector4 accent)
    {
        drawList.AddCircleFilled(center, size * 0.5f, ImGui.GetColorU32(accent), 28);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Check, CasinoArt.White, size * 0.5f);
    }

    private void DrawPlayFor(AppSkin ui, bool gilOpen, float width, float scale)
    {
        for (var index = 0; index < Currencies.Length; index++)
        {
            var currency = Currencies[index];
            var enabled = HostGames.Accepts(draft.Game, currency, gilOpen);
            var line = enabled ? Loc.T(CurrencyLines[index]) : Loc.T(RefusalFor(currency, gilOpen));
            using (ImRaii.PushId(index))
            {
                if (ChoiceCard(ui, CurrencyIcons[index], Loc.T(CurrencyTitles[index]), line,
                        draft.Currency == currency, enabled, TintOf(currency, ui.Accent), width, scale))
                {
                    inlineReason = string.Empty;
                    draft.SelectCurrency(currency, gilOpen);
                }
            }

            if (index < Currencies.Length - 1)
            {
                Gap(HostFlowLayout.CardGap, scale);
            }
        }
    }

    private LocString RefusalFor(int currency, bool gilOpen)
    {
        if (currency == CasinoCurrencies.Chips)
        {
            return L.Tables.PlayRoomsNoCoins;
        }

        return gilOpen ? L.Tables.PlayHoldemNoGil : L.Tables.PlayGilClosed;
    }

    private static Vector4 TintOf(int currency, Vector4 accent) => TableRow.CurrencyTint(currency, accent);

    private static bool ChoiceCard(AppSkin ui, FontAwesomeIcon icon, string title, string line, bool selected,
        bool enabled, Vector4 tint, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var textWidth = HostFlowLayout.ChoiceTextWidth(width, scale);
        var lineHeight = Typography.MeasureWrappedBlock(line, TextStyles.Footnote, textWidth).Y;
        var height = HostFlowLayout.ChoiceHeight(scale, titleHeight, lineHeight);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var radius = Metrics.Radius.Grouped * scale;
        var firstVertex = drawList.VtxBuffer.Size;
        var hovered = enabled && CasinoArt.PressCard(ImGui.GetID("choice"), card.Min, card.Max, out _, out _);
        ui.Card(drawList, card.Min, card.Max, radius);
        if (selected)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(tint with { W = ChoiceFillAlpha }));
        }
        else if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.HoverTint));
        }

        Squircle.Stroke(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(selected ? tint : ui.Hairline), (selected ? 2f : 1f) * scale);
        var iconRect = HostFlowLayout.ChoiceIconRect(card, scale);
        drawList.AddCircleFilled(iconRect.Center, iconRect.Width * 0.5f,
            ImGui.GetColorU32(tint with { W = IconFillAlpha }), 32);
        ProgressRing.CenterIcon(drawList, iconRect.Center, icon, tint, iconRect.Width * 0.46f);
        var left = HostFlowLayout.ChoiceTextLeft(card.Min.X, scale);
        var top = card.Center.Y - (titleHeight + HostFlowLayout.LineGap * scale + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, textWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(left, top + titleHeight + HostFlowLayout.LineGap * scale),
            line, ui.BodyInk, TextStyles.Footnote, textWidth);
        if (selected)
        {
            CheckBadge(drawList, HostFlowLayout.ChoiceCheck(card, scale), HostFlowLayout.CheckSize * scale, tint);
        }

        if (!enabled)
        {
            LayerCompositor.Fade(drawList, firstVertex, DisabledAlpha);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return enabled && !selected && UiInteract.Click(card.Min, card.Max, hovered);
    }

    private void DrawEssentials(AppSkin ui, float width, float scale)
    {
        switch (draft.Game)
        {
            case HostGame.Blackjack:
                if (draft.Gil)
                {
                    LadderSingle(ui, "##hostBank", Loc.T(L.Tables.Bank),
                        TableAmounts.Amount(texts, draft.BankValue, draft.Currency), HostLadders.All(HostLadders.Gil),
                        ref draft.Bank, width, scale);
                    Gap(HostFlowLayout.CardGap, scale);
                }

                LadderRange(ui, "##hostBets", Loc.T(L.Tables.Bets),
                    TableAmounts.Range(texts, draft.MinBetValue, draft.MaxBetValue, draft.Currency), draft.BetSpan,
                    ref draft.MinBet, ref draft.MaxBet, width, scale);
                Gap(HostFlowLayout.CardGap, scale);
                DrawSeats(ui, width, scale);
                break;
            case HostGame.Holdem:
                LadderSingle(ui, "##hostBlinds", Loc.T(L.Holdem.HostBlinds),
                    texts.Compacts(L.Holdem.BlindsShort, draft.BigBlindValue / 2, draft.BigBlindValue),
                    HostLadders.BigBlinds, ref draft.BigBlind, width, scale);
                Gap(HostFlowLayout.CardGap, scale);
                DrawSeats(ui, width, scale);
                break;
            case HostGame.DiceTable:
                LadderSingle(ui, "##hostSides", Loc.T(L.Venue.Sides), texts.Compact(L.Tables.DiceRange,
                    draft.SidesValue), HostLadders.All(HostLadders.DiceSides), ref draft.Sides, width, scale);
                break;
            case HostGame.Deathroll:
                LadderSingle(ui, "##hostStart", Loc.T(L.Venue.StartNumber), NumberText.Compact(draft.StartAtValue),
                    HostLadders.All(HostLadders.StartNumbers), ref draft.StartAt, width, scale);
                Gap(HostFlowLayout.CardGap, scale);
                LadderSingle(ui, "##hostStake", Loc.T(L.Venue.Stake),
                    TableAmounts.Amount(texts, draft.StakeValue, draft.Currency), draft.BetSpan, ref draft.Stake,
                    width, scale);
                break;
        }

        if (draft.Game != HostGame.Raffle)
        {
            Gap(HostFlowLayout.CardGap, scale);
        }

        DrawJoin(ui, width, scale);
    }

    private static void LadderSingle(AppSkin ui, string id, string label, string value, LadderSpan span, ref int index,
        float width, float scale)
    {
        var row = LadderCard(ui, label, value, width, scale, out var origin, out var height);
        LadderSlider.Single(ImGui.GetWindowDrawList(), id, row, span, ref index, ui.Accent,
            Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary), scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void LadderRange(AppSkin ui, string id, string label, string value, LadderSpan span, ref int low,
        ref int high, float width, float scale)
    {
        var row = LadderCard(ui, label, value, width, scale, out var origin, out var height);
        LadderSlider.Range(ImGui.GetWindowDrawList(), id, row, span, ref low, ref high, ui.Accent,
            Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary), scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static Rect LadderCard(AppSkin ui, string label, string value, float width, float scale,
        out Vector2 origin, out float height)
    {
        origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = HostFlowLayout.CardPad * scale;
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var valueHeight = Typography.LineHeight(TextStyles.Title2);
        height = pad * 2f + HostFlowLayout.LadderHeight(scale, labelHeight, valueHeight);
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Grouped * scale);
        var inner = width - pad * 2f;
        Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + pad),
            Typography.FitText(label, inner, TextStyles.FootnoteEmphasized), ui.MutedInk, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + pad + labelHeight),
            Typography.FitText(value, inner, TextStyles.Title2), CasinoColors.Money, TextStyles.Title2);
        return HostFlowLayout.LadderRow(origin.X + pad, origin.Y + pad, inner, scale, labelHeight, valueHeight);
    }

    private void DrawSeats(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = HostFlowLayout.CardPad * scale;
        var inner = width - pad * 2f;
        var maxSeats = HostGames.MaxSeats(draft.Game);
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var valueHeight = Typography.LineHeight(TextStyles.Title2);
        var block = HostFlowLayout.SeatBlockHeight(inner, maxSeats, scale);
        var height = pad * 2f + labelHeight + valueHeight + HostFlowLayout.LineGap * scale + block;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Grouped * scale);
        Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + pad),
            Typography.FitText(Loc.T(L.Tables.HostSeats), inner, TextStyles.FootnoteEmphasized), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + pad + labelHeight),
            Typography.FitText(texts.Count(L.Tables.SeatsValue, draft.Seats), inner, TextStyles.Title2), ui.TitleInk,
            TextStyles.Title2);
        var top = origin.Y + pad + labelHeight + valueHeight + HostFlowLayout.LineGap * scale;
        var disc = HostFlowLayout.SeatDisc * scale;
        for (var seatIndex = 0; seatIndex < maxSeats; seatIndex++)
        {
            var target = HostFlowLayout.SeatTarget(origin.X + pad, top, inner, seatIndex, maxSeats, scale);
            var filled = seatIndex < draft.Seats;
            var hovered = UiInteract.Hover(target.Min, target.Max);
            var center = target.Center;
            if (filled)
            {
                drawList.AddCircleFilled(center, disc * 0.5f, ImGui.GetColorU32(ui.Accent), 32);
                ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.User, CasinoArt.White, disc * 0.42f);
            }
            else
            {
                drawList.AddCircle(center, disc * 0.5f - scale, ImGui.GetColorU32(ui.MutedInk), 32, 1.5f * scale);
            }

            if (hovered)
            {
                drawList.AddCircleFilled(center, disc * 0.5f + 3f * scale, ImGui.GetColorU32(ui.HoverTint), 32);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (seatIndex + 1 >= CasinoHostingRules.MinSeats && UiInteract.Click(target.Min, target.Max, hovered))
            {
                draft.Seats = seatIndex + 1;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawJoin(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var count = JoinTitles.Length;
        var columnWidth = HostFlowLayout.JoinColumnWidth(width, count, scale);
        var textWidth = MathF.Max(1f, columnWidth - HostFlowLayout.CardPad * scale);
        var titleBlock = 0f;
        for (var index = 0; index < count; index++)
        {
            titleBlock = MathF.Max(titleBlock,
                Typography.MeasureWrappedBlock(Loc.T(JoinTitles[index]), TextStyles.SubheadlineEmphasized, textWidth).Y);
        }

        var tileHeight = HostFlowLayout.JoinHeight(scale, titleBlock);
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, origin, Typography.FitText(Loc.T(L.Tables.HostListing), width,
            TextStyles.FootnoteEmphasized), ui.MutedInk, TextStyles.FootnoteEmphasized);
        var top = origin.Y + labelHeight + HostFlowLayout.LineGap * scale;
        var radius = Metrics.Radius.Grouped * scale;
        for (var index = 0; index < count; index++)
        {
            var tile = HostFlowLayout.JoinColumn(origin.X, top, width, index, count, tileHeight, scale);
            var selected = draft.Listing == CasinoListings.All[index];
            var hovered = CasinoArt.PressCard(ImGui.GetID($"join{index}"), tile.Min, tile.Max, out _, out _);
            ui.Card(drawList, tile.Min, tile.Max, radius);
            if (selected)
            {
                Squircle.Fill(drawList, tile.Min, tile.Max, radius,
                    ImGui.GetColorU32(ui.Accent with { W = ChoiceFillAlpha }));
            }
            else if (hovered)
            {
                Squircle.Fill(drawList, tile.Min, tile.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            }

            Squircle.Stroke(drawList, tile.Min, tile.Max, radius,
                ImGui.GetColorU32(selected ? ui.Accent : ui.Hairline), (selected ? 2f : 1f) * scale);
            var iconSize = HostFlowLayout.JoinIcon * scale;
            var iconCenter = new Vector2(tile.Center.X, tile.Min.Y + HostFlowLayout.CardPad * scale + iconSize * 0.5f);
            ProgressRing.CenterIcon(drawList, iconCenter, JoinIcons[index], selected ? ui.Accent : ui.BodyInk,
                iconSize * 0.62f);
            var textTop = iconCenter.Y + iconSize * 0.5f + HostFlowLayout.LineGap * scale;
            Typography.DrawWrappedCentered(drawList, Loc.T(JoinTitles[index]), TextStyles.SubheadlineEmphasized,
                selected ? ui.TitleInk : ui.BodyInk, new Vector2(tile.Center.X, textTop), textWidth);
            if (UiInteract.Click(tile.Min, tile.Max, hovered))
            {
                draft.Listing = CasinoListings.All[index];
            }
        }

        var hintTop = top + tileHeight + HostFlowLayout.CardGap * scale;
        var hintHeight = Typography.DrawWrappedLeft(new Vector2(origin.X, hintTop),
            Loc.T(JoinLines[Math.Clamp(draft.Listing, 0, JoinLines.Length - 1)]), ui.BodyInk, TextStyles.Footnote,
            width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, hintTop + hintHeight - origin.Y));
    }

    private void DrawMoreToggle(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = HostFlowLayout.Touch * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, row.Min, row.Max, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = HostFlowLayout.CardPad * scale;
        var glyph = moreOpen ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.ChevronDown;
        var chevron = new Vector2(row.Max.X - pad - 6f * scale, row.Center.Y);
        ProgressRing.CenterIcon(drawList, chevron, glyph, ui.MutedInk, 12f * scale);
        var labelHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Min.X + pad, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(Loc.T(L.Tables.MoreOptions), width - pad * 3f - 12f * scale, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            moreOpen = !moreOpen;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + HostFlowLayout.CardGap * scale));
    }

    private void DrawMore(AppSkin ui, float scale)
    {
        var tableGame = HostGames.HasSeats(draft.Game);
        var rows = 1 + (tableGame ? 2 : 0);
        var segments = tableGame ? 1 : 0;
        var card = GroupCard.Begin(ui, RowUnits * rows + SegmentRowUnits * segments);
        var nameRow = card.NextRow(RowUnits);
        Venue.VenueFields.TextRow(ui, nameRow, "##hostName", Loc.T(L.Tables.HostName), Loc.T(L.Tables.HostNameHint),
            ref draft.Name, NameMaxLength, false, scale);
        if (tableGame)
        {
            var selected = Array.IndexOf(CasinoHostingRules.TurnSeconds, draft.TurnSeconds);
            var picked = Venue.VenueFields.Segment(ui, card.NextRow(SegmentRowUnits), "##hostTurnClock",
                Loc.T(L.Tables.TurnClock), turnOptions, selected < 0 ? 1 : selected, scale);
            draft.TurnSeconds = CasinoHostingRules.TurnSeconds[picked];
            draft.TimeBank = Venue.VenueFields.ToggleRow(ui, card.NextRow(RowUnits), "host.timebank",
                Loc.T(L.Tables.TimeBank), draft.TimeBank, scale);
            draft.Spectators = Venue.VenueFields.ToggleRow(ui, card.NextRow(RowUnits), "host.spectators",
                Loc.T(L.Tables.HostSpectators), draft.Spectators, scale);
        }

        card.End();
        Gap(HostFlowLayout.CardGap, scale);
        DrawGameOptions(ui, scale);
        venueOptions.DrawLocation(ui, scale);
    }

    private void DrawGameOptions(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        if (draft.Practice)
        {
            LadderSingle(ui, "##hostStack", Loc.T(L.Tables.PracticeStack), NumberText.Compact(draft.StackValue),
                HostLadders.All(HostLadders.PracticeStacks), ref draft.Stack, width, scale);
            Gap(HostFlowLayout.CardGap, scale);
            if (HostGames.HasSeats(draft.Game))
            {
                var card = GroupCard.Begin(ui, RowUnits * 2);
                draft.PracticeRebuy = Venue.VenueFields.ToggleRow(ui, card.NextRow(RowUnits), "host.rebuy",
                    Loc.T(L.Tables.Rebuys), draft.PracticeRebuy, scale);
                draft.FaceUp = Venue.VenueFields.ToggleRow(ui, card.NextRow(RowUnits), "host.faceup",
                    Loc.T(L.Tables.FaceUp), draft.FaceUp, scale);
                card.End();
                Gap(HostFlowLayout.CardGap, scale);
            }
        }

        if (draft.Gil && draft.Game == HostGame.Blackjack)
        {
            var index = draft.PayoutCapped ? draft.MaxPayout : draft.Bank;
            LadderSingle(ui, "##hostPayout", Loc.T(L.Tables.MaxPayout),
                TableAmounts.Amount(texts, draft.PayoutValue, draft.Currency), draft.PayoutSpan, ref index, width,
                scale);
            draft.PayoutCapped = index != draft.Bank;
            draft.MaxPayout = index;
            Gap(HostFlowLayout.CardGap, scale);
        }

        switch (draft.Game)
        {
            case HostGame.Blackjack when draft.SeatBanked:
                DrawDealerCard(ui, scale);
                DrawRulesCard(ui, scale);
                break;
            case HostGame.Holdem:
                DrawAnteCard(ui, scale);
                break;
            case HostGame.DiceTable:
                DrawDiceCard(ui, scale);
                break;
        }
    }

    private void DrawDealerCard(AppSkin ui, float scale)
    {
        var hostDeals = draft.DealerMode == CasinoDealerModes.Host;
        var card = GroupCard.Begin(ui, SegmentRowUnits + (hostDeals ? RowUnits : 0f));
        draft.DealerMode = Venue.VenueFields.Segment(ui, card.NextRow(SegmentRowUnits), "##hostDealerMode",
            Loc.T(L.Tables.DealerMode), dealerOptions, draft.DealerMode, scale);
        if (hostDeals)
        {
            draft.AutoDeal = Venue.VenueFields.ToggleRow(ui, card.NextRow(RowUnits), "host.autodeal",
                Loc.T(L.Tables.AutoDeal), draft.AutoDeal, scale);
        }

        card.End();
        Venue.VenueFields.Hint(ui, Loc.T(L.Tables.CoDealersNote), scale);
        Gap(HostFlowLayout.CardGap, scale);
    }

    private void DrawRulesCard(AppSkin ui, float scale)
    {
        ui.SectionHeading(Loc.T(L.Tables.SectionRules), Metrics.Space.Sm);
        var card = GroupCard.Begin(ui, SegmentRowUnits * 4 + RowUnits * 3);
        draft.Pays = Venue.VenueFields.Segment(ui, card.NextRow(SegmentRowUnits), "##hostRulesPays",
            Loc.T(L.Tables.RulesPays), paysOptions, draft.Pays, scale);
        var deck = Array.IndexOf(CasinoRuleSheet.Decks, draft.Decks);
        var deckPicked = Venue.VenueFields.Segment(ui, card.NextRow(SegmentRowUnits), "##hostRulesDecks",
            Loc.T(L.Tables.RulesDecks), deckOptions, deck < 0 ? 3 : deck, scale);
        draft.Decks = CasinoRuleSheet.Decks[deckPicked];
        draft.Splits = Venue.VenueFields.Segment(ui, card.NextRow(SegmentRowUnits), "##hostRulesSplits",
            Loc.T(L.Tables.RulesSplits), splitOptions, draft.Splits, scale);
        draft.Doubles = Venue.VenueFields.Segment(ui, card.NextRow(SegmentRowUnits), "##hostRulesDoubles",
            Loc.T(L.Tables.RulesDoubles), doubleOptions, draft.Doubles, scale);
        draft.HitsSoft17 = Venue.VenueFields.ToggleRow(ui, card.NextRow(RowUnits), "host.rules.soft17",
            Loc.T(L.Tables.RulesSoft17), draft.HitsSoft17, scale);
        draft.Charlie = Venue.VenueFields.ToggleRow(ui, card.NextRow(RowUnits), "host.rules.charlie",
            Loc.T(L.Tables.RulesCharlie), draft.Charlie, scale);
        draft.Peek = Venue.VenueFields.ToggleRow(ui, card.NextRow(RowUnits), "host.rules.peek",
            Loc.T(L.Tables.RulesPeek), draft.Peek, scale);
        card.End();
        Gap(HostFlowLayout.CardGap, scale);
    }

    private void DrawAnteCard(AppSkin ui, float scale)
    {
        RefreshAnte();
        var card = GroupCard.Begin(ui, SegmentRowUnits);
        draft.Ante = Venue.VenueFields.Segment(ui, card.NextRow(SegmentRowUnits), "##hostAnte",
            Loc.T(L.Holdem.HostAnte), anteOptions, draft.Ante, scale);
        card.End();
        Gap(HostFlowLayout.CardGap, scale);
    }

    private void DrawDiceCard(AppSkin ui, float scale)
    {
        var card = GroupCard.Begin(ui, RowUnits + SegmentRowUnits);
        draft.HighestWins = Venue.VenueFields.ToggleRow(ui, card.NextRow(RowUnits), "host.highest",
            Loc.T(L.Venue.HighestWins), draft.HighestWins, scale);
        draft.RoundSeconds = Venue.VenueFields.Segment(ui, card.NextRow(SegmentRowUnits), "##hostRoundSeconds",
            Loc.T(L.Venue.RoundSeconds), roundOptions, draft.RoundSeconds, scale);
        card.End();
        Gap(HostFlowLayout.CardGap, scale);
    }

    private void DrawFooter(AppSkin ui, Rect footer, string line, float summaryHeight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bandMin = new Vector2(footer.Min.X - AppSurface.SidePadding * scale, footer.Min.Y);
        var bandMax = new Vector2(footer.Max.X + AppSurface.SidePadding * scale, footer.Max.Y);
        Material.ThemedGlass(drawList, bandMin, bandMax, 0f, scale, ui.BackdropColor, TabBar.GlassOpacity);
        drawList.AddLine(bandMin, new Vector2(bandMax.X, bandMin.Y),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, FooterHairlineAlpha)), MathF.Max(1f, 0.5f * scale));
        var pad = HostFlowLayout.FooterPad * scale;
        Typography.DrawWrappedLeft(new Vector2(footer.Min.X, footer.Min.Y + pad), line, ui.BodyInk,
            TextStyles.Subheadline, footer.Width);
        var button = HostFlowLayout.FooterButton(footer, scale);
        if (Button.Draw(drawList, button, Loc.T(L.Tables.OpenTable), ui.Ink, ButtonStyle.Prominent,
                enabled: !tables.IntentInFlight, id: "casino.host.open"))
        {
            Submit();
        }
    }

    private static float SummaryBlock(string line, float width, float scale) =>
        Typography.MeasureWrappedBlock(line, TextStyles.Subheadline, width).Y;

    private string Summary()
    {
        var low = draft.Game switch
        {
            HostGame.Holdem => draft.BigBlindValue,
            HostGame.DiceTable => draft.SidesValue,
            HostGame.Deathroll => draft.StartAtValue,
            _ => draft.MinBetValue,
        };
        var high = draft.Game == HostGame.Deathroll ? draft.StakeValue : draft.MaxBetValue;
        var key = new HostSummaryKey(draft.Game, draft.Currency, draft.Seats, low, high, draft.Listing, Loc.Current);
        if (key == summaryKey && summary.Length > 0)
        {
            return summary;
        }

        summaryKey = key;
        var game = Loc.T(HostGames.Of(draft.Game).Title);
        var listing = Loc.T(SummaryListings[Math.Clamp(draft.Listing, 0, SummaryListings.Length - 1)]);
        var seats = draft.Seats.ToString(Loc.Culture);
        summary = draft.Game switch
        {
            HostGame.Blackjack => Loc.T(L.Tables.SummaryBlackjack, game, seats,
                TableAmounts.Range(texts, draft.MinBetValue, draft.MaxBetValue, draft.Currency), listing),
            HostGame.Holdem => Loc.T(L.Tables.SummaryHoldem, game, seats,
                texts.Compacts(L.Holdem.BlindsShort, draft.BigBlindValue / 2, draft.BigBlindValue), listing),
            HostGame.DiceTable => Loc.T(L.Tables.SummaryDice, game, NumberText.Compact(draft.SidesValue), listing),
            HostGame.Deathroll => Loc.T(L.Tables.SummaryDeathroll, game, NumberText.Compact(draft.StartAtValue),
                TableAmounts.Amount(texts, draft.StakeValue, draft.Currency), listing),
            _ => Loc.T(L.Tables.SummaryRaffle, game, listing),
        };
        return summary;
    }

    private void ConsumeOutcomes()
    {
        if (tables.TakeIntentFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        var outcome = tables.TakeNoticeOutcome();
        if (outcome is not null)
        {
            inlineReason = outcome.Granted ? string.Empty : outcome.Reason;
        }
    }

    private void Submit()
    {
        var config = draft.Build(venueOptions.Location());
        var reason = HostGames.IsVenue(draft.Game) ? VenueRules.Check(config) : CasinoHostingRules.Check(config);
        if (reason.Length > 0)
        {
            inlineReason = reason;
            return;
        }

        inlineReason = string.Empty;
        tables.HostTable(config);
    }

    private void DrawReason(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
        var pad = HostFlowLayout.CardPad * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Subheadline, width - pad * 2f);
        var max = new Vector2(origin.X + width, origin.Y + block.Y + pad * 2f);
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.12f)));
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), message, ui.TitleInk,
            TextStyles.Subheadline, width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, max.Y - origin.Y));
    }

    private void RefreshAnte()
    {
        if (anteBigBlind == draft.BigBlindValue && ReferenceEquals(optionsLanguage, Loc.Current))
        {
            return;
        }

        anteBigBlind = draft.BigBlindValue;
        anteOptions[0] = Loc.T(L.Tables.OptionOff);
        for (var index = 1; index < anteOptions.Length; index++)
        {
            anteOptions[index] = NumberText.Compact(anteBigBlind * HostLadders.AnteTenths[index] / 10);
        }
    }

    private void RefreshOptions()
    {
        if (ReferenceEquals(optionsLanguage, Loc.Current) && dealerOptions[0] is not null)
        {
            return;
        }

        optionsLanguage = Loc.Current;
        anteBigBlind = -1;
        Fill(dealerOptions, DealerLabels);
        Fill(paysOptions, PaysLabels);
        Fill(splitOptions, SplitLabels);
        Fill(doubleOptions, DoubleLabels);
        for (var index = 0; index < turnOptions.Length; index++)
        {
            turnOptions[index] = Loc.T(L.Tables.SecondsValue,
                CasinoHostingRules.TurnSeconds[index].ToString(Loc.Culture));
        }

        for (var index = 0; index < deckOptions.Length; index++)
        {
            deckOptions[index] = CasinoRuleSheet.Decks[index].ToString(Loc.Culture);
        }

        for (var index = 0; index < roundOptions.Length; index++)
        {
            roundOptions[index] = Loc.T(L.Tables.SecondsValue, HostLadders.RoundSeconds[index].ToString(Loc.Culture));
        }
    }

    private static void Fill(string[] target, LocString[] source)
    {
        for (var index = 0; index < target.Length; index++)
        {
            target[index] = Loc.T(source[index]);
        }
    }
}
