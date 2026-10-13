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

    private static readonly Vector4 PageTint = new(0.11f, 0.50f, 0.58f, 1f);
    private static readonly string[] RowIds = BuildRowIds();

    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Nameplate.Enabled),
        new(L.Nameplate.Priority),
        new(L.Nameplate.Custom),
        new(L.Nameplate.NowPlaying),
        new(L.Nameplate.Handle),
        new(L.Nameplate.Look),
        new(L.Nameplate.Position),
    };

    private readonly NameplateTitleService titles;
    private readonly Configuration configuration;
    private readonly ISettingsNavigator navigator;
    private readonly NameplateStatusPage statusPage;
    private readonly NameplateStage stage = new();
    private readonly string[] styleLabels = new string[3];
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
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            if (!titles.Available)
            {
                NameplateStage.DrawMessage(theme, scale, Loc.T(L.Nameplate.NotInstalledTitle));
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
                SettingsSection.Hint(Loc.T(L.Nameplate.NotInstalledBody), theme);
                return;
            }

            DrawStage(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawMasterSwitch(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawPriority(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawLook(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        ApplyPendingMove();
    }

    private void DrawStage(PhoneTheme theme, float scale)
    {
        var name = titles.CharacterName.Length > 0 ? titles.CharacterName : Loc.T(L.Nameplate.Example);
        stage.Draw(theme, scale, name, titles.Preview);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var caption = !titles.Current.IsNone
            ? Loc.T(L.Nameplate.Live)
            : titles.Settings.Enabled
                ? Loc.T(L.Nameplate.NothingActive)
                : Loc.T(L.Nameplate.Example);
        SettingsSection.Hint(caption, theme);
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

    private bool VelvetHidden => !configuration.IsVelvetOnboarded();

    private void DrawPriority(PhoneTheme theme, float scale)
    {
        var order = titles.Settings.Order;
        var velvetHidden = VelvetHidden;
        var count = velvetHidden && Array.IndexOf(order, NameplateStatus.Velvet) >= 0 ? order.Length - 1 : order.Length;
        SettingsSection.Header(Loc.T(L.Nameplate.Priority), theme);
        var card = GroupCard.Begin(theme, count);
        var shownIndex = 0;
        for (var index = 0; index < order.Length; index++)
        {
            if (velvetHidden && order[index] == NameplateStatus.Velvet)
            {
                continue;
            }

            DrawStatusRow(card.NextRow(), index, shownIndex, order[index], count, theme, scale);
            shownIndex++;
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(L.Nameplate.PriorityHint), theme);
    }

    private void DrawStatusRow(Rect row, int index, int shownIndex, NameplateStatus status, int count,
        PhoneTheme theme, float scale)
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

        if (SettingsReorder.Button(upCenter, radius, FontAwesomeIcon.ChevronUp, theme, shownIndex > 0))
        {
            moveIndex = index;
            moveDelta = -1;
        }

        if (SettingsReorder.Button(downCenter, radius, FontAwesomeIcon.ChevronDown, theme, shownIndex < count - 1))
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

        var settings = titles.Settings;
        settings.Move(moveIndex, moveDelta);
        if (VelvetHidden && settings.Order[moveIndex] == NameplateStatus.Velvet)
        {
            settings.Move(moveIndex + moveDelta, moveDelta);
        }

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
            var color = DrawSwatches(Loc.T(L.Nameplate.TextColor), settings.CustomColor, theme, scale);
            if (color >= 0)
            {
                settings.CustomColor = color;
                changed = true;
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            var glow = DrawSwatches(Loc.T(L.Nameplate.GlowColor), settings.CustomGlow, theme, scale);
            if (glow >= 0)
            {
                settings.CustomGlow = glow;
                changed = true;
            }
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

    private static int DrawSwatches(string header, int selected, PhoneTheme theme, float scale)
    {
        SettingsSection.Header(header, theme);
        var card = GroupCard.Begin(theme, 1);
        var row = card.NextRow();
        card.End();
        var colors = NameplatePalette.Colors;
        var radius = SwatchRadius * scale;
        var gap = SwatchGap * scale;
        var available = row.Width;
        var step = MathF.Min(radius * 2f + gap, available / colors.Length);
        var drawRadius = MathF.Min(radius, step * 0.5f - SwatchRing * scale);
        var startX = row.Min.X + (available - step * colors.Length) * 0.5f + step * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        var picked = -1;
        for (var index = 0; index < colors.Length; index++)
        {
            var center = new Vector2(startX + step * index, row.Center.Y);
            var min = center - new Vector2(drawRadius, drawRadius);
            var max = center + new Vector2(drawRadius, drawRadius);
            drawList.AddCircleFilled(center, drawRadius, ImGui.GetColorU32(new Vector4(colors[index], 1f)), 32);
            drawList.AddCircle(center, drawRadius, ImGui.GetColorU32(theme.Hairline), 32, scale);
            var hovered = UiInteract.Hover(min, max);
            if (index == selected)
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
                picked = index;
            }
        }

        return picked == selected ? -1 : picked;
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
