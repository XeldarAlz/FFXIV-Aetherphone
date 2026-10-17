using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Stage;

internal sealed class BetsRail
{
    public const float HandleWidth = 72f;
    public const float HandleHeight = 22f;

    private const float PanelHeightShare = 0.7f;
    private const float RowHeight = 44f;
    private const float TabsHeight = 36f;
    private const float FeedRowHeight = 52f;
    private const int MyBetsTab = 0;
    private const int HighRollersTab = 2;
    private const int TabCount = 3;

    private readonly SheetSurface sheet = new("casino.bets");
    private readonly Action<Rect> drawSheetBody;
    private readonly string[] tabLabels = new string[TabCount];

    private CasinoBetsLog log = null!;
    private CasinoFloorStore? feed;
    private AppSkin skin = null!;
    private int tab;
    private string requestedRound = string.Empty;

    public BetsRail()
    {
        drawSheetBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open()
    {
        tab = MyBetsTab;
        sheet.Open();
    }

    public void Close()
    {
        sheet.Close();
    }

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public string TakeRoundRequest()
    {
        var taken = requestedRound;
        requestedRound = string.Empty;
        return taken;
    }

    public static Rect HandleRect(Rect deck, float labelWidth, float scale)
    {
        var height = HandleHeight * scale;
        var width = MathF.Max(HandleWidth * scale, labelWidth + height);
        var center = new Vector2(deck.Center.X, deck.Min.Y);
        var half = new Vector2(width, height) * 0.5f;
        return new Rect(center - half, center + half);
    }

    public static Rect HandleHit(Rect handle, float scale)
    {
        var deckTop = handle.Center.Y;
        var top = MathF.Min(handle.Min.Y, deckTop - CasinoStageLayout.SafeInset * scale);
        return new Rect(new Vector2(handle.Min.X, top), handle.Max);
    }

    public static bool DrawHandle(ImDrawListPtr drawList, Rect deck, float scale)
    {
        var label = Loc.T(L.Strip.Bets);
        var style = TextStyles.FootnoteEmphasized;
        var handle = HandleRect(deck, Typography.Measure(label, style).X, scale);
        var hit = HandleHit(handle, scale);
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        Material.LiquidGlass(drawList, handle.Min, handle.Max, handle.Height * 0.5f, scale, GlassTone.Dark,
            hovered ? 0.4f : 0f);
        Typography.DrawCentered(drawList, handle.Center,
            Typography.FitText(label, handle.Width - handle.Height, style), CasinoColors.InkTitle, style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(hit.Min, hit.Max, hovered);
    }

    public void Draw(Rect screen, AppSkin ui, CasinoBetsLog bets, CasinoFloorStore? floorFeed)
    {
        skin = ui;
        log = bets;
        feed = floorFeed;
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(L.Strip.Bets), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        var scale = UiScale.Current;
        tabLabels[0] = Loc.T(L.Strip.MyBets);
        tabLabels[1] = Loc.T(L.Strip.AllBets);
        tabLabels[2] = Loc.T(L.Strip.HighRollers);
        var tabsRect = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + TabsHeight * scale));
        tab = SegmentStrip.Draw("casino.bets.tabs", tabsRect, tabLabels, tab,
            Surfaces.Fill(skin.TitleInk, FillLevel.Tertiary), skin.Accent, skin.MutedInk, CasinoColors.InkTitle,
            overlay: true);
        var listTop = tabsRect.Max.Y + Metrics.Space.Md * scale;
        var list = new Rect(new Vector2(content.Min.X, listTop), content.Max);
        ImGui.SetCursorScreenPos(list.Min);
        using (ImRaii.Child("##casinoBetsList", list.Size, false, ImGuiWindowFlags.NoBackground))
        {
            if (tab == MyBetsTab && log.Count > 0)
            {
                DrawRows(scale);
                return;
            }

            if (tab != MyBetsTab && FeedItems() is { Length: > 0 } items)
            {
                DrawFeedRows(items, scale);
                return;
            }

            var pending = tab != MyBetsTab && feed is not null && feed.FeedPending(FeedKey);
            DrawEmpty(tab == MyBetsTab ? L.Strip.MyBetsEmpty : pending ? L.Common.Loading : L.Strip.FloorFeedEmpty,
                scale);
        }
    }

    private string FeedKey => tab == HighRollersTab ? CasinoFeedTabs.High : CasinoFeedTabs.All;

    private CasinoFeedItemDto[]? FeedItems()
    {
        if (feed is null)
        {
            return null;
        }

        feed.EnsureFeed(FeedKey);
        return feed.Feed(FeedKey)?.Items;
    }

    private void DrawFeedRows(CasinoFeedItemDto[] items, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();
        var rowHeight = FeedRowHeight * scale;
        var columnWidth = width * 0.2f;
        var nameWidth = width - columnWidth * 3f - Metrics.Space.Sm * scale;
        var nameHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var gameHeight = Typography.LineHeight(TextStyles.Footnote);
        var accountId = feed?.AccountId ?? string.Empty;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var origin = ImGui.GetCursorScreenPos();
            var min = origin;
            var max = new Vector2(origin.X + width, origin.Y + rowHeight);
            var openable = OwnRound(item, accountId);
            var hovered = openable && UiInteract.HoverWindowOnly(min, max);
            if (hovered)
            {
                Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(skin.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var centerY = origin.Y + rowHeight * 0.5f;
            var won = item.Payout > item.Stake;
            var top = centerY - (nameHeight + gameHeight) * 0.5f;
            var name = Strip.WinsTicker.NameOf(item.Player);
            Typography.Draw(drawList, new Vector2(origin.X, top),
                Typography.FitText(name, nameWidth, TextStyles.FootnoteEmphasized),
                item.Player is null ? skin.BodyInk : skin.TitleInk, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(origin.X, top + nameHeight),
                Typography.FitText(Loc.T(CasinoGameNames.Of(CasinoRecentGames.ClientGameId(item.GameKind))), nameWidth,
                    TextStyles.Footnote), skin.BodyInk, TextStyles.Footnote);
            var left = origin.X + width - columnWidth * 3f;
            DrawCell(drawList, NumberText.Compact(item.Stake), left, columnWidth, centerY, skin.BodyInk);
            DrawCell(drawList, CasinoMultiples.Label(item.MultiplierTenths * 10), left + columnWidth, columnWidth,
                centerY, won ? CasinoColors.Money : skin.BodyInk);
            DrawCell(drawList, NumberText.Compact(item.Payout), left + columnWidth * 2f, columnWidth, centerY,
                won ? CasinoColors.Money : skin.BodyInk);
            drawList.AddLine(new Vector2(origin.X, origin.Y + rowHeight), new Vector2(origin.X + width, origin.Y + rowHeight),
                ImGui.GetColorU32(Palette.WithAlpha(skin.TitleInk, 0.06f)), 1f);
            if (openable && UiInteract.Click(min, max, hovered))
            {
                requestedRound = item.RoundId;
                Close();
            }

            ImGui.Dummy(new Vector2(width, rowHeight));
        }
    }

    private bool OwnRound(CasinoFeedItemDto item, string accountId)
    {
        if (item.RoundId.Length == 0)
        {
            return false;
        }

        if (accountId.Length > 0 && item.Player is not null
            && string.Equals(item.Player.UserId, accountId, StringComparison.Ordinal))
        {
            return true;
        }

        return item.Player is null && log.Contains(item.RoundId);
    }

    private void DrawEmpty(LocString message, float scale)
    {
        var width = ScrollLayout.NativeScrollContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + Metrics.Space.Md * scale),
            Loc.T(message), skin.BodyInk, TextStyles.Subheadline, width);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }

    private void DrawRows(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();
        var rowHeight = RowHeight * scale;
        var columnWidth = width * 0.22f;
        for (var index = 0; index < log.Count; index++)
        {
            var record = log.Newest(index);
            var origin = ImGui.GetCursorScreenPos();
            var min = origin;
            var max = new Vector2(origin.X + width, origin.Y + rowHeight);
            var openable = record.RoundId.Length > 0;
            var hovered = openable && UiInteract.HoverWindowOnly(min, max);
            if (hovered)
            {
                Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(skin.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var centerY = origin.Y + rowHeight * 0.5f;
            var lineHeight = Typography.LineHeight(TextStyles.Footnote);
            var gameWidth = width - columnWidth * 3f;
            var game = Typography.FitText(Loc.T(record.Game), gameWidth - Metrics.Space.Sm * scale,
                TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(origin.X, centerY - lineHeight * 0.5f), game, skin.TitleInk,
                TextStyles.FootnoteEmphasized);
            DrawCell(drawList, NumberText.Compact(record.Stake), origin.X + gameWidth, columnWidth, centerY,
                skin.BodyInk);
            DrawCell(drawList, CasinoMultiples.Label(record.MultipleHundredths), origin.X + gameWidth + columnWidth,
                columnWidth, centerY, record.Won ? CasinoColors.Money : skin.BodyInk);
            DrawCell(drawList, NumberText.Compact(record.Payout), origin.X + gameWidth + columnWidth * 2f,
                columnWidth, centerY, record.Won ? CasinoColors.Money : skin.BodyInk);
            drawList.AddLine(new Vector2(min.X, max.Y), max,
                ImGui.GetColorU32(Palette.WithAlpha(skin.TitleInk, 0.06f)), 1f);
            if (openable && UiInteract.Click(min, max, hovered))
            {
                requestedRound = record.RoundId;
                Close();
            }

            ImGui.Dummy(new Vector2(width, rowHeight));
        }
    }

    private static void DrawCell(ImDrawListPtr drawList, string text, float left, float width, float centerY,
        Vector4 ink)
    {
        var fitted = Typography.FitText(text, width, TextStyles.Footnote);
        var size = Typography.Measure(fitted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left + width - size.X, centerY - size.Y * 0.5f), fitted, ink,
            TextStyles.Footnote);
    }

}
