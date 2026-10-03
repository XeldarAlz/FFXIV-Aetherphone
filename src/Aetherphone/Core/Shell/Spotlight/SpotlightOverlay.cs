using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell.Spotlight;

internal sealed class SpotlightOverlay
{
    private const string SearchAnchorKey = "home.search";
    private const float VeilDim = 0.35f;
    private const float InteractiveThreshold = 0.6f;
    private const float RevealStart = 0.45f;
    private const float FieldFadeEnd = 0.15f;
    private const float ReopenGuard = 0.2f;
    private const float FallbackPillWidthUnits = 96f;
    private const float FallbackPillHeightUnits = 26f;
    private const float FallbackPillLiftUnits = 103f;
    private const float EmptyPanelHeightUnits = 74f;
    private const float BottomMarginUnits = 24f;
    private const float SelectionAlpha = 0.12f;
    private const float PillHoverAlpha = 0.08f;
    private const float SeparatorAlpha = 0.07f;
    private const int MinimumQueryLength = 2;
    private const int QueryMaxLength = 64;
    private static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 MutedInk = new(1f, 1f, 1f, 0.62f);
    private static readonly string[] RecentPressIds =
    {
        "spotlight.recent0", "spotlight.recent1", "spotlight.recent2", "spotlight.recent3", "spotlight.recent4",
        "spotlight.recent5",
    };

    private readonly SpotlightIndex index;
    private readonly Configuration configuration;
    private readonly List<IPhoneApp> recents = new(RecentLaunches.Capacity);
    private Spring slide;
    private bool open;
    private bool focusPending;
    private bool scrollToSelection;
    private bool hasOrigin;
    private int openedFrame;
    private int selected;
    private Rect origin;
    private string query = string.Empty;

    public SpotlightOverlay(SpotlightIndex index, Configuration configuration)
    {
        this.index = index;
        this.configuration = configuration;
    }

    public bool Active => open || slide.Value > 0.01f;

    public void Open()
    {
        if (open || slide.Value > ReopenGuard)
        {
            return;
        }

        open = true;
        focusPending = true;
        scrollToSelection = false;
        selected = 0;
        query = string.Empty;
        index.Clear();
        index.CollectRecents(recents);
        openedFrame = ImGui.GetFrameCount();
    }

    public void Close() => open = false;

    public void CloseImmediate()
    {
        open = false;
        slide.SnapTo(0f);
    }

    public void Draw(Rect screen, Rect content, PhoneTheme theme, INavigator navigation, float delta, float scale)
    {
        slide.Step(open ? 1f : 0f, Motion.SwitcherReveal, delta);
        var progress = Math.Clamp(slide.Value, 0f, 1f);
        if (progress <= 0.001f)
        {
            return;
        }

        TrackOrigin(content, scale);
        var layout = new SpotlightLayout(scale);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        Material.Veil(drawList, screen.Min, screen.Max, VeilDim * progress);
        var rest = SpotlightLayout.RestRect(content, scale);
        var field = SpotlightLayout.FieldRect(origin, rest, progress);
        var fieldRadius = SpotlightLayout.FieldRadius(origin, SpotlightLayout.FieldRadiusUnits * scale, progress);
        GlassField.Surface(drawList, field, fieldRadius, scale, WallpaperLegibility.Strength(theme),
            Easing.Segment(progress, 0f, FieldFadeEnd));
        var interactive = open && progress > InteractiveThreshold;
        var overContent = false;
        if (interactive)
        {
            overContent = UiInteract.Hover(field.Min, field.Max);
            DrawQueryField(drawList, field, theme, scale);
        }
        else
        {
            GlassField.SearchGlyph(drawList, field, theme, scale, progress);
        }

        var reveal = Easing.Segment(progress, RevealStart, 1f);
        if (reveal > 0.01f)
        {
            var listTop = rest.Max.Y + SpotlightLayout.ListGapUnits * scale;
            var list = new Rect(new Vector2(rest.Min.X, listTop),
                new Vector2(rest.Max.X, screen.Max.Y - BottomMarginUnits * scale));
            overContent |= DrawBelowField(drawList, list, in layout, theme, navigation, scale, reveal, interactive);
        }

        drawList.PopClipRect();
        if (!interactive || !open)
        {
            return;
        }

        HandleKeyboard(navigation);
        if (open && ImGui.GetFrameCount() != openedFrame && UiInteract.ClickedOutside(overContent))
        {
            Close();
        }
    }

    private void TrackOrigin(Rect content, float scale)
    {
        if (UiAnchors.TryGet(SearchAnchorKey, out var reported))
        {
            origin = reported;
            hasOrigin = true;
            return;
        }

        if (hasOrigin)
        {
            return;
        }

        var half = new Vector2(FallbackPillWidthUnits, FallbackPillHeightUnits) * (0.5f * scale);
        var center = new Vector2(content.Center.X, content.Max.Y - FallbackPillLiftUnits * scale);
        origin = new Rect(center - half, center + half);
    }

    private void DrawQueryField(ImDrawListPtr drawList, Rect field, PhoneTheme theme, float scale)
    {
        var previous = query;
        GlassField.Search(drawList, field, "##spotlightQuery", Loc.T(L.Spotlight.Hint), ref query, theme, scale,
            QueryMaxLength, focusPending);
        focusPending = false;
        if (string.Equals(previous, query, StringComparison.Ordinal))
        {
            return;
        }

        index.Search(query);
        selected = 0;
        scrollToSelection = true;
    }

    private bool DrawBelowField(ImDrawListPtr drawList, Rect list, in SpotlightLayout layout, PhoneTheme theme,
        INavigator navigation, float scale, float reveal, bool interactive)
    {
        if (index.Results.Count > 0)
        {
            return DrawResults(drawList, list, in layout, theme, navigation, reveal, interactive);
        }

        if (QueryLength(query) >= MinimumQueryLength)
        {
            return DrawEmpty(drawList, list, in layout, reveal, interactive);
        }

        if (recents.Count > 0)
        {
            return DrawRecents(drawList, list, in layout, theme, navigation, scale, reveal, interactive);
        }

        return false;
    }

    private static int QueryLength(string text)
    {
        var length = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                length++;
            }
        }

        return length;
    }

    private static void DrawPanel(ImDrawListPtr drawList, Rect panel, in SpotlightLayout layout, float reveal) =>
        Material.LiquidGlass(drawList, panel.Min, panel.Max, layout.PanelRadius, layout.Scale, GlassTone.Dark, 0f,
            reveal);

    private bool DrawRecents(ImDrawListPtr drawList, Rect list, in SpotlightLayout layout, PhoneTheme theme,
        INavigator navigation, float scale, float reveal, bool interactive)
    {
        var innerWidth = list.Width - layout.PanelPad * 2f;
        var panelHeight = MathF.Min(layout.RecentsPanelHeight(innerWidth), list.Height);
        var panel = new Rect(list.Min, new Vector2(list.Max.X, list.Min.Y + panelHeight));
        DrawPanel(drawList, panel, in layout, reveal);
        var vertexStart = drawList.VtxBuffer.Size;
        var inner = panel.Inset(layout.PanelPad);
        Typography.Draw(drawList, new Vector2(inner.Min.X + layout.RowInset, inner.Min.Y + layout.HeaderTextOffset),
            Loc.T(L.Spotlight.Recents), MutedInk, TextStyles.FootnoteEmphasized);
        var cellWidth = layout.RecentCellWidth(innerWidth);
        var tileSize = layout.RecentTileSize(innerWidth);
        var tileTop = inner.Min.Y + layout.HeaderHeight;
        var cellBottom = tileTop + tileSize + layout.RecentLabelBand;
        var tileCenterY = tileTop + tileSize * 0.5f;
        var zoom = scale / UiScale.Current;
        var count = Math.Min(recents.Count, RecentPressIds.Length);
        for (var slot = 0; slot < count; slot++)
        {
            var app = recents[slot];
            var centerX = inner.Min.X + cellWidth * (slot + 0.5f);
            var cell = new Rect(new Vector2(centerX - cellWidth * 0.5f, tileTop),
                new Vector2(centerX + cellWidth * 0.5f, cellBottom));
            var hovered = interactive && UiInteract.Hover(cell.Min, cell.Max);
            var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var press = PressFx.Scale(RecentPressIds[slot], pressed, Motion.PressScaleControl);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            HomeTileView.DrawApp(new Vector2(centerX, tileCenterY), tileSize, app, theme, press, 1f, true,
                cellWidth, configuration, zoom);
            if (hovered && UiInteract.Click(cell.Min, cell.Max, true))
            {
                navigation.Open(app.Id);
                Close();
                break;
            }
        }

        LayerCompositor.Fade(drawList, vertexStart, reveal);
        return interactive && UiInteract.Hover(panel.Min, panel.Max);
    }

    private bool DrawResults(ImDrawListPtr drawList, Rect list, in SpotlightLayout layout, PhoneTheme theme,
        INavigator navigation, float reveal, bool interactive)
    {
        var results = index.Results;
        var pad = layout.PanelPad;
        var panelHeight = MathF.Min(layout.Measure(results) + pad * 2f, list.Height);
        var panel = new Rect(list.Min, new Vector2(list.Max.X, list.Min.Y + panelHeight));
        DrawPanel(drawList, panel, in layout, reveal);
        var content = panel.Inset(pad);
        drawList.PushClipRect(content.Min, content.Max, true);
        var vertexStart = drawList.VtxBuffer.Size;
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##spotlightResults", content.Size, false,
                   ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar))
        {
            var scrollY = ImGui.GetScrollY();
            var y = content.Min.Y - scrollY;
            var lastKind = (SpotlightKind)255;
            for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
            {
                var result = results[resultIndex];
                var newSection = result.Kind != lastKind;
                if (newSection)
                {
                    lastKind = result.Kind;
                    if (y + layout.HeaderHeight > content.Min.Y && y < content.Max.Y)
                    {
                        Typography.Draw(drawList,
                            new Vector2(content.Min.X + layout.RowInset, y + layout.HeaderTextOffset),
                            Loc.T(SectionLabel(result.Kind)), MutedInk, TextStyles.FootnoteEmphasized);
                    }

                    y += layout.HeaderHeight;
                }

                var row = new Rect(new Vector2(content.Min.X, y), new Vector2(content.Max.X, y + layout.RowHeight));
                if (row.Max.Y > content.Min.Y && row.Min.Y < content.Max.Y)
                {
                    if (!newSection)
                    {
                        drawList.AddLine(new Vector2(layout.TextLeft(row), row.Min.Y),
                            new Vector2(row.Max.X - layout.RowInset, row.Min.Y),
                            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, SeparatorAlpha)), layout.Scale);
                    }

                    DrawRow(drawList, row, in result, resultIndex, in layout, theme, interactive, navigation);
                    if (!open)
                    {
                        break;
                    }
                }

                y += layout.RowHeight;
            }

            ImGui.Dummy(new Vector2(1f, MathF.Max(1f, layout.Measure(results))));
            if (scrollToSelection)
            {
                var rowTop = layout.RowTop(results, selected);
                ImGui.SetScrollY(SpotlightLayout.ScrollToReveal(rowTop, rowTop + layout.RowHeight, scrollY,
                    content.Height));
                scrollToSelection = false;
            }
        }

        LayerCompositor.Fade(drawList, vertexStart, reveal);
        drawList.PopClipRect();
        return interactive && UiInteract.Hover(panel.Min, panel.Max);
    }

    private bool DrawEmpty(ImDrawListPtr drawList, Rect list, in SpotlightLayout layout, float reveal,
        bool interactive)
    {
        var height = MathF.Min(EmptyPanelHeightUnits * layout.Scale, list.Height);
        var panel = new Rect(list.Min, new Vector2(list.Max.X, list.Min.Y + height));
        DrawPanel(drawList, panel, in layout, reveal);
        var vertexStart = drawList.VtxBuffer.Size;
        Typography.DrawCentered(drawList, panel.Center, Loc.T(L.Spotlight.NoResults), MutedInk,
            TextStyles.Subheadline);
        LayerCompositor.Fade(drawList, vertexStart, reveal);
        return interactive && UiInteract.Hover(panel.Min, panel.Max);
    }

    private void DrawRow(ImDrawListPtr drawList, Rect row, in SpotlightResult result, int resultIndex,
        in SpotlightLayout layout, PhoneTheme theme, bool interactive, INavigator navigation)
    {
        var hovered = interactive && UiInteract.Hover(row.Min, row.Max);
        if (hovered && PointerMoved())
        {
            selected = resultIndex;
        }

        var isSelected = resultIndex == selected;
        if (isSelected)
        {
            Squircle.Fill(drawList, row.Min, row.Max, layout.RowRadius,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, SelectionAlpha)));
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        DrawResultIcon(drawList, layout.TileRect(row), in result);
        var textLeft = layout.TextLeft(row);
        var textRight = row.Max.X - layout.RowInset;
        var overAction = false;
        if (isSelected && interactive)
        {
            textRight = DrawActions(drawList, row, in result, in layout, theme, navigation, textRight, out overAction);
            if (!open)
            {
                return;
            }
        }

        var textMax = MathF.Max(textRight - textLeft, 1f);
        var subtitle = result.Subtitle.Length > 0 ? result.Subtitle : Loc.T(SectionLabel(result.Kind));
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var titleTop = row.Center.Y - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, titleTop),
            Typography.FitText(result.Title, textMax, TextStyles.Body), Ink, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, titleTop + titleHeight),
            Typography.FitText(subtitle, textMax, TextStyles.Footnote), MutedInk, TextStyles.Footnote);
        if (interactive && !overAction && UiInteract.Click(row.Min, row.Max, hovered))
        {
            Activate(in result, navigation);
        }
    }

    private float DrawActions(ImDrawListPtr drawList, Rect row, in SpotlightResult result, in SpotlightLayout layout,
        PhoneTheme theme, INavigator navigation, float right, out bool overAction)
    {
        overAction = false;
        var brightness = WallpaperLegibility.Strength(theme);
        if (result.Kind == SpotlightKind.Contact && index.CallsAvailable)
        {
            var callLabel = Loc.T(L.Spotlight.Call);
            var callPill = layout.PillRect(row, right, Typography.Measure(callLabel, TextStyles.FootnoteEmphasized).X);
            if (DrawPill(drawList, callPill, "spotlight.pill.call", callLabel, theme, layout.Scale, brightness,
                    out var overCall))
            {
                index.Call(in result, navigation);
                Close();
                return right;
            }

            overAction |= overCall;
            right = callPill.Min.X - layout.PillGap;
        }

        var label = Loc.T(PrimaryActionLabel(result.Kind));
        var pill = layout.PillRect(row, right, Typography.Measure(label, TextStyles.FootnoteEmphasized).X);
        if (DrawPill(drawList, pill, "spotlight.pill.primary", label, theme, layout.Scale, brightness,
                out var overPrimary))
        {
            Activate(in result, navigation);
            return right;
        }

        overAction |= overPrimary;
        return pill.Min.X - layout.PillGap;
    }

    private static bool DrawPill(ImDrawListPtr drawList, Rect pill, string pressId, string label, PhoneTheme theme,
        float scale, float brightness, out bool hovered)
    {
        hovered = UiInteract.Hover(pill.Min, pill.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(pressId, pressed, Motion.PressScaleControl);
        var center = pill.Center;
        var half = pill.Size * (0.5f * press);
        Material.LiquidGlass(drawList, center - half, center + half, half.Y, scale, GlassTone.Light, brightness);
        if (hovered)
        {
            Squircle.Fill(drawList, center - half, center + half, half.Y,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, PillHoverAlpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, center, label, theme.TextStrong, TextStyles.FootnoteEmphasized);
        return UiInteract.Click(pill.Min, pill.Max, hovered);
    }

    private static void DrawResultIcon(ImDrawListPtr drawList, Rect tile, in SpotlightResult result)
    {
        var center = tile.Center;
        if (result.Kind is SpotlightKind.App or SpotlightKind.StoreApp)
        {
            IconTile.DrawApp(drawList, result.Payload, center, tile.Width,
                IconTile.Surface(AppAccents.For(result.Payload)));
            return;
        }

        var radius = tile.Width * 0.5f;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(KindTint(result.Kind)), 32);
        ProgressRing.CenterIcon(drawList, center, KindIcon(in result), Ink, radius * 1.05f);
    }

    private void Activate(in SpotlightResult result, INavigator navigation)
    {
        index.Activate(in result, navigation);
        Close();
    }

    private void HandleKeyboard(INavigator navigation)
    {
        if (!UiInteract.WindowFocused)
        {
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Close();
            return;
        }

        var count = index.Results.Count;
        if (count > 0 && ImGui.IsKeyPressed(ImGuiKey.DownArrow))
        {
            selected = Math.Min(selected + 1, count - 1);
            scrollToSelection = true;
        }
        else if (count > 0 && ImGui.IsKeyPressed(ImGuiKey.UpArrow))
        {
            selected = Math.Max(selected - 1, 0);
            scrollToSelection = true;
        }

        if (!ImGui.IsKeyPressed(ImGuiKey.Enter) && !ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))
        {
            return;
        }

        if (count == 0)
        {
            focusPending = true;
            return;
        }

        var result = index.Results[Math.Clamp(selected, 0, count - 1)];
        Activate(in result, navigation);
    }

    private static bool PointerMoved()
    {
        var delta = ImGui.GetIO().MouseDelta;
        return delta.X != 0f || delta.Y != 0f;
    }

    private static LocString PrimaryActionLabel(SpotlightKind kind) => kind switch
    {
        SpotlightKind.Calculation => L.Spotlight.Copy,
        SpotlightKind.Shortcut => L.Spotlight.Run,
        SpotlightKind.Aetheryte => L.Spotlight.Teleport,
        _ => L.Spotlight.Open,
    };

    private static Vector4 KindTint(SpotlightKind kind) => kind switch
    {
        SpotlightKind.Calculation => new Vector4(0.98f, 0.62f, 0.16f, 1f),
        SpotlightKind.Action => new Vector4(0.36f, 0.55f, 0.92f, 1f),
        SpotlightKind.Contact => new Vector4(0.30f, 0.62f, 0.95f, 1f),
        SpotlightKind.DmThread => new Vector4(0.20f, 0.78f, 0.35f, 1f),
        SpotlightKind.SettingsPage => new Vector4(0.55f, 0.57f, 0.62f, 1f),
        SpotlightKind.Shortcut => new Vector4(0.62f, 0.42f, 0.94f, 1f),
        SpotlightKind.Aetheryte => new Vector4(0.24f, 0.74f, 0.86f, 1f),
        SpotlightKind.Conversation => new Vector4(0.35f, 0.78f, 0.52f, 1f),
        SpotlightKind.Note => new Vector4(0.98f, 0.80f, 0.28f, 1f),
        SpotlightKind.Guide => new Vector4(0.90f, 0.32f, 0.36f, 1f),
        SpotlightKind.Venue => new Vector4(0.94f, 0.40f, 0.72f, 1f),
        _ => new Vector4(0.86f, 0.62f, 0.28f, 1f),
    };

    private static FontAwesomeIcon KindIcon(in SpotlightResult result) => result.Kind switch
    {
        SpotlightKind.Calculation => FontAwesomeIcon.Calculator,
        SpotlightKind.Action => SpotlightActions.Icon((SpotlightActionKind)result.PageIndex),
        SpotlightKind.Contact => FontAwesomeIcon.User,
        SpotlightKind.DmThread => FontAwesomeIcon.Comment,
        SpotlightKind.SettingsPage => FontAwesomeIcon.Cog,
        SpotlightKind.Shortcut => FontAwesomeIcon.Bolt,
        SpotlightKind.Aetheryte => FontAwesomeIcon.MapMarkerAlt,
        SpotlightKind.Conversation => FontAwesomeIcon.CommentDots,
        SpotlightKind.Note => FontAwesomeIcon.StickyNote,
        SpotlightKind.Guide => FontAwesomeIcon.BookOpen,
        SpotlightKind.Venue => FontAwesomeIcon.GlassCheers,
        _ => FontAwesomeIcon.Coins,
    };

    private static LocString SectionLabel(SpotlightKind kind) => kind switch
    {
        SpotlightKind.Calculation => L.Spotlight.Result,
        SpotlightKind.App => L.Spotlight.Apps,
        SpotlightKind.Action => L.Spotlight.Actions,
        SpotlightKind.Contact => L.Spotlight.Contacts,
        SpotlightKind.DmThread => L.Spotlight.Messages,
        SpotlightKind.SettingsPage => L.Spotlight.Settings,
        SpotlightKind.Shortcut => L.Spotlight.Shortcuts,
        SpotlightKind.Aetheryte => L.Apps.Maps,
        SpotlightKind.Conversation => L.Spotlight.Conversations,
        SpotlightKind.Note => L.Spotlight.Notes,
        SpotlightKind.Guide => L.Apps.Strats,
        SpotlightKind.Venue => L.Apps.Venues,
        SpotlightKind.StoreApp => L.Spotlight.Store,
        _ => L.Spotlight.Items,
    };
}
