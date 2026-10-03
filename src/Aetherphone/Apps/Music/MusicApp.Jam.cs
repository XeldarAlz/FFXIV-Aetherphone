using Aetherphone.Apps.Music.Jam;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float JamQueueToastWindowSeconds = 6f;
    private const string JamOverflowPrefix = "+";
    private const string JamHandlePrefix = "@";

    private static readonly MusicRoute JamLobbyRoute = MusicRoute.Of(MusicScreen.JamLobby);

    private readonly JamSession jam;
    private readonly JamLauncher jamLauncher;
    private readonly ContactBook contacts;
    private readonly AethernetSession jamAccount;
    private readonly HashSet<int> jamSeenEntries = new();
    private readonly JamCodeField jamCodeField = new();

    private JamMember[] jamMembers = Array.Empty<JamMember>();
    private string[] jamMemberNames = Array.Empty<string>();
    private string[] jamMemberHandles = Array.Empty<string>();
    private string jamOverflowLabel = string.Empty;
    private string jamBadgeOverflowLabel = string.Empty;
    private string jamHostName = string.Empty;
    private int jamMeIndex = -1;
    private int jamMembersVersion = -1;
    private int jamSeenDecline;
    private int jamSeenRefusal;
    private int jamSeenQueue = -1;
    private bool jamQueueBaselined;
    private bool jamSongMenuWasOpen;
    private float jamSongMenuClosedAt = float.NegativeInfinity;

    private bool JamActive => jam.InJam;

    private bool JamCapturesPointer => jamInviteSheet.CapturesPointer || jamMemberSheet.CapturesPointer;

    private string MyJamUserId => jamAccount.CurrentUser?.Id ?? string.Empty;

    private int JamOthersCount => jamMeIndex >= 0 ? jamMembers.Length - 1 : jamMembers.Length;

    private void OpenJamLobby()
    {
        CloseNowPlaying();
        if (Router.Current == JamLobbyRoute)
        {
            return;
        }

        Push(JamLobbyRoute);
    }

    private static string JamGlyph => IconGlyph.Of(FontAwesomeIcon.UserFriends);

    private static void DrawJamGlyph(ImDrawListPtr drawList, Vector2 center, Vector4 color, float glyphScale)
    {
        AppSkin.Icon(drawList, center, JamGlyph, color, glyphScale);
    }

    private void TickJam()
    {
        ConsumeJamLaunch();
        TrackJamSongMenu();
        if (jam.DeclineVersion != jamSeenDecline)
        {
            jamSeenDecline = jam.DeclineVersion;
            if (JamMessages.Decline(jam.LastDecline) is { } decline)
            {
                ShellToast.Show(Loc.T(decline));
            }
        }

        if (jam.RefusalVersion != jamSeenRefusal)
        {
            jamSeenRefusal = jam.RefusalVersion;
            if (JamMessages.Refusal(jam.LastRefusal) is { } refusal)
            {
                ShellToast.Show(Loc.T(refusal));
            }
        }

        TrackJamChatRefusal();
        TrackJamQueue();
        EnsureJamMembers();
    }

    private void TrackJamSongMenu()
    {
        var open = songMenu.IsOpen;
        if (jamSongMenuWasOpen && !open)
        {
            jamSongMenuClosedAt = clock;
        }

        jamSongMenuWasOpen = open;
    }

    private void TrackJamQueue()
    {
        if (jam.QueueVersion == jamSeenQueue)
        {
            return;
        }

        jamSeenQueue = jam.QueueVersion;
        var queue = jam.Queue;
        var inJam = jam.InJam;
        var addedByMe = false;
        if (jamQueueBaselined && inJam)
        {
            var me = MyJamUserId;
            for (var index = 0; index < queue.Length; index++)
            {
                if (!jamSeenEntries.Contains(queue[index].EntryId)
                    && string.Equals(queue[index].AddedByUserId, me, StringComparison.Ordinal))
                {
                    addedByMe = true;
                    break;
                }
            }
        }

        jamSeenEntries.Clear();
        for (var index = 0; index < queue.Length; index++)
        {
            jamSeenEntries.Add(queue[index].EntryId);
        }

        jamQueueBaselined = inJam;
        if (!addedByMe)
        {
            return;
        }

        var fromMenu = clock - jamSongMenuClosedAt <= JamQueueToastWindowSeconds;
        if (!fromMenu && jam.IsHost)
        {
            return;
        }

        jamSongMenuClosedAt = float.NegativeInfinity;
        ShellToast.Show(Loc.T(L.Music.Jam.AddedToQueue));
    }

    private void EnsureJamMembers()
    {
        if (jam.MembersVersion == jamMembersVersion)
        {
            return;
        }

        jamMembersVersion = jam.MembersVersion;
        var source = jam.Members;
        var count = source.Length;
        var ordered = new JamMember[count];
        var names = new string[count];
        var handles = new string[count];
        var me = MyJamUserId;
        var hostSlot = -1;
        for (var index = 0; index < count; index++)
        {
            if (source[index].IsHost)
            {
                hostSlot = index;
                break;
            }
        }

        var write = 0;
        if (hostSlot >= 0)
        {
            ordered[write] = source[hostSlot];
            write++;
        }

        for (var index = 0; index < count; index++)
        {
            if (index == hostSlot)
            {
                continue;
            }

            ordered[write] = source[index];
            write++;
        }

        jamMeIndex = -1;
        jamHostName = string.Empty;
        for (var index = 0; index < count; index++)
        {
            var member = ordered[index];
            handles[index] = member.Handle.Length > 0 ? string.Concat(JamHandlePrefix, member.Handle) : string.Empty;
            names[index] = member.DisplayName.Length > 0 ? member.DisplayName : handles[index];
            if (string.Equals(member.UserId, me, StringComparison.Ordinal))
            {
                jamMeIndex = index;
            }

            if (member.IsHost)
            {
                jamHostName = names[index];
            }
        }

        jamMembers = ordered;
        jamMemberNames = names;
        jamMemberHandles = handles;
        jamOverflowLabel = count > JamStackMax
            ? string.Concat(JamOverflowPrefix, (count - JamStackMax).ToString(Loc.Culture))
            : string.Empty;
        jamBadgeOverflowLabel = count > JamBadgeStackMax
            ? string.Concat(JamOverflowPrefix, (count - JamBadgeStackMax).ToString(Loc.Culture))
            : string.Empty;
    }

    private bool IsJamMember(string userId)
    {
        for (var index = 0; index < jamMembers.Length; index++)
        {
            if (string.Equals(jamMembers[index].UserId, userId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private string JamMemberName(int index)
    {
        var name = jamMemberNames[index];
        return name.Length > 0 ? name : Loc.T(L.Music.Jam.SomeoneName);
    }

    private string JamTitle => jam.Title.Length > 0 ? jam.Title : Loc.T(L.Music.Jam.Title);

    private Song JamNowPlaying => playback.SongActive ? playback.CurrentSong : jam.RemoteSong;

    private void ConsumeJamLaunch()
    {
        if (!jamLauncher.TryConsumeLobby(out var invited))
        {
            return;
        }

        ShowOnTab(tab, JamLobbyRoute);
        var code = PartyCode.Normalize(invited);
        jamCodeField.Set(code);
        if (code.Length == 0 || (jam.InJam && string.Equals(jam.Code, code, StringComparison.Ordinal)))
        {
            return;
        }

        if (jam.Mode == JamMode.Idle)
        {
            JoinJam(code);
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Music.Jam.SwitchTitle),
            Message = Loc.T(L.Music.Jam.SwitchBody),
            ConfirmLabel = Loc.T(L.Music.Jam.Join),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Sheet = true,
            Confirm = () => JoinJam(code),
        });
    }

    private void JoinJam(string code)
    {
        if (jam.Join(code))
        {
            jamCodeField.Clear();
        }
    }

    private void StartJam()
    {
        if (jam.Start(jamTitleDraft))
        {
            jamTitleDraft = string.Empty;
        }
    }

    private void DrawJamOverlays(Rect screen)
    {
        DrawJamInviteSheet(screen);
        DrawJamMemberSheet(screen);
    }
}
