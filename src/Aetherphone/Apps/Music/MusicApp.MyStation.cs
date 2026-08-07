using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int StationNameMaxLength = 40;
    private const int StationDescriptionMaxLength = 300;
    private const int StationLinkMaxLength = 200;
    private const int StationTagsMaxLength = 140;
    private const float FieldHeight = 44f;
    private const float DescriptionHeight = 92f;
    private const float CredentialRowHeight = 34f;
    private const float CopiedNoticeSeconds = 1.6f;

    private readonly string[] linkDrafts = new string[LinkLabels.Length];
    private readonly ChipRail scheduleRail = new();
    private readonly string[] scheduleLabels = new string[8];
    private readonly bool[] scheduleActive = new bool[8];
    private string stationNameDraft = string.Empty;
    private string stationDescriptionDraft = string.Empty;
    private string stationTagsDraft = string.Empty;
    private int scheduleDayDraft = -1;
    private int scheduleMinuteDraft = 21 * 60;
    private bool scheduleRepeatDraft = true;
    private bool stationDraftLoaded;
    private volatile bool stationSaving;
    private volatile bool stationSaveFailed;
    private volatile bool stationSaveDone;
    private float stationCopiedClock = float.NegativeInfinity;
    private int stationCopiedRow = -1;
    private ImagePickCrop? artworkPicker;
    private volatile bool artworkSaving;
    private volatile int artworkOutcome;

    private void OpenMyStation()
    {
        LoadStationDrafts();
        router.Push(View.MyStation);
    }

    private void OpenStationArtwork()
    {
        artworkPicker ??= new ImagePickCrop(photoLibrary, wallpaperImages);
        artworkPicker.Open();
        artworkOutcome = 0;
        router.Push(View.StationArtwork);
    }

    private void DrawStationArtwork(in PhoneContext context)
    {
        if (artworkPicker is null || community.Mine is null)
        {
            router.Pop();
            return;
        }

        if (artworkOutcome == 1)
        {
            artworkOutcome = 0;
            router.Pop();
            return;
        }

        if (artworkOutcome == 2)
        {
            artworkOutcome = 0;
            confirm.Alert(null, Loc.T(L.Account.CannotReach), Loc.T(L.Account.FailDismiss));
        }

        var labels = new ImagePickCropLabels(Loc.T(L.Music.StationArtwork), Loc.T(L.Account.ImportFromPc),
            Loc.T(L.Photos.NoPhotos), Loc.T(L.Account.MoveAndScale), Loc.T(L.Account.Use), Loc.T(L.Account.Saving),
            Loc.T(L.Account.GestureHint));
        var result = artworkPicker.Draw(context.Content, context, labels, ui.Accent, artworkSaving);
        if (result == ImagePickCropEvent.Cancelled)
        {
            router.Pop();
            return;
        }

        if (result == ImagePickCropEvent.Committed && !artworkSaving && artworkPicker.SourcePath.Length > 0)
        {
            UploadStationArtwork(artworkPicker.SourcePath, artworkPicker.Crop);
        }
    }

    private void UploadStationArtwork(string sourcePath, WallpaperCrop crop)
    {
        artworkSaving = true;
        var request = CurrentStationRequest();
        var pickedPath = sourcePath;
        var pickedCrop = crop;
        _ = Task.Run(async () =>
        {
            var ok = await StationArtworkUpload
                .RunAsync(aethernet.Media, community, request, pickedPath, pickedCrop, CancellationToken.None)
                .ConfigureAwait(false);
            artworkSaving = false;
            artworkOutcome = ok ? 1 : 2;
            if (ok)
            {
                LoadStationDrafts();
            }
        });
    }

    private UpdateCommunityStationRequest CurrentStationRequest()
    {
        var links = new List<CommunityLinkDto>(linkDrafts.Length);
        for (var index = 0; index < linkDrafts.Length; index++)
        {
            var url = linkDrafts[index].Trim();
            if (url.Length > 0)
            {
                links.Add(new CommunityLinkDto(index, url));
            }
        }

        return new UpdateCommunityStationRequest(stationNameDraft.Trim(), stationDescriptionDraft.Trim(),
            ParseTags(stationTagsDraft), links.ToArray(), null, ScheduleUnix(), scheduleRepeatDraft);
    }

    private long ScheduleUnix()
    {
        if (scheduleDayDraft < 0)
        {
            return 0L;
        }

        var now = DateTime.Now;
        var days = ((scheduleDayDraft - (int)now.DayOfWeek) + 7) % 7;
        var slot = now.Date.AddDays(days).AddMinutes(scheduleMinuteDraft);
        if (slot <= now)
        {
            slot = slot.AddDays(7);
        }

        return new DateTimeOffset(slot).ToUnixTimeSeconds();
    }

    private void LoadScheduleDraft(CommunityStationDto station)
    {
        if (station.NextBroadcastAtUnix <= 0)
        {
            scheduleDayDraft = -1;
            scheduleMinuteDraft = 21 * 60;
            scheduleRepeatDraft = true;
            return;
        }

        var local = DateTimeOffset.FromUnixTimeSeconds(station.NextBroadcastAtUnix).ToLocalTime();
        scheduleDayDraft = (int)local.DayOfWeek;
        scheduleMinuteDraft = local.Hour * 60 + local.Minute;
        scheduleRepeatDraft = station.RepeatsWeekly;
    }

    private static string[] ParseTags(string draft)
    {
        var parts = draft.Split(',');
        var tags = new List<string>(parts.Length);
        for (var index = 0; index < parts.Length; index++)
        {
            var tag = parts[index].Trim();
            if (tag.Length > 0)
            {
                tags.Add(tag);
            }
        }

        return tags.ToArray();
    }

    private void LoadStationDrafts()
    {
        if (community.Mine is not { } mine)
        {
            return;
        }

        stationNameDraft = mine.Station.Name;
        stationDescriptionDraft = mine.Station.Description;
        stationTagsDraft = string.Join(", ", mine.Station.Tags);
        LoadScheduleDraft(mine.Station);
        for (var index = 0; index < linkDrafts.Length; index++)
        {
            linkDrafts[index] = string.Empty;
        }

        for (var index = 0; index < mine.Station.Links.Length; index++)
        {
            var link = mine.Station.Links[index];
            if (link.Kind >= 0 && link.Kind < linkDrafts.Length)
            {
                linkDrafts[link.Kind] = link.Url;
            }
        }

        stationDraftLoaded = true;
        stationSaveFailed = false;
        stationSaveDone = false;
    }

    private void DrawMyStation(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        DrawTopBar(context, Loc.T(L.Music.MyStation), PopToCommunity);
        if (community.Mine is not { } mine)
        {
            return;
        }

        if (!stationDraftLoaded)
        {
            LoadStationDrafts();
        }

        var body = ScrollBody(content, scale);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            DrawStationStatusLine(scale, mine.Station);
            DrawArtworkRow(scale, mine.Station);
            DrawFieldLabel(scale, Loc.T(L.Music.StationNameLabel));
            DrawStationField(scale, "##stationName", ref stationNameDraft, StationNameMaxLength);
            DrawFieldLabel(scale, Loc.T(L.Music.StationDescriptionLabel));
            DrawStationDescription(scale);
            DrawFieldLabel(scale, Loc.T(L.Music.StationTagsLabel));
            DrawStationTags(scale);
            DrawFieldLabel(scale, Loc.T(L.Music.ScheduleLabel));
            DrawScheduleEditor(scale);
            DrawFieldLabel(scale, Loc.T(L.Music.StationLinksLabel));
            for (var index = 0; index < linkDrafts.Length; index++)
            {
                DrawLinkField(scale, index);
            }

            DrawStationSaveRow(scale, mine.Station);
            DrawCredentials(scale, mine.Credentials);
            ImGui.Dummy(new Vector2(0f, 14f * scale));
        }
    }

    private void DrawStationStatusLine(float scale, CommunityStationDto station)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var status = station.IsLive
            ? $"{Loc.T(L.Music.OnAir)} · {string.Format(Loc.T(L.Music.ListeningCount), station.Listeners)}"
            : Loc.T(L.Music.OffAir);
        var fitted = Typography.FitText(status, width - 32f * scale, TextStyles.Callout);
        Typography.Draw(drawList, new Vector2(origin.X + 16f * scale, origin.Y), fitted,
            station.IsLive ? ui.Accent : ui.MutedInk, TextStyles.Callout);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 26f * scale));
    }

    private void DrawArtworkRow(float scale, CommunityStationDto station)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var coverSize = 64f * scale;
        var coverMin = new Vector2(origin.X + 16f * scale, origin.Y);
        DrawStationArt(drawList, coverMin, coverMin + new Vector2(coverSize, coverSize), station, 12f * scale);

        var buttonMin = new Vector2(coverMin.X + coverSize + 14f * scale, origin.Y + (coverSize - 36f * scale) * 0.5f);
        var buttonRect = new Rect(buttonMin,
            new Vector2(MathF.Min(buttonMin.X + 170f * scale, origin.X + width - 16f * scale),
                buttonMin.Y + 36f * scale));
        if (ui.GhostButton(buttonRect, Loc.T(L.Music.StationArtwork)) && !artworkSaving)
        {
            OpenStationArtwork();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, coverSize + 12f * scale));
    }

    private void DrawFieldLabel(float scale, string label)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + 16f * scale, origin.Y + 8f * scale), label,
            ui.MutedInk, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 28f * scale));
    }

    private void DrawStationField(float scale, string id, ref string draft, int maxLength, string hint = "")
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var fieldMin = new Vector2(origin.X + 16f * scale, origin.Y);
        var fieldRect = new Rect(fieldMin, new Vector2(origin.X + width - 16f * scale,
            origin.Y + FieldHeight * scale));
        SubmitField.Draw(fieldRect, id, hint, ref draft, theme, maxLength);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (FieldHeight + 8f) * scale));
    }

    private void DrawStationTags(float scale)
    {
        DrawStationField(scale, "##stationTags", ref stationTagsDraft, StationTagsMaxLength,
            Loc.T(L.Music.StationTagsHint));
    }

    private void DrawScheduleEditor(float scale)
    {
        DrawScheduleDayRail(scale);
        if (scheduleDayDraft < 0)
        {
            DrawScheduleSummary(scale, Loc.T(L.Music.ScheduleNone), ui.MutedInk);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var fieldWidth = width - 32f * scale;
        var timeRect = new Rect(new Vector2(origin.X + 16f * scale, origin.Y),
            new Vector2(origin.X + 16f * scale + fieldWidth, origin.Y + FieldHeight * scale));
        scheduleMinuteDraft = TimeOfDayField.Draw(ui, timeRect, scheduleMinuteDraft, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (FieldHeight + 10f) * scale));

        DrawScheduleRepeatRow(scale);
        DrawScheduleSummary(scale, TimeText.FutureMoment(ScheduleUnix()), ui.Accent);
    }

    private void DrawScheduleDayRail(float scale)
    {
        scheduleLabels[0] = Loc.T(L.Music.ScheduleClear);
        scheduleActive[0] = scheduleDayDraft < 0;
        var names = Loc.Culture.DateTimeFormat.AbbreviatedDayNames;
        for (var day = 0; day < 7; day++)
        {
            scheduleLabels[day + 1] = Loc.Culture.TextInfo.ToTitleCase(names[day]);
            scheduleActive[day + 1] = scheduleDayDraft == day;
        }

        var tapped = scheduleRail.Draw(ui, scheduleLabels.AsSpan(), scheduleActive.AsSpan());
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (tapped >= 0)
        {
            scheduleDayDraft = tapped == 0 ? -1 : tapped - 1;
        }
    }

    private void DrawScheduleRepeatRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + 16f * scale, origin.Y + 6f * scale),
            Loc.T(L.Music.ScheduleRepeat), ui.BodyInk, TextStyles.Callout);
        var toggleWidth = 46f * scale;
        var toggleMin = new Vector2(origin.X + width - 16f * scale - toggleWidth, origin.Y);
        var toggleRect = new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, 26f * scale));
        scheduleRepeatDraft = Toggle.Draw("music.schedule.repeat", toggleRect, scheduleRepeatDraft, theme);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 36f * scale));
    }

    private void DrawScheduleSummary(float scale, string text, Vector4 color)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + 16f * scale, origin.Y), text, color,
            TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 24f * scale));
    }

    private void DrawLinkField(float scale, int kind)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var labelWidth = 74f * scale;
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(origin.X + 16f * scale, origin.Y + 13f * scale), LinkLabels[kind], ui.BodyInk,
            TextStyles.Caption1);
        var fieldMin = new Vector2(origin.X + 16f * scale + labelWidth, origin.Y);
        var fieldRect = new Rect(fieldMin, new Vector2(origin.X + width - 16f * scale,
            origin.Y + FieldHeight * scale));
        var draft = linkDrafts[kind];
        SubmitField.Draw(fieldRect, "##stationLink" + kind, string.Empty, ref draft, theme, StationLinkMaxLength);
        linkDrafts[kind] = draft;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (FieldHeight + 6f) * scale));
    }

    private void DrawStationDescription(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var fieldWidth = width - 32f * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 16f * scale, origin.Y));
        SoftWrapField.Multiline("##stationDescription", ref stationDescriptionDraft, StationDescriptionMaxLength,
            new Vector2(fieldWidth, DescriptionHeight * scale), fieldWidth - 16f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (DescriptionHeight + 10f) * scale));
    }

    private void DrawStationSaveRow(float scale, CommunityStationDto station)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var buttonWidth = MathF.Min(width - 32f * scale, 220f * scale);
        var buttonMin = new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y + 6f * scale);
        var buttonRect = new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, 40f * scale));
        var label = stationSaving ? Loc.T(L.Common.Loading) : Loc.T(L.Music.StationSave);
        if (ui.PillButton(buttonRect, label, true, "music.station.save") && !stationSaving)
        {
            SaveStation();
        }

        var noticeY = buttonRect.Max.Y + 6f * scale;
        if (stationSaveDone || stationSaveFailed)
        {
            var notice = stationSaveFailed ? Loc.T(L.Music.StationSaveFailed) : Loc.T(L.Music.StationSaved);
            var fitted = Typography.FitText(notice, width - 32f * scale, TextStyles.Caption1);
            var noticeSize = Typography.Measure(fitted, TextStyles.Caption1);
            Typography.Draw(ImGui.GetWindowDrawList(),
                new Vector2(origin.X + (width - noticeSize.X) * 0.5f, noticeY), fitted,
                stationSaveFailed ? theme.Danger : ui.MutedInk, TextStyles.Caption1);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 74f * scale));
    }

    private void SaveStation()
    {
        stationSaving = true;
        stationSaveFailed = false;
        stationSaveDone = false;
        _ = SaveStationAsync(CurrentStationRequest());
    }

    private async Task SaveStationAsync(UpdateCommunityStationRequest request)
    {
        var ok = await community.SaveMineAsync(request).ConfigureAwait(false);
        stationSaveFailed = !ok;
        stationSaveDone = ok;
        stationSaving = false;
        if (ok)
        {
            LoadStationDrafts();
        }
    }

    private void DrawCredentials(float scale, CommunityCredentialsDto credentials)
    {
        DrawFieldLabel(scale, Loc.T(L.Music.StationBroadcast));
        DrawCredentialRow(scale, 0, Loc.T(L.Music.StationServer), credentials.Host);
        DrawCredentialRow(scale, 1, Loc.T(L.Music.StationPort), credentials.Port.ToString());
        DrawCredentialRow(scale, 2, Loc.T(L.Music.StationMount), credentials.Mount);
        DrawCredentialRow(scale, 3, Loc.T(L.Music.StationUser), credentials.Username);
        DrawCredentialRow(scale, 4, Loc.T(L.Music.StationPassword), credentials.Password);
        DrawCredentialRow(scale, 5, Loc.T(L.Music.StationFormat),
            $"{credentials.Format} · {credentials.Bitrate}kbps · {credentials.SampleRate}Hz");

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var wrapWidth = width - 32f * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + 16f * scale, origin.Y + 8f * scale),
            Loc.T(L.Music.StationHelp), ui.MutedInk, TextStyles.Caption1, wrapWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 16f * scale));
    }

    private void DrawCredentialRow(float scale, int row, string label, string value)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = CredentialRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var labelLeft = origin.X + 16f * scale;
        Typography.Draw(drawList, new Vector2(labelLeft, origin.Y + 9f * scale), label, ui.MutedInk,
            TextStyles.Caption1);

        var copyRadius = 13f * scale;
        var copyCenter = new Vector2(origin.X + width - 16f * scale - copyRadius, origin.Y + rowHeight * 0.5f);
        var valueLeft = labelLeft + 80f * scale;
        var valueWidth = copyCenter.X - copyRadius - 8f * scale - valueLeft;
        var justCopied = stationCopiedRow == row && clock - stationCopiedClock < CopiedNoticeSeconds;
        var shown = justCopied ? Loc.T(L.Music.StationCopied) : value;
        var fitted = Typography.FitText(shown, valueWidth, TextStyles.Callout);
        Typography.Draw(drawList, new Vector2(valueLeft, origin.Y + 7f * scale), fitted,
            justCopied ? ui.Accent : ui.TitleInk, TextStyles.Callout);

        if (value.Length > 0 && ui.IconButton(copyCenter, copyRadius, FontAwesomeIcon.Copy.ToIconString(),
                ui.MutedInk, AppSkin.Transparent, 0.75f, label))
        {
            ImGui.SetClipboardText(value);
            stationCopiedClock = clock;
            stationCopiedRow = row;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }
}
