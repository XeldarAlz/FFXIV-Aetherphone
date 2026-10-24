using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class MinimizedPhonePage : ISettingsPage
{
    private const float ReorderRadius = 10f;
    private const float ReorderGap = 3f;
    private const float ToggleGap = 10f;
    private const float StagePadding = Metrics.Space.Lg;

    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Minimized.LockPosition, L.Minimized.Placement),
        new(L.Minimized.AlwaysOnTop, L.Minimized.Placement),
    };

    public string Title => Loc.T(L.Minimized.Title);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.MobileAlt;
    public Vector4 Tint => new(0.98f, 0.58f, 0.16f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly MinimizedLayoutService layout;
    private readonly MinimizedPreview preview;
    private readonly Configuration configuration;
    private readonly string[] shapeLabels = new string[MinimizedShapes.ShapeCount];
    private static readonly string[] RowIds = BuildRowIds();
    private int moveIndex = -1;
    private int moveDelta;

    public MinimizedPhonePage(MinimizedLayoutService layout, MinimizedPreview preview, Configuration configuration)
    {
        this.layout = layout;
        this.preview = preview;
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        var stageBottom = DrawStage(body, theme, scale);
        using (AppSurface.Begin(new Rect(new Vector2(body.Min.X, stageBottom), body.Max)))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            SettingsSection.Header(Loc.T(L.Minimized.Shape), theme);
            DrawShapePicker(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            SettingsSection.Hint(Loc.T(L.Minimized.ShapeHint), theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            SettingsSection.Hint(Loc.T(L.Minimized.ResizeHint), theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawPlacementSettings(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            if (configuration.MinimizedShape == MinimizedShape.Minimap)
            {
                DrawMinimapSettings(theme, scale);
            }
            else
            {
                DrawPhoneSettings(theme, scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        ApplyPendingMove();
    }

    private float DrawStage(Rect body, PhoneTheme theme, float scale)
    {
        var phoneSize = new Vector2(MinimizedShapes.BodyWidth, MinimizedShapes.BodyHeight) * scale;
        var inset = AppSurface.SidePadding * scale;
        var padding = StagePadding * scale;
        var stageMin = new Vector2(body.Min.X + inset, body.Min.Y + Metrics.Space.Md * scale);
        var stageMax = new Vector2(body.Max.X - inset, stageMin.Y + phoneSize.Y + padding * 2f);
        var drawList = ImGui.GetWindowDrawList();
        GroupCard.DrawSurface(drawList, stageMin, stageMax, theme, scale);
        var phoneMin = new Vector2((stageMin.X + stageMax.X - phoneSize.X) * 0.5f, stageMin.Y + padding);
        preview.Draw(drawList, new Rect(phoneMin, phoneMin + phoneSize), ImGui.GetIO().DeltaTime);
        return stageMax.Y;
    }

    private void DrawShapePicker(PhoneTheme theme)
    {
        for (var index = 0; index < shapeLabels.Length; index++)
        {
            shapeLabels[index] = Loc.T(MinimizedShapes.Label((MinimizedShape)index));
        }

        var card = GroupCard.Begin(theme, 1);
        var picked = SegmentStrip.Draw("minimized.shape", card.NextRow(), shapeLabels,
            (int)configuration.MinimizedShape, theme);
        card.End();
        if (picked == (int)configuration.MinimizedShape)
        {
            return;
        }

        configuration.MinimizedShape = (MinimizedShape)picked;
        configuration.Save();
    }

    private void DrawPlacementSettings(PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Minimized.Placement), theme);
        var card = GroupCard.Begin(theme, 2);
        var locked = SettingsRow.Bool(card.NextRow(), Loc.T(L.Minimized.LockPosition),
            configuration.LockMinimizedPosition, theme, "minimized.lockPosition");
        var onTop = SettingsRow.Bool(card.NextRow(), Loc.T(L.Minimized.AlwaysOnTop),
            configuration.MinimizedAlwaysOnTop, theme, "minimized.alwaysOnTop");
        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        SettingsSection.Hint(Loc.T(L.Minimized.PlacementHint), theme);
        if (locked == configuration.LockMinimizedPosition && onTop == configuration.MinimizedAlwaysOnTop)
        {
            return;
        }

        configuration.LockMinimizedPosition = locked;
        configuration.MinimizedAlwaysOnTop = onTop;
        configuration.Save();
    }

    private void DrawMinimapSettings(PhoneTheme theme, float scale)
    {
        SettingsSection.Hint(Loc.T(L.Minimized.MinimapHint), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(L.Minimized.MinimapZoomHint), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        DrawLiveSettings(theme, scale);
    }

    private void DrawLiveSettings(PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Minimized.Live), theme);
        var slots = layout.Slots;
        var card = GroupCard.Begin(theme, CountParts(slots, false));
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (!MinimizedParts.IsLive(slot.Part))
            {
                continue;
            }

            var enabled = SettingsRow.Bool(card.NextRow(), Loc.T(MinimizedParts.Label(slot.Part)), slot.Enabled, theme,
                RowIds[(int)slot.Part]);
            if (enabled == slot.Enabled)
            {
                continue;
            }

            layout.SetEnabled(index, enabled);
            if (enabled)
            {
                preview.Show(slot.Part);
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        SettingsSection.Hint(Loc.T(L.Minimized.LiveHint), theme);
    }

    private static string[] BuildRowIds()
    {
        var ids = new string[MinimizedParts.Count];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = "minimized.part." + MinimizedParts.Id((MinimizedPart)index);
        }

        return ids;
    }

    private static int CountParts(ReadOnlySpan<MinimizedSlot> slots, bool pages)
    {
        var count = 0;
        for (var index = 0; index < slots.Length; index++)
        {
            var part = slots[index].Part;
            if (pages ? MinimizedParts.IsPage(part) : MinimizedParts.IsLive(part))
            {
                count++;
            }
        }

        return count;
    }

    private void DrawPhoneSettings(PhoneTheme theme, float scale)
    {
        var faceCard = GroupCard.Begin(theme, 4);
        var wallpaper = SettingsRow.Bool(faceCard.NextRow(), Loc.T(L.Minimized.Wallpaper),
            configuration.MinimizedWallpaper, theme, "minimized.wallpaper");
        DrawPartToggle(faceCard.NextRow(), MinimizedPart.Clock, theme);
        DrawPartToggle(faceCard.NextRow(), MinimizedPart.Date, theme);
        DrawPartToggle(faceCard.NextRow(), MinimizedPart.Fps, theme);
        faceCard.End();
        if (wallpaper != configuration.MinimizedWallpaper)
        {
            configuration.MinimizedWallpaper = wallpaper;
            configuration.Save();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        SettingsSection.Hint(Loc.T(L.Minimized.WallpaperHint), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        DrawLiveSettings(theme, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Minimized.Pages), theme);
        var slots = layout.Slots;
        var total = CountParts(slots, true);
        var card = GroupCard.Begin(theme, total);
        var ordinal = 0;
        for (var index = 0; index < slots.Length; index++)
        {
            if (!MinimizedParts.IsPage(slots[index].Part))
            {
                continue;
            }

            DrawPartRow(card.NextRow(), index, slots[index], ordinal, total, theme, scale);
            ordinal++;
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        SettingsSection.Hint(Loc.T(L.Minimized.PagesHint), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        var resetCard = GroupCard.Begin(theme, 1);
        if (SettingsRow.Action(resetCard.NextRow(), Loc.T(L.Minimized.Reset), theme.Danger, theme))
        {
            layout.Reset();
        }

        resetCard.End();
    }

    private void DrawPartToggle(Rect row, MinimizedPart part, PhoneTheme theme)
    {
        var slots = layout.Slots;
        for (var index = 0; index < slots.Length; index++)
        {
            if (slots[index].Part != part)
            {
                continue;
            }

            var enabled = SettingsRow.Bool(row, Loc.T(MinimizedParts.Label(part)), slots[index].Enabled, theme,
                RowIds[(int)part]);
            if (enabled != slots[index].Enabled)
            {
                layout.SetEnabled(index, enabled);
                preview.Show(part);
            }

            return;
        }
    }

    private void DrawPartRow(Rect row, int index, in MinimizedSlot slot, int ordinal, int count, PhoneTheme theme,
        float scale)
    {
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(row.Max.X - toggleWidth, row.Center.Y - toggleHeight * 0.5f);
        var radius = ReorderRadius * scale;
        var downCenter = new Vector2(toggleMin.X - ToggleGap * scale - radius, row.Center.Y);
        var upCenter = new Vector2(downCenter.X - radius * 2f - ReorderGap * scale, row.Center.Y);
        var label = Loc.T(MinimizedParts.Label(slot.Part));
        var rowId = RowIds[(int)slot.Part];
        var labelMaxWidth = MathF.Max(1f, upCenter.X - radius - 8f * scale - row.Min.X);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Marquee.DrawLeftAuto(rowId, label, row.Min.X, row.Center.Y - labelSize.Y * 0.5f, labelMaxWidth,
            TextStyles.BodyEmphasized, slot.Enabled ? theme.TextStrong : theme.TextMuted);
        if (SettingsReorder.Button(upCenter, radius, FontAwesomeIcon.ChevronUp, theme, ordinal > 0))
        {
            moveIndex = index;
            moveDelta = -1;
        }

        if (SettingsReorder.Button(downCenter, radius, FontAwesomeIcon.ChevronDown, theme, ordinal < count - 1))
        {
            moveIndex = index;
            moveDelta = 1;
        }

        var enabled = Toggle.Draw(rowId, new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight)),
            slot.Enabled, theme);
        if (enabled == slot.Enabled)
        {
            return;
        }

        layout.SetEnabled(index, enabled);
        if (enabled)
        {
            preview.Show(slot.Part);
        }
    }

    private void ApplyPendingMove()
    {
        if (moveIndex < 0)
        {
            return;
        }

        var part = layout.Slots[moveIndex].Part;
        layout.MovePage(moveIndex, moveDelta);
        preview.Show(part);
        moveIndex = -1;
        moveDelta = 0;
    }
}
