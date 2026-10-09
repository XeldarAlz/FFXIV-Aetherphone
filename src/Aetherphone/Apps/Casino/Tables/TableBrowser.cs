using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class TableBrowser
{
    private const string CodeFieldId = "##casinoJoinCode";
    private const string JoinButtonId = "casino.tables.join";
    private const float RowGap = 10f;
    private const float SectionGap = 20f;
    private const float JoinGap = 8f;
    private const float JoinMinWidth = 92f;
    private const float CtaIcon = 44f;
    private const float EmptyIcon = 52f;
    private const float AccentFillAlpha = 0.14f;
    private const float AccentRimAlpha = 0.45f;
    private const int CodeBufferLength = 80;

    private static readonly LocString[] FilterLabels =
    {
        L.Casino.TableFilterAll,
        L.Tables.FilterBlackjack,
        L.Tables.FilterHoldem,
        L.Venue.FilterRooms,
        L.Casino.TableFilterLowStakes,
        L.Casino.TableFilterHighStakes,
        L.Tables.FilterPractice,
        L.Tables.FilterGil,
        L.Casino.TableFilterMine,
    };

    private static readonly LocString[] CurrencyNames =
        { L.Tables.CurrencyChips, L.Tables.CurrencyPractice, L.Tables.CurrencyGil };

    private readonly CasinoTablesStore tables;
    private readonly CasinoStore chips;
    private readonly Action<string> openTable;
    private readonly Action<string> openDoor;
    private readonly Action openHostSheet;
    private readonly Venue.NearbyTablesCard nearby;
    private readonly Action<CasinoTableRowDto> openNearby;
    private readonly ChipRail rail = new();
    private readonly string[] filterLabels = new string[FilterLabels.Length];
    private readonly bool[] filterActive = new bool[FilterLabels.Length];

    private CasinoTableRowDto[] viewSource = Array.Empty<CasinoTableRowDto>();
    private TableRowView[] views = Array.Empty<TableRowView>();
    private LanguageInfo? viewLanguage;
    private string viewAccount = string.Empty;
    private int filterIndex;
    private string codeBuffer = string.Empty;
    private string checkedBuffer = string.Empty;
    private JoinInput checkedInput;
    private string inlineReason = string.Empty;

    public TableBrowser(CasinoTablesStore tables, CasinoStore chips, Action<string> openTable, Action<string> openDoor,
        Action openHostSheet, Venue.NearbyTablesCard nearby)
    {
        this.nearby = nearby;
        openNearby = row => openTable(row.TableId);
        this.tables = tables;
        this.chips = chips;
        this.openTable = openTable;
        this.openDoor = openDoor;
        this.openHostSheet = openHostSheet;
    }

    private enum JoinInput : byte
    {
        None,
        Code,
        Token,
    }

    public CasinoTableFilter Filter => CasinoTableFilters.All[filterIndex];

    public void Enter()
    {
        inlineReason = string.Empty;
        rail.Reset();
        tables.RefreshNow();
    }

    public void Reset()
    {
        inlineReason = string.Empty;
        codeBuffer = string.Empty;
        rail.Reset();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        ConsumeOutcomes();
        RefreshViews();
        using var surface = AppSurface.Begin(body);
        var width = ScrollLayout.StableContentWidth();
        DrawJoinByCode(ui, width, scale);
        if (inlineReason.Length > 0)
        {
            DrawInlineReason(ui, width, scale);
        }

        Gap(SectionGap, scale);
        DrawHostCta(ui, width, scale);
        Gap(SectionGap, scale);
        DrawNearby(ui, width, scale);
        DrawFilters(ui);
        Gap(Metrics.Space.Sm, scale);
        DrawRows(ui, width, scale);
        Gap(RowGap, scale);
        DrawQuickSeat(ui, width, scale);
        Gap(Metrics.Space.Xl, scale);
    }

    internal static TableRowView ViewOf(CasinoTableRowDto row, string myUserId)
    {
        var currency = CasinoCurrencies.Of(row);
        var hosted = CasinoTableFilters.IsPrivate(row);
        var mine = myUserId.Length > 0 && string.Equals(row.OwnerUserId, myUserId, StringComparison.Ordinal);
        var name = row.Name.Length > 0
            ? row.Name
            : hosted && row.OwnerName.Length > 0
                ? Loc.T(L.Casino.TableHostedBy, row.OwnerName)
                : Loc.T(L.Casino.TableUnnamed);
        var room = VenueKinds.Of(row.GameKind);
        var stakes = room != VenueRoomKind.None
            ? Loc.T(Venue.VenueCabinet.NameOf(room))
            : currency == CasinoCurrencies.Gil
                ? Loc.T(L.Tables.GilStakes, NumberText.Group(row.MaxBet), NumberText.Group(row.Config?.Bank ?? 0))
                : Loc.T(L.Casino.TableStakes, NumberText.Compact(row.MinBet), NumberText.Compact(row.MaxBet));
        var seats = room != VenueRoomKind.None
            ? Loc.T(L.Venue.InRoom, row.Occupancy.ToString(Loc.Culture))
            : Loc.T(L.Casino.TableSeats, row.SeatedCount.ToString(Loc.Culture), row.MaxSeats.ToString(Loc.Culture));
        var watching = room != VenueRoomKind.None ? 0 : CasinoTableFilters.SpectatorsOf(row);
        var spectators = watching > 0
            ? Loc.T(L.Casino.TableSpectators, watching.ToString(Loc.Culture))
            : string.Empty;
        var reputation = ReputationOf(row, currency, out var warns);
        var draining = string.Equals(row.Reason, CasinoReasons.Draining, StringComparison.Ordinal)
            || string.Equals(row.Reason, CasinoReasons.TableClosed, StringComparison.Ordinal);
        var inviteOnly = hosted && !mine && row.Listing == CasinoListings.Private;
        var hostLine = hosted && row.OwnerName.Length > 0 ? Loc.T(L.Tables.HostedBy, row.OwnerName) : string.Empty;
        var monogram = hosted && row.OwnerName.Length > 0 ? Initials.Of(row.OwnerName) : string.Empty;
        return new TableRowView(name, stakes, seats, spectators,
            room == VenueRoomKind.None && !CasinoTableFilters.HasOpenSeat(row), inviteOnly,
            mine, draining, currency, Loc.T(CurrencyNames[currency]), reputation, warns, row.Paused,
            GameIdOf(row.GameKind), hostLine, monogram, room == VenueRoomKind.None ? row.SeatedCount : row.Occupancy,
            row.MaxSeats, room);
    }

    internal static string GameIdOf(string gameKind)
    {
        if (string.Equals(gameKind, HoldemRules.Kind, StringComparison.Ordinal))
        {
            return CasinoGames.Holdem;
        }

        return VenueKinds.Of(gameKind) switch
        {
            VenueRoomKind.Dice => CasinoGames.DiceTable,
            VenueRoomKind.Deathroll => CasinoGames.Deathroll,
            VenueRoomKind.Raffle => CasinoGames.Raffle,
            _ => CasinoGames.Blackjack,
        };
    }

    private static string ReputationOf(CasinoTableRowDto row, int currency, out bool warns)
    {
        warns = false;
        var reputation = row.Reputation;
        if (currency != CasinoCurrencies.Gil || reputation is null)
        {
            return string.Empty;
        }

        if (reputation.Frozen)
        {
            warns = true;
            return Loc.T(L.Tables.ReputationFrozen);
        }

        var hosted = reputation.GilTablesHosted.ToString(Loc.Culture);
        var confirmed = reputation.PayoutsConfirmed.ToString(Loc.Culture);
        if (reputation.DisputesOpen <= 0)
        {
            return Loc.T(L.Tables.Reputation, hosted, confirmed);
        }

        warns = true;
        return Loc.T(L.Tables.ReputationDisputes, hosted, confirmed, reputation.DisputesOpen.ToString(Loc.Culture));
    }

    private static void Gap(float units, float scale) => ImGui.Dummy(new Vector2(0f, units * scale));

    private void RefreshViews()
    {
        var source = tables.Listed;
        var account = tables.AccountId;
        if (ReferenceEquals(source, viewSource) && ReferenceEquals(viewLanguage, Loc.Current)
            && string.Equals(account, viewAccount, StringComparison.Ordinal))
        {
            return;
        }

        viewSource = source;
        viewLanguage = Loc.Current;
        viewAccount = account;
        views = new TableRowView[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            views[index] = ViewOf(source[index], account);
        }
    }

    private void ConsumeOutcomes()
    {
        var directoryFailed = tables.TakeTablesFailure();
        var intentFailed = tables.TakeIntentFailure();
        if (directoryFailed || intentFailed)
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        var notice = tables.TakeNoticeOutcome();
        if (notice is not null)
        {
            inlineReason = notice.Granted ? string.Empty : notice.Reason;
        }
    }

    private void DrawJoinByCode(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        Typography.Draw(drawList, origin, Typography.FitText(Loc.T(L.Tables.JoinHeading), width, TextStyles.Title3),
            ui.TitleInk, TextStyles.Title3);
        var top = origin.Y + titleHeight + Metrics.Space.Sm * scale;
        var height = Button.LargeHeight * scale;
        var label = Loc.T(L.Tables.JoinAction);
        var buttonWidth = MathF.Max(JoinMinWidth * scale, Button.WidthFor(label, ButtonSize.Large));
        var field = new Rect(new Vector2(origin.X, top),
            new Vector2(origin.X + width - buttonWidth - JoinGap * scale, top + height));
        var fill = Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary);
        Squircle.Fill(drawList, field.Min, field.Max, field.Height * 0.5f, ImGui.GetColorU32(fill));
        var submitted = GlassField.Title(field, CodeFieldId, Loc.T(L.Tables.JoinFieldHint), ref codeBuffer, ui.Theme,
            scale, CodeBufferLength, ImGuiInputTextFlags.EnterReturnsTrue);
        var input = Classify(codeBuffer);
        var ready = input != JoinInput.None && !tables.IntentInFlight;
        var button = new Rect(new Vector2(field.Max.X + JoinGap * scale, top),
            new Vector2(origin.X + width, top + height));
        var tapped = Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Prominent, enabled: ready,
            id: JoinButtonId);
        if ((tapped || submitted) && ready)
        {
            Join(input);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, top + height - origin.Y));
    }

    private JoinInput Classify(string buffer)
    {
        if (ReferenceEquals(buffer, checkedBuffer))
        {
            return checkedInput;
        }

        checkedBuffer = buffer;
        checkedInput = CasinoRoomCodes.IsCode(buffer)
            ? JoinInput.Code
            : buffer.Trim().Length > CasinoRoomCodes.RawMaxLength && CasinoShare.TryParse(buffer, out _)
                ? JoinInput.Token
                : JoinInput.None;
        return checkedInput;
    }

    private void Join(JoinInput input)
    {
        inlineReason = string.Empty;
        if (input == JoinInput.Code)
        {
            tables.JoinByCode(codeBuffer);
            codeBuffer = string.Empty;
            return;
        }

        if (CasinoShare.TryParse(codeBuffer, out var tableId))
        {
            tables.ResolveToken(tableId);
            codeBuffer = string.Empty;
        }
    }

    private void DrawInlineReason(AppSkin ui, float width, float scale)
    {
        Gap(RowGap, scale);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
        var pad = 14f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Subheadline, width - pad * 2f);
        var max = new Vector2(origin.X + width, origin.Y + block.Y + pad * 2f);
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, origin, max, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, origin, max, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)),
            1f * scale);
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), message, ui.TitleInk,
            TextStyles.Subheadline, width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, max.Y - origin.Y));
    }

    private void DrawHostCta(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var icon = CtaIcon * scale;
        var textLeft = origin.X + pad + icon + pad;
        var textWidth = MathF.Max(1f, origin.X + width - pad * 2f - 12f * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var line = Loc.T(L.Tables.HostCtaLine);
        var lineHeight = Typography.MeasureWrappedBlock(line, TextStyles.Subheadline, textWidth).Y;
        var height = MathF.Max(icon + pad * 2f, titleHeight + lineHeight + pad * 2f);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var radius = Metrics.Radius.Grouped * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, radius);
        Squircle.Fill(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(ui.Accent with { W = hovered ? AccentFillAlpha * 1.6f : AccentFillAlpha }));
        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.Accent with { W = AccentRimAlpha }),
            1.2f * scale);
        var iconCenter = new Vector2(origin.X + pad + icon * 0.5f, card.Center.Y);
        drawList.AddCircleFilled(iconCenter, icon * 0.5f, ImGui.GetColorU32(ui.Accent), 32);
        ProgressRing.CenterIcon(drawList, iconCenter, FontAwesomeIcon.Plus, CasinoArt.White, icon * 0.42f);
        var top = card.Center.Y - (titleHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(L.Tables.HostCta), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + titleHeight), line, ui.BodyInk, TextStyles.Subheadline,
            textWidth);
        CasinoArt.Chevron(drawList, new Vector2(card.Max.X - pad - 6f * scale, card.Center.Y), ui.MutedInk);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenHost();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void OpenHost()
    {
        if (tables.IntentInFlight)
        {
            return;
        }

        inlineReason = string.Empty;
        if (chips.HasFeature(CasinoFeatures.HostingV2))
        {
            openHostSheet();
            return;
        }

        tables.CreatePrivateTable(CasinoStakeTiers.From(Filter));
    }

    private void DrawNearby(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var bottom = nearby.Draw(ImGui.GetWindowDrawList(), ui, origin, width, openNearby);
        if (bottom <= origin.Y)
        {
            return;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y + SectionGap * scale));
    }

    private void DrawFilters(AppSkin ui)
    {
        for (var index = 0; index < FilterLabels.Length; index++)
        {
            filterLabels[index] = Loc.T(FilterLabels[index]);
            filterActive[index] = index == filterIndex;
        }

        var tapped = rail.Draw(ui, filterLabels, filterActive);
        if (tapped >= 0)
        {
            filterIndex = tapped;
        }
    }

    private void DrawRows(AppSkin ui, float width, float scale)
    {
        var directory = viewSource;
        var filter = Filter;
        var drawList = ImGui.GetWindowDrawList();
        var drawn = 0;
        for (var index = 0; index < directory.Length && index < views.Length; index++)
        {
            var row = directory[index];
            if (!CasinoTableFilters.Matches(filter, row, viewAccount))
            {
                continue;
            }

            var origin = ImGui.GetCursorScreenPos();
            var height = TableRow.HeightOf(views[index]) * scale;
            var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            using (ImRaii.PushId(index))
            {
                if (TableRow.Draw(drawList, rect, ui, views[index], scale))
                {
                    Open(row, views[index]);
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + RowGap * scale));
            drawn++;
        }

        if (drawn > 0)
        {
            return;
        }

        if (!tables.Loaded)
        {
            DrawNote(ui, width, Loc.T(L.Casino.TablesLoading), scale);
            return;
        }

        DrawEmpty(ui, width, filterIndex == 0 ? Loc.T(L.Tables.EmptyBody) : Loc.T(L.Tables.EmptyFiltered), scale);
    }

    private void Open(CasinoTableRowDto row, in TableRowView view)
    {
        inlineReason = string.Empty;
        if (view.Mine)
        {
            openDoor(row.TableId);
            return;
        }

        if (!string.Equals(row.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal)
            && !string.Equals(row.GameKind, HoldemRules.Kind, StringComparison.Ordinal)
            && !VenueKinds.IsVenue(row.GameKind))
        {
            inlineReason = CasinoReasons.Unavailable;
            return;
        }

        openTable(row.TableId);
    }

    private void DrawEmpty(AppSkin ui, float width, string body, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 18f * scale;
        var inner = width - pad * 2f;
        var icon = EmptyIcon * scale;
        var title = Loc.T(L.Tables.EmptyTitle);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Title3, inner).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, inner).Y;
        var buttonHeight = Button.LargeHeight * scale;
        var gap = Metrics.Space.Md * scale;
        var height = pad + icon + gap + titleHeight + Metrics.Space.Xs * scale + bodyHeight + gap + buttonHeight + pad;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var iconCenter = new Vector2(card.Center.X, origin.Y + pad + icon * 0.5f);
        drawList.AddCircleFilled(iconCenter, icon * 0.5f, ImGui.GetColorU32(ui.Accent with { W = 0.18f }), 40);
        ProgressRing.CenterIcon(drawList, iconCenter, FontAwesomeIcon.Users, ui.Accent, icon * 0.42f);
        var top = iconCenter.Y + icon * 0.5f + gap;
        Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(card.Center.X, top), inner);
        top += titleHeight + Metrics.Space.Xs * scale;
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, ui.BodyInk,
            new Vector2(card.Center.X, top), inner);
        var label = Loc.T(L.Tables.HostCta);
        var buttonWidth = MathF.Min(inner, Button.WidthFor(label, ButtonSize.Large) + 24f * scale);
        var button = new Rect(new Vector2(card.Center.X - buttonWidth * 0.5f, card.Max.Y - pad - buttonHeight),
            new Vector2(card.Center.X + buttonWidth * 0.5f, card.Max.Y - pad));
        if (Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Prominent, enabled: !tables.IntentInFlight,
                id: "casino.tables.emptyHost"))
        {
            OpenHost();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawNote(AppSkin ui, float width, string text, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 16f * scale;
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, width - pad * 2f);
        var max = new Vector2(origin.X + width, origin.Y + block.Y + pad * 2f);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), text, ui.BodyInk,
            TextStyles.Subheadline, width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, max.Y - origin.Y));
    }

    private void DrawQuickSeat(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = Button.LargeHeight * scale;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (Button.Draw(ImGui.GetWindowDrawList(), rect, Loc.T(L.Casino.QuickSeatAction), ui.Ink, ButtonStyle.Tinted,
                enabled: !tables.IntentInFlight, id: "casino.tables.quickSeat"))
        {
            inlineReason = string.Empty;
            tables.QuickSeat(CasinoStakeTiers.From(Filter));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }
}
