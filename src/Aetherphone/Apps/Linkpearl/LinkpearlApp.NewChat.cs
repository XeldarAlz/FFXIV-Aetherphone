using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private readonly record struct QuickTab(TabPreset Preset, string Glyph, string ProbeChannel, LocString Hint);

    private const float QuickTileGap = 8f;
    private const float QuickTileInset = 14f;
    private const float QuickTileRadius = 16f;
    private const float QuickNameGap = 10f;
    private const int QuickHintLines = 2;
    private const float QuickAddedBadge = 18f;
    private const float SheetSectionPadTop = 6f;
    private const float SheetSectionPadX = 4f;
    private const float SheetSectionExtra = 12f;
    private const float SheetSectionGap = 14f;
    private const float SheetMinimumFraction = 0.5f;
    private const float SheetMaximumFraction = 0.94f;
    private const string LinkshellProbeKey = "ls1";
    private const string NewChatMarqueePrefix = "linkpearl.newChat";

    private static readonly QuickTab[] QuickTabs =
    {
        new(TabPreset.FreeCompany, GameChatTiles.GlyphFor(ChannelCategory.Community), GameChannels.FreeCompanyKey,
            L.Linkpearl.PresetFreeCompanyHint),
        new(TabPreset.Linkshells, GameChatTiles.GlyphFor(ChannelCategory.Linkshell), LinkshellProbeKey,
            L.Linkpearl.PresetLinkshellsHint),
        new(TabPreset.Party, GameChatTiles.GlyphFor(ChannelCategory.Group), GameChannels.PartyKey,
            L.Linkpearl.PresetPartyHint),
        new(TabPreset.Local, GameChatTiles.GlyphFor(ChannelCategory.Local), GameChannels.SayKey,
            L.Linkpearl.PresetLocalHint),
    };

    private readonly string[][] quickHintLines = new string[QuickTabs.Length][];
    private readonly string[] quickHintSources = new string[QuickTabs.Length];
    private readonly float[] quickHintWidths = new float[QuickTabs.Length];

    private void OpenNewChat() => newChatSheet.Open();

    private SheetSkin NewChatSkin() => SheetSkin.From(ui.Palette, ink);

    private static float SheetSectionHeight(float scale) =>
        Typography.LineHeight(ChatListChrome.SectionStyle) + SheetSectionExtra * scale;

    private static float QuickTileHeight(float scale) =>
        (QuickTileInset * 2f + QuickTileRadius * 2f + QuickNameGap + ChatListChrome.RowLineGap) * scale +
        Typography.LineHeight(ChatListChrome.RowTitleStyle) +
        QuickHintLines * Typography.LineHeight(ChatListChrome.RowMetaStyle);

    private static float NewChatSheetFraction(Rect area)
    {
        var scale = UiScale.Current;
        var rows = QuickTabs.Length / 2;
        var content = SheetSectionHeight(scale) * 2f + rows * QuickTileHeight(scale) +
                      (rows - 1) * QuickTileGap * scale + SheetSectionGap * scale +
                      ChatListChrome.ActionRowHeight * 2f * scale;
        var fraction = (content + SheetSurface.ChromeHeight()) / MathF.Max(1f, area.Height);
        return Math.Clamp(fraction, SheetMinimumFraction, SheetMaximumFraction);
    }

    private void DrawNewChatSheet(Rect content)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cursorY = DrawSheetSectionLabel(drawList, Loc.T(L.Linkpearl.QuickTabs), content.Min.X, content.Min.Y,
            scale);
        var gap = QuickTileGap * scale;
        var tileWidth = (content.Width - gap) * 0.5f;
        var tileHeight = QuickTileHeight(scale);
        var tileRows = QuickTabs.Length / 2;
        for (var index = 0; index < QuickTabs.Length; index++)
        {
            var column = index % 2;
            var rowIndex = index / 2;
            var min = new Vector2(content.Min.X + column * (tileWidth + gap), cursorY + rowIndex * (tileHeight + gap));
            DrawQuickTile(drawList, index, new Rect(min, min + new Vector2(tileWidth, tileHeight)), scale);
        }

        cursorY += tileRows * tileHeight + (tileRows - 1) * gap + SheetSectionGap * scale;
        cursorY = DrawSheetSectionLabel(drawList, Loc.T(L.Linkpearl.OrStartFresh), content.Min.X, cursorY, scale);
        var rowHeight = ChatListChrome.ActionRowHeight * scale;
        var cardMin = new Vector2(content.Min.X, cursorY);
        var cardMax = new Vector2(content.Max.X, cursorY + rowHeight * 2f);
        var rounding = Metrics.Radius.Card * scale;
        Squircle.Fill(drawList, cardMin, cardMax, rounding, ImGui.GetColorU32(ink.FieldFill));
        Squircle.Stroke(drawList, cardMin, cardMax, rounding, ImGui.GetColorU32(ink.ChipStroke),
            Metrics.Stroke.Hairline);
        var rowInset = Metrics.Space.Lg * scale;
        var customRow = new Rect(new Vector2(cardMin.X + rowInset, cardMin.Y),
            new Vector2(cardMax.X - rowInset, cardMin.Y + rowHeight));
        var tellRow = new Rect(new Vector2(customRow.Min.X, customRow.Max.Y), new Vector2(customRow.Max.X, cardMax.Y));
        drawList.PushClipRect(cardMin, cardMax, true);
        var customTapped = chrome.DrawCardActionRow(drawList, customRow, PhoneIcons.AdjustmentsHorizontal, ink.Accent,
            Loc.T(L.Linkpearl.CustomTab), Loc.T(L.Linkpearl.CustomTabHint), NewChatMarqueePrefix);
        FeedCell.Hairline(drawList,
            customRow.Min.X + (ChatListChrome.SettingTileSize + ChatListChrome.RowTextGap) * scale, customRow.Max.X,
            customRow.Max.Y, ui.Hairline);
        var tellTapped = chrome.DrawCardActionRow(drawList, tellRow, PhoneIcons.MessagePlus, ChannelTints.Tell,
            Loc.T(L.Linkpearl.SendTell), Loc.T(L.Linkpearl.SendTellHint), NewChatMarqueePrefix);
        drawList.PopClipRect();
        if (customTapped)
        {
            newChatSheet.Close();
            CreateTab();
            return;
        }

        if (!tellTapped)
        {
            return;
        }

        newChatSheet.Close();
        SelectTab(MessagesTab.People);
        peopleSearchOpen = true;
        peopleSearchFocus = true;
    }

    private float DrawSheetSectionLabel(ImDrawListPtr drawList, string text, float left, float top, float scale)
    {
        Typography.Draw(drawList, new Vector2(left + SheetSectionPadX * scale, top + SheetSectionPadTop * scale),
            Loc.Upper(text), ink.FaintInk, ChatListChrome.SectionStyle);
        return top + SheetSectionHeight(scale);
    }

    private void DrawQuickTile(ImDrawListPtr drawList, int index, Rect tile, float scale)
    {
        var quick = QuickTabs[index];
        var existing = tabs.FirstTabWith(quick.ProbeChannel);
        var added = existing is not null;
        var hovered = UiInteract.Hover(tile.Min, tile.Max);
        var rounding = Metrics.Radius.Card * scale;
        Squircle.Fill(drawList, tile.Min, tile.Max, rounding,
            ImGui.GetColorU32(hovered ? ink.ButtonFill : ink.FieldFill));
        Squircle.Stroke(drawList, tile.Min, tile.Max, rounding,
            ImGui.GetColorU32(hovered ? ink.GlassStroke : ink.ChipStroke), Metrics.Stroke.Hairline);
        var inset = QuickTileInset * scale;
        var tileRadius = QuickTileRadius * scale;
        var glyphCenter = new Vector2(tile.Min.X + inset + tileRadius, tile.Min.Y + inset + tileRadius);
        GameChatTiles.DrawTile(drawList, glyphCenter, tileRadius, TabStore.PresetTint(quick.Preset), quick.Glyph);
        if (added)
        {
            PhoneIcon.Draw(drawList, new Vector2(tile.Max.X - inset - QuickAddedBadge * 0.5f * scale, glyphCenter.Y),
                PhoneIcons.CircleCheckFilled, ink.Accent, QuickAddedBadge * scale);
        }

        var textLeft = tile.Min.X + inset;
        var textWidth = tile.Width - inset * 2f;
        var name = Loc.T(TabStore.PresetLabel(quick.Preset));
        var nameTop = glyphCenter.Y + tileRadius + QuickNameGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, nameTop),
            Typography.FitText(name, textWidth, ChatListChrome.RowTitleStyle), ink.TitleInk,
            ChatListChrome.RowTitleStyle);
        var hint = Loc.T(added ? L.Linkpearl.PresetAdded : quick.Hint);
        var lines = QuickHintLinesFor(index, hint, textWidth);
        var hintTop = nameTop + Typography.LineHeight(ChatListChrome.RowTitleStyle) +
                      ChatListChrome.RowLineGap * scale;
        var lineHeight = Typography.LineHeight(ChatListChrome.RowMetaStyle);
        var hintInk = added ? ink.AccentLink : ink.MutedInk;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, hintTop + lineIndex * lineHeight), lines[lineIndex],
                hintInk, ChatListChrome.RowMetaStyle);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(tile.Min, tile.Max, hovered))
        {
            return;
        }

        newChatSheet.Close();
        if (existing is not null)
        {
            inbox.Sync();
            OpenConversation(ChatInbox.KeyForTab(existing));
            return;
        }

        var created = tabs.AddPreset(quick.Preset);
        inbox.Invalidate();
        inbox.Sync();
        OpenConversation(ChatInbox.KeyForTab(created));
    }

    private string[] QuickHintLinesFor(int index, string hint, float width)
    {
        if (ReferenceEquals(quickHintSources[index], hint) && quickHintWidths[index] == width &&
            quickHintLines[index] is { } cached)
        {
            return cached;
        }

        var wrapped = Typography.WrapText(hint, ChatListChrome.RowMetaStyle, width);
        if (wrapped.Length > QuickHintLines)
        {
            var shown = new string[QuickHintLines];
            Array.Copy(wrapped, shown, QuickHintLines - 1);
            shown[QuickHintLines - 1] = Typography.FitText(
                string.Join(' ', wrapped, QuickHintLines - 1, wrapped.Length - QuickHintLines + 1), width,
                ChatListChrome.RowMetaStyle);
            wrapped = shown;
        }

        quickHintSources[index] = hint;
        quickHintWidths[index] = width;
        quickHintLines[index] = wrapped;
        return wrapped;
    }
}
