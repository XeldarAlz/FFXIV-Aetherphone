using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const int ComposerMinimumLines = 2;
    private const int ComposerMaximumLines = 8;
    private const int SplitIntervalMinimum = 250;
    private const int SplitIntervalMaximum = 5000;
    private const int SplitIntervalStep = 250;
    private const int SplitIndicatorMaxLength = 8;
    private const int RecentSentRows = 8;

    private static readonly string[] SentRowIds =
    {
        "linkpearl.settings.sent0", "linkpearl.settings.sent1", "linkpearl.settings.sent2",
        "linkpearl.settings.sent3", "linkpearl.settings.sent4", "linkpearl.settings.sent5",
        "linkpearl.settings.sent6", "linkpearl.settings.sent7",
    };

    private readonly List<string> sentPreviews = new(RecentSentRows);
    private string splitIndicatorDraft = string.Empty;
    private bool splitIndicatorActive;
    private long sentPreviewStamp = -1;
    private int sentPreviewCount = -1;

    private void DrawComposerSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.ComposerSection), frameTheme);
        var card = GroupCard.Begin(frameTheme, 4);
        var multiline = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.ComposerMultiline),
            configuration.LinkpearlComposerMultiline, frameTheme, "linkpearl.settings.multiline",
            Loc.T(L.Linkpearl.ComposerMultilineHint));
        if (multiline != configuration.LinkpearlComposerMultiline)
        {
            configuration.LinkpearlComposerMultiline = multiline;
            configuration.Save();
        }

        DrawMaxLinesRow(card.NextRow(), scale);
        var doubleEnter = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.ComposerDoubleEnter),
            configuration.LinkpearlDoubleEnterSend, frameTheme, "linkpearl.settings.doubleEnter",
            Loc.T(L.Linkpearl.ComposerDoubleEnterHint));
        if (doubleEnter != configuration.LinkpearlDoubleEnterSend)
        {
            configuration.LinkpearlDoubleEnterSend = doubleEnter;
            configuration.Save();
        }

        var autosave = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.DraftAutosave),
            configuration.LinkpearlDraftAutosave, frameTheme, "linkpearl.settings.drafts",
            Loc.T(L.Linkpearl.DraftHint));
        if (autosave != configuration.LinkpearlDraftAutosave)
        {
            configuration.LinkpearlDraftAutosave = autosave;
            if (!autosave)
            {
                ChatDrafts.Forget();
            }

            configuration.Save();
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.ComposerHint), frameTheme);
        DrawSplitSettings(scale);
        DrawEmojiSettings();
        DrawRecentSentSettings(scale);
    }

    private void DrawEmojiSettings()
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.EmojiSection), frameTheme);
        var card = GroupCard.Begin(frameTheme, 2);
        var shortcodes = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.EmojiShortcodes),
            configuration.LinkpearlEmojiShortcodes, frameTheme, "linkpearl.settings.emojiShortcodes");
        if (shortcodes != configuration.LinkpearlEmojiShortcodes)
        {
            configuration.LinkpearlEmojiShortcodes = shortcodes;
            configuration.Save();
            ChatRuns.Reset();
            RunText.Reset();
        }

        var picker = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.EmojiPickerRow),
            configuration.LinkpearlEmojiPicker, frameTheme, "linkpearl.settings.emojiPicker");
        if (picker != configuration.LinkpearlEmojiPicker)
        {
            configuration.LinkpearlEmojiPicker = picker;
            configuration.Save();
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.EmojiShortcodesHint), frameTheme);
    }

    private void DrawSplitSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.SplitSection), frameTheme);
        var card = GroupCard.Begin(frameTheme, 3);
        var split = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.SplitLongMessages),
            configuration.LinkpearlSplitLongMessages, frameTheme, "linkpearl.settings.split");
        if (split != configuration.LinkpearlSplitLongMessages)
        {
            configuration.LinkpearlSplitLongMessages = split;
            configuration.Save();
        }

        DrawIndicatorRow(card.NextRow(), scale);
        DrawSplitIntervalRow(card.NextRow(), scale);
        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.SplitHint), frameTheme);
    }

    private void DrawMaxLinesRow(Rect row, float scale)
    {
        var lines = Math.Clamp(configuration.LinkpearlComposerMaxLines, ComposerMinimumLines, ComposerMaximumLines);
        var span = ComposerMaximumLines - ComposerMinimumLines;
        var result = DrawLabeledSlider(row, "linkpearl.settings.maxLines", Loc.T(L.Linkpearl.ComposerMaxLines),
            lines.ToString(Loc.Culture), (float)(lines - ComposerMinimumLines) / span, scale);
        var next = ComposerMinimumLines + (int)MathF.Round(result.Value * span);
        if (next != configuration.LinkpearlComposerMaxLines)
        {
            configuration.LinkpearlComposerMaxLines = next;
        }

        if (result.Released)
        {
            configuration.Save();
        }
    }

    private void DrawSplitIntervalRow(Rect row, float scale)
    {
        var interval = Math.Clamp(configuration.LinkpearlSplitIntervalMilliseconds, SplitIntervalMinimum,
            SplitIntervalMaximum);
        var span = SplitIntervalMaximum - SplitIntervalMinimum;
        var label = Loc.T(L.Linkpearl.SplitIntervalValue, (interval / 1000f).ToString("0.##", Loc.Culture));
        var result = DrawLabeledSlider(row, "linkpearl.settings.splitInterval", Loc.T(L.Linkpearl.SplitInterval),
            label, (float)(interval - SplitIntervalMinimum) / span, scale);
        var steps = (int)MathF.Round(result.Value * span / SplitIntervalStep);
        var next = SplitIntervalMinimum + steps * SplitIntervalStep;
        if (next != configuration.LinkpearlSplitIntervalMilliseconds)
        {
            configuration.LinkpearlSplitIntervalMilliseconds = next;
        }

        if (result.Released)
        {
            configuration.Save();
        }
    }

    private void DrawIndicatorRow(Rect row, float scale)
    {
        var labelWidth = row.Width * LinkpearlSettingRows.SliderLabelWidth;
        DrawSliderLabel(row, Loc.T(L.Linkpearl.SplitIndicator), labelWidth, scale);
        if (!splitIndicatorActive)
        {
            splitIndicatorDraft = configuration.LinkpearlSplitIndicator;
        }

        var fieldLeft = row.Min.X + labelWidth + Metrics.Space.Md * scale;
        ImGui.SetCursorScreenPos(new Vector2(fieldLeft, row.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, row.Max.X - fieldLeft));
        Plugin.Fonts.NoticeText(splitIndicatorDraft);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.InputText("##linkpearl.settings.indicator", ref splitIndicatorDraft, SplitIndicatorMaxLength);
        }

        splitIndicatorActive = ImGui.IsItemActive();
        if (!ImGui.IsItemDeactivatedAfterEdit())
        {
            return;
        }

        configuration.LinkpearlSplitIndicator = splitIndicatorDraft.Trim();
        configuration.Save();
    }

    private Slider.Result DrawLabeledSlider(Rect row, string id, string label, string value, float normalized,
        float scale)
    {
        var labelWidth = row.Width * LinkpearlSettingRows.SliderLabelWidth;
        var valueSize = Typography.Measure(value, TextStyles.Caption1);
        DrawSliderLabel(row, label, labelWidth - valueSize.X - Metrics.Space.Sm * scale, scale);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(row.Min.X + labelWidth - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value,
            frameTheme.TextMuted, TextStyles.Caption1);
        return Slider.Draw(id, row, normalized, frameTheme, labelWidth + Metrics.Space.Md * scale,
            Metrics.Space.Xs * scale);
    }

    private void DrawSliderLabel(Rect row, string label, float available, float scale)
    {
        var size = Typography.Measure(label, TextStyles.BodyEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - size.Y * 0.5f),
            Typography.FitText(label, MathF.Max(1f, available), TextStyles.BodyEmphasized), frameTheme.TextStrong,
            TextStyles.BodyEmphasized);
    }

    private void DrawRecentSentSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.RecentSent), frameTheme);
        var recent = configuration.LinkpearlRecentSent;
        if (recent.Count == 0)
        {
            SettingsSection.Hint(Loc.T(L.Linkpearl.RecentSentEmpty), frameTheme);
            return;
        }

        SyncSentPreviews(recent);
        var card = GroupCard.Begin(frameTheme, sentPreviews.Count);
        for (var index = 0; index < sentPreviews.Count; index++)
        {
            DrawSentRow(card.NextRow(), recent[index], sentPreviews[index], index, scale);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var clearCard = GroupCard.Begin(frameTheme, 1);
        if (SettingsRow.Action(clearCard.NextRow(), Loc.T(L.Linkpearl.RecentSentClear), frameTheme.Danger,
                frameTheme))
        {
            ChatDrafts.ClearRecent();
            sentPreviews.Clear();
            sentPreviewCount = -1;
            sentPreviewStamp = -1;
        }

        clearCard.End();
    }

    private void DrawSentRow(Rect row, SentMessage entry, string preview, int index, float scale)
    {
        var stamp = TimeText.Ago(entry.SentAt);
        var stampSize = Typography.Measure(stamp, TextStyles.Caption1);
        if (SettingsRow.Selectable(row, preview, false, frameTheme, SentRowIds[index],
                stampSize.X + Metrics.Space.Md * scale, true))
        {
            ImGui.SetClipboardText(entry.Text);
            ShellToast.Show();
        }

        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(row.Max.X - stampSize.X, row.Center.Y - stampSize.Y * 0.5f), stamp, frameTheme.TextMuted,
            TextStyles.Caption1);
    }

    private void SyncSentPreviews(List<SentMessage> recent)
    {
        var head = recent.Count > 0 ? recent[0].SentAt : 0L;
        if (sentPreviewCount == recent.Count && sentPreviewStamp == head)
        {
            return;
        }

        sentPreviewCount = recent.Count;
        sentPreviewStamp = head;
        sentPreviews.Clear();
        var rows = Math.Min(recent.Count, RecentSentRows);
        for (var index = 0; index < rows; index++)
        {
            sentPreviews.Add(Flatten(recent[index].Text));
        }
    }

    private static string Flatten(string text) => text.IndexOf('\n') < 0 ? text : text.Replace('\n', ' ');
}
