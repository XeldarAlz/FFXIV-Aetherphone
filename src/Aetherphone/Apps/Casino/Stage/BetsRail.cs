using Aetherphone.Core;
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
    private const int MyBetsTab = 0;
    private const int TabCount = 3;

    private readonly SheetSurface sheet = new("casino.bets");
    private readonly Action<Rect> drawSheetBody;
    private readonly string[] tabLabels = new string[TabCount];

    private CasinoBetsLog log = null!;
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

    public static bool DrawHandle(ImDrawListPtr drawList, Rect deck, float scale)
    {
        var size = new Vector2(HandleWidth, HandleHeight) * scale;
        var center = new Vector2(deck.Center.X, deck.Min.Y);
        var min = center - size * 0.5f;
        var max = center + size * 0.5f;
        var hovered = UiInteract.Hover(min, max);
        Material.LiquidGlass(drawList, min, max, size.Y * 0.5f, scale, GlassTone.Dark, hovered ? 0.4f : 0f);
        var label = Typography.FitText(Loc.T(L.Strip.Bets), size.X - size.Y, TextStyles.Caption1);
        Typography.DrawCentered(drawList, center, label, hovered ? CasinoColors.InkTitle : CasinoColors.InkBody,
            TextStyles.Caption1);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public void Draw(Rect screen, AppSkin ui, CasinoBetsLog bets)
    {
        skin = ui;
        log = bets;
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

            DrawEmpty(tab == MyBetsTab ? L.Strip.MyBetsEmpty : L.Strip.FloorFeedEmpty, scale);
        }
    }

    private void DrawEmpty(LocString message, float scale)
    {
        var width = ScrollLayout.NativeScrollContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + Metrics.Space.Md * scale),
            Loc.T(message), skin.MutedInk, TextStyles.Footnote, width);
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
                columnWidth, centerY, record.Won ? CasinoColors.Money : skin.MutedInk);
            DrawCell(drawList, NumberText.Compact(record.Payout), origin.X + gameWidth + columnWidth * 2f,
                columnWidth, centerY, record.Won ? CasinoColors.Money : skin.MutedInk);
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
