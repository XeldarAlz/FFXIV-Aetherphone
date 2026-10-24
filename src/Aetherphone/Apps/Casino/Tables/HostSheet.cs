using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

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
    private const float DisabledAlpha = 0.38f;
    private const float IconWashAlpha = 0.20f;
    private const float HeaderWashAlpha = 0.24f;
    private const float HeaderRimAlpha = 0.45f;
    private const float ChipLift = 0.12f;
    private const float ChipHoleDarken = 0.30f;
    private const float SwitcherGlyphExtent = 7f;
    private const float EmptySeatFillAlpha = 0.06f;
    private const float EmptySeatRimAlpha = 0.25f;
    private const float EmptySeatIconAlpha = 0.35f;
    private const float SeatRimWidth = 1.5f;
    private const float SeatHoverGrowth = 3f;
    private const float SeatIconFactor = 0.5f;
    private const float IconFactor = 0.5f;
    private const float FieldRimAlpha = 0.08f;
    private const float RevealSettled = 0.999f;
    private const float MaxRevealStep = 0.1f;
    private const int NameMaxLength = CasinoHostingRules.NameMaxLength;

    private static readonly Vector4 ChipHole = new(0.110f, 0.086f, 0.141f, 1f);

    private static readonly int[] Currencies =
        { CasinoCurrencies.Chips, CasinoCurrencies.Practice, CasinoCurrencies.Gil };

    private static readonly LocString[] CurrencyTitles =
        { L.Tables.CurrencyChips, L.Tables.CurrencyPractice, L.Tables.CurrencyGil };

    private static readonly FontAwesomeIcon[] CurrencyIcons =
        { FontAwesomeIcon.Coins, FontAwesomeIcon.GraduationCap, FontAwesomeIcon.Landmark };

    private static readonly int[] Listings = { CasinoListings.Open, CasinoListings.Knock, CasinoListings.Private };

    private static readonly LocString[] ListingTitles =
        { L.HostSetup.JoinAnyone, L.HostSetup.JoinKnock, L.Tables.JoinInvite };

    private static readonly FontAwesomeIcon[] ListingIcons =
        { FontAwesomeIcon.Globe, FontAwesomeIcon.DoorClosed, FontAwesomeIcon.Lock };

    private static readonly string[] SideCommands = Commands(HostLadders.DiceSides);

    private readonly CasinoTablesStore tables;
    private readonly CasinoStore chips;
    private readonly Venue.VenueHostOptions venueOptions;
    private readonly CasinoPreferences preferences;
    private readonly HostDraft draft = new();
    private readonly CasinoTextCache texts = new();
    private readonly HostRulesPage rulesPage;
    private HostSummaryKey summaryKey;
    private string summary = string.Empty;
    private HostRulesKey rulesKey;
    private string rulesSummary = string.Empty;
    private string inlineReason = string.Empty;
    private bool switcherOpen;
    private Spring switcherReveal;

    public HostSheet(CasinoTablesStore tables, CasinoStore chips, Venue.VenueHostOptions venueOptions,
        CasinoPreferences preferences)
    {
        this.tables = tables;
        this.chips = chips;
        this.venueOptions = venueOptions;
        this.preferences = preferences;
        rulesPage = new HostRulesPage(venueOptions, texts);
    }

    public Action? OpenRules { get; set; }

    public void Enter() => Begin(HostGame.Blackjack);

    public void Enter(string gameKind) => Begin(HostGames.FromKind(gameKind));

    public void EnterGame(HostGame game, bool preferGil)
    {
        Begin(game);
        if (preferGil)
        {
            draft.SelectCurrency(CasinoCurrencies.Gil, chips.HasFeature(CasinoFeatures.GilTables));
        }
    }

    public void EnterDraft(CasinoHostDraftMemory saved)
    {
        Begin(HostDraft.GameOf(saved));
        draft.Restore(saved);
        venueOptions.PinLocation = saved.PinLocation;
    }

    public void RememberDraft() => preferences.RememberHostDraft(draft.Snapshot(venueOptions.PinLocation));

    public bool DrawRules(Rect body, AppSkin ui)
    {
        draft.Normalize(chips.HasFeature(CasinoFeatures.GilTables));
        return rulesPage.Draw(body, ui, draft, RulesSummary(), UiScale.Current);
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        ConsumeOutcomes();
        var gilOpen = chips.HasFeature(CasinoFeatures.GilTables);
        draft.Normalize(gilOpen);
        var line = Summary();
        var note = PinNote();
        var reason = inlineReason.Length > 0 ? Loc.T(CasinoReasons.MessageFor(inlineReason)) : string.Empty;
        var block = HostCards.FooterBlock(reason, line, note, body.Width, scale);
        var scroll = new Rect(body.Min,
            new Vector2(body.Max.X, body.Max.Y - HostFlowLayout.FooterHeight(scale, block)));
        using (UiScope.PushId("casino.host"))
        {
            using (AppSurface.Begin(scroll))
            using (UiScope.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            {
                DrawContent(ui, gilOpen, scale);
            }

            var footer = new Rect(new Vector2(body.Min.X, scroll.Max.Y), body.Max);
            var label = Loc.T(draft.Game == HostGame.Book ? L.Book.OpenLedger : L.Tables.OpenTable);
            if (HostCards.Footer(ui, footer, reason, line, note, label, !tables.IntentInFlight, "casino.host.open",
                    scale))
            {
                Submit();
            }
        }
    }

    private void Begin(HostGame game)
    {
        draft.Reset(game);
        venueOptions.Reset();
        inlineReason = string.Empty;
        switcherOpen = false;
    }

    private void DrawContent(AppSkin ui, bool gilOpen, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        HostCards.Gap(HostFlowLayout.LineGap, scale);
        DrawHeader(ui, width, scale);
        if (switcherOpen)
        {
            HostCards.Gap(HostFlowLayout.CardGap, scale);
            DrawSwitcher(ui, gilOpen, width, scale);
        }

        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        DrawPlayFor(ui, gilOpen, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        DrawTable(ui, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        DrawJoin(ui, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        DrawName(ui, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        DrawRulesRow(ui, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
    }

    private void DrawHeader(AppSkin ui, float width, float scale)
    {
        ref readonly var info = ref HostGames.Of(draft.Game);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = HostFlowLayout.HeaderPad * scale;
        var tile = HostFlowLayout.HeaderTile * scale;
        var label = Loc.T(switcherOpen ? L.HostSetup.Done : L.HostSetup.Change);
        var pillHeight = Button.Height(ButtonSize.Regular) * scale;
        var pillWidth = Button.WidthFor(label, ButtonSize.Regular);
        var textLeft = origin.X + pad + tile + HostFlowLayout.HeaderGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - pillWidth - HostFlowLayout.CardGap * scale - textLeft);
        var line = Loc.T(info.Line);
        var nameHeight = Typography.LineHeight(TextStyles.Title3);
        var lineHeight = Typography.MeasureWrappedBlock(line, TextStyles.Footnote, textWidth).Y;
        var height = HostFlowLayout.HeaderHeight(scale, nameHeight, lineHeight);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, card.Min, card.Max, radius);
        Squircle.FillHorizontalGradient(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(info.Tint with { W = HeaderWashAlpha }), ImGui.GetColorU32(info.Tint with { W = 0f }));
        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(info.Tint with { W = HeaderRimAlpha }),
            scale);
        CasinoArt.GameTile(drawList, info.GameId, new Vector2(origin.X + pad + tile * 0.5f, card.Center.Y), tile);
        var textTop = card.Center.Y - (nameHeight + HostFlowLayout.LineGap * scale + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(Loc.T(info.Title), textWidth, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
        Typography.DrawWrappedLeft(new Vector2(textLeft, textTop + nameHeight + HostFlowLayout.LineGap * scale), line,
            ui.BodyInk, TextStyles.Footnote, textWidth);
        var pill = new Rect(new Vector2(card.Max.X - pad - pillWidth, card.Center.Y - pillHeight * 0.5f),
            new Vector2(card.Max.X - pad, card.Center.Y + pillHeight * 0.5f));
        if (Button.Draw(drawList, pill, label, ui.Ink, ButtonStyle.Gray, id: "casino.host.change"))
        {
            switcherOpen = !switcherOpen;
            switcherReveal.SnapTo(0f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawSwitcher(AppSkin ui, bool gilOpen, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var firstVertex = drawList.VtxBuffer.Size;
        var count = HostGames.Count(chips.HasFeature(CasinoFeatures.VenueBook));
        var height = HostFlowLayout.SwitcherHeight * scale;
        var gap = HostFlowLayout.SwitcherGap * scale;
        var glyph = HostFlowLayout.SwitcherGlyph * scale;
        var glyphGap = Metrics.Space.Xs * scale;
        var radius = Metrics.Radius.Md * scale;
        var nameHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var picked = -1;
        for (var index = 0; index < count; index++)
        {
            ref readonly var info = ref HostGames.All[index];
            var cell = HostFlowLayout.GridCell(origin.X, origin.Y, width, index, HostFlowLayout.Columns, height, gap);
            var selected = info.Game == draft.Game;
            using (UiScope.PushId(index))
            {
                var hovered = CasinoArt.PressCard(ImGui.GetID("switch"), cell.Min, cell.Max, out var pressedMin,
                    out var pressedMax);
                if (selected)
                {
                    Squircle.FillVerticalGradient(drawList, pressedMin, pressedMax, radius,
                        ImGui.GetColorU32(Palette.Lighten(ui.Accent, ChipLift)), ImGui.GetColorU32(ui.Accent));
                }
                else
                {
                    HostCards.ChoiceSurface(drawList, ui, new Rect(pressedMin, pressedMax), radius, ui.Accent, false,
                        hovered, scale);
                }

                var ink = selected ? CasinoArt.White : ui.BodyInk;
                var hole = selected ? Palette.Darken(ui.Accent, ChipHoleDarken) : ChipHole;
                var name = Typography.FitText(Loc.T(info.Title),
                    MathF.Max(1f, cell.Width - glyph - glyphGap - gap * 2f), TextStyles.FootnoteEmphasized);
                var groupWidth = glyph + glyphGap + Typography.Measure(name, TextStyles.FootnoteEmphasized).X;
                var centerY = (pressedMin.Y + pressedMax.Y) * 0.5f;
                var left = (pressedMin.X + pressedMax.X) * 0.5f - groupWidth * 0.5f;
                CasinoGlyphs.Draw(drawList, info.GameId, new Vector2(left + glyph * 0.5f, centerY),
                    SwitcherGlyphExtent * scale, ImGui.GetColorU32(ink), ImGui.GetColorU32(hole));
                Typography.Draw(drawList, new Vector2(left + glyph + glyphGap, centerY - nameHeight * 0.5f), name, ink,
                    TextStyles.FootnoteEmphasized);
                if (UiInteract.Click(cell.Min, cell.Max, hovered))
                {
                    picked = index;
                }
            }
        }

        var reveal = Math.Clamp(switcherReveal.Step(1f, Motion.Appear,
            MathF.Min(ImGui.GetIO().DeltaTime, MaxRevealStep)), 0f, 1f);
        if (reveal < RevealSettled)
        {
            LayerCompositor.Fade(drawList, firstVertex, reveal);
        }

        if (picked >= 0)
        {
            inlineReason = string.Empty;
            draft.SelectGame(HostGames.All[picked].Game, gilOpen);
            switcherOpen = false;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, HostFlowLayout.GridHeight(count, HostFlowLayout.Columns, height, gap)));
    }

    private void DrawPlayFor(AppSkin ui, bool gilOpen, float width, float scale)
    {
        HostCards.Heading(ui, Loc.T(L.Tables.StepPlayFor), string.Empty, width, scale);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var gap = HostFlowLayout.ChoiceGap * scale;
        var labelWidth = MathF.Max(1f,
            HostFlowLayout.ColumnWidth(width, HostFlowLayout.Columns, gap) - HostFlowLayout.CardPad * scale);
        var labelBlock = 0f;
        for (var index = 0; index < Currencies.Length; index++)
        {
            labelBlock = MathF.Max(labelBlock, Typography.MeasureWrappedBlock(Loc.T(CurrencyTitles[index]),
                TextStyles.SubheadlineEmphasized, labelWidth).Y);
        }

        var height = HostFlowLayout.ChoiceTileHeight(scale, labelBlock);
        var radius = Metrics.Radius.Card * scale;
        var icon = HostFlowLayout.ChoiceIcon * scale;
        var innerGap = HostFlowLayout.ChoiceInnerGap * scale;
        for (var index = 0; index < Currencies.Length; index++)
        {
            var currency = Currencies[index];
            var enabled = HostGames.Accepts(draft.Game, currency, gilOpen);
            var selected = draft.Currency == currency;
            var tint = TableRow.CurrencyTint(currency, ui.Accent);
            var cell = HostFlowLayout.GridCell(origin.X, origin.Y, width, index, HostFlowLayout.Columns, height, gap);
            using (UiScope.PushId(index))
            {
                var firstVertex = drawList.VtxBuffer.Size;
                var hovered = CasinoArt.PressCard(ImGui.GetID("currency"), cell.Min, cell.Max, out var pressedMin,
                    out var pressedMax);
                var face = new Rect(pressedMin, pressedMax);
                HostCards.ChoiceSurface(drawList, ui, face, radius, tint, selected, hovered && enabled, scale);
                var title = Loc.T(CurrencyTitles[index]);
                var titleBlock = Typography.MeasureWrappedBlock(title, TextStyles.SubheadlineEmphasized, labelWidth).Y;
                var top = face.Center.Y - (icon + innerGap + titleBlock) * 0.5f;
                var iconCenter = new Vector2(face.Center.X, top + icon * 0.5f);
                drawList.AddCircleFilled(iconCenter, icon * 0.5f, ImGui.GetColorU32(tint with { W = IconWashAlpha }),
                    32);
                ProgressRing.CenterIcon(drawList, iconCenter, CurrencyIcons[index], tint, icon * IconFactor);
                Typography.DrawWrappedCentered(drawList, title, TextStyles.SubheadlineEmphasized, ui.TitleInk,
                    new Vector2(face.Center.X, top + icon + innerGap), labelWidth);
                if (selected)
                {
                    HostCards.Check(drawList, HostFlowLayout.CheckCenter(face, scale), HostFlowLayout.CheckSize * scale,
                        ui.Accent);
                }

                if (!enabled)
                {
                    LayerCompositor.Fade(drawList, firstVertex, DisabledAlpha);
                }

                if (UiInteract.Click(cell.Min, cell.Max, hovered))
                {
                    PickCurrency(currency, enabled, gilOpen);
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        HostCards.Gap(HostFlowLayout.CardGap, scale);
        HostCards.Hint(ui, Loc.T(HostRules.CurrencyHint(draft.Currency)), width);
    }

    private void PickCurrency(int currency, bool enabled, bool gilOpen)
    {
        if (!enabled)
        {
            ShellToast.Show(Loc.T(HostRules.Refusal(draft.Game, currency, gilOpen)));
            return;
        }

        inlineReason = string.Empty;
        draft.SelectCurrency(currency, gilOpen);
    }

    private void DrawTable(AppSkin ui, float width, float scale)
    {
        HostCards.Heading(ui, Loc.T(L.HostSetup.TableHeading), string.Empty, width, scale);
        switch (draft.Game)
        {
            case HostGame.Blackjack:
                if (draft.Gil)
                {
                    DrawBank(ui, width, scale);
                    HostCards.Gap(HostFlowLayout.CardGap, scale);
                }

                HostCards.Range(ui, "##hostBets", Loc.T(L.Tables.Bets),
                    TableAmounts.Range(texts, draft.MinBetValue, draft.MaxBetValue, draft.Currency), string.Empty,
                    draft.BetSpan, ref draft.MinBet, ref draft.MaxBet, width, scale);
                HostCards.Gap(HostFlowLayout.CardGap, scale);
                DrawSeats(ui, width, scale);
                break;
            case HostGame.Holdem:
                HostCards.Ladder(ui, "##hostBlinds", Loc.T(L.Holdem.HostBlinds),
                    texts.Compacts(L.Holdem.BlindsShort, draft.BigBlindValue / 2, draft.BigBlindValue),
                    draft.Practice
                        ? string.Empty
                        : texts.Counts(L.HostSetup.BlindsHint, (int)HoldemRules.MinBuyInBigBlinds,
                            (int)HoldemRules.MaxBuyInBigBlinds),
                    HostLadders.BigBlinds, ref draft.BigBlind, width, scale);
                HostCards.Gap(HostFlowLayout.CardGap, scale);
                DrawSeats(ui, width, scale);
                break;
            case HostGame.DiceTable:
                HostCards.Ladder(ui, "##hostSides", Loc.T(L.HostSetup.RollsUpTo), NumberText.Compact(draft.SidesValue),
                    texts.Named(L.HostSetup.RollsHint, SideCommands[draft.Sides]),
                    HostLadders.All(HostLadders.DiceSides), ref draft.Sides, width, scale);
                break;
            case HostGame.Deathroll:
                HostCards.Ladder(ui, "##hostStart", Loc.T(L.HostSetup.StartFrom),
                    NumberText.Compact(draft.StartAtValue), string.Empty, HostLadders.All(HostLadders.StartNumbers),
                    ref draft.StartAt, width, scale);
                HostCards.Gap(HostFlowLayout.CardGap, scale);
                HostCards.Ladder(ui, "##hostStake", Loc.T(L.HostSetup.DuelStake),
                    TableAmounts.Amount(texts, draft.StakeValue, draft.Currency), Loc.T(L.HostSetup.DuelStakeHint),
                    draft.BetSpan, ref draft.Stake, width, scale);
                break;
            case HostGame.Book:
                DrawBank(ui, width, scale);
                break;
            case HostGame.Raffle:
                HostCards.InfoCard(ui, Loc.T(L.HostSetup.RaffleInfo), width, scale);
                break;
        }
    }

    private void DrawBank(AppSkin ui, float width, float scale) =>
        HostCards.Ladder(ui, "##hostBank", Loc.T(L.Tables.Bank),
            TableAmounts.Amount(texts, draft.BankValue, draft.Currency), Loc.T(L.HostSetup.BankHint),
            HostLadders.All(HostLadders.Gil), ref draft.Bank, width, scale);

    private void DrawSeats(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = HostFlowLayout.CardPad * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var maxSeats = HostGames.MaxSeats(draft.Game);
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var valueHeight = Typography.LineHeight(TextStyles.Headline);
        var head = MathF.Max(labelHeight, valueHeight);
        var height = HostFlowLayout.SeatCardHeight(scale, head);
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Grouped * scale);
        var left = origin.X + pad;
        var top = origin.Y + HostFlowLayout.CardPadTop * scale;
        var value = Typography.FitText(texts.Count(L.Tables.SeatsValue, draft.Seats), inner * 0.5f,
            TextStyles.Headline);
        var valueWidth = Typography.Measure(value, TextStyles.Headline).X;
        Typography.Draw(drawList, new Vector2(left + inner - valueWidth, top + head - valueHeight), value, ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, top + head - labelHeight),
            Typography.FitText(Loc.T(L.Tables.HostSeats),
                MathF.Max(1f, inner - valueWidth - Metrics.Space.Sm * scale), TextStyles.FootnoteEmphasized),
            ui.MutedInk, TextStyles.FootnoteEmphasized);
        var rowTop = top + head;
        var radius = HostFlowLayout.SeatDiscSize(inner, maxSeats, scale) * 0.5f;
        for (var seatIndex = 0; seatIndex < maxSeats; seatIndex++)
        {
            var slot = HostFlowLayout.SeatSlot(left, rowTop, inner, seatIndex, maxSeats, scale);
            var center = HostFlowLayout.SeatCenter(left, rowTop, inner, seatIndex, maxSeats, scale);
            var hovered = UiInteract.Hover(slot.Min, slot.Max);
            if (hovered)
            {
                drawList.AddCircleFilled(center, radius + SeatHoverGrowth * scale, ImGui.GetColorU32(ui.HoverTint), 32);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (seatIndex < draft.Seats)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.Accent), 32);
                ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.User, CasinoArt.White,
                    radius * 2f * SeatIconFactor);
            }
            else
            {
                drawList.AddCircleFilled(center, radius,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, EmptySeatFillAlpha)), 32);
                drawList.AddCircle(center, radius - SeatRimWidth * 0.5f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, EmptySeatRimAlpha)), 32, SeatRimWidth * scale);
                ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.User,
                    Palette.WithAlpha(ui.TitleInk, EmptySeatIconAlpha), radius * 2f * SeatIconFactor);
            }

            if (UiInteract.Click(slot.Min, slot.Max, hovered))
            {
                draft.Seats = Math.Max(CasinoHostingRules.MinSeats, seatIndex + 1);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawJoin(AppSkin ui, float width, float scale)
    {
        HostCards.Heading(ui, Loc.T(L.Tables.HostListing), string.Empty, width, scale);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var gap = HostFlowLayout.ChoiceGap * scale;
        var labelWidth = MathF.Max(1f,
            HostFlowLayout.ColumnWidth(width, HostFlowLayout.Columns, gap) - HostFlowLayout.CardPad * scale);
        var labelBlock = 0f;
        for (var index = 0; index < Listings.Length; index++)
        {
            labelBlock = MathF.Max(labelBlock, Typography.MeasureWrappedBlock(Loc.T(ListingTitles[index]),
                TextStyles.SubheadlineEmphasized, labelWidth).Y);
        }

        var height = HostFlowLayout.JoinTileHeight(scale, labelBlock);
        var radius = Metrics.Radius.Card * scale;
        var icon = HostFlowLayout.JoinIcon * scale;
        var innerGap = HostFlowLayout.JoinInnerGap * scale;
        for (var index = 0; index < Listings.Length; index++)
        {
            var listing = Listings[index];
            var selected = draft.Listing == listing;
            var cell = HostFlowLayout.GridCell(origin.X, origin.Y, width, index, HostFlowLayout.Columns, height, gap);
            using (UiScope.PushId(index))
            {
                var hovered = CasinoArt.PressCard(ImGui.GetID("listing"), cell.Min, cell.Max, out var pressedMin,
                    out var pressedMax);
                var face = new Rect(pressedMin, pressedMax);
                HostCards.ChoiceSurface(drawList, ui, face, radius, ui.Accent, selected, hovered, scale);
                var ink = selected ? ui.TitleInk : ui.BodyInk;
                var title = Loc.T(ListingTitles[index]);
                var titleBlock = Typography.MeasureWrappedBlock(title, TextStyles.SubheadlineEmphasized, labelWidth).Y;
                var top = face.Center.Y - (icon + innerGap + titleBlock) * 0.5f;
                ProgressRing.CenterIcon(drawList, new Vector2(face.Center.X, top + icon * 0.5f), ListingIcons[index],
                    ink, icon);
                Typography.DrawWrappedCentered(drawList, title, TextStyles.SubheadlineEmphasized, ink,
                    new Vector2(face.Center.X, top + icon + innerGap), labelWidth);
                if (UiInteract.Click(cell.Min, cell.Max, hovered))
                {
                    draft.Listing = listing;
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        HostCards.Gap(HostFlowLayout.CardGap, scale);
        HostCards.Hint(ui, Loc.T(HostRules.ListingHint(draft.Listing)), width);
    }

    private void DrawName(AppSkin ui, float width, float scale)
    {
        HostCards.Heading(ui, Loc.T(L.Tables.HostName), Loc.T(L.HostSetup.Optional), width, scale);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = HostFlowLayout.FieldHeight * scale;
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        Squircle.Fill(drawList, field.Min, field.Max, height * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        Squircle.Stroke(drawList, field.Min, field.Max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, FieldRimAlpha)), scale);
        GlassField.Text(field, "##hostName", texts.Named(L.HostSetup.NamePlaceholder, Loc.T(HostGames.Of(draft.Game).Title)),
            ref draft.Name, ui.Theme, scale, NameMaxLength, false, ImGuiInputTextFlags.None);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawRulesRow(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var line = RulesSummary();
        var pad = HostFlowLayout.CardPad * scale;
        var icon = HostFlowLayout.RulesIcon * scale;
        var textWidth = HostFlowLayout.RulesTextWidth(width, scale);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.MeasureWrappedBlock(line, TextStyles.Footnote, textWidth).Y;
        var height = HostFlowLayout.RulesRowHeight(scale, titleHeight, lineHeight);
        var max = new Vector2(origin.X + width, origin.Y + height);
        var enabled = OpenRules is not null && !tables.IntentInFlight;
        var hovered = CasinoArt.PressCard(ImGui.GetID("rules"), origin, max, out var pressedMin, out var pressedMax,
            enabled);
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, pressedMin, pressedMax, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, pressedMin, pressedMax, radius, ImGui.GetColorU32(ui.HoverTint));
        }

        var centerY = (pressedMin.Y + pressedMax.Y) * 0.5f;
        CasinoArt.IconTileAt(drawList, new Vector2(pressedMin.X + pad + icon * 0.5f, centerY), icon, AccentRing.Slate,
            FontAwesomeIcon.SlidersH);
        var left = pressedMin.X + pad + icon + HostFlowLayout.HeaderGap * scale;
        var top = centerY - (titleHeight + 2f * scale + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(Loc.T(L.HostSetup.RulesTitle), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(left, top + titleHeight + 2f * scale), line, ui.MutedInk,
            TextStyles.Footnote, textWidth);
        CasinoArt.Chevron(drawList,
            new Vector2(pressedMax.X - pad - HostFlowLayout.Chevron * 0.5f * scale, centerY), ui.MutedInk);
        if (enabled && UiInteract.Click(origin, max, hovered))
        {
            OpenRules!.Invoke();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private string PinNote()
    {
        if ((venueOptions.Notes(draft.Listing) & Venue.VenuePinNotes.InviteOnly) == 0)
        {
            return string.Empty;
        }

        return Loc.T(draft.Game == HostGame.Book ? L.HostSetup.BookInviteOnlyNote : L.HostSetup.InviteOnlyNote);
    }

    private string RulesSummary()
    {
        var key = HostRulesKey.Of(draft, venueOptions.Pinned);
        if (key == rulesKey && rulesSummary.Length > 0)
        {
            return rulesSummary;
        }

        rulesKey = key;
        rulesSummary = HostRules.Summary(draft, key.Pinned);
        return rulesSummary;
    }

    private string Summary()
    {
        var low = draft.Game switch
        {
            HostGame.Holdem => draft.BigBlindValue,
            HostGame.DiceTable => draft.SidesValue,
            HostGame.Deathroll => draft.StartAtValue,
            HostGame.Book => draft.BankValue,
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
        var listing = Loc.T(HostRules.ListingSummary(draft.Listing));
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
            HostGame.Book => Loc.T(L.Book.Summary, game, TableAmounts.Amount(texts, draft.BankValue, draft.Currency),
                listing),
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

    private static string[] Commands(long[] values)
    {
        var commands = new string[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            commands[index] = values[index].ToString(CultureInfo.InvariantCulture);
        }

        return commands;
    }
}
