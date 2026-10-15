using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Localization;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NameplateTitlePage : ISettingsPage
{
    private const float ReorderRadius = 10f;
    private const float ReorderGap = 3f;
    private const float ToggleGap = 10f;
    private const float LabelGap = 8f;
    private const float LiveDotRadius = 4f;
    private const float SwatchRadius = 10f;
    private const float SwatchGap = 8f;
    private const float SwatchRing = 2f;
    private const int NoSwatch = -1;
    private const int CustomSwatch = int.MaxValue;
    private const int GalleryColumns = 2;

    public static readonly Vector4 PageTint = new(0.11f, 0.50f, 0.58f, 1f);
    private static readonly string[] RowIds = BuildRowIds();

    private static readonly NameplateGradientMotion[] MotionOrder =
    {
        NameplateGradientMotion.Wave,
        NameplateGradientMotion.Pulse,
        NameplateGradientMotion.Static,
    };

    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Nameplate.Enabled),
        new(L.Nameplate.Priority),
        new(L.Nameplate.Custom),
        new(L.Nameplate.NowPlaying),
        new(L.Nameplate.Handle),
        new(L.Nameplate.Look),
        new(L.Nameplate.Glow),
        new(L.Nameplate.Gradient),
        new(L.Nameplate.Position),
    };

    private readonly NameplateTitleService titles;
    private readonly Configuration configuration;
    private readonly ISettingsNavigator navigator;
    private readonly NameplateStatusPage statusPage;
    private readonly NameplateColorPage colorPage;
    private readonly NameplateStage stage = new();
    private readonly string[] styleLabels = new string[3];
    private readonly string[] glowLabels = new string[2];
    private readonly string[] motionLabels = new string[3];
    private readonly string[] positionLabels = new string[2];
    private string characterSourceName = string.Empty;
    private string characterSourceTemplate = string.Empty;
    private string characterLine = string.Empty;
    private int moveIndex = -1;
    private int moveDelta;

    public NameplateTitlePage(NameplateTitleService titles, Configuration configuration, ISettingsNavigator navigator,
        PcMediaSource pcMedia)
    {
        this.titles = titles;
        this.configuration = configuration;
        this.navigator = navigator;
        statusPage = new NameplateStatusPage(titles, configuration, pcMedia, navigator,
            new NameplateStatusPage(titles, configuration, pcMedia, navigator, null));
        colorPage = new NameplateColorPage(titles);
    }

    public string Title => Loc.T(L.Nameplate.Title);

    public string Summary => !titles.Settings.Enabled ? Loc.T(L.Nameplate.Off) : titles.Current.Text;

    public FontAwesomeIcon Icon => FontAwesomeIcon.IdBadge;

    public Vector4 Tint => PageTint;

    public bool ShowsBadge => configuration.HasUnseenFeaturePin(NewFeaturePins.Nameplate);

    public ReadOnlySpan<SettingsEntry> Entries => Searchable;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        if (!titles.Available)
        {
            var messageBottom = NameplateStage.DrawPinnedMessage(body, theme, scale,
                Loc.T(L.Nameplate.NotInstalledTitle));
            using (AppSurface.Begin(new Rect(new Vector2(body.Min.X, messageBottom), body.Max)))
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
                SettingsSection.Hint(Loc.T(L.Nameplate.NotInstalledBody), theme);
            }

            return;
        }

        var stageBottom = stage.DrawPinned(body, theme, scale, NameplateStage.NameOf(titles), titles.Preview,
            NameplateStage.CaptionOf(titles));
        using (AppSurface.Begin(new Rect(new Vector2(body.Min.X, stageBottom), body.Max)))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            DrawMasterSwitch(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawPriority(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawLook(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        ApplyPendingMove();
    }

    private void DrawMasterSwitch(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        var card = GroupCard.Begin(theme, 1);
        var enabled = SettingsRow.Switch(card.NextRow(), FontAwesomeIcon.IdBadge, PageTint,
            Loc.T(L.Nameplate.Enabled), settings.Enabled, theme, id: "nameplate.enabled");
        card.End();
        if (enabled != settings.Enabled)
        {
            settings.Enabled = enabled;
            titles.Commit();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(L.Nameplate.PublicHint), theme);
        if (titles.CharacterName.Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
        SettingsSection.Hint(CharacterLine(), theme);
    }

    private void DrawPriority(PhoneTheme theme, float scale)
    {
        var order = titles.Settings.Order;
        SettingsSection.Header(Loc.T(L.Nameplate.Priority), theme);
        var card = GroupCard.Begin(theme, order.Length);
        for (var index = 0; index < order.Length; index++)
        {
            DrawStatusRow(card.NextRow(), index, order[index], order.Length, theme, scale);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(L.Nameplate.PriorityHint), theme);
    }

    private void DrawStatusRow(Rect row, int index, NameplateStatus status, int count, PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        ref readonly var info = ref NameplateStatusCatalog.For(status);
        var shown = settings.Shows(status);
        var rowId = RowIds[NameplateStatusCatalog.IndexOf(status)];
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(row.Max.X - toggleWidth, row.Center.Y - toggleHeight * 0.5f);
        var radius = ReorderRadius * scale;
        var downCenter = new Vector2(toggleMin.X - ToggleGap * scale - radius, row.Center.Y);
        var upCenter = new Vector2(downCenter.X - radius * 2f - ReorderGap * scale, row.Center.Y);
        var editMax = new Vector2(upCenter.X - radius - LabelGap * scale, row.Max.Y);
        var hovered = UiInteract.Hover(row.Min, editMax);
        if (hovered)
        {
            SettingsRow.DrawRowHighlight(new Rect(row.Min, editMax), theme);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tileMax = SettingsRow.DrawIconTile(row, info.Icon, info.Tint, theme, hovered, false, scale);
        var live = titles.Current.Kind;
        if (live == status || NameplateStatusCatalog.For(live).Parent == status)
        {
            var dot = new Vector2(tileMax.X, row.Center.Y - (tileMax.Y - row.Center.Y));
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddCircleFilled(dot, (LiveDotRadius + 1.5f) * scale, ImGui.GetColorU32(theme.GroupedCard), 16);
            drawList.AddCircleFilled(dot, LiveDotRadius * scale, ImGui.GetColorU32(theme.ToggleOn), 16);
        }

        var labelX = tileMax.X + Metrics.Space.Md * scale;
        var label = Loc.T(info.Label);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Marquee.DrawLeftAuto(rowId, label, labelX, row.Center.Y - labelSize.Y * 0.5f,
            MathF.Max(1f, editMax.X - labelX), TextStyles.BodyEmphasized, shown ? theme.TextStrong : theme.TextMuted);
        if (UiInteract.Click(row.Min, editMax, hovered))
        {
            statusPage.Show(status);
            navigator.Open(statusPage);
        }

        if (SettingsReorder.Button(upCenter, radius, FontAwesomeIcon.ChevronUp, theme, index > 0))
        {
            moveIndex = index;
            moveDelta = -1;
        }

        if (SettingsReorder.Button(downCenter, radius, FontAwesomeIcon.ChevronDown, theme, index < count - 1))
        {
            moveIndex = index;
            moveDelta = 1;
        }

        var next = Toggle.Draw(rowId, new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight)), shown,
            theme);
        if (next != shown)
        {
            settings.Set(status, next);
            titles.Commit();
        }
    }

    private void ApplyPendingMove()
    {
        if (moveIndex < 0)
        {
            return;
        }

        titles.Settings.Move(moveIndex, moveDelta);
        moveIndex = -1;
        moveDelta = 0;
        titles.Commit();
    }

    private void DrawLook(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        SettingsSection.Header(Loc.T(L.Nameplate.Look), theme);
        styleLabels[0] = Loc.T(L.Nameplate.StyleApp);
        styleLabels[1] = Loc.T(L.Nameplate.StyleMine);
        styleLabels[2] = Loc.T(L.Nameplate.StyleCustom);
        var styleCard = GroupCard.Begin(theme, 1);
        var style = SegmentStrip.Draw("nameplate.style", styleCard.NextRow(), styleLabels, (int)settings.Style, theme);
        styleCard.End();
        var changed = false;
        if (style != (int)settings.Style)
        {
            settings.Style = (NameplateTitleStyle)style;
            changed = true;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(StyleHint(settings.Style), theme);
        if (settings.Style == NameplateTitleStyle.Custom)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            SettingsSection.Header(Loc.T(L.Nameplate.TextColor), theme);
            changed |= DrawColorChoice(NameplateColorSlot.Text, theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            changed |= DrawGlow(theme, scale);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Nameplate.Position), theme);
        positionLabels[0] = Loc.T(L.Nameplate.Above);
        positionLabels[1] = Loc.T(L.Nameplate.Below);
        var positionCard = GroupCard.Begin(theme, 1);
        var position = SegmentStrip.Draw("nameplate.position", positionCard.NextRow(), positionLabels,
            settings.Prefix ? 0 : 1, theme);
        positionCard.End();
        if (position != (settings.Prefix ? 0 : 1))
        {
            settings.Prefix = position == 0;
            changed = true;
        }

        if (changed)
        {
            titles.Commit();
        }
    }

    private string StyleHint(NameplateTitleStyle style) => style switch
    {
        NameplateTitleStyle.MatchMine when titles.HasOwnLook => Loc.T(L.Nameplate.StyleMineHint),
        NameplateTitleStyle.MatchMine => Loc.T(L.Nameplate.StyleMineMissing),
        NameplateTitleStyle.AppColors => Loc.T(L.Nameplate.StyleAppHint),
        _ => string.Empty,
    };

    private bool DrawGlow(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        SettingsSection.Header(Loc.T(L.Nameplate.Glow), theme);
        glowLabels[0] = Loc.T(L.Nameplate.Solid);
        glowLabels[1] = Loc.T(L.Nameplate.Gradient);
        var glowCard = GroupCard.Begin(theme, 1);
        var glow = SegmentStrip.Draw("nameplate.glow", glowCard.NextRow(), glowLabels, (int)settings.Glow, theme);
        glowCard.End();
        var changed = false;
        if (glow != (int)settings.Glow)
        {
            settings.Glow = (NameplateGlow)glow;
            changed = true;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        if (settings.Glow == NameplateGlow.Solid)
        {
            return DrawColorChoice(NameplateColorSlot.Glow, theme, scale) | changed;
        }

        motionLabels[0] = Loc.T(L.Nameplate.MotionWave);
        motionLabels[1] = Loc.T(L.Nameplate.MotionPulse);
        motionLabels[2] = Loc.T(L.Nameplate.MotionStatic);
        var shownMotion = Array.IndexOf(MotionOrder, settings.Motion);
        var motionCard = GroupCard.Begin(theme, 1);
        var motion = SegmentStrip.Draw("nameplate.motion", motionCard.NextRow(), motionLabels, shownMotion, theme);
        motionCard.End();
        if (motion != shownMotion)
        {
            settings.Motion = MotionOrder[motion];
            changed = true;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(MotionHint(settings.Motion)), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        changed |= DrawGallery(theme, scale);
        if (settings.Gradient != NameplateGradients.TwoColors)
        {
            return changed;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Nameplate.FirstColor), theme);
        changed |= DrawColorChoice(NameplateColorSlot.GradientFirst, theme, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Nameplate.SecondColor), theme);
        return DrawColorChoice(NameplateColorSlot.GradientSecond, theme, scale) | changed;
    }

    private static LocString MotionHint(NameplateGradientMotion motion) => motion switch
    {
        NameplateGradientMotion.Pulse => L.Nameplate.MotionPulseHint,
        NameplateGradientMotion.Static => L.Nameplate.MotionStaticHint,
        _ => L.Nameplate.MotionWaveHint,
    };

    private bool DrawGallery(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        var tileCount = NameplateGradients.All.Length + 1;
        var rows = (tileCount + GalleryColumns - 1) / GalleryColumns;
        var card = GroupCard.Begin(theme,
            Metrics.Space.Md * 2f + rows * Metrics.Size.Row + (rows - 1) * Metrics.Space.Sm);
        var bounds = card.Bounds;
        card.End();
        var padding = Metrics.Space.Md * scale;
        var gap = Metrics.Space.Sm * scale;
        var tileSize = new Vector2((bounds.Width - padding * 2f - gap * (GalleryColumns - 1)) / GalleryColumns,
            Metrics.Size.Row * scale);
        var tile = new NameplateGradientTile(settings, NameplateGradients.Tick(Environment.TickCount64), theme, scale);
        var picked = settings.Gradient;
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var gradient = tileIndex == 0 ? NameplateGradients.TwoColors : tileIndex - 1;
            var column = tileIndex % GalleryColumns;
            var row = tileIndex / GalleryColumns;
            var min = bounds.Min + new Vector2(padding + column * (tileSize.X + gap), padding + row * (tileSize.Y + gap));
            var max = min + tileSize;
            var hovered = UiInteract.Hover(min, max);
            tile.Draw(min, max, gradient, gradient == settings.Gradient, hovered);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                picked = gradient;
            }
        }

        if (picked == settings.Gradient)
        {
            return false;
        }

        settings.Gradient = picked;
        return true;
    }

    private bool DrawColorChoice(NameplateColorSlot slot, PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        var picked = DrawSwatches(settings.ColorOf(slot), theme, scale);
        if (picked == CustomSwatch)
        {
            colorPage.Show(slot);
            navigator.Open(colorPage);
            return false;
        }

        if (picked == NoSwatch)
        {
            return false;
        }

        settings.SetColor(slot, NameplatePalette.PackedAt(picked));
        return true;
    }

    private static int DrawSwatches(uint color, PhoneTheme theme, float scale)
    {
        var card = GroupCard.Begin(theme, 1);
        var row = card.NextRow();
        card.End();
        var colors = NameplatePalette.Colors;
        var selected = NameplatePalette.IndexOf(color);
        var slots = colors.Length + 1;
        var radius = SwatchRadius * scale;
        var gap = SwatchGap * scale;
        var available = row.Width;
        var step = MathF.Min(radius * 2f + gap, available / slots);
        var drawRadius = MathF.Min(radius, step * 0.5f - SwatchRing * scale);
        var startX = row.Min.X + (available - step * slots) * 0.5f + step * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        var picked = NoSwatch;
        for (var index = 0; index < slots; index++)
        {
            var center = new Vector2(startX + step * index, row.Center.Y);
            var custom = index == colors.Length;
            var chosen = custom ? selected < 0 : index == selected;
            if (custom && !chosen)
            {
                SwatchStrip.DrawWheel(drawList, center, drawRadius);
            }
            else
            {
                var fill = custom ? NameplatePalette.Unpack(color) : colors[index];
                drawList.AddCircleFilled(center, drawRadius, ImGui.GetColorU32(new Vector4(fill, 1f)), 32);
            }

            drawList.AddCircle(center, drawRadius, ImGui.GetColorU32(theme.Hairline), 32, scale);
            var min = center - new Vector2(drawRadius, drawRadius);
            var max = center + new Vector2(drawRadius, drawRadius);
            var hovered = UiInteract.Hover(min, max);
            if (chosen)
            {
                drawList.AddCircle(center, drawRadius + SwatchRing * scale * 1.5f, ImGui.GetColorU32(theme.Accent),
                    32, SwatchRing * scale);
            }
            else if (hovered)
            {
                drawList.AddCircle(center, drawRadius + SwatchRing * scale * 1.5f,
                    ImGui.GetColorU32(theme.TextMuted), 32, SwatchRing * scale * 0.5f);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                picked = custom ? CustomSwatch : index;
            }
        }

        return picked == selected ? NoSwatch : picked;
    }

    private string CharacterLine()
    {
        var template = Loc.T(L.Nameplate.Character);
        var name = titles.CharacterName;
        if (!ReferenceEquals(template, characterSourceTemplate) || !ReferenceEquals(name, characterSourceName))
        {
            characterSourceTemplate = template;
            characterSourceName = name;
            characterLine = string.Format(template, name);
        }

        return characterLine;
    }

    private static string[] BuildRowIds()
    {
        var ids = new string[NameplateStatusCatalog.All.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = "nameplate.status." + NameplateStatusCatalog.All[index].Status;
        }

        return ids;
    }
}
