using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Report;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal enum StationPanel : byte
{
    Chat,
    Requests,
    About,
    Host,
}

internal enum ChatMenuAction : byte
{
    Report,
    Hide,
    Block,
    Delete,
    MuteShort,
    MuteHour,
    MuteDay,
    Unmute,
}

internal sealed partial class MusicApp
{
    private const int ChatMenuCapacity = 8;
    private const int MuteShortMinutes = 10;
    private const int MuteHourMinutes = 60;
    private const int MuteDayMinutes = 24 * 60;

    private readonly RadioRoomSession room;
    private readonly AethernetSession session;
    private readonly ActionSheet chatMenu = new();
    private readonly ActionSheet.Item[] chatMenuItems = new ActionSheet.Item[ChatMenuCapacity];
    private readonly ChatMenuAction[] chatMenuActions = new ChatMenuAction[ChatMenuCapacity];
    private int chatMenuCount;
    private RadioChatEntry? chatMenuEntry;
    private bool chatMenuForJam;
    private int roomVisitFrame = -1;
    private string roomStationId = string.Empty;
    private StationPanel stationPanel = StationPanel.Chat;

    private void VisitStationRoom(string stationId)
    {
        roomVisitFrame = ImGui.GetFrameCount();
        if (!string.Equals(roomStationId, stationId, StringComparison.Ordinal))
        {
            roomStationId = stationId;
            ResetStationPanels();
        }

        if (room.TryTakeRefusal(out var refusal) && refusal.Action != RadioRoomAction.Attach)
        {
            ShellToast.Show(Loc.T(RefusalText(refusal.Kind)));
        }

        if (!session.IsSignedIn)
        {
            return;
        }

        var sameStation = string.Equals(room.StationId, stationId, StringComparison.Ordinal);
        if (sameStation && room.Status != RadioRoomStatus.Idle)
        {
            return;
        }

        room.Attach(stationId);
    }

    private void RetryStationRoom()
    {
        if (roomStationId.Length == 0)
        {
            return;
        }

        room.Detach();
        room.Attach(roomStationId);
    }

    private void ResetStationPanels()
    {
        stationPanel = StationPanel.Chat;
        stationTranscript?.Reset();
        chatEditor.Adopt(string.Empty);
        requestDraft = string.Empty;
        reactionTrayOpen = false;
        chatMenu.Close();
        chatMenuEntry = null;
    }

    private void TrackStationRoom()
    {
        if (room.StationId.Length == 0 || ImGui.GetFrameCount() - roomVisitFrame <= 1)
        {
            return;
        }

        room.Detach();
    }

    private void LeaveStationRoom()
    {
        roomVisitFrame = -1;
        chatMenu.Close();
        if (room.StationId.Length > 0)
        {
            room.Detach();
        }
    }

    private void GateStationOverlays()
    {
        chatMenu.Gate();
    }

    private void DrawStationOverlays(Rect screen)
    {
        if (chatMenuEntry is not { } entry)
        {
            return;
        }

        var picked = chatMenu.Draw(screen, ActionSheetStyle.From(ui), chatMenuItems.AsSpan(0, chatMenuCount),
            Loc.T(L.Common.Cancel), false, entry.DisplayName);
        if (!chatMenu.IsOpen)
        {
            chatMenuEntry = null;
        }

        if (picked < 0)
        {
            return;
        }

        RunChatMenu(chatMenuActions[picked], entry);
    }

    private void OpenChatMenu(RadioChatEntry entry)
    {
        chatMenuForJam = false;
        chatMenuCount = 0;
        if (!entry.IsMine)
        {
            AddChatMenu(ChatMenuAction.Report, Loc.T(L.Music.Live.ReportMessage), FontAwesomeIcon.Flag, true);
            AddChatMenu(ChatMenuAction.Hide, Loc.T(L.Music.Live.HideUser), FontAwesomeIcon.EyeSlash, false);
            AddChatMenu(ChatMenuAction.Block, Loc.T(L.Music.Live.BlockUser), FontAwesomeIcon.Ban, true);
        }

        if (entry.IsMine || room.CanModerate)
        {
            AddChatMenu(ChatMenuAction.Delete, Loc.T(L.Common.Delete), FontAwesomeIcon.TrashAlt, true);
        }

        if (room.CanModerate && !entry.IsMine)
        {
            if (room.MutedUntilFor(entry.UserId) > 0)
            {
                AddChatMenu(ChatMenuAction.Unmute, Loc.T(L.Music.Live.Unmute), FontAwesomeIcon.Microphone, false);
            }
            else
            {
                AddChatMenu(ChatMenuAction.MuteShort, Loc.T(L.Music.Live.MuteShort), FontAwesomeIcon.MicrophoneSlash,
                    false);
                AddChatMenu(ChatMenuAction.MuteHour, Loc.T(L.Music.Live.MuteHour), FontAwesomeIcon.MicrophoneSlash,
                    false);
                AddChatMenu(ChatMenuAction.MuteDay, Loc.T(L.Music.Live.MuteDay), FontAwesomeIcon.MicrophoneSlash,
                    false);
            }
        }

        if (chatMenuCount == 0)
        {
            return;
        }

        chatMenuEntry = entry;
        chatMenu.Open();
    }

    private void AddChatMenu(ChatMenuAction action, string label, FontAwesomeIcon icon, bool danger)
    {
        chatMenuItems[chatMenuCount] = new ActionSheet.Item(label, IconGlyph.Of(icon), danger);
        chatMenuActions[chatMenuCount] = action;
        chatMenuCount++;
    }

    private void RunChatMenu(ChatMenuAction action, RadioChatEntry entry)
    {
        if (chatMenuForJam)
        {
            RunJamChatMenu(action, entry);
            return;
        }

        switch (action)
        {
            case ChatMenuAction.Report:
                ReportChatMessage(entry);
                return;
            case ChatMenuAction.Hide:
                room.HideUser(entry.UserId);
                ShellToast.Show(Loc.T(L.Music.Live.HiddenToast));
                return;
            case ChatMenuAction.Block:
                AskBlockChatUser(entry);
                return;
            case ChatMenuAction.Delete:
                room.Delete(entry.MessageId);
                return;
            case ChatMenuAction.MuteShort:
                room.Mute(entry.UserId, MuteShortMinutes);
                return;
            case ChatMenuAction.MuteHour:
                room.Mute(entry.UserId, MuteHourMinutes);
                return;
            case ChatMenuAction.MuteDay:
                room.Mute(entry.UserId, MuteDayMinutes);
                return;
            case ChatMenuAction.Unmute:
                room.Unmute(entry.UserId);
                return;
        }
    }

    private void ReportChatMessage(RadioChatEntry entry)
    {
        var stationId = room.StationId;
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Music.Live.ReportTitle),
            Submit = (reason, done) =>
                SubmitChatReport(entry, RadioRoomReport.ComposeReason(reason, stationId, entry), done),
        });
    }

    private void SubmitChatReport(RadioChatEntry entry, string composed, Action<bool> done)
    {
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await aethernet.Safety
                    .ReportAsync(RadioRoomReport.TargetType, entry.UserId, composed, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Music] chat report failed");
            }

            done(succeeded);
        });
    }

    private void AskBlockChatUser(RadioChatEntry entry)
    {
        var userId = entry.UserId;
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Music.Live.BlockConfirm, entry.DisplayName),
            ConfirmLabel = Loc.T(L.Music.Live.BlockAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            FailedMessage = Loc.T(L.Music.Live.BlockFailed),
            ConfirmAsync = done => BlockChatUser(userId, done),
        });
    }

    private void BlockChatUser(string userId, Action<bool> done)
    {
        room.HideUser(userId);
        _ = Task.Run(async () =>
        {
            var blocked = false;
            try
            {
                blocked = await aethernet.Safety.BlockAsync(userId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Radio] chat block failed");
            }

            done(blocked);
        });
    }

    private static LocString RefusalText(RadioRefusalKind kind)
    {
        return kind switch
        {
            RadioRefusalKind.Unavailable => L.Music.Live.RefusedUnavailable,
            RadioRefusalKind.Cooldown => L.Music.Live.RefusedCooldown,
            RadioRefusalKind.NotInRoom => L.Music.Live.RefusedNotInRoom,
            RadioRefusalKind.Forbidden => L.Music.Live.RefusedForbidden,
            RadioRefusalKind.Banned => L.Music.Live.RefusedBanned,
            RadioRefusalKind.Muted => L.Music.Live.RefusedMuted,
            RadioRefusalKind.Empty => L.Music.Live.RefusedEmpty,
            RadioRefusalKind.TooLong => L.Music.Live.RefusedTooLong,
            RadioRefusalKind.RequestsClosed => L.Music.Live.RefusedRequestsClosed,
            RadioRefusalKind.RequestOpen => L.Music.Live.RefusedRequestOpen,
            RadioRefusalKind.QueueFull => L.Music.Live.RefusedQueueFull,
            RadioRefusalKind.NotFound => L.Music.Live.RefusedNotFound,
            RadioRefusalKind.Invalid => L.Music.Live.RefusedInvalid,
            _ => L.Music.Live.RefusedUnknown,
        };
    }
}
