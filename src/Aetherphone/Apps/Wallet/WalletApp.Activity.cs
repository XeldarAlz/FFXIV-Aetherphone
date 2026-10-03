using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Wallet;

internal sealed partial class WalletApp
{
    private const float DayHeaderHeight = 30f;
    private const float DayGap = 18f;

    private void DrawActivity(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        if (!wallet.Ready)
        {
            DrawSignedOut(navBar.Body);
        }
        else
        {
            using (AppSurface.Begin(navBar.Body))
            {
                var scale = UiScale.Current;
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var cursorY = DrawDays(drawList, origin, width, scale);
                ReserveTo(origin, width, cursorY + BottomPad * scale);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "wallet.activity.nav", Loc.T(L.Wallet.ActivityTitle),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private float DrawDays(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (text.LineCount == 0)
        {
            var title = Loc.T(L.Wallet.RecentEmptyTitle);
            var body = Loc.T(L.Wallet.RecentEmptyBody);
            var height = WalletArt.PanelHeight(title, body, width, scale);
            WalletArt.Panel(drawList, ui, origin, width, height, FontAwesomeIcon.Receipt, title, body, scale);
            return origin.Y + height;
        }

        var cursorY = origin.Y;
        var start = 0;
        while (start < text.LineCount)
        {
            var day = text.Line(start).Day;
            var end = start + 1;
            while (end < text.LineCount && text.Line(end).Day == day)
            {
                end++;
            }

            if (start > 0)
            {
                cursorY += DayGap * scale;
            }

            var headerHeight = DayHeaderHeight * scale;
            var label = Typography.FitText(text.Line(start).DayLabel, width, TextStyles.FootnoteEmphasized);
            var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(origin.X, cursorY + headerHeight - labelHeight - WalletArt.HeaderGap *
                scale), label, ui.MutedInk, TextStyles.FootnoteEmphasized);
            cursorY += headerHeight;
            cursorY = DrawLinesCard(drawList, new Vector2(origin.X, cursorY), width, start, end - start, 0, false,
                true, scale);
            start = end;
        }

        return cursorY;
    }
}
