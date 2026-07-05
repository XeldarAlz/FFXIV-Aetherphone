using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Inventory;

internal sealed class InventoryApp : IPhoneApp
{
    private const float SearchHeight = 46f;
    private const float RowHeight = 46f;
    private const float HubRowHeight = 52f;
    private const float CachedRowHeight = 60f;
    private const float RebuildIntervalSeconds = 1.0f;
    public string Id => "inventory";
    public string DisplayName => Loc.T(L.Apps.Inventory);
    public string Glyph => "I";
    public int BadgeCount => 0;
    private readonly InventoryCaptureService capture;
    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly InventorySearch search;
    private readonly List<InventoryResultGroup> groups = new();
    private readonly List<InventoryResultGroup> localScratch = new();
    private readonly List<InventoryResultGroup> cachedScratch = new();
    private readonly ViewRouter<InventoryView> router;
    private readonly RouterDraw<InventoryView> drawView;
    private readonly Action back;
    private string query = string.Empty;
    private string lastBuiltQuery = " ";
    private float sinceRebuild;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public InventoryApp(InventoryCaptureService capture, GameData gameData, ITextureProvider textures)
    {
        this.capture = capture;
        this.gameData = gameData;
        this.textures = textures;
        search = new InventorySearch(gameData);
        router = new ViewRouter<InventoryView>(InventoryView.Root(), Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        query = string.Empty;
        lastBuiltQuery = " ";
        sinceRebuild = 0f;
        groups.Clear();
        router.Reset();
    }

    public void OnClosed()
    {
        query = string.Empty;
        groups.Clear();
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        if (gameData.LocalPlayer is null)
        {
            router.Reset();
            AppHeader.Draw(context, DisplayName);
            Typography.DrawCentered(context.Content.Center, Loc.T(L.Inventory.LogInToView), context.Theme.TextMuted);
            return;
        }

        MaybeRebuild();
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(InventoryView view, Rect area, int depth)
    {
        if (view.Kind == InventoryViewKind.Source)
        {
            DrawSource(area, view.Source, view.Title);
            return;
        }

        DrawRoot(area);
    }

    private void DrawRoot(Rect area)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);
        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var searchTop = area.Min.Y + AppHeader.Height * scale;
        var searchBar = new Rect(new Vector2(area.Min.X + pad, searchTop),
            new Vector2(area.Max.X - pad, searchTop + SearchHeight * scale));
        SearchField.Draw(searchBar, "##inventorySearch", Loc.T(L.Inventory.Search), ref query, frameTheme);
        var body = new Rect(new Vector2(area.Min.X, searchBar.Max.Y), area.Max);
        using (AppSurface.Begin(body))
        {
            if (query.Trim().Length == 0)
            {
                DrawStorageHub(frameTheme);
            }
            else
            {
                DrawResults(frameTheme);
            }
        }
    }

    private void DrawSource(Rect area, InventorySourceKind kind, string title)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, title, back);
        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var group = FindGroup(kind, title);
        using (AppSurface.Begin(body))
        {
            if (group is null || group.Rows.Count == 0)
            {
                DrawHint(frameTheme, Loc.T(L.Inventory.NoMatches));
                return;
            }

            if (group.IsCached)
            {
                DrawCachedTimestamp(frameTheme, group.CapturedUtc);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 6f * scale));
            }

            var card = GroupCard.Begin(frameTheme, group.Rows.Count, RowHeight);
            for (var rowIndex = 0; rowIndex < group.Rows.Count; rowIndex++)
            {
                DrawItemRow(card.NextRow(), group.Rows[rowIndex], frameTheme);
            }

            card.End();
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void MaybeRebuild()
    {
        var trimmed = query.Trim();
        sinceRebuild += ImGui.GetIO().DeltaTime;
        if (!string.Equals(trimmed, lastBuiltQuery, StringComparison.Ordinal) || sinceRebuild >= RebuildIntervalSeconds)
        {
            search.Build(capture, trimmed, groups);
            lastBuiltQuery = trimmed;
            sinceRebuild = 0f;
        }
    }

    private void DrawStorageHub(PhoneTheme theme)
    {
        DrawSummaryCard(theme);
        localScratch.Clear();
        cachedScratch.Clear();
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            if (group.IsCached)
            {
                cachedScratch.Add(group);
            }
            else
            {
                localScratch.Add(group);
            }
        }

        if (localScratch.Count > 0)
        {
            SettingsSection.Header(Loc.T(L.Inventory.OnHand), theme);
            var card = GroupCard.Begin(theme, localScratch.Count, HubRowHeight);
            for (var index = 0; index < localScratch.Count; index++)
            {
                var group = localScratch[index];
                if (DrawStorageRow(card.NextRow(), IconFor(group.Kind), group.Title, string.Empty, group.Rows.Count,
                        true, theme))
                {
                    Open(group.Kind, group.Title);
                }
            }

            card.End();
        }

        var placeholders = (search.HasRetainerCache ? 0 : 1) + (search.HasFreeCompanyCache ? 0 : 1);
        var cachedRows = cachedScratch.Count + placeholders;
        if (cachedRows > 0)
        {
            SettingsSection.Header(Loc.T(L.Inventory.CachedSources), theme);
            var card = GroupCard.Begin(theme, cachedRows, CachedRowHeight);
            for (var index = 0; index < cachedScratch.Count; index++)
            {
                var group = cachedScratch[index];
                var subtitle = Loc.T(L.Inventory.Updated, RelativeTime.Ago(group.CapturedUtc));
                if (DrawStorageRow(card.NextRow(), IconFor(group.Kind), group.Title, subtitle, group.Rows.Count, true,
                        theme))
                {
                    Open(group.Kind, group.Title);
                }
            }

            if (!search.HasRetainerCache)
            {
                DrawStorageRow(card.NextRow(), IconFor(InventorySourceKind.Retainer), Loc.T(L.Inventory.SourceRetainer),
                    Loc.T(L.Inventory.RetainerEmpty), -1, false, theme);
            }

            if (!search.HasFreeCompanyCache)
            {
                DrawStorageRow(card.NextRow(), IconFor(InventorySourceKind.FreeCompany),
                    Loc.T(L.Inventory.SourceFreeCompany), Loc.T(L.Inventory.FreeCompanyEmpty), -1, false, theme);
            }

            card.End();
        }

        DrawFooterHint(theme);
    }

    private bool DrawStorageRow(Rect row, FontAwesomeIcon icon, string title, string subtitle, int count,
        bool interactive, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = false;
        if (interactive)
        {
            var hitMin = new Vector2(row.Min.X - 8f * scale, row.Min.Y + 2f * scale);
            var hitMax = new Vector2(row.Max.X + 8f * scale, row.Max.Y - 2f * scale);
            hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
            if (hovered)
            {
                Squircle.Fill(drawList, hitMin, hitMax, 10f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.14f)));
            }
        }

        var iconBox = 30f * scale;
        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconBox * 0.5f);
        var iconMax = iconMin + new Vector2(iconBox, iconBox);
        Squircle.Fill(drawList, iconMin, iconMax, 9f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, interactive ? 0.18f : 0.10f)));
        var iconColor = interactive ? theme.Accent : Palette.WithAlpha(theme.TextMuted, 0.9f);
        ProgressRing.CenterIcon(new Vector2((iconMin.X + iconMax.X) * 0.5f, row.Center.Y), icon, iconColor,
            iconBox * 0.46f);
        var rightEdge = row.Max.X;
        if (interactive)
        {
            DrawChevronRight(new Vector2(rightEdge, row.Center.Y), 5.5f * scale, 2f * scale,
                hovered ? theme.Accent : theme.TextMuted);
            rightEdge -= 14f * scale;
        }

        var textRight = rightEdge;
        if (count >= 0)
        {
            var countText = FormatCount(count);
            var countSize = Typography.Measure(countText, TextStyles.Subheadline);
            var countX = rightEdge - countSize.X;
            Typography.Draw(new Vector2(countX, row.Center.Y - countSize.Y * 0.5f), countText, theme.TextMuted,
                TextStyles.Subheadline);
            textRight = countX - 10f * scale;
        }

        var textLeft = iconMax.X + 12f * scale;
        if (subtitle.Length > 0)
        {
            var name = Fit(title, textRight - textLeft, TextStyles.Body);
            Typography.Draw(new Vector2(textLeft, row.Min.Y + 10f * scale), name, theme.TextStrong, TextStyles.Body);
            var sub = Fit(subtitle, textRight - textLeft, TextStyles.Caption1);
            Typography.Draw(new Vector2(textLeft, row.Min.Y + 31f * scale), sub, theme.TextMuted, TextStyles.Caption1);
        }
        else
        {
            var name = Fit(title, textRight - textLeft, TextStyles.Body);
            var nameSize = Typography.Measure(name, TextStyles.Body);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name, theme.TextStrong,
                TextStyles.Body);
        }

        if (!interactive || !hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawFooterHint(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 18f * scale));
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var text = Fit(Loc.T(L.Inventory.SearchHint), width - 24f * scale, TextStyles.Footnote);
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 8f * scale), text, theme.TextMuted,
            TextStyles.Footnote);
        ImGui.Dummy(new Vector2(width, 28f * scale));
    }

    private void DrawSummaryCard(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = 92f * scale;
        var cardMin = origin;
        var cardMax = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 20f * scale;
        Elevation.Card(drawList, cardMin, cardMax, rounding, scale, 0.7f);
        Squircle.Fill(drawList, cardMin, cardMax, rounding, ImGui.GetColorU32(theme.GroupedCard));
        Material.TopGlow(drawList, cardMin, cardMax, rounding, theme.Accent, 0.82f, 0.15f);
        Material.EdgeSquircle(drawList, cardMin, cardMax, rounding, scale);
        var columnWidth = width * 0.5f;
        DrawSummaryColumn(new Vector2(cardMin.X + columnWidth * 0.5f, cardMin.Y), height, scale, theme,
            FormatCount(search.LocalItemCount), Loc.T(L.Inventory.TotalItems));
        DrawSummaryColumn(new Vector2(cardMin.X + columnWidth * 1.5f, cardMin.Y), height, scale, theme, FormatGil(),
            Loc.T(L.Inventory.Gil));
        drawList.AddLine(new Vector2(cardMin.X + columnWidth, cardMin.Y + 22f * scale),
            new Vector2(cardMin.X + columnWidth, cardMax.Y - 22f * scale), ImGui.GetColorU32(theme.Separator), 1f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
    }

    private static void DrawSummaryColumn(Vector2 columnTop, float height, float scale, PhoneTheme theme, string value,
        string label)
    {
        Typography.DrawCentered(new Vector2(columnTop.X, columnTop.Y + height * 0.40f), value, theme.TextStrong,
            TextStyles.Title1);
        Typography.DrawCentered(new Vector2(columnTop.X, columnTop.Y + height * 0.74f), label.ToUpperInvariant(),
            theme.TextMuted, TextStyles.Caption1);
    }

    private void DrawResults(PhoneTheme theme)
    {
        if (groups.Count == 0)
        {
            DrawHint(theme, Loc.T(L.Inventory.NoMatches));
            return;
        }

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            DrawGroup(groups[groupIndex], theme);
        }
    }

    private void DrawGroup(InventoryResultGroup group, PhoneTheme theme)
    {
        SettingsSection.Header(group.Title, theme);
        if (group.IsCached)
        {
            DrawCachedTimestamp(theme, group.CapturedUtc);
        }

        var rows = group.Rows;
        var card = GroupCard.Begin(theme, rows.Count, RowHeight);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            DrawItemRow(card.NextRow(), rows[rowIndex], theme);
        }

        card.End();
    }

    private static void DrawCachedTimestamp(PhoneTheme theme, DateTime capturedUtc)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
        var label = Loc.T(L.Inventory.Updated, RelativeTime.Ago(capturedUtc));
        Typography.Draw(ImGui.GetCursorScreenPos(), label, theme.TextMuted, TextStyles.Caption1);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private void DrawItemRow(Rect row, InventoryResultRow item, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var iconSize = 30f * scale;
        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        if (item.IconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrEmpty();
            drawList.AddImageRounded(texture.Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                6f * scale);
        }

        var quantityText = "x" + FormatCount(item.Quantity);
        var quantitySize = Typography.Measure(quantityText, TextStyles.Callout);
        var quantityX = row.Max.X - quantitySize.X;
        Typography.Draw(new Vector2(quantityX, row.Center.Y - quantitySize.Y * 0.5f), quantityText, theme.Accent,
            TextStyles.Callout);
        var textLeft = iconMax.X + 12f * scale;
        var textRight = quantityX - 10f * scale;
        if (item.HasHighQuality)
        {
            textRight -= 22f * scale;
            DrawHqBadge(new Vector2(textRight + 6f * scale, row.Center.Y), scale, theme);
        }

        var name = Fit(item.Name, textRight - textLeft, TextStyles.Body);
        var nameSize = Typography.Measure(name, TextStyles.Body);
        Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name, theme.TextStrong,
            TextStyles.Body);
    }

    private static void DrawHqBadge(Vector2 center, float scale, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var halfWidth = 9f * scale;
        var halfHeight = 7f * scale;
        var min = new Vector2(center.X - halfWidth, center.Y - halfHeight);
        var max = new Vector2(center.X + halfWidth, center.Y + halfHeight);
        Squircle.Fill(drawList, min, max, 3f * scale, ImGui.GetColorU32(theme.Accent));
        Typography.DrawCentered(center, "HQ", new Vector4(0.99f, 0.99f, 1f, 1f), TextStyles.Caption2);
    }

    private void DrawHint(PhoneTheme theme, string message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 20f * scale));
        var width = ImGui.GetContentRegionAvail().X;
        var wrapped = Fit(message, width, TextStyles.Subheadline);
        var origin = ImGui.GetCursorScreenPos();
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 12f * scale), wrapped, theme.TextMuted,
            TextStyles.Subheadline);
    }

    private void Open(InventorySourceKind kind, string title) => router.Push(InventoryView.ForSource(kind, title));

    private InventoryResultGroup? FindGroup(InventorySourceKind kind, string title)
    {
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            if (group.Kind == kind && string.Equals(group.Title, title, StringComparison.Ordinal))
            {
                return group;
            }
        }

        return null;
    }

    private static FontAwesomeIcon IconFor(InventorySourceKind kind) =>
        kind switch
        {
            InventorySourceKind.Inventory => FontAwesomeIcon.Briefcase,
            InventorySourceKind.Armoury => FontAwesomeIcon.ShieldAlt,
            InventorySourceKind.Crystals => FontAwesomeIcon.Bolt,
            InventorySourceKind.Saddlebag => FontAwesomeIcon.Paw,
            InventorySourceKind.Equipped => FontAwesomeIcon.Tshirt,
            InventorySourceKind.Retainer => FontAwesomeIcon.IdCard,
            _ => FontAwesomeIcon.Home,
        };

    private static void DrawChevronRight(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var drawList = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }

    private static string FormatCount(int value) => value.ToString("N0", Loc.Culture);
    private static string FormatGil() => InventoryGil.Read().ToString("N0", Loc.Culture);

    private static string Fit(string text, float maxWidth, in TextStyle style)
    {
        if (text.Length == 0 || maxWidth <= 0f)
        {
            return text;
        }

        if (Typography.Measure(text, style).X <= maxWidth)
        {
            return text;
        }

        var low = 1;
        var high = text.Length;
        var best = "…";
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var candidate = text.Substring(0, mid) + "…";
            if (Typography.Measure(candidate, style).X <= maxWidth)
            {
                best = candidate;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }

    public void Dispose()
    {
    }
}
