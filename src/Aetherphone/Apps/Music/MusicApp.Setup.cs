using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float SetupCardHeight = 74f;
    private const float SetupButtonHeight = Button.LargeHeight;

    private bool setupDismissed;
    private bool setupChecked;

    private bool NeedsSetup => songResolver.Media is not null && !songResolver.IsInstalled && !setupDismissed;

    private void DrawSetupGate(Rect area, float scale)
    {
        var media = songResolver.Media;
        if (media is null)
        {
            return;
        }

        ui.Body(area);
        if (!setupChecked)
        {
            setupChecked = true;
            resolverWork.Run("check song components",
                async token => await media.CheckSongSizesAsync(token).ConfigureAwait(false));
        }

        var margin = Metrics.Space.Xl * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, area.Min.Y + margin),
            new Vector2(area.Max.X - margin, area.Max.Y - margin));
        var width = content.Width;
        var drawList = ImGui.GetWindowDrawList();

        var tileSize = 64f * scale;
        var tileMin = new Vector2(content.Center.X - tileSize * 0.5f, content.Min.Y + Metrics.Space.Xl * scale);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tileSize, tileSize),
            tileSize * Metrics.Radius.TileFactor, IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tileSize * 0.5f, tileSize * 0.5f),
            FontAwesomeIcon.Music, AccentRing.Ink, tileSize * 0.5f);

        var titleY = tileMin.Y + tileSize + Metrics.Space.Lg * scale;
        var title = Loc.T(L.Music.SetupTitle);
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, titleY + titleHeight * 0.5f), title,
            ui.TitleInk, TextStyles.Title2);

        var bodyY = titleY + titleHeight + Metrics.Space.Sm * scale;
        var bodyHeight = Typography.DrawWrappedCentered(new Vector2(content.Center.X, bodyY),
            Loc.T(L.Music.SetupBody), ui.MutedInk, TextStyles.Subheadline, width);

        var cardTop = bodyY + bodyHeight + Metrics.Space.Xl * scale;
        var cardHeight = SetupCardHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        DependencySetup.Card(ui, theme, new Rect(new Vector2(content.Min.X, cardTop),
                new Vector2(content.Max.X, cardTop + cardHeight)), media.LinkResolver,
            L.AetherStream.SetupLinkResolver, L.AetherStream.SetupLinkResolverDetail, scale);

        var runtimeTop = cardTop + cardHeight + gap;
        DependencySetup.Card(ui, theme, new Rect(new Vector2(content.Min.X, runtimeTop),
                new Vector2(content.Max.X, runtimeTop + cardHeight)), media.JsRuntime,
            L.AetherStream.SetupJsRuntime, L.AetherStream.SetupJsRuntimeDetail, scale);

        var buttonHeight = SetupButtonHeight * scale;
        var buttonTop = content.Max.Y - buttonHeight - Typography.LineHeight(TextStyles.Subheadline)
            - Metrics.Space.Lg * scale;
        DrawSetupAction(new Rect(new Vector2(content.Min.X, buttonTop),
            new Vector2(content.Max.X, buttonTop + buttonHeight)), media, scale);
    }

    private void DrawSetupAction(Rect button, MediaDependencies media, float scale)
    {
        var resolver = media.LinkResolver.Snapshot();
        var runtime = media.JsRuntime.Snapshot();
        var busy = DependencySetup.IsBusy(resolver) || DependencySetup.IsBusy(runtime);
        var installing = DependencySetup.IsInstalling(resolver) || DependencySetup.IsInstalling(runtime);
        var failed = resolver.State == DependencyState.Failed || runtime.State == DependencyState.Failed;
        var pending = media.PendingSongBytes;
        var label = installing
            ? Loc.T(L.AetherStream.SetupInstalling)
            : busy
                ? Loc.T(L.AetherStream.SetupChecking)
                : failed
                    ? Loc.T(L.AetherStream.SetupRetry)
                    : pending > 0
                        ? string.Format(Loc.T(L.AetherStream.SetupInstallSized),
                            DependencySetup.FormatMegabytes(pending))
                        : Loc.T(L.AetherStream.SetupInstall);

        if (Button.Draw(button, label, ui.Ink, ButtonStyle.Prominent, enabled: !busy) && !busy)
        {
            resolverWork.Run("install song components",
                async token => await media.EnsureSongsReadyAsync(token).ConfigureAwait(false));
        }

        var linkTop = button.Max.Y + Metrics.Space.Sm * scale;
        var linkHeight = Typography.LineHeight(TextStyles.Subheadline);
        var linkRect = new Rect(new Vector2(button.Min.X, linkTop),
            new Vector2(button.Max.X, linkTop + linkHeight));
        var hovered = UiInteract.Hover(linkRect.Min, linkRect.Max);
        Typography.DrawCentered(ImGui.GetWindowDrawList(),
            new Vector2(linkRect.Center.X, linkRect.Center.Y), Loc.T(L.AetherStream.SetupNotNow),
            hovered ? ui.TitleInk : ui.MutedInk, TextStyles.Subheadline);
        if (UiInteract.Click(linkRect.Min, linkRect.Max, hovered))
        {
            setupDismissed = true;
        }
    }
}
