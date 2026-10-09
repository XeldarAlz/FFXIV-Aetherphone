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
    private const float SignHeight = 38f;
    private const float BadgeHeight = 20f;
    private const float DotsRow = 10f;

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
        var pad = Metrics.Space.Lg * scale;
        var inner = width - pad * 2f;
        var pitch = Loc.T(L.Holdem.PitHint);
        var signHeight = CasinoSigns.HeightToFit(CasinoSign.Holdem, width * 0.7f, SignHeight * scale);
        var height = MathF.Max(HeroHeight * scale,
            pad * 2f + signHeight + Metrics.Space.Sm * scale + PitText.Height(pitch, TextStyles.Subheadline, inner));
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Grouped * scale;
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(CasinoColors.FeltTop), ImGui.GetColorU32(CasinoColors.FeltBottom));
        CasinoLights.BulbChase(drawList, rect, rounding, scale, phase, CasinoLights.BulbPitch, CasinoColors.Money,
            CasinoColors.LightA, 0.7f);
        CasinoSigns.Draw(drawList, CasinoSign.Holdem, new Vector2(rect.Center.X, rect.Min.Y + pad + signHeight * 0.5f),
            signHeight, CasinoColors.LightA, 1f);
        PitText.Draw(drawList, pitch, TextStyles.Subheadline, StageText.Strong, rect.Min.X + pad,
            rect.Min.Y + pad + signHeight + Metrics.Space.Sm * scale, inner, true);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawRoom(AppSkin ui, in HoldemRoomView view, int tier, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var textWidth = width - pad * 2f;
        var badge = view.Locked ? Loc.T(L.Strip.TitleWhale) : string.Empty;
        var badgeWidth = badge.Length > 0 ? Typography.Measure(badge, TextStyles.Footnote).X + 30f * scale : 0f;
        var nameWidth = textWidth - (badgeWidth > 0f ? badgeWidth + Metrics.Space.Sm * scale : 0f);
        var detail = view.Locked ? Loc.T(L.Holdem.RoyalLocked) : view.BuyIn;
        var nameHeight = MathF.Max(PitText.Height(view.Name, TextStyles.SubheadlineEmphasized, nameWidth),
            badgeWidth > 0f ? BadgeHeight * scale : 0f);
        var height = pad * 2f + nameHeight + 2f * scale
            + PitText.Height(view.Blinds, TextStyles.SubheadlineEmphasized, textWidth)
            + PitText.Height(detail, TextStyles.Footnote, textWidth) + (3f + DotsRow) * scale;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + MathF.Max(RoomHeight * scale, height)));
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

        var left = rect.Min.X + pad;
        var y = rect.Min.Y + pad;
        if (badgeWidth > 0f)
        {
            var badgeMax = new Vector2(rect.Max.X - pad, y + BadgeHeight * scale);
            var badgeMin = new Vector2(badgeMax.X - badgeWidth, y);
            Squircle.Fill(drawList, badgeMin, badgeMax, BadgeHeight * 0.5f * scale,
                ImGui.GetColorU32(CasinoColors.Money with { W = 0.16f }));
            PhoneIcon.Draw(drawList, new Vector2(badgeMin.X + 11f * scale, (badgeMin.Y + badgeMax.Y) * 0.5f), LockGlyph,
                CasinoColors.Money, 11f * scale);
            Typography.Draw(drawList, new Vector2(badgeMin.X + 20f * scale,
                    (badgeMin.Y + badgeMax.Y) * 0.5f - Typography.LineHeight(TextStyles.Footnote) * 0.5f), badge,
                CasinoColors.Money, TextStyles.Footnote);
        }

        PitText.Draw(drawList, view.Name, TextStyles.SubheadlineEmphasized, ui.TitleInk, left, y, nameWidth, false);
        y += nameHeight + 2f * scale;
        y = PitText.Draw(drawList, view.Blinds, TextStyles.SubheadlineEmphasized, CasinoColors.Money, left, y,
            textWidth, false);
        y = PitText.Draw(drawList, detail, TextStyles.Footnote, ui.BodyInk, left, y, textWidth, false);
        y += 3f * scale;
        var dotsY = y + DotsRow * 0.5f * scale;
        var dotsWidth = CasinoArt.SeatDots(drawList, new Vector2(left, dotsY), view.Seated, view.MaxSeats,
            ui.Accent, ui.MutedInk with { W = 0.3f }, scale);
        Typography.Draw(drawList, new Vector2(left + dotsWidth + Metrics.Space.Sm * scale,
                dotsY - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            Typography.FitText(view.Seats, textWidth - dotsWidth - Metrics.Space.Sm * scale, TextStyles.Footnote),
            ui.BodyInk, TextStyles.Footnote);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            inlineReason = string.Empty;
            openTable(view.RoomId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rect.Height + RowGap * scale));
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
                ui.BodyInk, TextStyles.Subheadline, width - Metrics.Space.Md * 2f * scale);
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
        var textWidth = width - 62f * scale;
        var title = Loc.T(L.Holdem.HostAction);
        var hint = Loc.T(L.Holdem.HostHint);
        var pad = 12f * scale;
        var height = MathF.Max(HostRowHeight * scale, pad * 2f
            + PitText.Height(title, TextStyles.SubheadlineEmphasized, textWidth)
            + PitText.Height(hint, TextStyles.Footnote, textWidth));
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
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
        var y = PitText.Draw(drawList, title, TextStyles.SubheadlineEmphasized, ui.TitleInk, textLeft,
            rect.Min.Y + pad, textWidth, false);
        PitText.Draw(drawList, hint, TextStyles.Footnote, ui.BodyInk, textLeft, y, textWidth, false);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            openHost();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
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
