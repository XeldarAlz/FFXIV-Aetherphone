using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private static readonly int[] QualityOptions = [144, 240, 360, 480, 720, 1080];
    private static readonly string[] QualityLabels = ["144p", "240p", "360p", "480p", "720p", "1080p"];

    private readonly DropdownMenu qualityMenu = new();
    private readonly DropdownMenu.Item[] qualityItems = new DropdownMenu.Item[QualityOptions.Length];
    private Rect qualityRowRect;

    private void DrawSettings(Rect area, float scale)
    {
        SocialChrome.DrawScreenHeader(area, Loc.T(L.AetherStream.SettingsTitle), Ink, back, ScreenTitleStyle);
        var content = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var dependencies = screen.Engine.Dependencies;
        qualityMenu.Gate();
        using (AppSurface.Begin(content))
        {
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionPlayback), accentedTheme);
            var playbackCard = GroupCard.Begin(accentedTheme, 3);
            DrawQualityRow(playbackCard.NextRow());
            var hideNameplates = SettingsRow.Bool(playbackCard.NextRow(),
                Loc.T(L.AetherStream.SettingsHideNameplates), configuration.VideoHideNameplates, accentedTheme);
            var muteInBackground = SettingsRow.Bool(playbackCard.NextRow(),
                Loc.T(L.AetherStream.SettingsMuteInBackground), configuration.VideoMuteInBackground, accentedTheme,
                hint: Loc.T(L.AetherStream.SettingsMuteInBackgroundHint));
            playbackCard.End();
            if (hideNameplates != configuration.VideoHideNameplates)
            {
                configuration.VideoHideNameplates = hideNameplates;
                configuration.Save();
            }

            if (muteInBackground != configuration.VideoMuteInBackground)
            {
                configuration.VideoMuteInBackground = muteInBackground;
                configuration.Save();
                video.MuteInBackground = muteInBackground;
            }

            Gap(Metrics.Space.Md);
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionWatching), accentedTheme);
            var watchingCard = GroupCard.Begin(accentedTheme, 1);
            var sharePresence = SettingsRow.Bool(watchingCard.NextRow(),
                Loc.T(L.AetherStream.SettingsShareWatchPresence), configuration.VideoShareWatchPresence,
                accentedTheme, hint: Loc.T(L.AetherStream.SettingsShareWatchPresenceHint));
            watchingCard.End();
            if (sharePresence != configuration.VideoShareWatchPresence)
            {
                configuration.VideoShareWatchPresence = sharePresence;
                configuration.Save();
            }

            Gap(Metrics.Space.Md);
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionStatus), accentedTheme);
            var statusCard = GroupCard.Begin(accentedTheme, 7);
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyStatus),
                DependencyStatusText(dependencies, dependencies.VideoLibrary), accentedTheme);
            DrawDependencyAction(statusCard.NextRow(), dependencies, dependencies.VideoLibrary,
                L.AetherStream.SettingsDownloadMpv, L.AetherStream.SettingsUpdateMpv);
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyYtdlp),
                DependencyStatusText(dependencies, dependencies.LinkResolver), accentedTheme);
            DrawDependencyAction(statusCard.NextRow(), dependencies, dependencies.LinkResolver,
                L.AetherStream.SettingsDownloadYtdlp, L.AetherStream.SettingsUpdateYtdlp);
            var nightlyResolver = SettingsRow.Bool(statusCard.NextRow(),
                Loc.T(L.AetherStream.SettingsNightlyResolver), configuration.VideoNightlyLinkResolver, accentedTheme,
                hint: Loc.T(L.AetherStream.SettingsNightlyResolverHint));
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyDeno),
                DependencyStatusText(dependencies, dependencies.JsRuntime), accentedTheme);
            DrawDependencyAction(statusCard.NextRow(), dependencies, dependencies.JsRuntime,
                L.AetherStream.SettingsDownloadDeno, L.AetherStream.SettingsUpdateDeno);
            statusCard.End();
            if (nightlyResolver != configuration.VideoNightlyLinkResolver)
            {
                configuration.VideoNightlyLinkResolver = nightlyResolver;
                configuration.Save();
                SwitchLinkResolverChannel(dependencies, nightlyResolver);
            }

            Gap(Metrics.Space.Md);
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionAdvanced), accentedTheme);
            var wine = WineEnvironment.IsWine;
            var advancedCard = GroupCard.Begin(accentedTheme, wine ? 2 : 1);
            var hardwareDecoding = SettingsRow.Bool(advancedCard.NextRow(),
                Loc.T(L.AetherStream.SettingsHardwareDecoding), configuration.VideoHardwareDecoding, accentedTheme,
                hint: Loc.T(L.AetherStream.SettingsHardwareDecodingHint));
            var allowInsecure = configuration.VideoAllowInsecureDirectUrls;
            if (wine)
            {
                allowInsecure = SettingsRow.Bool(advancedCard.NextRow(), Loc.T(L.AetherStream.SettingsTls),
                    allowInsecure, accentedTheme, hint: Loc.T(L.AetherStream.SettingsTlsHint));
            }

            advancedCard.End();
            if (hardwareDecoding != configuration.VideoHardwareDecoding)
            {
                configuration.VideoHardwareDecoding = hardwareDecoding;
                configuration.Save();
                video.HardwareDecoding = hardwareDecoding;
            }

            if (allowInsecure != configuration.VideoAllowInsecureDirectUrls)
            {
                configuration.VideoAllowInsecureDirectUrls = allowInsecure;
                configuration.Save();
                video.AllowInsecureDirectUrls = allowInsecure;
            }

            Gap(Metrics.Space.Md);
            var helpCard = GroupCard.Begin(accentedTheme, 1);
            if (SettingsRow.Disclosure(helpCard.NextRow(), Loc.T(L.AetherStream.InfoTitle), string.Empty,
                    accentedTheme))
            {
                router.Push(new StreamRoute(StreamScreen.Info));
            }

            helpCard.End();
            Gap(Metrics.Space.Lg);
        }

        DrawQualityMenu(area);
    }

    private void DrawQualityRow(Rect row)
    {
        qualityRowRect = row;
        if (SettingsRow.Disclosure(row, Loc.T(L.AetherStream.SettingsMaxQuality),
                QualityLabels[QualityIndex(configuration.VideoMaxQualityHeight)], accentedTheme))
        {
            qualityMenu.Toggle("aetherstream.quality", qualityRowRect);
        }
    }

    private static int QualityIndex(int height)
    {
        for (var index = 0; index < QualityOptions.Length; index++)
        {
            if (QualityOptions[index] == height)
            {
                return index;
            }
        }

        return QualityOptions.Length - 2;
    }

    private void DrawQualityMenu(Rect area)
    {
        if (!qualityMenu.IsOpenFor("aetherstream.quality"))
        {
            return;
        }

        for (var index = 0; index < QualityOptions.Length; index++)
        {
            qualityItems[index] = new DropdownMenu.Item(QualityLabels[index],
                Selected: QualityOptions[index] == configuration.VideoMaxQualityHeight);
        }

        var picked = qualityMenu.Draw(area, accentedTheme, qualityItems);
        if (picked < 0)
        {
            return;
        }

        configuration.VideoMaxQualityHeight = QualityOptions[picked];
        configuration.Save();
        video.MaxQualityHeight = QualityOptions[picked];
    }

    private void DrawDependencyAction(Rect row, MediaDependencies dependencies, MediaDependency dependency,
        LocString installLabel, LocString updateLabel)
    {
        var snapshot = dependency.Snapshot();
        var busy = snapshot.State is DependencyState.Checking or DependencyState.Downloading
            or DependencyState.Installing;
        var label = busy
            ? Loc.T(L.AetherStream.SettingsDownloading)
            : snapshot.State == DependencyState.Ready ? Loc.T(updateLabel) : Loc.T(installLabel);

        if (!SettingsRow.Action(row, label, busy ? accentedTheme.TextMuted : accentedTheme.Accent, accentedTheme)
            || busy)
        {
            return;
        }

        dependencyWork.Run("install " + dependency.Id,
            async token => await dependencies.ReinstallAsync(dependency, token).ConfigureAwait(false));
    }

    private void SwitchLinkResolverChannel(MediaDependencies dependencies, bool nightly)
    {
        dependencies.UseNightlyLinkResolver(nightly);
        if (dependencies.LinkResolverPath is null)
        {
            return;
        }

        dependencyWork.Run("switch link resolver",
            async token => await dependencies.UpdateIfNewerAsync(dependencies.LinkResolver, token)
                .ConfigureAwait(false));
    }

    private static string DependencyStatusText(MediaDependencies dependencies, MediaDependency dependency)
    {
        var snapshot = dependency.Snapshot();
        switch (snapshot.State)
        {
            case DependencyState.Checking:
                return Loc.T(L.AetherStream.SetupChecking);
            case DependencyState.Downloading:
                return DependencySizeText(snapshot);
            case DependencyState.Installing:
                return Loc.T(L.AetherStream.SetupInstalling);
            case DependencyState.Failed:
                return snapshot.FailureReason ?? Loc.T(L.AetherStream.SetupFailed);
        }

        if (snapshot.State != DependencyState.Ready)
        {
            return Loc.T(L.AetherStream.SettingsDependencyNotInstalled);
        }

        if (dependency.RequiresRestart)
        {
            return Loc.T(L.AetherStream.SettingsDependencyRestartPending);
        }

        return dependencies.HasUpdate(dependency)
            ? Loc.T(L.AetherStream.SettingsDependencyUpdateAvailable)
            : Loc.T(L.AetherStream.SettingsDependencyOk);
    }

    private static string DependencySizeText(DependencyProgress snapshot)
    {
        if (snapshot.TotalBytes <= 0)
        {
            return Loc.T(L.AetherStream.SetupDownloading);
        }

        return string.Format(Loc.T(L.AetherStream.SetupProgress),
            DependencySetup.FormatMegabytes(snapshot.ReceivedBytes),
            DependencySetup.FormatMegabytes(snapshot.TotalBytes));
    }

    private void DrawInfo(Rect area, float scale)
    {
        SocialChrome.DrawScreenHeader(area, Loc.T(L.AetherStream.InfoTitle), Ink, back, ScreenTitleStyle);
        var content = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(content))
        {
            DrawInfoEntry(L.AetherStream.InfoStartupTitle, L.AetherStream.InfoStartupBody);
            DrawInfoEntry(L.AetherStream.InfoPartiesTitle, L.AetherStream.InfoPartiesBody);
            DrawInfoEntry(L.AetherStream.InfoCodesTitle, L.AetherStream.InfoCodesBody);
            DrawInfoEntry(L.AetherStream.InfoSitesTitle, L.AetherStream.InfoSitesBody);
            DrawInfoEntry(L.AetherStream.InfoFailuresTitle, L.AetherStream.InfoFailuresBody);
            DrawInfoEntry(L.AetherStream.InfoVpnTitle, L.AetherStream.InfoVpnBody);
            Gap(Metrics.Space.Lg);
        }
    }

    private void DrawInfoEntry(LocString title, LocString body)
    {
        SettingsSection.Header(Loc.T(title), accentedTheme);
        SettingsSection.Hint(Loc.T(body), accentedTheme);
        Gap(Metrics.Space.Md);
    }
}
