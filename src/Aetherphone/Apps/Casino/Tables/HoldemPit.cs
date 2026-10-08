using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly record struct HoldemRoomView(
    string RoomId,
    string Name,
    string Blinds,
    string BuyIn,
    string Seats,
    int Seated,
    int MaxSeats,
    bool Locked);

internal sealed class HoldemPit
{
    private const float HeroHeight = 112f;
    private const float RoomHeight = 102f;
    private const float RowGap = 10f;
    private const float HostRowHeight = 60f;

    private static readonly LocString[] RoomNames =
    {
        L.Holdem.RoomLow,
        L.Holdem.RoomMid,
        L.Holdem.RoomHigh,
        L.Holdem.RoomRoyal,
    };

    private static readonly string LockGlyph = IconGlyph.Of(FontAwesomeIcon.Lock);
    private static readonly string HostGlyph = IconGlyph.Of(FontAwesomeIcon.UserFriends);

    private readonly HoldemStore store;
    private readonly CasinoStore chips;
    private readonly Action<string> openTable;
    private readonly Action<string> openDoor;
    private readonly Action openHost;
    private readonly HoldemRoomView[] houseViews = new HoldemRoomView[HoldemRules.TierCount];

    private CasinoTableRowDto[] hosted = Array.Empty<CasinoTableRowDto>();
    private TableRowView[] hostedViews = Array.Empty<TableRowView>();
    private CasinoTableRowDto[] viewSource = Array.Empty<CasinoTableRowDto>();
    private LanguageInfo? viewLanguage;
    private bool viewLocked;
    private string inlineReason = string.Empty;
    private float phase;

    public HoldemPit(HoldemStore store, CasinoStore chips, Action<string> openTable, Action<string> openDoor,
        Action openHost)
    {
        this.store = store;
        this.chips = chips;
        this.openTable = openTable;
        this.openDoor = openDoor;
        this.openHost = openHost;
    }

    public void Enter()
    {
        inlineReason = string.Empty;
        store.RefreshTables();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        phase += MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        store.EnsureTables();
        if (store.TakeTablesFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        RefreshViews();
        using var surface = AppSurface.Begin(body);
        DrawHero(ui, scale);
        if (inlineReason.Length > 0)
        {
            DrawReason(ui, scale);
        }

        ui.SectionHeading(Loc.T(L.Holdem.HouseTables), Metrics.Space.Sm);
        for (var tier = 0; tier < houseViews.Length; tier++)
        {
            DrawRoom(ui, houseViews[tier], tier, scale);
        }

        ui.SectionHeading(Loc.T(L.Holdem.HostedTables), Metrics.Space.Md);
        DrawHosted(ui, scale);
        DrawHostRow(ui, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void RefreshViews()
    {
        var source = store.Tables;
        var locked = !HoldemRules.RoyalOpenTo(Balance());
        if (ReferenceEquals(source, viewSource) && ReferenceEquals(viewLanguage, Loc.Current) && locked == viewLocked
            && houseViews[0].Name is not null)
        {
            return;
        }

        viewSource = source;
        viewLanguage = Loc.Current;
        viewLocked = locked;
        for (var tier = 0; tier < houseViews.Length; tier++)
        {
            houseViews[tier] = HouseView(tier, RowOf(source, HoldemRules.HouseRooms[tier]), locked);
        }

        var count = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (IsHosted(source[index]))
            {
                count++;
            }
        }

        hosted = new CasinoTableRowDto[count];
        hostedViews = new TableRowView[count];
        var next = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (!IsHosted(source[index]))
            {
                continue;
            }

            hosted[next] = source[index];
            hostedViews[next] = TableBrowser.ViewOf(source[index], store.AccountId);
            next++;
        }
    }

    internal static bool IsHosted(CasinoTableRowDto row) =>
        string.Equals(row.GameKind, HoldemRules.Kind, StringComparison.Ordinal)
        && !HoldemRules.IsHouseRoom(row.TableId) && row.Kind != CasinoTableKinds.House;

    private static CasinoTableRowDto? RowOf(CasinoTableRowDto[] rows, string roomId)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].TableId, roomId, StringComparison.Ordinal))
            {
                return rows[index];
            }
        }

        return null;
    }

    internal static HoldemRoomView HouseView(int tier, CasinoTableRowDto? row, bool royalLocked)
    {
        var small = row is { MinBet: > 0 } ? row.MinBet : HoldemRules.SmallBlindFor(tier);
        var big = row is { MaxBet: > 0 } ? row.MaxBet : HoldemRules.BigBlindFor(tier);
        var minBuyIn = row is { MinBuyIn: > 0 } ? row.MinBuyIn : HoldemRules.MinBuyInFor(tier);
        var maxBuyIn = row is { MaxBuyIn: > 0 } ? row.MaxBuyIn : HoldemRules.MaxBuyInFor(tier);
        var maxSeats = row is { MaxSeats: > 0 } ? row.MaxSeats : HoldemRules.HouseSeats;
        var seated = row?.SeatedCount ?? 0;
        return new HoldemRoomView(HoldemRules.HouseRooms[tier], Loc.T(RoomNames[tier]),
            Loc.T(L.Holdem.BlindsValue, NumberText.Compact(small), NumberText.Compact(big)),
            Loc.T(L.Holdem.BuyInValue, NumberText.Compact(minBuyIn), NumberText.Compact(maxBuyIn)),
            Loc.T(L.Holdem.SeatedLine, Games.Framework.GameNumber.Label(seated),
                Games.Framework.GameNumber.Label(maxSeats)),
            seated, maxSeats, tier == HoldemRules.RoyalTier && royalLocked);
    }

    private long Balance() => (chips.State?.Sitting?.Stack ?? 0) + (chips.State?.TableSitting?.Stack ?? 0);

    private void DrawHero(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + HeroHeight * scale));
        var rounding = Metrics.Radius.Grouped * scale;
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(CasinoColors.FeltTop), ImGui.GetColorU32(CasinoColors.FeltBottom));
        CasinoLights.BulbChase(drawList, rect, rounding, scale, phase, CasinoLights.BulbPitch, CasinoColors.Money,
            CasinoColors.LightA, 0.7f);
        var signHeight = CasinoSigns.HeightToFit(CasinoSign.Holdem, rect.Width * 0.7f, rect.Height * 0.34f);
        CasinoSigns.Draw(drawList, CasinoSign.Holdem, new Vector2(rect.Center.X, rect.Min.Y + rect.Height * 0.36f),
            signHeight, CasinoColors.LightA, 1f);
        var pitch = Loc.T(L.Holdem.PitHint);
        var pad = Metrics.Space.Lg * scale;
        Typography.DrawWrappedCentered(drawList, pitch, TextStyles.Footnote, CasinoColors.InkBody,
            new Vector2(rect.Center.X, rect.Min.Y + rect.Height * 0.62f), rect.Width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, HeroHeight * scale + Metrics.Space.Sm * scale));
    }

    private void DrawRoom(AppSkin ui, in HoldemRoomView view, int tier, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + RoomHeight * scale));
        var rounding = Metrics.Radius.Grouped * scale;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        ui.Card(drawList, rect.Min, rect.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (tier == HoldemRules.RoyalTier)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(CasinoColors.Money with { W = 0.45f }), MathF.Max(1f, scale));
        }

        var pad = 14f * scale;
        var left = rect.Min.X + pad;
        var textWidth = rect.Width - pad * 2f;
        var y = rect.Min.Y + 11f * scale;
        var badge = view.Locked ? Loc.T(L.Strip.TitleWhale) : string.Empty;
        var badgeWidth = 0f;
        if (badge.Length > 0)
        {
            badgeWidth = Typography.Measure(badge, TextStyles.Footnote).X + 30f * scale;
            var badgeMax = new Vector2(rect.Max.X - pad, y + 20f * scale);
            var badgeMin = new Vector2(badgeMax.X - badgeWidth, y);
            Squircle.Fill(drawList, badgeMin, badgeMax, 10f * scale,
                ImGui.GetColorU32(CasinoColors.Money with { W = 0.16f }));
            PhoneIcon.Draw(drawList, new Vector2(badgeMin.X + 11f * scale, (badgeMin.Y + badgeMax.Y) * 0.5f), LockGlyph,
                CasinoColors.Money, 11f * scale);
            Typography.Draw(drawList, new Vector2(badgeMin.X + 20f * scale,
                    (badgeMin.Y + badgeMax.Y) * 0.5f - Typography.LineHeight(TextStyles.Footnote) * 0.5f), badge,
                CasinoColors.Money, TextStyles.Footnote);
        }

        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(view.Name, textWidth - badgeWidth - Metrics.Space.Sm * scale,
                TextStyles.SubheadlineEmphasized), ui.TitleInk, TextStyles.SubheadlineEmphasized);
        y += Typography.LineHeight(TextStyles.SubheadlineEmphasized) + 2f * scale;
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(view.Blinds, textWidth, TextStyles.Footnote),
            CasinoColors.Money, TextStyles.Footnote);
        y += Typography.LineHeight(TextStyles.Footnote);
        var detail = view.Locked ? Loc.T(L.Holdem.RoyalLocked) : view.BuyIn;
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(detail, textWidth, TextStyles.Footnote),
            ui.BodyInk, TextStyles.Footnote);
        y += Typography.LineHeight(TextStyles.Footnote) + 3f * scale;
        var dotsWidth = CasinoArt.SeatDots(drawList, new Vector2(left, y + 4f * scale), view.Seated, view.MaxSeats,
            ui.Accent, ui.MutedInk with { W = 0.3f }, scale);
        Typography.Draw(drawList, new Vector2(left + dotsWidth + Metrics.Space.Sm * scale,
                y + 4f * scale - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            Typography.FitText(view.Seats, textWidth - dotsWidth - Metrics.Space.Sm * scale, TextStyles.Footnote),
            ui.BodyInk, TextStyles.Footnote);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            inlineReason = string.Empty;
            openTable(view.RoomId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, RoomHeight * scale + RowGap * scale));
    }

    private void DrawHosted(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        if (hosted.Length == 0)
        {
            var origin = ImGui.GetCursorScreenPos();
            var text = Loc.T(store.Loaded ? L.Holdem.NoHostedTables : L.Casino.TablesLoading);
            var height = Typography.DrawWrappedLeft(new Vector2(origin.X + Metrics.Space.Md * scale, origin.Y), text,
                ui.MutedInk, TextStyles.Footnote, width - Metrics.Space.Md * 2f * scale);
            ImGui.Dummy(new Vector2(width, height + RowGap * scale));
            return;
        }

        for (var index = 0; index < hosted.Length; index++)
        {
            var origin = ImGui.GetCursorScreenPos();
            var height = TableRow.HeightOf(hostedViews[index]) * scale;
            var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            if (TableRow.Draw(drawList, rect, ui, hostedViews[index], scale))
            {
                inlineReason = string.Empty;
                var row = hosted[index];
                if (row.Admitted)
                {
                    openTable(row.TableId);
                }
                else
                {
                    openDoor(row.TableId);
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + RowGap * scale));
        }
    }

    private void DrawHostRow(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + HostRowHeight * scale));
        var rounding = Metrics.Radius.Grouped * scale;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        ui.Card(drawList, rect.Min, rect.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconCenter = new Vector2(rect.Min.X + 26f * scale, rect.Center.Y);
        drawList.AddCircleFilled(iconCenter, 15f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 32);
        PhoneIcon.Draw(drawList, iconCenter, HostGlyph, ui.Accent, 15f * scale);
        var textLeft = rect.Min.X + 48f * scale;
        var textWidth = rect.Width - 62f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, rect.Min.Y + 12f * scale),
            Typography.FitText(Loc.T(L.Holdem.HostAction), textWidth, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, rect.Min.Y + 32f * scale),
            Typography.FitText(Loc.T(L.Holdem.HostHint), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            openHost();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, HostRowHeight * scale + RowGap * scale));
    }

    private void DrawReason(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var bottom = CasinoNotice.Draw(ImGui.GetWindowDrawList(), ui, CasinoNoticeKind.Reason, string.Empty,
            Loc.T(CasinoReasons.MessageFor(inlineReason)), origin.X, origin.Y, width, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y + Metrics.Space.Sm * scale));
    }
}
