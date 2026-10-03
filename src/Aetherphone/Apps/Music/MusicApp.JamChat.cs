using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Report;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float JamChatListHeight = 300f;

    private readonly SoftWrapEditor jamChatEditor = new();
    private readonly RadioCountLabel jamChatCounter = new();
    private LiveChatTranscript? jamTranscript;
    private int jamSeenChatRefusal;
    private string jamTranscriptJamCode = string.Empty;

    private LiveChatTranscript JamTranscript =>
        jamTranscript ??= new LiveChatTranscript("music.jam.chat", ui, images, lodestone, JamBadgeFor);

    private void DrawJamChat(float scale)
    {
        var transcript = JamTranscript;
        if (!string.Equals(jamTranscriptJamCode, jam.Code, StringComparison.Ordinal))
        {
            jamTranscriptJamCode = jam.Code;
            transcript.Reset();
            jamChatEditor.Adopt(string.Empty);
        }

        transcript.Sync(jam.Chat);
        SectionHeader.Draw(ui, Loc.T(L.Music.Jam.ChatHeader), false);
        var growth = jamChatEditor.Growth(LiveChatTranscript.ComposerMaxLines);
        var listHeight = JamChatListHeight * scale;
        var composerHeight = LiveChatTranscript.ComposerBaseHeight * scale + growth;
        var card = BeginJamBlock(listHeight + composerHeight);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale);
        var list = new Rect(card.Min, new Vector2(card.Max.X, card.Min.Y + listHeight));
        var composer = new Rect(new Vector2(card.Min.X, list.Max.Y), card.Max);
        if (transcript.Count == 0)
        {
            EmptyState.Draw(list, ui, FontAwesomeIcon.Comments, Loc.T(L.Music.Jam.ChatEmptyTitle),
                Loc.T(L.Music.Jam.ChatEmptyBody));
        }
        else if (transcript.DrawTranscript(list, scale) is { } picked)
        {
            OpenJamChatMenu(picked);
        }

        transcript.DrawJumpPill(list, scale);
        DrawJamChatComposer(composer, scale);
        EndJamBlock();
    }

    private void DrawJamChatComposer(Rect bar, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);
        var send = LiveChatTranscript.DrawComposerField(ui, bar, bar.Min.X + Metrics.Space.Md * scale,
            "##musicJamChatComposer", Loc.T(L.Music.Jam.ChatHint), jamChatEditor, JamWire.MaxChatLength,
            jam.CanSendChat(), jamChatCounter, scale);
        if (send && jam.SendChat(jamChatEditor.Text))
        {
            jamChatEditor.Adopt(string.Empty);
            JamTranscript.RequestJump();
            UiFeedback.Play(UiSound.MessageSent);
        }
    }

    private string JamBadgeFor(RadioChatEntry entry)
    {
        return entry.IsDj ? Loc.T(L.Music.Jam.HostBadge) : string.Empty;
    }

    private void TrackJamChatRefusal()
    {
        if (jam.ChatRefusalVersion == jamSeenChatRefusal)
        {
            return;
        }

        jamSeenChatRefusal = jam.ChatRefusalVersion;
        ShellToast.Show(Loc.T(JamChatRefusalText(jam.LastChatRefusal)));
    }

    private static LocString JamChatRefusalText(JamChatRefusal refusal)
    {
        return refusal switch
        {
            JamChatRefusal.Cooldown => L.Music.Live.RefusedCooldown,
            JamChatRefusal.TooLong => L.Music.Live.RefusedTooLong,
            JamChatRefusal.Empty => L.Music.Live.RefusedEmpty,
            JamChatRefusal.NotInJam => L.Music.Jam.ChatNotInJam,
            _ => L.Music.Live.RefusedUnknown,
        };
    }

    private void OpenJamChatMenu(RadioChatEntry entry)
    {
        chatMenuForJam = true;
        chatMenuCount = 0;
        if (!entry.IsMine)
        {
            AddChatMenu(ChatMenuAction.Report, Loc.T(L.Music.Live.ReportMessage), FontAwesomeIcon.Flag, true);
            AddChatMenu(ChatMenuAction.Hide, Loc.T(L.Music.Live.HideUser), FontAwesomeIcon.EyeSlash, false);
        }

        if (jam.CanDelete(entry))
        {
            AddChatMenu(ChatMenuAction.Delete, Loc.T(L.Common.Delete), FontAwesomeIcon.TrashAlt, true);
        }

        if (chatMenuCount == 0)
        {
            return;
        }

        chatMenuEntry = entry;
        chatMenu.Open();
    }

    private void RunJamChatMenu(ChatMenuAction action, RadioChatEntry entry)
    {
        switch (action)
        {
            case ChatMenuAction.Report:
                ReportJamChatMessage(entry);
                return;
            case ChatMenuAction.Hide:
                jam.HideUser(entry.UserId);
                ShellToast.Show(Loc.T(L.Music.Live.HiddenToast));
                return;
            case ChatMenuAction.Delete:
                jam.DeleteMessage(entry.MessageId);
                return;
        }
    }

    private void ReportJamChatMessage(RadioChatEntry entry)
    {
        var code = jam.Code;
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Music.Live.ReportTitle),
            Submit = (reason, done) =>
                SubmitChatReport(entry, RadioRoomReport.Compose(reason, RadioRoomReport.JamEvidenceTag, code, entry),
                    done),
        });
    }
}
