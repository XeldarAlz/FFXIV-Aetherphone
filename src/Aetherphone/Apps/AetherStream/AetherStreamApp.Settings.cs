using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    // Capped at 1080p, not YouTube's actual ceiling - the screen's own texture is a fixed
    // 1920x1080 (VideoEngine.ScreenWidth/Height), so anything higher would just be downscaled
    // into the same buffer by mpv for no visible gain, at the cost of extra bandwidth and decode
    // work. Enforced via mpv's own ytdl-format option (see MpvRenderer.Initialize), not a
    // separate resolver - AlphaChannel's engine resolves YouTube itself through mpv's bundled
    // ytdl_hook/yt-dlp.
    private static readonly int[] QualityOptions = { 144, 240, 360, 480, 720, 1080 };
    private readonly DropdownMenu qualityMenu = new();
    private Rect qualityRowRect;

    private void DrawSettings(PhoneContext context, Rect area, float scale)
    {
        // Every other tab in this app draws through `ui` (AppSkin, accented pink via
        // AppAccents.For("aetherstream")). SettingsRow/Toggle/GroupCard/AppHeader all take a
        // plain PhoneTheme instead, so without this the whole Settings screen silently fell back
        // to the system theme's accent/toggle-green - a different colour from every other screen
        // in this same app. PhoneTheme is a sealed class, not a record, so it can't use `with`;
        // this copies it field-by-field with just Accent/ToggleOn swapped.
        var accentedTheme = AccentedTheme(context.Theme);
        var accentedContext = new PhoneContext(context.Content, accentedTheme, context.Navigation);

        AppHeader.Draw(accentedContext, Loc.T(L.AetherStream.SettingsTitle), () => router.Pop());

        var margin = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top), new Vector2(area.Max.X - margin, area.Max.Y));

        var resources = screen.Engine.Resources;

        using (AppSurface.Begin(content))
        {
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionStatus), accentedTheme);
            var statusCard = GroupCard.Begin(accentedTheme, 3);
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyStatus),
                MpvStatusText(resources), accentedTheme);
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyYtdlp),
                YtdlpStatusText(resources), accentedTheme);
            if (SettingsRow.Disclosure(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsScreen), ScreenStateText(),
                    accentedTheme))
            {
                activeTab = AetherStreamTab.Casting;
                router.Pop();
            }

            statusCard.End();

            if (NeedsMpvDownload(resources) || NeedsYtdlpDownload(resources))
            {
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                var buttonHeight = 34f * scale;
                var buttonTop = ImGui.GetCursorScreenPos().Y;

                if (NeedsMpvDownload(resources))
                {
                    var mpvRect = new Rect(new Vector2(content.Min.X, buttonTop),
                        new Vector2(content.Max.X, buttonTop + buttonHeight));
                    var mpvLabel = resources.GetLocationMPV() is null
                        ? Loc.T(L.AetherStream.SettingsDownloadMpv)
                        : Loc.T(L.AetherStream.SettingsUpdateMpv);
                    if (SmallButton(mpvRect, mpvLabel, !mpvDownloading, scale))
                    {
                        mpvDownloading = true;
                        dependencyWork.Run("download mpv", async token =>
                        {
                            // MpvCheckResult can still be empty here: the initial check fired at
                            // Resources construction may not have finished yet, or may have
                            // failed outright (no network yet at plugin load, GitHub rate limit).
                            // Re-running it before downloading means a tap always either starts a
                            // real download or retries the check, never hands DownloadMPVAsync an
                            // empty URL (that used to throw straight into HttpClient).
                            if (resources.MpvCheckResult[0].Length == 0)
                            {
                                await resources.CheckMPVAsync().ConfigureAwait(false);
                            }

                            if (resources.MpvCheckResult[0].Length > 0)
                            {
                                await resources.DownloadMPVAsync().ConfigureAwait(false);
                            }
                        }, () => mpvDownloading = false);
                    }

                    buttonTop += buttonHeight + 8f * scale;
                }

                if (NeedsYtdlpDownload(resources))
                {
                    var ytdlpRect = new Rect(new Vector2(content.Min.X, buttonTop),
                        new Vector2(content.Max.X, buttonTop + buttonHeight));
                    var ytdlpLabel = resources.GetLocationYTDLP() is null
                        ? Loc.T(L.AetherStream.SettingsDownloadYtdlp)
                        : Loc.T(L.AetherStream.SettingsUpdateYtdlp);
                    if (SmallButton(ytdlpRect, ytdlpLabel, !ytdlpDownloading, scale))
                    {
                        ytdlpDownloading = true;
                        dependencyWork.Run("download yt-dlp", async token =>
                        {
                            // Same "retry the check first if it's empty" reasoning as the mpv
                            // button above.
                            if (resources.YtdlpCheckResult[0].Length == 0)
                            {
                                await resources.CheckYTDLPAsync().ConfigureAwait(false);
                            }

                            if (resources.YtdlpCheckResult[0].Length > 0)
                            {
                                await resources.DownloadYTDLPAsync().ConfigureAwait(false);
                            }
                        }, () => ytdlpDownloading = false);
                    }
                }

                ImGui.SetCursorScreenPos(new Vector2(content.Min.X, buttonTop + buttonHeight));
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionPlayback), accentedTheme);
            var playbackCard = GroupCard.Begin(accentedTheme, 2);
            var hideNameplates = SettingsRow.Bool(playbackCard.NextRow(),
                Loc.T(L.AetherStream.SettingsHideNameplates), configuration.VideoHideNameplates, accentedTheme);
            DrawQualityRow(playbackCard.NextRow(), accentedTheme);
            playbackCard.End();
            if (hideNameplates != configuration.VideoHideNameplates)
            {
                configuration.VideoHideNameplates = hideNameplates;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionWatching), accentedTheme);
            var watchingCard = GroupCard.Begin(accentedTheme, 2);
            var sharePresence = SettingsRow.Bool(watchingCard.NextRow(),
                Loc.T(L.AetherStream.SettingsShareWatchPresence), configuration.VideoShareWatchPresence,
                accentedTheme);
            // See WatchAlongSession.PendingRequests. Layers on top of the mutual-contact + block
            // gate, it does not replace it - a non-contact still can't reach the host either way.
            var approvalRequired = SettingsRow.Bool(watchingCard.NextRow(),
                Loc.T(L.AetherStream.SettingsApprovalRequired), configuration.VideoStreamApprovalRequired,
                accentedTheme);
            watchingCard.End();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsShareWatchPresenceHint), accentedTheme);
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsApprovalRequiredHint), accentedTheme);
            if (sharePresence != configuration.VideoShareWatchPresence)
            {
                configuration.VideoShareWatchPresence = sharePresence;
                configuration.Save();
            }

            if (approvalRequired != configuration.VideoStreamApprovalRequired)
            {
                configuration.VideoStreamApprovalRequired = approvalRequired;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionAdvanced), accentedTheme);
            var hardwareCard = GroupCard.Begin(accentedTheme, 1);
            var hardwareDecoding = SettingsRow.Bool(hardwareCard.NextRow(),
                Loc.T(L.AetherStream.SettingsHardwareDecoding), configuration.VideoHardwareDecoding, accentedTheme);
            hardwareCard.End();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsHardwareDecodingHint), accentedTheme);
            if (hardwareDecoding != configuration.VideoHardwareDecoding)
            {
                configuration.VideoHardwareDecoding = hardwareDecoding;
                configuration.Save();
                video.HardwareDecoding = hardwareDecoding;
            }

            var allowInsecure = configuration.VideoAllowInsecureDirectUrls;
            if (WineEnvironment.IsWine)
            {
                ImGui.Dummy(new Vector2(0f, 12f * scale));
                var tlsCard = GroupCard.Begin(accentedTheme, 1);
                allowInsecure = SettingsRow.Bool(tlsCard.NextRow(), Loc.T(L.AetherStream.SettingsTls), allowInsecure,
                    accentedTheme);
                tlsCard.End();
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                SettingsSection.Hint(Loc.T(L.AetherStream.SettingsTlsHint), accentedTheme);
            }

            if (allowInsecure != configuration.VideoAllowInsecureDirectUrls)
            {
                configuration.VideoAllowInsecureDirectUrls = allowInsecure;
                configuration.Save();
                video.AllowInsecureDirectUrls = allowInsecure;
            }
        }

        qualityMenu.Gate();
        if (qualityMenu.IsOpenFor("aetherstream.quality"))
        {
            var items = new DropdownMenu.Item[QualityOptions.Length];
            for (var index = 0; index < QualityOptions.Length; index++)
            {
                items[index] = new DropdownMenu.Item($"{QualityOptions[index]}p",
                    Selected: QualityOptions[index] == configuration.VideoMaxQualityHeight);
            }

            var picked = qualityMenu.Draw(context.Content, accentedTheme, items);
            if (picked >= 0)
            {
                configuration.VideoMaxQualityHeight = QualityOptions[picked];
                configuration.Save();
                video.MaxQualityHeight = QualityOptions[picked];
            }
        }
    }

    // PhoneTheme is a sealed class with required init-only properties, not a record, so there is
    // no `with` support - this is the only way to hand existing PhoneTheme-typed components
    // (SettingsRow, Toggle, GroupCard, AppHeader, ...) this app's own accent instead of the
    // system theme's, without touching those shared components' own colour logic.
    private static PhoneTheme AccentedTheme(PhoneTheme baseTheme)
    {
        var accent = AppAccents.For("aetherstream");
        return new PhoneTheme
        {
            Case = baseTheme.Case,
            CaseKind = baseTheme.CaseKind,
            CaseTextureId = baseTheme.CaseTextureId,
            ScreenBase = baseTheme.ScreenBase,
            LightWallpaperId = baseTheme.LightWallpaperId,
            DarkWallpaperId = baseTheme.DarkWallpaperId,
            AppBackground = baseTheme.AppBackground,
            GroupedCard = baseTheme.GroupedCard,
            Separator = baseTheme.Separator,
            ToggleOn = accent,
            ToggleOff = baseTheme.ToggleOff,
            Surface = baseTheme.Surface,
            SurfaceMuted = baseTheme.SurfaceMuted,
            TextStrong = baseTheme.TextStrong,
            TextMuted = baseTheme.TextMuted,
            Accent = accent,
            Danger = baseTheme.Danger,
            RailWidth = baseTheme.RailWidth,
            MetalWidth = baseTheme.MetalWidth,
            GlassWidth = baseTheme.GlassWidth,
            DeviceRounding = baseTheme.DeviceRounding,
            TopZoneHeight = baseTheme.TopZoneHeight,
            BottomZoneHeight = baseTheme.BottomZoneHeight,
            SidePadding = baseTheme.SidePadding,
        };
    }

    // Unlike the old bundled Native/libmpv-2.dll this replaces, AlphaChannel's engine downloads
    // mpv-winbuild and yt-dlp itself into Dalamud's plugin config directory the first time it
    // checks (see Resources.Initialize/CheckMPVAsync/CheckYTDLPAsync) - it only checks though,
    // the actual download needs an explicit call, which is what these rows/buttons are for.
    private static string MpvStatusText(Resources resources)
    {
        if (resources.GetLocationMPV() is null)
        {
            return Loc.T(L.AetherStream.SettingsDependencyNotInstalled);
        }

        return resources.MpvCheckResult[0].Length > 0
            ? Loc.T(L.AetherStream.SettingsDependencyUpdateAvailable)
            : Loc.T(L.AetherStream.SettingsDependencyOk);
    }

    private static string YtdlpStatusText(Resources resources)
    {
        if (resources.GetLocationYTDLP() is null)
        {
            return Loc.T(L.AetherStream.SettingsDependencyNotInstalled);
        }

        return resources.YtdlpCheckResult[0].Length > 0
            ? Loc.T(L.AetherStream.SettingsDependencyUpdateAvailable)
            : Loc.T(L.AetherStream.SettingsDependencyOk);
    }

    private static bool NeedsMpvDownload(Resources resources) =>
        resources.GetLocationMPV() is null || resources.MpvCheckResult[0].Length > 0;

    private static bool NeedsYtdlpDownload(Resources resources) =>
        resources.GetLocationYTDLP() is null || resources.YtdlpCheckResult[0].Length > 0;

    private string ScreenStateText() => screen.Engine.IsActive
        ? Loc.T(L.AetherStream.CastingStateReady)
        : Loc.T(L.AetherStream.CastingStateNotReady);

    private void DrawQualityRow(Rect row, PhoneTheme theme)
    {
        qualityRowRect = row;
        if (SettingsRow.Disclosure(row, Loc.T(L.AetherStream.SettingsMaxQuality),
                $"{configuration.VideoMaxQualityHeight}p", theme))
        {
            qualityMenu.Toggle("aetherstream.quality", qualityRowRect);
        }
    }

}
