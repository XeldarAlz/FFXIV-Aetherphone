using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float SliderLabelWidth = 0.42f;
    private const float PopoutOpacityMinimum = 0.5f;
    private const float IdleOpacityMinimum = 0.15f;
    private const float OpacityEpsilon = 0.002f;
    private const float SettingsBottomPad = 24f;

    private static readonly float[] TextScaleChoices = { 0.8f, 0.9f, 1f, 1.15f, 1.3f, 1.5f };

    private readonly List<DropdownMenu.Item> settingsItems = new(6);

    private static readonly LinkpearlSettingsSection[] SettingsSections =
    {
        LinkpearlSettingsSection.Popouts, LinkpearlSettingsSection.Behavior, LinkpearlSettingsSection.Composer,
        LinkpearlSettingsSection.Channels, LinkpearlSettingsSection.History,
    };

    private static readonly Vector4[] SettingsSectionTints =
    {
        ChatListChrome.TintAzure, ChatListChrome.TintGold, ChatListChrome.TintTeal, ChatListChrome.TintViolet,
        ChatListChrome.TintGreen,
    };

    private void DrawSettingsTab(Rect area)
    {
        var scale = UiScale.Current;
        using (AppSurface.BeginEdgeToEdge(area))
        {
            var drawList = ImGui.GetWindowDrawList();
            chrome.DrawSectionLabel(Loc.T(L.Linkpearl.Appearance));
            if (chrome.DrawSettingRow(drawList, PhoneIcons.Palette, activeTheme.Accent, Loc.T(L.Message.ChatTheme),
                    Loc.T(activeTheme.Name)))
            {
                router.Push(LinkpearlRoute.ChatTheme);
            }

            if (chrome.DrawSettingRow(drawList, PhoneIcons.Wallpaper, ChatListChrome.TintViolet,
                    Loc.T(L.Message.Wallpaper)))
            {
                router.Push(LinkpearlRoute.Wallpaper(string.Empty));
            }

            var layoutRow = NextSettingRow(scale);
            var bubblesDefault = configuration.LinkpearlDefaultDensity == (int)ChatDensity.Bubbles;
            if (chrome.DrawSettingRow(drawList, PhoneIcons.LayoutList, ChatListChrome.TintAzure,
                    Loc.T(L.Linkpearl.DefaultLayout),
                    Loc.T(bubblesDefault ? L.Linkpearl.LayoutBubbles : L.Linkpearl.LayoutLog)))
            {
                settingsMenu.Toggle("linkpearl.settings.layout", layoutRow);
            }

            var textSizeRow = NextSettingRow(scale);
            if (chrome.DrawSettingRow(drawList, PhoneIcons.TextSize, ChatListChrome.TintTeal,
                    Loc.T(L.Linkpearl.TextSize), PercentLabel(configuration.LinkpearlTextScale)))
            {
                settingsMenu.Toggle("linkpearl.settings.textScale", textSizeRow);
            }

            DrawLogSwitch(drawList, PhoneIcons.Clock, ChatListChrome.TintSlate, L.Linkpearl.LogTimestamps,
                configuration.LinkpearlLogTimestamps, "linkpearl.settings.logTimestamps",
                static (configuration, value) => configuration.LinkpearlLogTimestamps = value);
            DrawLogSwitch(drawList, PhoneIcons.World, ChatListChrome.TintAzure, L.Linkpearl.LogWorldNames,
                configuration.LinkpearlLogWorldNames, "linkpearl.settings.logWorldNames",
                static (configuration, value) => configuration.LinkpearlLogWorldNames = value);
            DrawLogSwitch(drawList, PhoneIcons.Brush, ChatListChrome.TintGold, L.Linkpearl.LogGameColors,
                configuration.LinkpearlLogGameColors, "linkpearl.settings.logGameColors",
                static (configuration, value) => configuration.LinkpearlLogGameColors = value);
            DrawLogSwitch(drawList, PhoneIcons.Users, ChatListChrome.TintGreen, L.Linkpearl.LogGroupLines,
                configuration.LinkpearlLogGroupLines, "linkpearl.settings.logGroupLines",
                static (configuration, value) => configuration.LinkpearlLogGroupLines = value);
            DrawLogSwitch(drawList, PhoneIcons.Checks, ChatListChrome.TintViolet, L.Linkpearl.CollapseDuplicates,
                configuration.LinkpearlCollapseDuplicates, "linkpearl.settings.collapseDuplicates",
                static (configuration, value) => configuration.LinkpearlCollapseDuplicates = value, false);

            chrome.DrawSectionLabel(Loc.T(L.Settings.Notifications));
            var paused = chrome.DrawSwitchRow(drawList, PhoneIcons.BellOff, ChatListChrome.TintSlate,
                Loc.T(L.Messages.PauseNotifications), notificationGate.Paused, "linkpearl.settings.pause",
                separator: false);
            if (paused != notificationGate.Paused)
            {
                notificationGate.SetPaused(paused);
            }

            chrome.DrawSectionLabel(Loc.T(L.Settings.Privacy));
            var screenshotMode = chrome.DrawSwitchRow(drawList, PhoneIcons.Eye, ChatListChrome.TintSlate,
                Loc.T(L.Linkpearl.ScreenshotMode), NameMask.Enabled, "linkpearl.settings.screenshotMode",
                separator: false);
            if (screenshotMode != NameMask.Enabled)
            {
                NameMask.Set(screenshotMode);
                ChatRuns.Reset();
                RunText.Reset();
            }

            chrome.DrawSectionLabel(Loc.T(L.Linkpearl.ChatSettings));
            for (var index = 0; index < SettingsSections.Length; index++)
            {
                var section = SettingsSections[index];
                if (chrome.DrawSettingRow(drawList, SectionGlyph(section), SettingsSectionTints[index],
                        Loc.T(SectionTitle(section)), separator: index < SettingsSections.Length - 1))
                {
                    router.Push(LinkpearlRoute.SettingsFor(section));
                }
            }

            ImGui.Dummy(new Vector2(0f, SettingsBottomPad * scale));
        }

        DrawSettingsMenu(area);
    }

    private static Rect NextSettingRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        return new Rect(origin,
            new Vector2(origin.X + ScrollLayout.StableContentWidth(), origin.Y + ChatListChrome.SettingRowHeight * scale));
    }

    private void DrawLogSwitch(ImDrawListPtr drawList, string glyph, Vector4 tint, LocString label, bool value,
        string id, Action<Configuration, bool> apply, bool separator = true)
    {
        var next = chrome.DrawSwitchRow(drawList, glyph, tint, Loc.T(label), value, id, separator);
        if (next == value)
        {
            return;
        }

        apply(configuration, next);
        configuration.Save();
    }

    private void DrawSettingsSection(Rect area, LinkpearlSettingsSection section)
    {
        var scale = UiScale.Current;
        chrome.DrawScreenHeader(area, Loc.T(SectionTitle(section)), backToSettings);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            switch (section)
            {
                case LinkpearlSettingsSection.Popouts:
                    DrawPopoutSettings(scale);
                    break;
                case LinkpearlSettingsSection.Behavior:
                    DrawBehaviorSettings(scale);
                    break;
                case LinkpearlSettingsSection.Composer:
                    DrawComposerSettings(scale);
                    break;
                case LinkpearlSettingsSection.Channels:
                    DrawChannelSettings(scale);
                    break;
                default:
                    DrawHistorySettings(scale);
                    break;
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }

        DrawSettingsMenu(area);
    }

    private static string SectionGlyph(LinkpearlSettingsSection section) => section switch
    {
        LinkpearlSettingsSection.Popouts => PhoneIcons.ExternalLink,
        LinkpearlSettingsSection.Behavior => PhoneIcons.AdjustmentsHorizontal,
        LinkpearlSettingsSection.Composer => PhoneIcons.Pencil,
        LinkpearlSettingsSection.Channels => PhoneIcons.Hash,
        _ => PhoneIcons.Clock,
    };

    private static LocString SectionTitle(LinkpearlSettingsSection section) => section switch
    {
        LinkpearlSettingsSection.Popouts => L.Linkpearl.PopoutSection,
        LinkpearlSettingsSection.Behavior => L.Linkpearl.BehaviorSection,
        LinkpearlSettingsSection.Composer => L.Linkpearl.ComposerSection,
        LinkpearlSettingsSection.Channels => L.Linkpearl.ChannelStyleSection,
        _ => L.Linkpearl.KeepHistory,
    };

    private void DrawPopoutSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.PopoutSection), frameTheme);
        var behaviour = GroupCard.Begin(frameTheme, 5);
        var grouped = SettingsRow.Bool(behaviour.NextRow(), Loc.T(L.Linkpearl.PopoutTabs),
            configuration.LinkpearlPopoutTabs, frameTheme, "linkpearl.settings.popoutTabs");
        if (grouped != configuration.LinkpearlPopoutTabs)
        {
            configuration.LinkpearlPopoutTabs = grouped;
            configuration.Save();
        }

        var popTells = SettingsRow.Bool(behaviour.NextRow(), Loc.T(L.Linkpearl.PopoutTells),
            configuration.LinkpearlPopoutTells, frameTheme, "linkpearl.settings.popTells");
        if (popTells != configuration.LinkpearlPopoutTells)
        {
            configuration.LinkpearlPopoutTells = popTells;
            configuration.Save();
        }

        var outgoing = SettingsRow.Bool(behaviour.NextRow(), Loc.T(L.Linkpearl.PopoutOutgoingTells),
            configuration.LinkpearlPopoutOutgoingTells, frameTheme, "linkpearl.settings.outgoingTells", null,
            !popTells);
        if (outgoing != configuration.LinkpearlPopoutOutgoingTells)
        {
            configuration.LinkpearlPopoutOutgoingTells = outgoing;
            configuration.Save();
        }

        var closeOnLogout = SettingsRow.Bool(behaviour.NextRow(), Loc.T(L.Linkpearl.PopoutCloseOnLogout),
            configuration.LinkpearlPopoutCloseOnLogout, frameTheme, "linkpearl.settings.closeOnLogout");
        if (closeOnLogout != configuration.LinkpearlPopoutCloseOnLogout)
        {
            configuration.LinkpearlPopoutCloseOnLogout = closeOnLogout;
            configuration.Save();
        }

        var flash = SettingsRow.Bool(behaviour.NextRow(), Loc.T(L.Linkpearl.PopoutFlash),
            configuration.LinkpearlPopoutFlash, frameTheme, "linkpearl.settings.popoutFlash");
        if (flash != configuration.LinkpearlPopoutFlash)
        {
            configuration.LinkpearlPopoutFlash = flash;
            configuration.Save();
        }

        behaviour.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.PopoutTabsHint), frameTheme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var fade = configuration.LinkpearlPopoutFade;
        var look = GroupCard.Begin(frameTheme, fade ? 4 : 3);
        DrawOpacityRow(look.NextRow(), scale);
        var nextFade = SettingsRow.Bool(look.NextRow(), Loc.T(L.Linkpearl.PopoutFade), fade, frameTheme,
            "linkpearl.settings.popoutFade");
        if (nextFade != fade)
        {
            configuration.LinkpearlPopoutFade = nextFade;
            configuration.Save();
        }

        if (fade)
        {
            DrawIdleOpacityRow(look.NextRow(), scale);
        }

        var textSizeRow = look.NextRow();
        if (SettingsRow.Disclosure(textSizeRow, Loc.T(L.Linkpearl.PopoutTextSize),
                PercentLabel(configuration.LinkpearlPopoutTextScale), frameTheme, "linkpearl.settings.textSize"))
        {
            settingsMenu.Toggle("linkpearl.settings.textSize", textSizeRow);
        }

        look.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.PopoutHint), frameTheme);
        if (popouts.OpenCount == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var closeCard = GroupCard.Begin(frameTheme, 2);
        var anyExpanded = popouts.AnyExpanded;
        if (SettingsRow.Action(closeCard.NextRow(),
                Loc.T(anyExpanded ? L.Linkpearl.CollapseAllPopouts : L.Linkpearl.ExpandAllPopouts, popouts.OpenCount),
                frameTheme.Accent, frameTheme))
        {
            popouts.SetAllCollapsed(anyExpanded);
        }

        if (SettingsRow.Action(closeCard.NextRow(), Loc.T(L.Linkpearl.CloseAllPopouts, popouts.OpenCount),
                frameTheme.Accent, frameTheme))
        {
            popouts.CloseAll();
        }

        closeCard.End();
    }

    private void DrawOpacityRow(Rect row, float scale)
    {
        configuration.LinkpearlPopoutOpacity = DrawOpacitySlider(row, scale, "linkpearl.settings.opacity",
            Loc.T(L.Linkpearl.PopoutOpacity), configuration.LinkpearlPopoutOpacity, PopoutOpacityMinimum,
            out var released);
        if (released)
        {
            configuration.Save();
        }
    }

    private void DrawIdleOpacityRow(Rect row, float scale)
    {
        configuration.LinkpearlPopoutIdleOpacity = DrawOpacitySlider(row, scale, "linkpearl.settings.idleOpacity",
            Loc.T(L.Linkpearl.PopoutIdleOpacity), configuration.LinkpearlPopoutIdleOpacity, IdleOpacityMinimum,
            out var released);
        if (released)
        {
            configuration.Save();
        }
    }

    private float DrawOpacitySlider(Rect row, float scale, string id, string label, float value, float minimum,
        out bool released)
    {
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        var labelWidth = row.Width * SliderLabelWidth;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f),
            Typography.FitText(label, labelWidth, TextStyles.BodyEmphasized), frameTheme.TextStrong,
            TextStyles.BodyEmphasized);
        var span = 1f - minimum;
        var normalized = (Math.Clamp(value, minimum, 1f) - minimum) / span;
        var result = Slider.Draw(id, row, normalized, frameTheme, labelWidth + Metrics.Space.Md * scale,
            Metrics.Space.Xs * scale);
        released = result.Released;
        var next = minimum + result.Value * span;
        return MathF.Abs(next - value) > OpacityEpsilon ? next : value;
    }

    private void DrawHistorySettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.KeepHistory), frameTheme);
        var card = GroupCard.Begin(frameTheme, 2);
        var stored = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.StoreHistory), configuration.ArchiveTellsToDisk,
            frameTheme, "linkpearl.settings.store");
        if (stored != configuration.ArchiveTellsToDisk)
        {
            configuration.ArchiveTellsToDisk = stored;
            configuration.Save();
        }

        var defaultPolicy = (HistoryPolicy)Math.Clamp(configuration.LinkpearlHistory, 0, (int)HistoryPolicy.Forever);
        var historyRow = card.NextRow();
        if (SettingsRow.Disclosure(historyRow, Loc.T(L.Linkpearl.HistoryDefault),
                Loc.T(HistoryLabelFor(defaultPolicy)), frameTheme, "linkpearl.settings.history",
                !configuration.ArchiveTellsToDisk))
        {
            settingsMenu.Toggle("linkpearl.settings.history", historyRow);
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.StoredOnThisPc), frameTheme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var exportCard = GroupCard.Begin(frameTheme, 1);
        if (SettingsRow.Action(exportCard.NextRow(), Loc.T(L.Linkpearl.ExportHistory), frameTheme.Accent, frameTheme))
        {
            ShellToast.Show(Loc.T(archive.Export() is null ? L.Linkpearl.ExportFailed : L.Linkpearl.ExportedHistory));
        }

        exportCard.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        var dangerCard = GroupCard.Begin(frameTheme, 1);
        if (SettingsRow.Action(dangerCard.NextRow(), Loc.T(L.Linkpearl.ClearAllHistory), frameTheme.Danger, frameTheme))
        {
            AskClearAllHistory();
        }

        dangerCard.End();
    }

    private void DrawSettingsMenu(Rect area)
    {
        if (settingsMenu.IsOpenFor("linkpearl.settings.layout"))
        {
            settingsItems.Clear();
            var bubbles = configuration.LinkpearlDefaultDensity == (int)ChatDensity.Bubbles;
            settingsItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.LayoutLog), string.Empty, false, !bubbles));
            settingsItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.LayoutBubbles), string.Empty, false, bubbles));
            var pickedLayout = settingsMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(settingsItems));
            if (pickedLayout >= 0)
            {
                configuration.LinkpearlDefaultDensity = pickedLayout == 1
                    ? (int)ChatDensity.Bubbles
                    : (int)ChatDensity.Log;
                configuration.Save();
            }

            return;
        }

        if (settingsMenu.IsOpenFor("linkpearl.settings.textScale"))
        {
            settingsItems.Clear();
            for (var index = 0; index < TextScaleChoices.Length; index++)
            {
                settingsItems.Add(new DropdownMenu.Item(PercentLabel(TextScaleChoices[index]), string.Empty, false,
                    MathF.Abs(TextScaleChoices[index] - configuration.LinkpearlTextScale) < 0.01f));
            }

            var pickedScale = settingsMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(settingsItems));
            if (pickedScale >= 0)
            {
                configuration.LinkpearlTextScale = TextScaleChoices[pickedScale];
                configuration.Save();
            }

            return;
        }

        if (settingsMenu.IsOpenFor("linkpearl.settings.textSize"))
        {
            settingsItems.Clear();
            for (var index = 0; index < TextScaleChoices.Length; index++)
            {
                settingsItems.Add(new DropdownMenu.Item(PercentLabel(TextScaleChoices[index]), string.Empty, false,
                    MathF.Abs(TextScaleChoices[index] - configuration.LinkpearlPopoutTextScale) < 0.01f));
            }

            var picked = settingsMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(settingsItems));
            if (picked >= 0)
            {
                configuration.LinkpearlPopoutTextScale = TextScaleChoices[picked];
                configuration.Save();
            }

            return;
        }

        if (!settingsMenu.IsOpenFor("linkpearl.settings.history"))
        {
            return;
        }

        settingsItems.Clear();
        var current = (HistoryPolicy)Math.Clamp(configuration.LinkpearlHistory, 0, (int)HistoryPolicy.Forever);
        for (var index = 0; index < HistoryChoices.Length; index++)
        {
            settingsItems.Add(new DropdownMenu.Item(Loc.T(HistoryLabelFor(HistoryChoices[index])), string.Empty, false,
                current == HistoryChoices[index]));
        }

        var choice = settingsMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(settingsItems));
        if (choice < 0)
        {
            return;
        }

        configuration.LinkpearlHistory = (int)HistoryChoices[choice];
        configuration.Save();
    }

    private void AskClearAllHistory() =>
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Linkpearl.ClearAllHistory),
            Message = Loc.T(L.Linkpearl.ClearAllHistoryConfirm),
            ConfirmLabel = Loc.T(L.Linkpearl.ClearHistory),
            CancelLabel = Loc.T(L.Messages.DeleteHistoryCancel),
            Sheet = true,
            Confirm = () =>
            {
                popouts.CloseAll();
                archive.DeleteAll();
                chatLog.Clear();
                inbox.Invalidate();
                threadKey = string.Empty;
            },
        });

    private static string PercentLabel(float value) =>
        string.Concat(MathF.Round(value * 100f).ToString(Loc.Culture), "%");
}
