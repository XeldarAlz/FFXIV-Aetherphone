using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Localization;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NameplateStatusPage : ISettingsPage
{
    private const int MaxLines = 6;
    private const float RemoveRadius = 10f;
    private static readonly string[] LineIds = BuildLineIds();

    private const int MaxChildren = 4;

    private static readonly string[] ChildIds = BuildChildIds();

    private readonly NameplateTitleService titles;
    private readonly Configuration configuration;
    private readonly PcMediaSource pcMedia;
    private readonly ISettingsNavigator navigator;
    private readonly NameplateStatusPage? childPage;
    private readonly NameplateStage stage = new();
    private readonly string[] handleLabels = new string[3];
    private readonly string[] longTitleLabels = new string[2];
    private NameplateStatus status = NameplateStatus.Jam;
    private readonly string[] lines = new string[MaxLines];
    private int lineCount = 1;
    private bool editing;
    private string sampleTemplate = string.Empty;
    private NameplateTitle sample = NameplateTitle.None;
    private long sampleTick;
    private int turnSecondsShown;
    private string turnSecondsTemplate = string.Empty;
    private string turnSecondsLine = string.Empty;
    private string lengthSource = string.Empty;
    private string lengthTemplate = string.Empty;
    private string lengthLine = string.Empty;
    private string tokensTemplate = string.Empty;
    private string tokensLine = string.Empty;

    public NameplateStatusPage(NameplateTitleService titles, Configuration configuration, PcMediaSource pcMedia,
        ISettingsNavigator navigator, NameplateStatusPage? childPage)
    {
        this.titles = titles;
        this.configuration = configuration;
        this.pcMedia = pcMedia;
        this.navigator = navigator;
        this.childPage = childPage;
    }

    public string Title => Loc.T(NameplateStatusCatalog.For(status).Label);

    public string Summary => string.Empty;

    public FontAwesomeIcon Icon => NameplateStatusCatalog.For(status).Icon;

    public Vector4 Tint => NameplateStatusCatalog.For(status).Tint;

    public bool IsHidden => true;

    public void Show(NameplateStatus shownStatus)
    {
        status = shownStatus;
        LoadLines(titles.Settings.Template(shownStatus));
        editing = false;
        sampleTemplate = string.Empty;
        tokensTemplate = string.Empty;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawPreview(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawShowSwitch(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawText(theme, scale);
            DrawExtras(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void DrawPreview(PhoneTheme theme, float scale)
    {
        var template = titles.TemplateFor(status);
        var tick = status == NameplateStatus.NowPlaying ? titles.TurnTick : 0L;
        if (!ReferenceEquals(template, sampleTemplate) || tick != sampleTick)
        {
            sampleTemplate = template;
            sampleTick = tick;
            sample = titles.Sample(status);
        }

        var name = titles.CharacterName.Length > 0 ? titles.CharacterName : Loc.T(L.Nameplate.Example);
        stage.Draw(theme, scale, name, sample);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(LengthLine(), theme);
    }

    private void DrawShowSwitch(PhoneTheme theme)
    {
        ref readonly var info = ref NameplateStatusCatalog.For(status);
        var settings = titles.Settings;
        var shown = settings.Shows(status);
        var card = GroupCard.Begin(theme, 1);
        var next = SettingsRow.Switch(card.NextRow(), info.Icon, info.Tint, Loc.T(L.Nameplate.Show), shown, theme,
            Loc.T(info.Hint), "nameplate.detail.show");
        card.End();
        if (next == shown)
        {
            return;
        }

        settings.Set(status, next);
        titles.Commit();
    }

    private void DrawText(PhoneTheme theme, float scale)
    {
        var custom = status == NameplateStatus.Custom;
        var multiLine = status == NameplateStatus.NowPlaying;
        SettingsSection.Header(Loc.T(L.Nameplate.Text), theme);
        DrawLines(theme, scale, custom, multiLine);
        if (multiLine)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            if (SettingsForm.ActionCard(Loc.T(L.Nameplate.AddLine), theme.Accent, theme, lineCount < MaxLines))
            {
                AddLine();
            }
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var hint = custom ? L.Nameplate.CustomHint : multiLine ? L.Nameplate.LinesHint : L.Nameplate.TextHint;
        SettingsSection.Hint(Loc.T(hint), theme);
        var tokens = TokensLine();
        if (tokens.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            SettingsSection.Hint(tokens, theme);
        }

        if (custom || titles.Settings.Template(status).Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        var resetCard = GroupCard.Begin(theme, 1);
        var reset = SettingsRow.Action(resetCard.NextRow(), Loc.T(L.Nameplate.Reset), theme.Accent, theme);
        resetCard.End();
        if (!reset)
        {
            return;
        }

        LoadLines(string.Empty);
        titles.Settings.SetTemplate(status, string.Empty);
        titles.Commit();
    }

    private void DrawLines(PhoneTheme theme, float scale, bool custom, bool multiLine)
    {
        var removable = multiLine && lineCount > 1;
        var radius = RemoveRadius * scale;
        var gap = Metrics.Space.Sm * scale;
        var reserve = removable ? radius * 2f + gap : 0f;
        var changed = false;
        var active = false;
        var removeIndex = -1;
        for (var index = 0; index < lineCount; index++)
        {
            if (index > 0)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            }

            var hint = index > 0 ? Loc.T(L.Nameplate.LinePlaceholder)
                : custom ? Loc.T(L.Nameplate.CustomPlaceholder) : NameplateTitleService.DefaultTemplate(status);
            changed |= SettingsForm.TextField(LineIds[index], hint, ref lines[index], theme,
                NameplateTitleSettings.MaxTemplateLength, ImGuiInputTextFlags.None, out var lineActive, reserve);
            active |= lineActive;
            if (!removable)
            {
                continue;
            }

            var fieldMin = ImGui.GetItemRectMin();
            var fieldMax = ImGui.GetItemRectMax();
            var center = new Vector2(fieldMax.X + gap + radius, (fieldMin.Y + fieldMax.Y) * 0.5f);
            if (SettingsReorder.Button(center, radius, FontAwesomeIcon.Times, theme, true))
            {
                removeIndex = index;
            }
        }

        if (changed)
        {
            StoreLines();
            titles.Refresh();
        }

        if (editing && !active)
        {
            titles.Commit();
        }

        editing = active;
        if (removeIndex < 0)
        {
            return;
        }

        Array.Copy(lines, removeIndex + 1, lines, removeIndex, lineCount - removeIndex - 1);
        lineCount--;
        lines[lineCount] = string.Empty;
        StoreLines();
        titles.Commit();
    }

    private void AddLine()
    {
        if (lineCount >= MaxLines)
        {
            return;
        }

        if (AllLinesEmpty())
        {
            lines[0] = NameplateTitleService.DefaultTemplate(status);
        }

        lines[lineCount] = string.Empty;
        lineCount++;
        StoreLines();
        titles.Commit();
    }

    private bool AllLinesEmpty()
    {
        for (var index = 0; index < lineCount; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void LoadLines(string template)
    {
        Array.Fill(lines, string.Empty);
        var parts = template.Split(NameplateTitleText.LineBreak);
        lineCount = Math.Clamp(parts.Length, 1, MaxLines);
        for (var index = 0; index < lineCount && index < parts.Length; index++)
        {
            lines[index] = parts[index];
        }
    }

    private void StoreLines() =>
        titles.Settings.SetTemplate(status, string.Join(NameplateTitleText.LineBreak, lines, 0, lineCount));

    private static string[] BuildLineIds()
    {
        var ids = new string[MaxLines];
        for (var index = 0; index < MaxLines; index++)
        {
            ids[index] = "##nameplate.line" + index;
        }

        return ids;
    }

    private void DrawExtras(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        if (status == NameplateStatus.NowPlaying)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            var card = GroupCard.Begin(theme, 1);
            var windowsMediaOff = !configuration.ShowWindowsMedia;
            var pcMedia = SettingsRow.Bool(card.NextRow(), Loc.T(L.Nameplate.PcMedia), settings.IncludePcMedia, theme,
                "nameplate.pcMedia", windowsMediaOff ? Loc.T(L.Nameplate.PcMediaHint) : null, windowsMediaOff);
            card.End();
            if (pcMedia != settings.IncludePcMedia)
            {
                settings.IncludePcMedia = pcMedia;
                titles.Commit();
            }

            if (settings.IncludePcMedia && configuration.ShowWindowsMedia)
            {
                MusicMediaSettings.DrawSource(this.pcMedia, theme);
            }

            DrawLongTitles(theme, scale);
            return;
        }

        if (status == NameplateStatus.Gamba)
        {
            DrawChildren(theme, scale);
            return;
        }

        if (status != NameplateStatus.Handle)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Nameplate.HandleApp), theme);
        handleLabels[0] = Loc.T(L.Apps.Chirper);
        handleLabels[1] = Loc.T(L.Apps.Aethergram);
        handleLabels[2] = Loc.T(L.Apps.Velvet);
        var appCard = GroupCard.Begin(theme, 1);
        var picked = SegmentStrip.Draw("nameplate.handleApp", appCard.NextRow(), handleLabels, (int)settings.HandleApp,
            theme);
        appCard.End();
        if (picked == (int)settings.HandleApp)
        {
            return;
        }

        settings.HandleApp = (NameplateHandleApp)picked;
        sampleTemplate = string.Empty;
        titles.Commit();
    }

    private void DrawChildren(PhoneTheme theme, float scale)
    {
        Span<NameplateStatus> children = stackalloc NameplateStatus[MaxChildren];
        var count = NameplateStatusCatalog.ChildrenOf(status, children);
        if (count == 0 || childPage is null)
        {
            return;
        }

        var settings = titles.Settings;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Casino.GameSlots), theme);
        var card = GroupCard.Begin(theme, count);
        for (var index = 0; index < count; index++)
        {
            var child = children[index];
            ref readonly var info = ref NameplateStatusCatalog.For(child);
            var value = Loc.T(settings.Shows(status) && settings.Shows(child) ? L.Common.On : L.Common.Off);
            if (!SettingsRow.Link(card.NextRow(), info.Icon, info.Tint, Loc.T(info.Label), value, theme,
                    id: ChildIds[NameplateStatusCatalog.IndexOf(child)]))
            {
                continue;
            }

            childPage.Show(child);
            navigator.Open(childPage);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(L.Nameplate.GambaSlotsHint), theme);
    }

    private void DrawLongTitles(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        var takeTurns = settings.LongTitles == NameplateLongTitles.TakeTurns;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Nameplate.LongTitles), theme);
        longTitleLabels[0] = Loc.T(L.Nameplate.TakeTurns);
        longTitleLabels[1] = Loc.T(L.Nameplate.Shorten);
        var rotates = takeTurns || lineCount > 1;
        var card = GroupCard.Begin(theme, rotates ? 3 : 1);
        var picked = SegmentStrip.Draw("nameplate.longTitles", card.NextRow(), longTitleLabels,
            (int)settings.LongTitles, theme);
        if (rotates)
        {
            DrawTurnSeconds(ref card, theme);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(takeTurns ? L.Nameplate.TakeTurnsHint : L.Nameplate.ShortenHint), theme);
        if (picked == (int)settings.LongTitles)
        {
            return;
        }

        settings.LongTitles = (NameplateLongTitles)picked;
        sampleTemplate = string.Empty;
        titles.Commit();
    }

    private void DrawTurnSeconds(ref GroupCard card, PhoneTheme theme)
    {
        const float smallest = NameplateTitleSettings.MinimumTurnSeconds;
        const float span = NameplateTitleSettings.MaximumTurnSeconds - NameplateTitleSettings.MinimumTurnSeconds;
        var settings = titles.Settings;
        SettingsRow.Info(card.NextRow(), Loc.T(L.Nameplate.SwitchEvery), TurnSecondsLine(settings.TurnSeconds),
            theme, "nameplate.switchEvery");
        var slider = Slider.Draw("nameplate.turnSeconds", card.NextRow(), (settings.TurnSeconds - smallest) / span,
            theme, 0f, 0f);
        var seconds = (int)MathF.Round(smallest + slider.Value * span);
        if ((slider.Dragging || slider.Released) && seconds != settings.TurnSeconds)
        {
            settings.TurnSeconds = seconds;
        }

        if (slider.Released)
        {
            titles.Commit();
        }
    }

    private string TurnSecondsLine(int seconds)
    {
        var template = Loc.T(L.Nameplate.TurnSeconds);
        if (seconds != turnSecondsShown || !ReferenceEquals(template, turnSecondsTemplate))
        {
            turnSecondsShown = seconds;
            turnSecondsTemplate = template;
            turnSecondsLine = string.Format(template, seconds);
        }

        return turnSecondsLine;
    }

    private string LengthLine()
    {
        var template = Loc.T(L.Nameplate.Length);
        if (!ReferenceEquals(sample.Text, lengthSource) || !ReferenceEquals(template, lengthTemplate))
        {
            lengthSource = sample.Text;
            lengthTemplate = template;
            lengthLine = string.Format(template, sample.Text.Length, NameplateTitleText.MaxLength);
        }

        return lengthLine;
    }

    private string TokensLine()
    {
        var tokens = NameplateStatusCatalog.For(status).Tokens;
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        var template = Loc.T(L.Nameplate.Tokens);
        if (!ReferenceEquals(template, tokensTemplate))
        {
            tokensTemplate = template;
            tokensLine = string.Format(template, tokens);
        }

        return tokensLine;
    }

    private static string[] BuildChildIds()
    {
        var ids = new string[NameplateStatusCatalog.All.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = "nameplate.child." + NameplateStatusCatalog.All[index].Status;
        }

        return ids;
    }
}
