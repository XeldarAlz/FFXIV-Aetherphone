using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float ComposerBaseHeight = LiveChatTranscript.ComposerBaseHeight;
    private const float ComposerFieldInsetY = LiveChatTranscript.ComposerFieldInsetY;
    private const int ComposerMaxLines = LiveChatTranscript.ComposerMaxLines;
    private const float ComposerButtonRadius = LiveChatTranscript.ComposerButtonRadius;
    private const float ReactionRise = 190f;
    private const float ReactionSize = 22f;
    private const float ReactionLaneSpacing = 12f;
    private const float ReactionSwayWidth = 14f;
    private const float ReactionMineScale = 1.15f;
    private const float ReactionTraySlot = 38f;
    private const float ReactionTrayHeight = 46f;
    private const float PinnedTintAlpha = 0.14f;

    private static readonly TextStyle ChatBodyStyle = LiveChatTranscript.BodyStyle;

    private static readonly string[] ReactionShortcodes =
    {
        "heart", "fire", "clap", "joy", "notes", "star_struck", "dancer", "raised_hands",
    };

    private readonly SoftWrapEditor chatEditor = new();
    private readonly string[] reactionFiles = new string[RadioRoomSession.ReactionKinds];
    private readonly RadioCountLabel counterLabel = new();
    private readonly RadioCountLabel durationLabel = new();
    private readonly RadioFittedText composerLockFit = new();
    private readonly RadioWrappedText pinnedText = new();
    private LiveChatTranscript? stationTranscript;
    private bool reactionFilesReady;
    private bool reactionTrayOpen;
    private LanguageInfo? muteLanguage;
    private string muteDuration = string.Empty;
    private string muteText = string.Empty;

    private RadioComposerLock ChatLock()
    {
        var status = string.Equals(room.StationId, roomStationId, StringComparison.Ordinal)
            ? room.Status
            : RadioRoomStatus.Attaching;
        return RadioLiveRules.Evaluate(session.IsBanned, session.IsSignedIn, status, room.IsMuted(),
            room.CanModerate);
    }

    private LiveChatTranscript StationTranscript =>
        stationTranscript ??= new LiveChatTranscript("radio.chat", ui, images, lodestone, StationBadgeFor);

    private void DrawStationChat(Rect panel, float scale)
    {
        StationTranscript.Sync(room);
        var lockState = ChatLock();
        var growth = lockState == RadioComposerLock.None ? chatEditor.Growth(ComposerMaxLines) : 0f;
        var composerHeight = ComposerBaseHeight * scale + growth;
        var composer = new Rect(new Vector2(panel.Min.X, panel.Max.Y - composerHeight), panel.Max);
        var top = panel.Min.Y + DrawPinnedNotice(panel, scale);
        var list = new Rect(new Vector2(panel.Min.X, top), new Vector2(panel.Max.X, composer.Min.Y));
        DrawChatTranscript(list, lockState, scale);
        DrawFloatingReactions(list, scale);
        StationTranscript.DrawJumpPill(list, scale);
        DrawChatComposer(composer, lockState, scale);
    }

    private float DrawPinnedNotice(Rect panel, float scale)
    {
        if (room.Pinned is not { Length: > 0 } pinned)
        {
            return 0f;
        }

        var inset = Metrics.Space.Lg * scale;
        var pad = Metrics.Space.Md * scale;
        var min = new Vector2(panel.Min.X + inset, panel.Min.Y + Metrics.Space.Xs * scale);
        var right = panel.Max.X - inset;
        var closeReserve = room.CanModerate ? 26f * scale : 0f;
        var textLeft = min.X + pad + 20f * scale;
        var textWidth = MathF.Max(1f, right - pad - closeReserve - textLeft);
        pinnedText.Wrap(pinned, textWidth, ChatBodyStyle);
        var captionHeight = Typography.LineHeight(TextStyles.Caption2);
        var height = pad * 2f + captionHeight + Metrics.Space.Xxs * scale + pinnedText.Height;
        var max = new Vector2(right, min.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, min, max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, PinnedTintAlpha)));
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, PinnedTintAlpha * 2f)), Metrics.Stroke.Hairline);
        AppSkin.Icon(drawList, new Vector2(min.X + pad + 7f * scale, min.Y + pad + captionHeight * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.Thumbtack), ui.Accent, 0.7f);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + pad), Loc.T(L.Music.Live.PinnedByHost), ui.Accent,
            TextStyles.Caption2);
        pinnedText.Draw(drawList, new Vector2(textLeft, min.Y + pad + captionHeight + Metrics.Space.Xxs * scale),
            ui.TitleInk, ChatBodyStyle);

        if (room.CanModerate)
        {
            var closeCenter = new Vector2(right - pad - 6f * scale, min.Y + pad + captionHeight * 0.5f);
            if (ui.IconButton(closeCenter, 11f * scale, IconGlyph.Of(FontAwesomeIcon.Times), ui.MutedInk,
                    AppSkin.Transparent, 0.7f, Loc.T(L.Music.Live.Unpin)))
            {
                room.Unpin();
            }
        }

        return height + Metrics.Space.Sm * scale;
    }

    private void DrawChatTranscript(Rect list, RadioComposerLock lockState, float scale)
    {
        var transcript = StationTranscript;
        if (transcript.Count == 0)
        {
            DrawChatEmpty(list, lockState, scale);
            return;
        }

        if (transcript.DrawTranscript(list, scale) is { } picked)
        {
            OpenChatMenu(picked);
        }
    }

    private void DrawChatEmpty(Rect list, RadioComposerLock lockState, float scale)
    {
        switch (lockState)
        {
            case RadioComposerLock.SignedOut:
                EmptyState.Draw(list, ui, FontAwesomeIcon.UserSlash, Loc.T(L.Music.Live.LockSignedOut),
                    Loc.T(L.Music.StationSignedOutSub));
                return;
            case RadioComposerLock.Connecting:
                LoadingPulse.Draw(list.Center, 14f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Music.Live.LockConnecting));
                return;
            case RadioComposerLock.Unavailable:
                if (EmptyState.Draw(list, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Music.Live.LockUnavailable),
                        Loc.T(L.Music.StationOfflineSub), Loc.T(L.Common.Retry)))
                {
                    RetryStationRoom();
                }

                return;
            default:
                EmptyState.Draw(list, ui, FontAwesomeIcon.Comments, Loc.T(L.Music.Live.ChatEmptyTitle),
                    Loc.T(L.Music.Live.ChatEmptySub));
                return;
        }
    }

    private string StationBadgeFor(RadioChatEntry entry)
    {
        if (entry.IsDj)
        {
            return Loc.T(L.Music.Live.DjBadge);
        }

        return entry.IsMine && room.IsModerator ? Loc.T(L.Music.Live.ModBadge) : string.Empty;
    }

    private void EnsureReactionFiles()
    {
        if (reactionFilesReady || !EmojiCatalog.Ready)
        {
            return;
        }

        for (var index = 0; index < ReactionShortcodes.Length; index++)
        {
            reactionFiles[index] = EmojiCatalog.TryResolve(ReactionShortcodes[index], out var file)
                ? file
                : string.Empty;
        }

        reactionFilesReady = true;
    }

    private void DrawFloatingReactions(Rect list, float scale)
    {
        var count = room.ReactionCount;
        if (count == 0)
        {
            return;
        }

        EnsureReactionFiles();
        if (!reactionFilesReady)
        {
            return;
        }

        var now = Environment.TickCount64;
        var drawList = ImGui.GetWindowDrawList();
        var rise = MathF.Min(ReactionRise * scale, list.Height * 0.75f);
        var baseX = list.Max.X - Metrics.Space.Xxl * scale;
        var baseY = list.Max.Y - Metrics.Space.Sm * scale;
        drawList.PushClipRect(list.Min, list.Max, true);
        for (var index = 0; index < count; index++)
        {
            var pulse = room.ReactionAt(index);
            var file = reactionFiles[pulse.Reaction];
            if (string.IsNullOrEmpty(file))
            {
                continue;
            }

            var lane = RadioLiveRules.ReactionLane(pulse.AtTick, pulse.Reaction);
            var pose = RadioLiveRules.Pose(now - pulse.AtTick, RadioRoomSession.ReactionLifetimeMilliseconds, lane);
            var fontSize = ReactionSize * scale * pose.Scale * (pulse.IsMine ? ReactionMineScale : 1f);
            var side = EmojiRender.LineHeight(fontSize);
            var center = new Vector2(baseX - lane * ReactionLaneSpacing * scale + pose.Sway * ReactionSwayWidth * scale,
                baseY - side * 0.5f - pose.Rise * rise);
            EmojiRender.Draw(drawList, file, center - new Vector2(side * 0.5f, side * 0.5f), fontSize, pose.Alpha);
        }

        drawList.PopClipRect();
    }

    private void DrawChatComposer(Rect bar, RadioComposerLock lockState, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);
        var inset = Metrics.Space.Md * scale;
        var buttonRadius = ComposerButtonRadius * scale;
        var rowCenterY = bar.Max.Y - ComposerBaseHeight * scale * 0.5f;
        var canReact = room.IsAttached && lockState is RadioComposerLock.None or RadioComposerLock.Muted;
        var reactCenter = new Vector2(bar.Min.X + inset + buttonRadius, rowCenterY);
        var fieldLeft = bar.Min.X + inset;
        var overReactionToggle = false;
        if (canReact)
        {
            fieldLeft = reactCenter.X + buttonRadius + Metrics.Space.Sm * scale;
            overReactionToggle = UiInteract.Hover(reactCenter - new Vector2(buttonRadius, buttonRadius),
                reactCenter + new Vector2(buttonRadius, buttonRadius));
            if (ui.IconButton(reactCenter, buttonRadius, IconGlyph.Of(FontAwesomeIcon.Heart),
                    reactionTrayOpen ? ui.Accent : ui.MutedInk, Palette.WithAlpha(ui.FieldSurface, 0.9f), 0.8f,
                    Loc.T(L.Music.Live.React)))
            {
                reactionTrayOpen = !reactionTrayOpen;
            }
        }
        else
        {
            reactionTrayOpen = false;
        }

        if (lockState != RadioComposerLock.None)
        {
            DrawComposerLock(drawList, new Rect(new Vector2(fieldLeft, bar.Min.Y + ComposerFieldInsetY * scale),
                new Vector2(bar.Max.X - inset, bar.Max.Y - ComposerFieldInsetY * scale)), lockState, scale);
            DrawReactionTray(bar, reactCenter, overReactionToggle, scale);
            return;
        }

        var send = LiveChatTranscript.DrawComposerField(ui, bar, fieldLeft, "##radioChatComposer",
            Loc.T(L.Music.Live.ChatHint), chatEditor, RadioRoomSession.MaxChatLength, room.CanSendChat(), counterLabel,
            scale);
        DrawReactionTray(bar, reactCenter, overReactionToggle, scale);
        if (send && room.SendChat(chatEditor.Text))
        {
            chatEditor.Adopt(string.Empty);
            StationTranscript.RequestJump();
            UiFeedback.Play(UiSound.MessageSent);
        }
    }

    private void DrawComposerLock(ImDrawListPtr drawList, Rect field, RadioComposerLock lockState, float scale)
    {
        Squircle.Fill(drawList, field.Min, field.Max, field.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.FieldSurface, 0.7f)));
        var glyphCenter = new Vector2(field.Min.X + Metrics.Space.Lg * scale, field.Center.Y);
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(FontAwesomeIcon.Lock), ui.MutedInk, 0.7f);
        var textLeft = glyphCenter.X + Metrics.Space.Md * scale;
        var fitted = composerLockFit.Fit(LockText(lockState), field.Max.X - Metrics.Space.Md * scale - textLeft,
            TextStyles.Footnote);
        var size = Typography.Measure(fitted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, field.Center.Y - size.Y * 0.5f), fitted, ui.MutedInk,
            TextStyles.Footnote);
    }

    private string LockText(RadioComposerLock lockState)
    {
        return lockState switch
        {
            RadioComposerLock.Suspended => Loc.T(L.Music.Live.LockSuspended),
            RadioComposerLock.SignedOut => Loc.T(L.Music.Live.LockSignedOut),
            RadioComposerLock.Unavailable => Loc.T(L.Music.Live.LockUnavailable),
            RadioComposerLock.Muted => MuteText(),
            _ => Loc.T(L.Music.Live.LockConnecting),
        };
    }

    private string MuteText()
    {
        var remaining = RadioLiveRules.Remaining(room.MutedUntilUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var duration = remaining.Unit switch
        {
            RadioDurationUnit.Seconds => durationLabel.Format(L.Music.Live.DurationSeconds, remaining.Major),
            RadioDurationUnit.Minutes => durationLabel.Format(L.Music.DurationMinutes, remaining.Major),
            _ => durationLabel.Format(L.Music.DurationHoursMinutes, remaining.Major, remaining.Minor),
        };

        if (ReferenceEquals(duration, muteDuration) && ReferenceEquals(muteLanguage, Loc.Current))
        {
            return muteText;
        }

        muteDuration = duration;
        muteLanguage = Loc.Current;
        muteText = Loc.T(L.Music.Live.LockMuted, duration);
        return muteText;
    }

    private void DrawReactionTray(Rect bar, Vector2 toggleCenter, bool overToggle, float scale)
    {
        if (!reactionTrayOpen)
        {
            return;
        }

        EnsureReactionFiles();
        var slot = ReactionTraySlot * scale;
        var height = ReactionTrayHeight * scale;
        var width = slot * RadioRoomSession.ReactionKinds + Metrics.Space.Sm * 2f * scale;
        var left = MathF.Max(bar.Min.X + Metrics.Space.Sm * scale, toggleCenter.X - ComposerButtonRadius * scale);
        left = MathF.Min(left, bar.Max.X - Metrics.Space.Sm * scale - width);
        var min = new Vector2(left, bar.Min.Y - height - Metrics.Space.Xs * scale);
        var max = new Vector2(left + width, min.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, theme);
        var enabled = room.CanReact();
        var overTray = UiInteract.Hover(min, max);
        for (var index = 0; index < RadioRoomSession.ReactionKinds; index++)
        {
            var slotMin = new Vector2(min.X + Metrics.Space.Sm * scale + index * slot, min.Y);
            var slotMax = new Vector2(slotMin.X + slot, max.Y);
            var hovered = UiInteract.Hover(slotMin, slotMax);
            var fontSize = ReactionSize * scale * (hovered ? 1.15f : 1f);
            var side = EmojiRender.LineHeight(fontSize);
            var center = (slotMin + slotMax) * 0.5f;
            var file = reactionFiles[index];
            if (!string.IsNullOrEmpty(file))
            {
                EmojiRender.Draw(drawList, file, center - new Vector2(side * 0.5f, side * 0.5f), fontSize,
                    enabled ? 1f : 0.45f);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(slotMin, slotMax, hovered) && enabled)
            {
                room.React(index);
            }
        }

        if (UiInteract.ClickedOutside(overTray || overToggle))
        {
            reactionTrayOpen = false;
        }
    }
}
