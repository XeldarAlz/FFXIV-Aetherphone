using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp
{
    private string settingsKeyInput = string.Empty;
    private bool settingsKeyVisible;
    private List<VenueSyncVenue> settingsVenues = new();
    private bool settingsVenuePickerExpanded;
    private string? settingsError;
    private string? settingsCharacterLinkStatus;

    private void DrawSettings(Rect area)
    {
        var scale = UiScale.Current;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), Loc.T(L.VenueSync.SyncSettings), back);

        if (settingsKeyInput.Length == 0 && configuration.VenueSyncApiKey.Length > 0)
        {
            settingsKeyInput = configuration.VenueSyncApiKey;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.VenueSync.ApiKeyHeader), theme);

            var apiKeyCard = GroupCard.Begin(theme, 1, Metrics.Size.Row, fillOverride: ui.Palette.CardFill);
            var keyRow = apiKeyCard.NextRow();
            var eyeWidth = 60f * scale;
            var eyeHeight = Metrics.Size.FieldHeight * scale;
            var fieldWidth = MathF.Max(1f, keyRow.Width - eyeWidth - Metrics.Space.Sm * scale);
            var keyFieldMin = new Vector2(keyRow.Min.X, keyRow.Center.Y - eyeHeight * 0.5f);
            var keyFieldMax = new Vector2(keyRow.Min.X + fieldWidth, keyRow.Center.Y + eyeHeight * 0.5f);
            Squircle.Fill(ImGui.GetWindowDrawList(), keyFieldMin, keyFieldMax, Metrics.Radius.Field * scale,
                ImGui.GetColorU32(ui.Palette.FieldSurface));
            ImGui.SetCursorScreenPos(new Vector2(keyFieldMin.X, keyRow.Center.Y - ImGui.GetFrameHeight() * 0.5f));
            ImGui.SetNextItemWidth(fieldWidth);
            var flags = settingsKeyVisible ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password;
            using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
            {
                if (ImGui.InputTextWithHint("##sync-api-key", Loc.T(L.VenueSync.ApiKeyHint), ref settingsKeyInput,
                        128, flags))
                {
                    configuration.VenueSyncApiKey = settingsKeyInput;
                    configuration.Save();
                }
            }

            var eyeRow = new Rect(
                new Vector2(keyRow.Min.X + fieldWidth + Metrics.Space.Sm * scale, keyRow.Center.Y - eyeHeight * 0.5f),
                new Vector2(keyRow.Max.X, keyRow.Center.Y + eyeHeight * 0.5f));
            if (ui.PillButton(eyeRow, settingsKeyVisible ? Loc.T(L.VenueSync.Hide) : Loc.T(L.VenueSync.Show), false))
            {
                settingsKeyVisible = !settingsKeyVisible;
            }

            apiKeyCard.End();
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            SettingsSection.Hint(Loc.T(L.VenueSync.ApiKeySourceHint), theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));

            var websiteOrigin = ImGui.GetCursorScreenPos();
            var websiteWidth = ImGui.GetContentRegionAvail().X;
            var websiteHeight = Metrics.Size.FieldHeight * scale;
            var websiteRect = new Rect(websiteOrigin,
                new Vector2(websiteOrigin.X + websiteWidth, websiteOrigin.Y + websiteHeight));
            if (ui.PillButton(websiteRect, Loc.T(L.VenueSync.OpenWebsite), false))
            {
                UrlActions.OpenInBrowser("https://xivvenuemanager.com");
            }

            ImGui.SetCursorScreenPos(websiteOrigin);
            ImGui.Dummy(new Vector2(websiteWidth, websiteHeight));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

            DrawPatronTrackingSection(scale);

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

            SettingsSection.Header(Loc.T(L.VenueSync.VenueHeader), theme);

            var venueLabel = string.IsNullOrEmpty(configuration.VenueSyncSelectedVenueName)
                ? Loc.T(L.VenueSync.SelectVenue)
                : configuration.VenueSyncSelectedVenueName;
            var venueExpanded = settingsVenuePickerExpanded;
            var venueCard = GroupCard.Begin(theme, 1 + (venueExpanded ? settingsVenues.Count : 0),
                Metrics.Size.Row, fillOverride: ui.Palette.CardFill);
            if (SettingsRow.Disclosure(venueCard.NextRow(), Loc.T(L.VenueSync.Venue), venueLabel, theme))
            {
                if (settingsVenues.Count == 0)
                {
                    _ = LoadVenuesAsync();
                }

                settingsVenuePickerExpanded = !venueExpanded;
            }

            if (venueExpanded)
            {
                foreach (var venue in settingsVenues)
                {
                    var selected = venue.Id == configuration.VenueSyncSelectedVenueId;
                    if (SettingsRow.Selectable(venueCard.NextRow(), venue.Name, selected, theme,
                            $"venuesync.settings.venue-{venue.Id}"))
                    {
                        configuration.VenueSyncSelectedVenueId = venue.Id;
                        configuration.VenueSyncSelectedVenueName = venue.Name;
                        configuration.Save();
                        state.EnsureShiftsFresh(true);
                        settingsVenuePickerExpanded = false;
                    }
                }
            }

            venueCard.End();

            if (settingsError is not null)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
                var errorOrigin = ImGui.GetCursorScreenPos();
                var errorWidth = ImGui.GetContentRegionAvail().X;
                Marquee.DrawLeftAuto("venuesync.settings.error", settingsError, errorOrigin.X, errorOrigin.Y,
                    errorWidth, TextStyles.Caption1, theme.Danger);
                ImGui.Dummy(new Vector2(0f, 20f * scale));
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

            if (!string.IsNullOrEmpty(configuration.VenueSyncSelectedVenueId))
            {
                DrawHouseLinkSection(scale);
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            }

            var localName = gameData.LocalPlayer?.Name.TextValue;
            var localWorldId = gameData.LocalHomeWorldId;
            var localWorld = localWorldId != 0 ? gameData.WorldName(localWorldId) : string.Empty;
            if (string.IsNullOrEmpty(localName) || string.IsNullOrEmpty(localWorld))
            {
                DrawCompactHint(FontAwesomeIcon.User, Loc.T(L.VenueSync.NoCharacterDetected), scale);
                return;
            }

            SettingsSection.Header(Loc.T(L.VenueSync.CharacterHeader), theme);

            var characterCard = GroupCard.Begin(theme, 2, Metrics.Size.Row, fillOverride: ui.Palette.CardFill);
            SettingsRow.Info(characterCard.NextRow(), Loc.T(L.VenueSync.Character), $"{localName} @ {localWorld}",
                theme);
            var linkLabel = settingsCharacterLinkStatus ?? Loc.T(L.VenueSync.LinkThisCharacter);
            var linkRow = characterCard.NextRow();
            var linkButtonHeight = Metrics.Size.FieldHeight * scale;
            var linkButtonRect = new Rect(new Vector2(linkRow.Min.X, linkRow.Center.Y - linkButtonHeight * 0.5f),
                new Vector2(linkRow.Max.X, linkRow.Center.Y + linkButtonHeight * 0.5f));
            if (ui.PillButton(linkButtonRect, linkLabel, true))
            {
                _ = LinkCharacterAsync(localName, localWorld);
            }

            characterCard.End();
        }
    }

    private void DrawCompactHint(FontAwesomeIcon icon, string message, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        AppSkin.Icon(new Vector2(centerX, origin.Y + 4f * scale), icon.ToIconString(), ui.MutedInk, 1.3f);
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 34f * scale), message, ui.MutedInk,
            TextStyles.Footnote);
        ImGui.Dummy(new Vector2(0f, 46f * scale));
    }

    private void DrawPatronTrackingSection(float scale)
    {
        SettingsSection.Header(Loc.T(L.VenueSync.PatronTrackingHeader), theme);

        var isEnabled = configuration.VenueSyncPatronTrackingEnabled;
        var rowCount = isEnabled ? 2 : 1;
        var card = GroupCard.Begin(theme, rowCount, Metrics.Size.Row, fillOverride: ui.Palette.CardFill);
        var trackRow = card.NextRow();
        var trackEnabled = SettingsRow.Bool(trackRow, Loc.T(L.VenueSync.TrackPatrons), isEnabled, theme);
        if (trackEnabled != isEnabled)
        {
            configuration.VenueSyncPatronTrackingEnabled = trackEnabled;
            configuration.Save();
        }

        if (isEnabled)
        {
            var onlyDuringEventsRow = card.NextRow();
            var onlyDuringEvents = SettingsRow.Bool(onlyDuringEventsRow, Loc.T(L.VenueSync.OnlyDuringEvents),
                configuration.VenueSyncPatronTrackingOnlyDuringEvents, theme);
            if (onlyDuringEvents != configuration.VenueSyncPatronTrackingOnlyDuringEvents)
            {
                configuration.VenueSyncPatronTrackingOnlyDuringEvents = onlyDuringEvents;
                configuration.Save();
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(L.VenueSync.TrackPatronsHint), theme);
    }

    private void DrawHouseLinkSection(float scale)
    {
        SettingsSection.Header(Loc.T(L.VenueSync.YourPlotHeader), theme);

        var currentHouse = VenueSyncHouseDetector.Current(gameData);

        var statusText = currentHouse is not null
            ? Loc.T(L.VenueSync.CurrentlyIn, currentHouse.Value.Label)
            : Loc.T(L.VenueSync.NotInsideHouse);
        var statusOrigin = ImGui.GetCursorScreenPos();
        var statusWidth = ImGui.GetContentRegionAvail().X;
        Marquee.DrawLeftAuto("venuesync.settings.house-status", statusText, statusOrigin.X, statusOrigin.Y,
            statusWidth, TextStyles.Body, currentHouse is not null ? ui.TitleInk : ui.MutedInk);
        ImGui.Dummy(new Vector2(0f, Typography.Measure(statusText, TextStyles.Body).Y + Metrics.Space.Sm * scale));

        if (currentHouse is not null)
        {
            var linkRowOrigin = ImGui.GetCursorScreenPos();
            var linkButtonHeight = Metrics.Size.FieldHeight * scale;
            var linkRow = new Rect(linkRowOrigin,
                new Vector2(linkRowOrigin.X + ImGui.GetContentRegionAvail().X, linkRowOrigin.Y + linkButtonHeight));
            if (ui.PillButton(linkRow, Loc.T(L.VenueSync.SetAsVenuePlot, configuration.VenueSyncSelectedVenueName),
                    true))
            {
                configuration.VenueSyncHouseLinks[currentHouse.Value.HouseId] = configuration.VenueSyncSelectedVenueId;
                configuration.Save();
            }

            ImGui.SetCursorScreenPos(new Vector2(linkRowOrigin.X, linkRowOrigin.Y + linkButtonHeight));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        SettingsSection.Header(Loc.T(L.VenueSync.LinkedPlotsHeader), theme);

        var linkedHouseIds = new List<long>();
        foreach (var pair in configuration.VenueSyncHouseLinks)
        {
            if (pair.Value == configuration.VenueSyncSelectedVenueId)
            {
                linkedHouseIds.Add(pair.Key);
            }
        }

        if (linkedHouseIds.Count == 0)
        {
            DrawCompactHint(FontAwesomeIcon.MapMarkerAlt, Loc.T(L.VenueSync.NoLinkedPlots), scale);
            return;
        }

        var unlinkButtonWidth = 80f * scale;
        var linkedCard = GroupCard.Begin(theme, linkedHouseIds.Count, Metrics.Size.Row,
            fillOverride: ui.Palette.CardFill);
        for (var index = 0; index < linkedHouseIds.Count; index++)
        {
            var houseId = linkedHouseIds[index];
            var row = linkedCard.NextRow();
            var label = VenueSyncHouseDetector.LabelFor(houseId, gameData);
            var labelMaxWidth = MathF.Max(1f, row.Width - unlinkButtonWidth - Metrics.Space.Sm * scale);
            Marquee.DrawLeftAuto($"venuesync.settings.linked-plot-{houseId}", label, row.Min.X,
                row.Center.Y - Typography.Measure(label, TextStyles.Body).Y * 0.5f, labelMaxWidth, TextStyles.Body,
                theme.TextStrong);

            var unlinkHeight = Metrics.Size.FieldHeight * scale;
            var unlinkRect = new Rect(new Vector2(row.Max.X - unlinkButtonWidth, row.Center.Y - unlinkHeight * 0.5f),
                new Vector2(row.Max.X, row.Center.Y + unlinkHeight * 0.5f));
            if (AppSkin.DangerPillButton(unlinkRect, Loc.T(L.VenueSync.Unlink), theme))
            {
                configuration.VenueSyncHouseLinks.Remove(houseId);
                configuration.Save();
            }
        }

        linkedCard.End();
    }

    private async Task LoadVenuesAsync()
    {
        try
        {
            var response = await client.GetVenuesAsync(CancellationToken.None).ConfigureAwait(false);
            if (response is not null)
            {
                settingsVenues = response.Venues;
                settingsError = null;
            }
            else
            {
                settingsError = Loc.T(L.VenueSync.FailedToLoadVenues);
            }
        }
        catch (Exception)
        {
            settingsError = Loc.T(L.VenueSync.FailedToLoadVenues);
        }
    }

    private async Task LinkCharacterAsync(string characterName, string world)
    {
        settingsCharacterLinkStatus = Loc.T(L.VenueSync.Linking);
        try
        {
            var response = await client.LinkCharacterAsync(characterName, world, CancellationToken.None)
                .ConfigureAwait(false);
            if (response?.Character is not null)
            {
                settingsCharacterLinkStatus = Loc.T(L.VenueSync.Linked);
            }
            else
            {
                settingsCharacterLinkStatus = response?.Error ?? Loc.T(L.VenueSync.FailedToLink);
            }
        }
        catch (Exception)
        {
            settingsCharacterLinkStatus = Loc.T(L.VenueSync.FailedToLink);
        }
    }
}
