using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal readonly struct GameChatTarget
{
    public readonly string Key;
    public readonly string[] Streams;
    public readonly string[] SendChannels;
    public readonly string SendChannelKey;
    public readonly string SendTarget;
    public readonly ChatDensity Density;
    public readonly bool ShowTags;

    public GameChatTarget(string key, string[] streams, string[] sendChannels, string sendChannelKey,
        string sendTarget, ChatDensity density, bool showTags)
    {
        Key = key;
        Streams = streams;
        SendChannels = sendChannels;
        SendChannelKey = sendChannelKey;
        SendTarget = sendTarget;
        Density = density;
        ShowTags = showTags;
    }
}

internal sealed class GameChatThread : IChatTranscriptInteractions, IChatTranscriptPaging, IDisposable
{
    private const long GroupWindowSeconds = 240;
    private const float FailureHeight = 26f;
    private const float SearchBarHeight = 40f;
    private const float JumpPillHeight = 26f;
    private const float LoadOlderThreshold = 48f;
    private const int WindowPageLines = 100;
    private const int LineSweepThreshold = 256;
    private const int RevealMarginLines = 8;
    private const int CompactRestoreFrames = 3;
    private const string SelfId = "linkpearl.self";

    private readonly ChatStreamView view;
    private readonly ChatSend send;
    private readonly GameData gameData;
    private readonly ChatTranscript transcript = new();
    private readonly Dictionary<string, LineHeight> lineHeights = new(StringComparer.Ordinal);
    private int lastLineSweepFrame = -1;
    private readonly ChatEntranceTracker entrance = new();
    private readonly GameComposer composer = new();
    private readonly List<PendingSend> ghostSends = new(4);
    private readonly List<ChatEntry> ghostEntries = new(4);
    private TranscriptMessage[] mapped = Array.Empty<TranscriptMessage>();
    private int mappedCount;
    private int mappedRequired = -1;
    private int mappedStyleRevision = -1;
    private int mappedWindowStart = -1;
    private long mappedRevision = -1;
    private GameChatTarget target;
    private string activeChannel = string.Empty;
    private string trackedKey = string.Empty;
    private bool followBottom = true;
    private bool snapToBottom;
    private string? revealId;
    private readonly List<int> matches = new(16);
    private string searchQuery = string.Empty;
    private string appliedQuery = string.Empty;
    private int matchCursor;
    private bool searchOpen;
    private bool searchFocus;
    private bool atBottom = true;
    private string? seenTailId;
    private int shownLines = WindowPageLines;
    private string? firstDrawnId;
    private float firstDrawnContentY;
    private float compactRestoreTarget;
    private float compactRestoreShift;
    private int compactRestoreFrames;

    public GameChatThread(ChatLog log, ChatSend send, GameData gameData)
    {
        view = new ChatStreamView(log);
        this.send = send;
        this.gameData = gameData;
    }

    public Action<ChatEntry>? Context { get; set; }

    public Action<ChatEntry, ChatChunk>? Link { get; set; }

    public void Gate() => composer.Gate();

    public void CloseMenus() => composer.CloseMenus();

    public bool SearchOpen => searchOpen;

    public bool IsOpenFor(string key) =>
        target.Streams is not null && string.Equals(target.Key, key, StringComparison.Ordinal);

    public void ToggleSearch()
    {
        searchOpen = !searchOpen;
        searchFocus = searchOpen;
        if (searchOpen)
        {
            return;
        }

        searchQuery = string.Empty;
        appliedQuery = string.Empty;
        matches.Clear();
        matchCursor = 0;
    }

    public void Reveal(string entryId)
    {
        revealId = entryId;
        snapToBottom = false;
    }

    public void Open(in GameChatTarget next)
    {
        if (string.Equals(target.Key, next.Key, StringComparison.Ordinal))
        {
            target = next;
            return;
        }

        target = next;
        activeChannel = next.SendChannelKey;
        composer.Bind(next.Key);
        followBottom = true;
        snapToBottom = revealId is null;
        ResetWindow();
    }

    public void Close()
    {
        composer.Unbind();
        if (searchOpen)
        {
            ToggleSearch();
        }

        target = default;
        trackedKey = string.Empty;
        ResetWindow();
    }

    bool IChatTranscriptPaging.HasMoreOlder => HasOlderVisible();

    bool IChatTranscriptPaging.LoadingOlder => false;

    void IChatTranscriptPaging.LoadOlder() => GrowWindow();

    public void Draw(Rect area, PhoneTheme theme)
    {
        if (target.Streams is null)
        {
            return;
        }

        var scale = UiScale.Current;
        view.Target(target.Streams);
        view.Sync();
        send.Tick();
        ChatDrafts.Tick();
        SyncGhosts();
        var model = new GameComposerModel
        {
            Theme = theme,
            Screen = area,
            Channels = target.SendChannels,
            ActiveChannel = activeChannel.Length > 0 ? activeChannel : target.SendChannelKey,
            SendTarget = target.SendTarget,
        };
        var composerBar = new Rect(new Vector2(area.Min.X, area.Max.Y - composer.Measure(area.Width, model)),
            area.Max);
        var failure = FirstFailure();
        var failureBlock = failure is null ? 0f : FailureHeight * scale;
        var searchHeight = searchOpen ? SearchBarHeight * scale : 0f;
        if (searchOpen)
        {
            DrawSearchBar(new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + searchHeight)), theme);
        }

        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + searchHeight),
            new Vector2(area.Max.X, composerBar.Min.Y - failureBlock));
        ShrinkWindow();
        EnsureRevealShown(view.Entries);
        if (target.Density == ChatDensity.Bubbles)
        {
            DrawBubbles(listRect, theme);
        }
        else
        {
            DrawCompact(listRect, theme);
        }

        DrawJumpPill(listRect, theme);
        if (failure is not null)
        {
            DrawFailure(new Rect(new Vector2(area.Min.X, listRect.Max.Y),
                new Vector2(area.Max.X, composerBar.Min.Y)), theme, failure);
        }

        var result = composer.Draw(composerBar, model);
        activeChannel = result.ChannelKey;
        if (!result.Submitted)
        {
            return;
        }

        if (result.IsCommand)
        {
            if (ChatSender.TrySend(result.Text))
            {
                composer.Clear();
            }

            return;
        }

        if (!GameChannels.TryByKey(activeChannel, out var channel) || !Submit(channel, result.Text))
        {
            return;
        }

        composer.Clear();
        snapToBottom = true;
    }

    private bool Submit(GameChannel channel, string text)
    {
        var configuration = Plugin.Cfg;
        var sent = configuration is null || configuration.LinkpearlSplitLongMessages
            ? send.SendSplit(channel, target.SendTarget, text,
                configuration?.LinkpearlSplitIndicator ?? string.Empty,
                configuration?.LinkpearlSplitIntervalMilliseconds ?? ChatSend.MinimumIntervalMilliseconds)
            : send.Send(channel, target.SendTarget, text);
        if (sent)
        {
            ChatDrafts.Record(channel.Key, target.SendTarget, text);
        }

        return sent;
    }

    public void OnMessageContext(string messageId)
    {
        if (Context is not { } handler)
        {
            return;
        }

        var entries = view.Entries;
        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Id, messageId, StringComparison.Ordinal))
            {
                handler(entries[index]);
                return;
            }
        }
    }

    public void OnLinkClick(string messageId, int target)
    {
        var entries = view.Entries;
        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Id, messageId, StringComparison.Ordinal))
            {
                RaiseLink(entries[index], target);
                return;
            }
        }
    }

    private void RaiseLink(ChatEntry entry, int target)
    {
        if (Link is not { } handler)
        {
            return;
        }

        var targets = ChatRuns.For(entry, ImGui.GetFrameCount()).Targets;
        if (target >= 0 && target < targets.Length)
        {
            handler(entry, targets[target]);
        }
    }

    public void OnQuoteClick(string messageId)
    {
    }

    public void OnReactionClick(string messageId, string token)
    {
    }

    public void Dispose() => view.Dispose();

    private void DrawCompact(Rect listRect, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var entries = view.Entries;
        entrance.Sync(target.Key, entries.Count, entries.Count > 0 ? entries[entries.Count - 1].Id : null,
            ImGui.GetIO().DeltaTime, false);
        using (var surface = AppSurface.BeginEdgeToEdge(listRect))
        {
            if (entries.Count == 0 && ghostEntries.Count == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    Loc.T(L.Messages.Empty), theme.TextMuted);
                return;
            }

            SyncFollow();
            SettleCompactRestore();
            var seamId = MaybeGrowCompact(surface.Scrolling);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            var contentOrigin = ImGui.GetWindowPos().Y - ImGui.GetScrollY();
            var lineWidth = ScrollLayout.StableContentWidth();
            var recordFirst = surface.Pull <= 0f;
            string? nextFirstId = null;
            var nextFirstContentY = 0f;
            var seamFound = false;
            var seamShift = 0f;
            ChatEntry? previous = null;
            for (var index = WindowStart(entries.Count); index < entries.Count; index++)
            {
                var entry = entries[index];
                if (Hidden(entry))
                {
                    continue;
                }

                if (previous is null || !TimeText.SameLocalDay(Seconds(previous.At), Seconds(entry.At)))
                {
                    DrawDaySeparator(entry.At, theme);
                }
                else if (!Grouped(previous, entry))
                {
                    ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxs * scale));
                }

                var contentTop = ImGui.GetCursorScreenPos().Y - contentOrigin;
                if (seamId is not null && string.Equals(entry.Id, seamId, StringComparison.Ordinal))
                {
                    seamShift = contentTop - firstDrawnContentY;
                    seamFound = true;
                    seamId = null;
                }

                if (nextFirstId is null)
                {
                    nextFirstId = entry.Id;
                    nextFirstContentY = contentTop;
                }

                var headsLine = previous is null || !Grouped(previous, entry);
                var style = new ChatLineStyle(RailFor(entry), headsLine, false, entrance.Progress(index));
                var lineStart = ImGui.GetCursorScreenPos();
                var isReveal = revealId is not null && string.Equals(entry.Id, revealId, StringComparison.Ordinal);
                if (!isReveal && TryCullLine(entry, headsLine, lineWidth, scale, lineStart))
                {
                    previous = entry;
                    continue;
                }

                if (ChatLineView.Draw(entry, theme, style, theme.Accent, out var tapped))
                {
                    Context?.Invoke(entry);
                }

                RecordLine(entry, headsLine, lineWidth, scale, ImGui.GetCursorScreenPos().Y - lineStart.Y);

                if (tapped >= 0)
                {
                    RaiseLink(entry, tapped);
                }

                if (revealId is not null && string.Equals(entry.Id, revealId, StringComparison.Ordinal))
                {
                    ImGui.SetScrollHereY(0.4f);
                    revealId = null;
                    followBottom = false;
                }

                previous = entry;
            }

            if (recordFirst)
            {
                firstDrawnId = nextFirstId;
                firstDrawnContentY = nextFirstContentY;
            }

            if (seamFound && MathF.Abs(seamShift) >= 0.5f)
            {
                ApplyCompactShift(seamShift);
                compactRestoreFrames = CompactRestoreFrames;
            }

            for (var index = 0; index < ghostEntries.Count; index++)
            {
                var ghost = ghostEntries[index];
                ChatLineView.Draw(ghost, theme, new ChatLineStyle(RailFor(ghost), true, true), theme.Accent, out _);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            if (followBottom)
            {
                ImGui.SetScrollHereY(1f);
            }
        }
    }

    private void DrawSearchBar(Rect bar, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var entries = view.Entries;
        var controls = 96f * scale;
        var field = new Rect(new Vector2(bar.Min.X + Metrics.Space.Md * scale, bar.Min.Y + 4f * scale),
            new Vector2(bar.Max.X - controls, bar.Max.Y - 4f * scale));
        if (searchFocus)
        {
            ImGui.SetKeyboardFocusHere();
            searchFocus = false;
        }

        SearchField.Draw(field, "##gamechat.search", Loc.T(L.Common.Search), ref searchQuery, theme);
        if (!string.Equals(searchQuery, appliedQuery, StringComparison.Ordinal))
        {
            appliedQuery = searchQuery;
            RebuildMatches(entries);
        }

        var label = matches.Count == 0
            ? appliedQuery.Trim().Length == 0 ? string.Empty : "0"
            : string.Concat((matchCursor + 1).ToString(Loc.Culture), "/", matches.Count.ToString(Loc.Culture));
        var drawList = ImGui.GetWindowDrawList();
        if (label.Length > 0)
        {
            var size = Typography.Measure(label, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(bar.Max.X - controls + 4f * scale, bar.Center.Y - size.Y * 0.5f),
                label, theme.TextMuted, TextStyles.Caption1);
        }

        var upCenter = new Vector2(bar.Max.X - 46f * scale, bar.Center.Y);
        var downCenter = new Vector2(bar.Max.X - 22f * scale, bar.Center.Y);
        var enabled = matches.Count > 0;
        var ink = enabled ? theme.Accent : theme.TextMuted;
        AppSkin.Icon(drawList, upCenter, IconGlyph.Of(FontAwesomeIcon.ChevronUp), ink, 0.9f);
        AppSkin.Icon(drawList, downCenter, IconGlyph.Of(FontAwesomeIcon.ChevronDown), ink, 0.9f);
        if (!enabled)
        {
            return;
        }

        if (UiInteract.HoverClickCircle(upCenter, 12f * scale))
        {
            Step(-1, entries);
        }

        if (UiInteract.HoverClickCircle(downCenter, 12f * scale))
        {
            Step(1, entries);
        }
    }

    private void RebuildMatches(IReadOnlyList<ChatEntry> entries)
    {
        matches.Clear();
        matchCursor = 0;
        var needle = appliedQuery.Trim();
        if (needle.Length < 2)
        {
            return;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(index);
            }
        }

        if (matches.Count == 0)
        {
            return;
        }

        matchCursor = matches.Count - 1;
        Reveal(entries[matches[matchCursor]].Id);
    }

    private void Step(int direction, IReadOnlyList<ChatEntry> entries)
    {
        if (matches.Count == 0)
        {
            return;
        }

        matchCursor = (matchCursor + direction + matches.Count) % matches.Count;
        var index = matches[matchCursor];
        if (index >= 0 && index < entries.Count)
        {
            Reveal(entries[index].Id);
        }
    }

    private void DrawJumpPill(Rect listRect, PhoneTheme theme)
    {
        var entries = view.Entries;
        var tailId = entries.Count > 0 ? entries[entries.Count - 1].Id : null;
        if (atBottom)
        {
            seenTailId = tailId;
            return;
        }

        if (tailId is null || string.Equals(tailId, seenTailId, StringComparison.Ordinal))
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var label = Loc.T(L.Linkpearl.NewMessages);
        var size = Typography.Measure(label, TextStyles.Caption1);
        var width = size.X + 26f * scale;
        var height = JumpPillHeight * scale;
        var min = new Vector2(listRect.Center.X - width * 0.5f, listRect.Max.Y - height - 6f * scale);
        var max = min + new Vector2(width, height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(theme.GroupedCard));
        Squircle.Stroke(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, 0.35f)), 1f);
        AppSkin.Icon(drawList, new Vector2(min.X + 13f * scale, min.Y + height * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.ArrowDown), theme.Accent, 0.78f);
        Typography.Draw(drawList, new Vector2(min.X + 22f * scale, min.Y + height * 0.5f - size.Y * 0.5f), label,
            theme.TextStrong, TextStyles.Caption1);
        if (UiInteract.HoverClick(min, max))
        {
            snapToBottom = true;
            revealId = null;
        }
    }

    private void DrawBubbles(Rect listRect, PhoneTheme theme)
    {
        Remap(theme);
        var model = new ChatTranscriptModel
        {
            ThreadId = target.Key,
            Messages = mapped.AsSpan(0, mappedCount),
            MyUserId = SelfId,
            Accent = theme.Accent,
            Theme = theme,
            MutedInk = theme.TextMuted,
            BodyInk = theme.TextStrong,
            EmptyText = Loc.T(L.Messages.Empty),
            LoadingText = Loc.T(L.Messages.Empty),
            SidePadding = AppSurface.SidePadding,
            IsGroup = target.Streams.Length > 1 || target.SendTarget.Length == 0,
            LabelsOwnMessages = target.Streams.Length > 1 || target.SendTarget.Length == 0,
            Interactions = this,
            Paging = this,
        };
        if (snapToBottom)
        {
            transcript.RequestSnapToBottom();
            snapToBottom = false;
        }

        if (revealId is not null)
        {
            transcript.RequestScrollTo(revealId);
            revealId = null;
        }

        transcript.Draw(listRect, model);
    }

    private void Remap(PhoneTheme theme)
    {
        var entries = view.Entries;
        var windowStart = WindowStart(entries.Count);
        var required = entries.Count - windowStart + ghostEntries.Count;
        var styleRevision = ChannelStyles.Shared.Revision;
        if (mappedRevision == view.Revision && mappedRequired == required &&
            mappedStyleRevision == styleRevision && mappedWindowStart == windowStart)
        {
            return;
        }

        mappedRevision = view.Revision;
        mappedRequired = required;
        mappedStyleRevision = styleRevision;
        mappedWindowStart = windowStart;
        if (mapped.Length < required)
        {
            mapped = new TranscriptMessage[Math.Max(required, 64)];
        }

        var written = 0;
        for (var index = windowStart; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (Hidden(entry))
            {
                continue;
            }

            mapped[written++] = Map(entry, theme, 0);
        }

        for (var index = 0; index < ghostEntries.Count; index++)
        {
            mapped[written++] = Map(ghostEntries[index], theme, TranscriptFlags.Placeholder);
        }

        mappedCount = written;
    }

    private static bool Hidden(ChatEntry entry) =>
        entry.IsSelf && ChannelStyles.Shared.HidesOutgoing(entry.ChannelKey);

    private int WindowStart(int entryCount) => Math.Max(0, entryCount - shownLines);

    private void ResetWindow()
    {
        shownLines = WindowPageLines;
        firstDrawnId = null;
        compactRestoreFrames = 0;
        mappedRevision = -1;
    }

    private bool TryCullLine(ChatEntry entry, bool headsLine, float width, float scale, Vector2 lineStart)
    {
        if (!lineHeights.TryGetValue(entry.Id, out var cached)
            || !cached.Matches(width, scale, Plugin.Fonts.Generation, headsLine))
        {
            return false;
        }

        if (ImGui.IsRectVisible(new Vector2(width, cached.Height)))
        {
            return false;
        }

        cached.LastUsedFrame = ImGui.GetFrameCount();
        ImGui.SetCursorScreenPos(new Vector2(lineStart.X, lineStart.Y + cached.Height));
        return true;
    }

    private void RecordLine(ChatEntry entry, bool headsLine, float width, float scale, float height)
    {
        var frame = ImGui.GetFrameCount();
        if (!lineHeights.TryGetValue(entry.Id, out var cached))
        {
            if (lineHeights.Count > LineSweepThreshold)
            {
                SweepIdleLines(frame);
            }

            cached = new LineHeight();
            lineHeights[entry.Id] = cached;
        }

        cached.Remember(width, scale, Plugin.Fonts.Generation, headsLine, height, frame);
    }

    private void SweepIdleLines(int frame)
    {
        if (lastLineSweepFrame == frame)
        {
            return;
        }

        lastLineSweepFrame = frame;
        foreach (var pair in lineHeights)
        {
            if (pair.Value.LastUsedFrame < frame - 1)
            {
                lineHeights.Remove(pair.Key);
            }
        }
    }

    private void ShrinkWindow()
    {
        if (shownLines <= WindowPageLines || revealId is not null || compactRestoreFrames > 0)
        {
            return;
        }

        var following = target.Density == ChatDensity.Bubbles ? transcript.AtBottom : followBottom;
        if (!following)
        {
            return;
        }

        shownLines = WindowPageLines;
        firstDrawnId = null;
    }

    private void GrowWindow()
    {
        var entries = view.Entries;
        var start = WindowStart(entries.Count);
        var added = 0;
        while (start > 0 && added < WindowPageLines)
        {
            start--;
            if (!Hidden(entries[start]))
            {
                added++;
            }
        }

        shownLines = entries.Count - start;
    }

    private bool HasOlderVisible()
    {
        var entries = view.Entries;
        for (var index = WindowStart(entries.Count) - 1; index >= 0; index--)
        {
            if (!Hidden(entries[index]))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureRevealShown(IReadOnlyList<ChatEntry> entries)
    {
        if (revealId is null)
        {
            return;
        }

        for (var index = entries.Count - 1; index >= 0; index--)
        {
            if (!string.Equals(entries[index].Id, revealId, StringComparison.Ordinal))
            {
                continue;
            }

            var needed = Math.Min(entries.Count, entries.Count - index + RevealMarginLines);
            if (needed > shownLines)
            {
                shownLines = needed;
            }

            return;
        }
    }

    private string? MaybeGrowCompact(bool scrolling)
    {
        if (compactRestoreFrames > 0 || scrolling || followBottom || firstDrawnId is null || !HasOlderVisible())
        {
            return null;
        }

        if (ImGui.GetScrollMaxY() <= 0f || ImGui.GetScrollY() > LoadOlderThreshold * UiScale.Current)
        {
            return null;
        }

        GrowWindow();
        return firstDrawnId;
    }

    private void SettleCompactRestore()
    {
        if (compactRestoreFrames <= 0)
        {
            return;
        }

        if (followBottom || revealId is not null)
        {
            compactRestoreFrames = 0;
            return;
        }

        var expected = Math.Clamp(compactRestoreTarget, 0f, ImGui.GetScrollMaxY());
        if (MathF.Abs(ImGui.GetScrollY() - expected) <= 1f)
        {
            compactRestoreFrames = 0;
            return;
        }

        compactRestoreFrames--;
        ApplyCompactShift(compactRestoreShift);
    }

    private void ApplyCompactShift(float shift)
    {
        compactRestoreShift = shift;
        compactRestoreTarget = ImGui.GetScrollY() + shift;
        ImGui.SetScrollY(compactRestoreTarget);
    }

    private TranscriptMessage Map(ChatEntry entry, PhoneTheme theme, byte flags)
    {
        var tag = string.Empty;
        var tagTint = theme.TextMuted;
        if (target.ShowTags && GameChannels.TryByKey(entry.ChannelKey, out var channel))
        {
            tag = GameChannels.DisplayName(channel);
            tagTint = channel.Tint;
        }

        var runs = ChatRuns.For(entry, ImGui.GetFrameCount());
        var overrides = ChannelStyles.Shared.For(entry.ChannelKey);
        var packedName = overrides is null ? 0u : entry.IsSelf ? overrides.OutgoingName : overrides.IncomingName;
        var packedBody = overrides is null ? 0u : entry.IsSelf ? overrides.OutgoingBody : overrides.IncomingBody;
        var senderTint = packedName != 0u
            ? ChannelInk.Unpack(packedName)
            : entry.IsSelf ? theme.Accent : SenderTint.Of(entry.AuthorName);
        var bodyInk = packedBody != 0u ? ChannelInk.Unpack(packedBody) : default;
        return new TranscriptMessage(entry.Id, entry.IsSelf ? SelfId : entry.SenderKey, entry.Text, 0,
            Seconds(entry.At), 0, 0, null, entry.AuthorName, senderTint, flags,
            channelTag: tag, channelTint: tagTint, runs: runs.HasLinks || runs.HasEmoji ? runs.Runs : null,
            bodyInk: bodyInk);
    }

    private void SyncGhosts()
    {
        for (var index = ghostSends.Count - 1; index >= 0; index--)
        {
            var pending = ghostSends[index];
            if (!StillPending(send.Pending, pending) || pending.Failed || !Owns(pending))
            {
                ghostSends.RemoveAt(index);
                ghostEntries.RemoveAt(index);
            }
        }

        var live = send.Pending;
        for (var index = 0; index < live.Count; index++)
        {
            var pending = live[index];
            if (pending.Failed || !Owns(pending) || Tracked(pending))
            {
                continue;
            }

            ghostSends.Add(pending);
            ghostEntries.Add(new ChatEntry(0, pending.ChannelKey, LocalName(), LocalWorld(), pending.Text,
                new[] { ChatChunk.Plain(pending.Text) }, DateTime.Now, ChatEntryFlags.Self));
        }
    }

    private bool Tracked(PendingSend pending)
    {
        for (var index = 0; index < ghostSends.Count; index++)
        {
            if (ReferenceEquals(ghostSends[index], pending))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StillPending(IReadOnlyList<PendingSend> live, PendingSend entry)
    {
        for (var index = 0; index < live.Count; index++)
        {
            if (ReferenceEquals(live[index], entry))
            {
                return true;
            }
        }

        return false;
    }

    private PendingSend? FirstFailure()
    {
        var live = send.Pending;
        for (var index = 0; index < live.Count; index++)
        {
            if (live[index].Failed && Owns(live[index]))
            {
                return live[index];
            }
        }

        return null;
    }

    private void DrawFailure(Rect bar, PhoneTheme theme, PendingSend pending)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var label = Loc.T(L.Linkpearl.NotDelivered);
        var retry = Loc.T(L.Linkpearl.Retry);
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var retrySize = Typography.Measure(retry, TextStyles.FootnoteEmphasized);
        var left = bar.Min.X + Metrics.Space.Lg * scale;
        var center = bar.Min.Y + bar.Height * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left, center), IconGlyph.Of(FontAwesomeIcon.ExclamationCircle),
            theme.Danger, 0.62f);
        Typography.Draw(drawList, new Vector2(left + 12f * scale, center - labelSize.Y * 0.5f), label, theme.Danger,
            TextStyles.Caption1);
        var retryMin = new Vector2(bar.Max.X - Metrics.Space.Lg * scale - retrySize.X, center - retrySize.Y * 0.5f);
        Typography.Draw(drawList, retryMin, retry, theme.Accent, TextStyles.FootnoteEmphasized);
        var hit = new Rect(retryMin - new Vector2(6f * scale, 6f * scale),
            retryMin + retrySize + new Vector2(6f * scale, 6f * scale));
        if (!UiInteract.HoverClick(hit.Min, hit.Max))
        {
            return;
        }

        composer.Refill(pending.Text);
        send.Discard(pending);
    }

    private void DrawDaySeparator(DateTime at, PhoneTheme theme)
    {
        var label = TimeText.DayLabel(Seconds(at));
        if (label.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var origin = ImGui.GetCursorScreenPos();
        var chipMin = new Vector2(origin.X + (available - textSize.X - 20f * scale) * 0.5f, origin.Y + 4f * scale);
        var chipMax = chipMin + new Vector2(textSize.X + 20f * scale, textSize.Y + 8f * scale);
        Squircle.Fill(drawList, chipMin, chipMax, (chipMax.Y - chipMin.Y) * 0.5f,
            ImGui.GetColorU32(ChatInk.Wash(theme, 0.08f)));
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, theme.TextMuted, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, chipMax.Y + 8f * scale));
    }

    private Vector4 RailFor(ChatEntry entry)
    {
        if (!target.ShowTags || !GameChannels.TryByKey(entry.ChannelKey, out var channel))
        {
            return default;
        }

        return channel.Tint;
    }

    private bool Owns(PendingSend pending)
    {
        if (string.Equals(pending.ChannelKey, GameChannels.TellKey, StringComparison.Ordinal))
        {
            return string.Equals(pending.Target, target.SendTarget, StringComparison.OrdinalIgnoreCase);
        }

        for (var index = 0; index < target.Streams.Length; index++)
        {
            if (string.Equals(target.Streams[index], pending.ChannelKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void SyncFollow()
    {
        var scale = UiScale.Current;
        if (string.Equals(trackedKey, target.Key, StringComparison.Ordinal))
        {
            followBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4f * scale;
        }
        else
        {
            trackedKey = target.Key;
            followBottom = true;
        }

        if (snapToBottom)
        {
            followBottom = true;
            snapToBottom = false;
        }

        atBottom = followBottom;
    }

    private string LocalName() => gameData.LocalPlayer?.Name.TextValue ?? string.Empty;

    private string LocalWorld() => gameData.WorldName(gameData.LocalHomeWorldId);

    private static bool Grouped(ChatEntry previous, ChatEntry entry) =>
        string.Equals(previous.SenderKey, entry.SenderKey, StringComparison.Ordinal) &&
        string.Equals(previous.ChannelKey, entry.ChannelKey, StringComparison.Ordinal) &&
        (entry.At - previous.At).TotalSeconds <= GroupWindowSeconds;

    private static long Seconds(DateTime at) => new DateTimeOffset(at).ToUnixTimeSeconds();

    private sealed class LineHeight
    {
        public float Height;
        public int LastUsedFrame;
        private float width;
        private float scale;
        private int fontGeneration;
        private bool headsLine;

        public bool Matches(float rowWidth, float uiScale, int fonts, bool heads) =>
            Height > 0f && width == rowWidth && scale == uiScale && fontGeneration == fonts && headsLine == heads;

        public void Remember(float rowWidth, float uiScale, int fonts, bool heads, float rowHeight, int frame)
        {
            width = rowWidth;
            scale = uiScale;
            fontGeneration = fonts;
            headsLine = heads;
            Height = rowHeight;
            LastUsedFrame = frame;
        }
    }
}

internal static class GameChatTargets
{
    public static GameChatTarget For(InboxRow row)
    {
        if (row.Tab is { } tab)
        {
            var channels = tab.Channels.ToArray();
            return new GameChatTarget(row.Key, channels, channels, tab.SendChannel, string.Empty, tab.Density,
                channels.Length > 1);
        }

        var target = ChatStreams.IsTell(row.Key) ? SendTarget(row) : string.Empty;
        return new GameChatTarget(row.Key, new[] { row.StreamKey }, new[] { GameChannels.TellKey },
            GameChannels.TellKey, target, ChatDensity.Bubbles, false);
    }

    public static string SendTarget(InboxRow row) =>
        row.World.Length > 0 ? string.Concat(row.Title, "@", row.World) : row.Title;

    public static string Subtitle(InboxRow row)
    {
        if (row.Tab is not { } tab)
        {
            return row.World;
        }

        var joined = string.Empty;
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            if (!GameChannels.TryByKey(tab.Channels[index], out var channel))
            {
                continue;
            }

            var label = LinkshellNames.Label(channel);
            joined = joined.Length == 0 ? label : string.Concat(joined, " · ", label);
        }

        return joined;
    }
}
