using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
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
    private const float SearchHeight = 50f;
    private const float ItemRowHeight = 56f;
    private const float StorageRowHeight = 68f;
    private const float HeroHeight = 120f;
    private const float RebuildIntervalSeconds = 1.0f;
    private static readonly Vector4 GoldTint = new(0.95f, 0.74f, 0.30f, 1f);
    private static readonly Vector4 White = new(0.99f, 0.99f, 1f, 1f);
    public string Id => "inventory";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Inventory);
    public string Glyph => "I";
    public int BadgeCount => 0;
    private readonly InventoryCaptureService capture;
    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly InventorySearch search;
    private readonly AppSkin ui = new(AppPalettes.Inventory);
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
        router = new ViewRouter<InventoryView>(InventoryView.Root());
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
        ui.Theme = context.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        if (gameData.LocalPlayer is null)
        {
            router.Reset();
            ui.Body(context.Content);
            DrawNavBar(context.Content, DisplayName, null);
            Typography.DrawCentered(context.Content.Center, Loc.T(L.Inventory.LogInToView), ui.MutedInk,
                TextStyles.Subheadline);
            return;
        }

        MaybeRebuild();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(InventoryView view, Rect area, int depth)
    {
        ui.Body(area);
        if (view.Kind == InventoryViewKind.Source)
        {
            DrawSource(area, view.Source, view.Title);
            return;
        }

        DrawRoot(area);
    }

    private void DrawNavBar(Rect area, string title, Action? onBack)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var fitted = Typography.FitText(title, area.Width - 96f * scale, TextStyles.Title3);
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), fitted, ui.TitleInk, TextStyles.Title3);
        if (onBack is null)
        {
            return;
        }

        var hitMin = new Vector2(area.Min.X, area.Min.Y);
        var hitMax = new Vector2(area.Min.X + 46f * scale, area.Min.Y + AppHeader.Height * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var center = new Vector2(area.Min.X + 17f * scale, rowCenterY);
        if (BackButton.Draw("inventory.back", center, 15f * scale, ui.TitleInk, hovered, scale))
        {
            onBack();
        }
    }

    private void DrawRoot(Rect area)
    {
        DrawNavBar(area, DisplayName, null);
        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var searchTop = area.Min.Y + AppHeader.Height * scale;
        var searchBar = new Rect(new Vector2(area.Min.X + pad, searchTop),
            new Vector2(area.Max.X - pad, searchTop + SearchHeight * scale));
        UiAnchors.Report("inventory.search", searchBar);
        SearchField.Draw(searchBar, "##inventorySearch", Loc.T(L.Inventory.Search), ref query, ui.Palette);
        var body = new Rect(new Vector2(area.Min.X, searchBar.Max.Y), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            if (query.Trim().Length == 0)
            {
                DrawStorageHub();
            }
            else
            {
                DrawResults();
            }
        }
    }

    private void DrawSource(Rect area, InventorySourceKind kind, string title)
    {
        DrawNavBar(area, title, back);
        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var group = FindGroup(kind, title);
        using (AppSurface.Begin(body))
        {
            if (group is null || group.Rows.Count == 0)
            {
                DrawHint(Loc.T(L.Inventory.NoMatches));
                return;
            }

            DrawSourceSummary(group);
            DrawItemPanel(group.Rows);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
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

    private void DrawStorageHub()
    {
        DrawSummaryCard();
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
            SectionLabel(Loc.T(L.Inventory.OnHand));
            DrawLocalPanel();
        }

        DrawCachedPanel();
        DrawFooterHint();
    }

    private void DrawLocalPanel()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = StorageRowHeight * scale;
        var panelMax = new Vector2(origin.X + width, origin.Y + localScratch.Count * rowHeight);
        ui.Card(drawList, origin, panelMax, Metrics.Radius.Card * scale, elevated: true);
        var separatorLeft = StorageSeparatorLeft(origin, scale);
        for (var index = 0; index < localScratch.Count; index++)
        {
            var group = localScratch[index];
            var row = PanelRow(drawList, origin, width, rowHeight, index, scale, separatorLeft,
                Palette.WithAlpha(ui.Accent, 0.10f), true, out var hovered, out var hitMin, out var hitMax);
            if (DrawStorageRow(row, group.Kind, group.Title, string.Empty, group.Rows.Count, true, hovered, hitMin,
                    hitMax))
            {
                Open(group.Kind, group.Title);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, localScratch.Count * rowHeight));
    }

    private void DrawCachedPanel()
    {
        var showRetainer = !search.HasRetainerCache;
        var showFreeCompany = !search.HasFreeCompanyCache;
        var total = cachedScratch.Count + (showRetainer ? 1 : 0) + (showFreeCompany ? 1 : 0);
        if (total == 0)
        {
            return;
        }

        SectionLabel(Loc.T(L.Inventory.CachedSources));
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = StorageRowHeight * scale;
        var panelMax = new Vector2(origin.X + width, origin.Y + total * rowHeight);
        ui.Card(drawList, origin, panelMax, Metrics.Radius.Card * scale, elevated: true);
        var separatorLeft = StorageSeparatorLeft(origin, scale);
        var accentHover = Palette.WithAlpha(ui.Accent, 0.10f);
        var rowIndex = 0;
        for (var index = 0; index < cachedScratch.Count; index++)
        {
            var group = cachedScratch[index];
            var subtitle = Loc.T(L.Inventory.Updated, TimeText.Ago(group.CapturedUtc));
            var row = PanelRow(drawList, origin, width, rowHeight, rowIndex++, scale, separatorLeft, accentHover, true,
                out var hovered, out var hitMin, out var hitMax);
            if (DrawStorageRow(row, group.Kind, group.Title, subtitle, group.Rows.Count, true, hovered, hitMin,
                    hitMax))
            {
                Open(group.Kind, group.Title);
            }
        }

        if (showRetainer)
        {
            var row = PanelRow(drawList, origin, width, rowHeight, rowIndex++, scale, separatorLeft, accentHover, false,
                out _, out var hitMin, out var hitMax);
            DrawStorageRow(row, InventorySourceKind.Retainer, Loc.T(L.Inventory.SourceRetainer),
                Loc.T(L.Inventory.RetainerEmpty), -1, false, false, hitMin, hitMax);
        }

        if (showFreeCompany)
        {
            var row = PanelRow(drawList, origin, width, rowHeight, rowIndex, scale, separatorLeft, accentHover, false,
                out _, out var hitMin, out var hitMax);
            DrawStorageRow(row, InventorySourceKind.FreeCompany, Loc.T(L.Inventory.SourceFreeCompany),
                Loc.T(L.Inventory.FreeCompanyEmpty), -1, false, false, hitMin, hitMax);
        }

        UiAnchors.Report("inventory.sources", new Rect(origin, panelMax));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, total * rowHeight));
    }

    private static float StorageSeparatorLeft(Vector2 origin, float scale) =>
        origin.X + 16f * scale + 44f * scale + 14f * scale;

    private Rect PanelRow(ImDrawListPtr drawList, Vector2 origin, float width, float rowHeight, int index, float scale,
        float separatorLeft, Vector4 hoverTint, bool interactive, out bool hovered, out Vector2 hitMin,
        out Vector2 hitMax)
    {
        var pad = 16f * scale;
        var rowTop = origin.Y + index * rowHeight;
        var rowMin = new Vector2(origin.X, rowTop);
        var rowMax = new Vector2(origin.X + width, rowTop + rowHeight);
        hitMin = rowMin;
        hitMax = rowMax;
        hovered = interactive && UiInteract.Hover(rowMin, rowMax);
        if (hovered)
        {
            var pressed = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var fill = pressed ? Palette.WithAlpha(hoverTint, MathF.Min(1f, hoverTint.W * 1.8f)) : hoverTint;
            Squircle.Fill(drawList, new Vector2(rowMin.X + 4f * scale, rowMin.Y + 3f * scale),
                new Vector2(rowMax.X - 4f * scale, rowMax.Y - 3f * scale), 12f * scale, ImGui.GetColorU32(fill));
        }
        else if (index > 0)
        {
            drawList.AddLine(new Vector2(separatorLeft, rowTop), new Vector2(rowMax.X - pad, rowTop),
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), 1f);
        }

        return new Rect(new Vector2(rowMin.X + pad, rowMin.Y), new Vector2(rowMax.X - pad, rowMax.Y));
    }

    private bool DrawStorageRow(Rect row, InventorySourceKind kind, string title, string subtitle, int count,
        bool navigable, bool hovered, Vector2 hitMin, Vector2 hitMax)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var tileSize = 44f * scale;
        var tileCenter = new Vector2(row.Min.X + tileSize * 0.5f, row.Center.Y);
        DrawSourceTile(drawList, tileCenter, tileSize, AccentFor(kind), IconFor(kind), scale, navigable || count >= 0);
        var rightEdge = row.Max.X;
        if (navigable)
        {
            DrawChevronRight(new Vector2(rightEdge, row.Center.Y), 6f * scale, 2.2f * scale,
                hovered ? ui.Accent : ui.MutedInk);
            rightEdge -= 16f * scale;
        }

        var textRight = rightEdge;
        if (count >= 0)
        {
            textRight = DrawCountPill(drawList, new Vector2(rightEdge, row.Center.Y), count, AccentFor(kind)) -
                12f * scale;
        }

        var textLeft = tileCenter.X + tileSize * 0.5f + 14f * scale;
        if (subtitle.Length > 0)
        {
            var name = Fit(title, textRight - textLeft, TextStyles.Headline);
            Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 15f * scale), name, ui.TitleInk,
                TextStyles.Headline);
            var sub = Fit(subtitle, textRight - textLeft, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 38f * scale), sub, ui.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            var name = Fit(title, textRight - textLeft, TextStyles.Headline);
            var nameSize = Typography.Measure(name, TextStyles.Headline);
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name, ui.TitleInk,
                TextStyles.Headline);
        }

        if (!navigable)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(hitMin, hitMax, hovered);
    }

    private void DrawFooterHint()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 18f * scale));
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var text = Loc.T(L.Inventory.SearchHint);
        Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 4f * scale), text, ui.MutedInk,
            TextStyles.Footnote, width - 40f * scale);
        ImGui.Dummy(new Vector2(width, 40f * scale));
    }

    private void DrawSummaryCard()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = HeroHeight * scale;
        var cardMin = origin;
        var cardMax = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("inventory.summary", new Rect(cardMin, cardMax));
        var rounding = 22f * scale;
        ui.Card(drawList, cardMin, cardMax, rounding, elevated: true);
        Material.TopGlow(drawList, cardMin, cardMax, rounding, ui.Accent, 0.78f, 0.10f);
        var columnWidth = width * 0.5f;
        var leftCenterX = cardMin.X + columnWidth * 0.5f;
        var rightCenterX = cardMin.X + columnWidth * 1.5f;
        DrawHeroStat(drawList, new Vector2(leftCenterX, cardMin.Y), height, scale, GoldTint, FontAwesomeIcon.Coins,
            FormatGil(), Loc.T(L.Inventory.Gil), columnWidth);
        DrawHeroStat(drawList, new Vector2(rightCenterX, cardMin.Y), height, scale, ui.Accent,
            FontAwesomeIcon.Briefcase, FormatCount(search.LocalItemCount), Loc.T(L.Inventory.TotalItems), columnWidth);
        drawList.AddLine(new Vector2(cardMin.X + columnWidth, cardMin.Y + 26f * scale),
            new Vector2(cardMin.X + columnWidth, cardMax.Y - 22f * scale),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.08f)), 1f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 6f * scale));
    }

    private void DrawHeroStat(ImDrawListPtr drawList, Vector2 columnTop, float height, float scale, Vector4 tint,
        FontAwesomeIcon icon, string value, string label, float columnWidth)
    {
        DrawSourceTile(drawList, new Vector2(columnTop.X, columnTop.Y + 32f * scale), 40f * scale, tint, icon, scale,
            true);
        var display = Typography.FitText(value, columnWidth - 20f * scale, TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(columnTop.X, columnTop.Y + 68f * scale), display, ui.TitleInk,
            TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(columnTop.X, columnTop.Y + 94f * scale),
            Loc.Culture.TextInfo.ToUpper(label), ui.MutedInk, TextStyles.Caption1);
    }

    private void DrawSourceSummary(InventoryResultGroup group)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var tileSize = 24f * scale;
        var tileCenter = new Vector2(origin.X + tileSize * 0.5f, origin.Y + tileSize * 0.5f);
        DrawSourceTile(drawList, tileCenter, tileSize, AccentFor(group.Kind), IconFor(group.Kind), scale, true);
        var textLeft = tileCenter.X + tileSize * 0.5f + 10f * scale;
        var summary = FormatCount(group.Rows.Count) + " · " + FormatCount(group.TotalQuantity);
        Typography.Draw(drawList,
            new Vector2(textLeft, tileCenter.Y - Typography.Measure(summary, TextStyles.Subheadline).Y * 0.5f), summary,
            ui.BodyInk, TextStyles.Subheadline);
        if (group.IsCached)
        {
            var label = Loc.T(L.Inventory.Updated, TimeText.Ago(group.CapturedUtc));
            var labelSize = Typography.Measure(label, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(origin.X + width - labelSize.X, tileCenter.Y - labelSize.Y * 0.5f),
                label, ui.MutedInk, TextStyles.Caption1);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tileSize + 12f * scale));
    }

    private void DrawResults()
    {
        if (groups.Count == 0)
        {
            DrawHint(Loc.T(L.Inventory.NoMatches));
            return;
        }

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            DrawGroup(groups[groupIndex]);
        }
    }

    private void DrawGroup(InventoryResultGroup group)
    {
        DrawGroupHeader(group);
        DrawItemPanel(group.Rows);
    }

    private void DrawGroupHeader(InventoryResultGroup group)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var tileSize = 22f * scale;
        var tileCenter = new Vector2(origin.X + tileSize * 0.5f, origin.Y + tileSize * 0.5f);
        DrawSourceTile(drawList, tileCenter, tileSize, AccentFor(group.Kind), IconFor(group.Kind), scale, true);
        var textLeft = tileCenter.X + tileSize * 0.5f + 10f * scale;
        Typography.Draw(drawList,
            new Vector2(textLeft, tileCenter.Y - Typography.Measure(group.Title, TextStyles.Headline).Y * 0.5f),
            group.Title, ui.TitleInk, TextStyles.Headline);
        if (group.IsCached)
        {
            var label = Loc.T(L.Inventory.Updated, TimeText.Ago(group.CapturedUtc));
            var labelSize = Typography.Measure(label, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(origin.X + width - labelSize.X, tileCenter.Y - labelSize.Y * 0.5f),
                label, ui.MutedInk, TextStyles.Caption1);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tileSize + 8f * scale));
    }

    private void DrawItemPanel(List<InventoryResultRow> rows)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = ItemRowHeight * scale;
        var panelMax = new Vector2(origin.X + width, origin.Y + rows.Count * rowHeight);
        ui.Card(drawList, origin, panelMax, Metrics.Radius.Card * scale, elevated: true);
        var separatorLeft = origin.X + 16f * scale + 38f * scale + 14f * scale;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = PanelRow(drawList, origin, width, rowHeight, index, scale, separatorLeft, ui.HoverTint, true,
                out var hovered, out _, out _);
            DrawItemRow(row, rows[index], hovered);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows.Count * rowHeight));
    }

    private void DrawItemRow(Rect row, InventoryResultRow item, bool hovered)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var iconSize = 38f * scale;
        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        Squircle.Fill(drawList, iconMin, iconMax, 9f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)));
        if (item.IconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrEmpty();
            drawList.AddImageRounded(texture.Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                9f * scale);
        }

        Material.EdgeSquircle(drawList, iconMin, iconMax, 9f * scale, scale, 0.5f);
        var quantityText = "x" + FormatCount(item.Quantity);
        var quantitySize = Typography.Measure(quantityText, TextStyles.BodyEmphasized);
        var quantityX = row.Max.X - quantitySize.X;
        Typography.Draw(drawList, new Vector2(quantityX, row.Center.Y - quantitySize.Y * 0.5f), quantityText, ui.Accent,
            TextStyles.BodyEmphasized);
        var textLeft = iconMax.X + 14f * scale;
        var textRight = quantityX - 12f * scale;
        if (item.HasHighQuality)
        {
            textRight -= 26f * scale;
            DrawHqBadge(new Vector2(textRight + 8f * scale, row.Center.Y), scale);
        }

        var name = Fit(item.Name, textRight - textLeft, TextStyles.Body);
        var nameSize = Typography.Measure(name, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name,
            hovered ? ui.TitleInk : ui.BodyInk, TextStyles.Body);
    }

    private void DrawSourceTile(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color,
        FontAwesomeIcon icon, float scale, bool enabled)
    {
        var half = size * 0.5f;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var radius = size * Metrics.Radius.TileFactor;
        if (!enabled)
        {
            var muted = new Vector4(0.52f, 0.54f, 0.62f, 1f);
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(muted, 0.16f)));
            Material.EdgeSquircle(drawList, min, max, radius, scale, 0.5f);
            ProgressRing.CenterIcon(drawList, center, icon, Palette.WithAlpha(muted, 0.95f), size * 0.46f);
            return;
        }

        var surface = IconTile.Surface(color);
        IconTile.FillShaded(drawList, min, max, radius, surface, 1f);
        Material.EdgeSquircle(drawList, min, max, radius, scale, 0.9f);
        ProgressRing.CenterIcon(drawList, center, icon, White, size * 0.46f);
    }

    private float DrawCountPill(ImDrawListPtr drawList, Vector2 rightCenter, int count, Vector4 color)
    {
        var text = FormatCount(count);
        var textSize = Typography.Measure(text, TextStyles.SubheadlineEmphasized);
        var padX = 9f * ImGuiHelpers.GlobalScale;
        var padY = 4f * ImGuiHelpers.GlobalScale;
        var width = textSize.X + padX * 2f;
        var height = textSize.Y + padY * 2f;
        var max = new Vector2(rightCenter.X, rightCenter.Y + height * 0.5f);
        var min = new Vector2(max.X - width, rightCenter.Y - height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(color, 0.18f)));
        var ink = Palette.Mix(color, ui.TitleInk, 0.25f);
        Typography.Draw(drawList, new Vector2(min.X + padX, rightCenter.Y - textSize.Y * 0.5f), text, ink,
            TextStyles.SubheadlineEmphasized);
        return min.X;
    }

    private void DrawHqBadge(Vector2 center, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var halfWidth = 10f * scale;
        var halfHeight = 8f * scale;
        var min = new Vector2(center.X - halfWidth, center.Y - halfHeight);
        var max = new Vector2(center.X + halfWidth, center.Y + halfHeight);
        Squircle.Fill(drawList, min, max, 4f * scale, ImGui.GetColorU32(ui.Accent));
        Typography.DrawCentered(drawList, center, Loc.T(L.Common.Hq), White, TextStyles.Caption2);
    }

    private void DrawHint(string message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 24f * scale));
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        AppSkin.Icon(new Vector2(centerX, origin.Y + 4f * scale), FontAwesomeIcon.BoxOpen.ToIconString(), ui.MutedInk,
            1.5f);
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 42f * scale), message, ui.MutedInk,
            TextStyles.Subheadline);
    }

    private void SectionLabel(string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        ui.SectionLabel(label);
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
            InventorySourceKind.FreeCompany => FontAwesomeIcon.Users,
            _ => FontAwesomeIcon.Home,
        };

    private static Vector4 AccentFor(InventorySourceKind kind) =>
        kind switch
        {
            InventorySourceKind.Inventory => new Vector4(0.98f, 0.60f, 0.23f, 1f),
            InventorySourceKind.Armoury => new Vector4(0.28f, 0.56f, 0.96f, 1f),
            InventorySourceKind.Crystals => new Vector4(0.62f, 0.44f, 0.96f, 1f),
            InventorySourceKind.Saddlebag => new Vector4(0.90f, 0.55f, 0.33f, 1f),
            InventorySourceKind.Equipped => new Vector4(0.28f, 0.78f, 0.52f, 1f),
            InventorySourceKind.Retainer => new Vector4(0.40f, 0.47f, 0.92f, 1f),
            InventorySourceKind.FreeCompany => new Vector4(0.93f, 0.72f, 0.30f, 1f),
            _ => new Vector4(0.55f, 0.55f, 0.60f, 1f),
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
