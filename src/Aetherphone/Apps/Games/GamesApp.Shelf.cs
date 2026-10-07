using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string ShelfNavId = "games.shelf.nav";
    private const string CategoryScopeId = "games.shelf";
    private const string CategoryTitleMarquee = "games.shelf.title.";
    private const float CategoryRowHeight = 76f;
    private const float CategoryRowInset = 14f;
    private const float CategoryRowPadY = 8f;
    private const float CategoryIconSize = 60f;
    private const float CategoryTextGap = 12f;
    private const float CategoryLineGap = 2f;
    private const float CategoryPlayMinWidth = 64f;
    private const int CategoryHookLines = 2;

    private ClampedLines[] categoryHooks = Array.Empty<ClampedLines>();

    internal static ReadOnlySpan<int> ShelfEntries(GamesLibrary library, GamesShelf shelf) => shelf switch
    {
        GamesShelf.New => library.Latest,
        GamesShelf.All => library.Ordered,
        _ => library.Genre((GameGenre)shelf),
    };

    internal static float CategoryRowHeightFor(float titleHeight, float lineHeight, float scale) =>
        MathF.Max(CategoryRowHeight * scale,
            titleHeight + CategoryLineGap * scale + lineHeight * CategoryHookLines + CategoryRowPadY * 2f * scale);

    private static string ShelfTitle(GamesShelf shelf) => shelf switch
    {
        GamesShelf.New => Loc.T(L.GamesHub.JustAdded),
        GamesShelf.All => Loc.T(L.Games.LibraryHeading),
        _ => Loc.T(GameGenres.Label((GameGenre)shelf)),
    };

    private string CategoryBackTitle() => tab == GamesTab.Home ? Loc.T(L.GamesHub.TabHome) : TabTitle(tab);

    private void DrawShelfPage(in PhoneContext context, GamesShelf shelf)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        using (ImRaii.PushId(CategoryScopeId))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var entries = ShelfEntries(library, shelf);
            Typography.Draw(drawList, origin, CountLabel(entries.Length), ui.MutedInk, TextStyles.Footnote);
            var top = origin.Y + Typography.LineHeight(TextStyles.Footnote) + Metrics.Space.Sm * scale;
            var bottom = DrawCategoryCard(drawList, new Vector2(origin.X, top), width, entries, scale);
            FinishPage(origin, width, bottom, scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, ShelfNavId, ShelfTitle(shelf), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, CategoryBackTitle(), back);
    }

    private float DrawCategoryCard(ImDrawListPtr drawList, Vector2 origin, float width, ReadOnlySpan<int> entries,
        float scale)
    {
        if (entries.Length == 0)
        {
            return origin.Y;
        }

        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var rowHeight = CategoryRowHeightFor(titleHeight, lineHeight, scale);
        var cardMax = new Vector2(origin.X + width, origin.Y + entries.Length * rowHeight);
        ui.Card(drawList, origin, cardMax, HubMetrics.CardRadius * scale);
        var inset = CategoryRowInset * scale;
        var dividerLeft = origin.X + inset + (CategoryIconSize + CategoryTextGap) * scale;
        var dividerColor = ImGui.GetColorU32(ui.Hairline);
        var rows = VisibleRows.Between(entries.Length, origin.Y, rowHeight, drawList.GetClipRectMin().Y,
            drawList.GetClipRectMax().Y);
        var tapped = -1;
        var tappedIcon = default(Rect);
        for (var rowIndex = rows.First; rowIndex < rows.End; rowIndex++)
        {
            var rowTop = origin.Y + rowIndex * rowHeight;
            if (rowIndex > 0)
            {
                drawList.AddLine(new Vector2(dividerLeft, rowTop), new Vector2(cardMax.X - inset, rowTop),
                    dividerColor, Metrics.Stroke.Hairline);
            }

            var row = new Rect(new Vector2(origin.X + inset, rowTop),
                new Vector2(cardMax.X - inset, rowTop + rowHeight));
            if (DrawCategoryRow(drawList, row, entries[rowIndex], titleHeight, lineHeight, scale, out var icon))
            {
                tapped = entries[rowIndex];
                tappedIcon = icon;
            }
        }

        if (tapped >= 0)
        {
            Activate(tapped, tappedIcon);
        }

        return cardMax.Y;
    }

    private bool DrawCategoryRow(ImDrawListPtr drawList, Rect row, int entryIndex, float titleHeight, float lineHeight,
        float scale, out Rect icon)
    {
        var playLabel = Loc.T(L.Games.Play);
        var playHeight = Button.SmallHeight * scale;
        var playWidth = MathF.Max(Button.WidthFor(playLabel, ButtonSize.Small), CategoryPlayMinWidth * scale);
        var play = new Rect(new Vector2(row.Max.X - playWidth, row.Center.Y - playHeight * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + playHeight * 0.5f));
        var overPlay = UiInteract.Hover(play.Min, play.Max);
        var hovered = UiInteract.Hover(row.Min, row.Max) && !overPlay;
        var tileId = library.TileIds[entryIndex];
        var hover = HoverFx.Amount(tileId, hovered);
        var press = PressFx.Scale(tileId, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), Motion.PressScaleCard);
        if (hover > 0f)
        {
            var wash = ui.HoverTint;
            var bleed = CategoryRowInset * 0.5f * scale;
            var trim = Metrics.Space.Xxs * scale;
            Squircle.Fill(drawList, new Vector2(row.Min.X - bleed, row.Min.Y + trim),
                new Vector2(row.Max.X + bleed, row.Max.Y - trim), Metrics.Radius.Md * scale,
                ImGui.GetColorU32(wash with { W = wash.W * hover }));
        }

        var side = CategoryIconSize * scale;
        var center = new Vector2(row.Min.X + side * 0.5f, row.Center.Y);
        icon = new Rect(new Vector2(row.Min.X, center.Y - side * 0.5f),
            new Vector2(row.Min.X + side, center.Y + side * 0.5f));
        var half = side * 0.5f * press;
        GameIconArt.Draw(drawList, library.IconIds[entryIndex], library.Accent(entryIndex),
            new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half), null, true);
        var textLeft = row.Min.X + side + CategoryTextGap * scale;
        var textWidth = MathF.Max(1f, play.Min.X - CategoryTextGap * scale - textLeft);
        ref readonly var hook = ref CategoryHook(entryIndex, textWidth);
        var lines = hook.Count;
        var blockHeight = titleHeight + (lines > 0 ? CategoryLineGap * scale + lines * lineHeight : 0f);
        var titleTop = row.Center.Y - blockHeight * 0.5f;
        Marquee.DrawLeft(drawList, new MarqueeId(CategoryTitleMarquee, library.Entries[entryIndex].Id),
            library.Title(entryIndex), textLeft, titleTop, textWidth, TextStyles.Headline, ui.TitleInk, hovered);
        var lineTop = titleTop + titleHeight + CategoryLineGap * scale;
        if (lines > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, lineTop), hook.First, ui.MutedInk, TextStyles.Footnote);
        }

        if (lines > 1)
        {
            Typography.Draw(drawList, new Vector2(textLeft, lineTop + lineHeight), hook.Second, ui.MutedInk,
                TextStyles.Footnote);
        }

        var played = Button.Draw(drawList, play, playLabel, ui.Ink, ButtonStyle.Gray,
            id: library.PlayIds[entryIndex]);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return played || UiInteract.Click(row.Min, row.Max, hovered);
    }

    private ref readonly ClampedLines CategoryHook(int entryIndex, float width)
    {
        if (categoryHooks.Length != library.Entries.Length)
        {
            categoryHooks = new ClampedLines[library.Entries.Length];
        }

        var hook = library.Hook(entryIndex);
        ref var lines = ref categoryHooks[entryIndex];
        lines.Update(hook.Length > 0 ? hook : library.Meta(entryIndex), width, TextStyles.Footnote);
        return ref lines;
    }
}
