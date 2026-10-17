using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class DependencySetup
{
    public static void Card(AppSkin ui, PhoneTheme theme, Rect card, MediaDependency dependency, LocString name,
        LocString detail, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var snapshot = dependency.Snapshot();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale);

        var pad = Metrics.Space.Md * scale;
        var markerRadius = 7f * scale;
        var markerCenter = new Vector2(card.Min.X + pad + markerRadius, card.Min.Y + pad + markerRadius);
        Marker(ui, theme, drawList, markerCenter, markerRadius, snapshot);

        var textLeft = markerCenter.X + markerRadius + Metrics.Space.Md * scale;
        var textRight = card.Max.X - pad;
        var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + pad),
            Typography.FitText(Loc.T(name), textRight - textLeft, TextStyles.BodyEmphasized), ui.TitleInk,
            TextStyles.BodyEmphasized);

        var statusText = StatusText(snapshot);
        var statusColor = snapshot.State == DependencyState.Failed ? theme.Danger : ui.MutedInk;
        var detailY = card.Min.Y + pad + nameHeight + 1f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, detailY),
            Typography.FitText(Loc.T(detail), textRight - textLeft, TextStyles.Caption1), ui.MutedInk,
            TextStyles.Caption1);

        var statusY = detailY + Typography.LineHeight(TextStyles.Caption1) + 3f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, statusY),
            Typography.FitText(statusText, textRight - textLeft, TextStyles.Caption1), statusColor,
            TextStyles.Caption1);

        if (snapshot.State != DependencyState.Downloading)
        {
            return;
        }

        var trackY = card.Max.Y - pad - 2f * scale;
        var track = new Rect(new Vector2(textLeft, trackY - 2f * scale),
            new Vector2(textRight, trackY + 2f * scale));
        Scrubber.Draw(track, snapshot.Fraction, ui.Accent, Palette.WithAlpha(ui.MutedInk, 0.3f), 1f);
    }

    public static bool IsBusy(DependencyProgress snapshot) =>
        snapshot.State == DependencyState.Checking || IsInstalling(snapshot);

    public static bool IsInstalling(DependencyProgress snapshot) =>
        snapshot.State is DependencyState.Downloading or DependencyState.Installing;

    public static string StatusText(DependencyProgress snapshot)
    {
        switch (snapshot.State)
        {
            case DependencyState.Ready:
                return Loc.T(L.AetherStream.SetupReady);
            case DependencyState.Checking:
                return Loc.T(L.AetherStream.SetupChecking);
            case DependencyState.Installing:
                return Loc.T(L.AetherStream.SetupInstalling);
            case DependencyState.Failed:
                return snapshot.FailureReason ?? Loc.T(L.AetherStream.SetupFailed);
            case DependencyState.Downloading:
                return snapshot.TotalBytes > 0
                    ? string.Format(Loc.T(L.AetherStream.SetupProgress), FormatMegabytes(snapshot.ReceivedBytes),
                        FormatMegabytes(snapshot.TotalBytes))
                    : Loc.T(L.AetherStream.SetupDownloading);
            default:
                return snapshot.TotalBytes > 0
                    ? string.Format(Loc.T(L.AetherStream.SetupSize), FormatMegabytes(snapshot.TotalBytes))
                    : Loc.T(L.AetherStream.SetupWaiting);
        }
    }

    public static string FormatMegabytes(long bytes) =>
        (bytes / (1024d * 1024d)).ToString("0.#", Loc.Culture);

    private static void Marker(AppSkin ui, PhoneTheme theme, ImDrawListPtr drawList, Vector2 center, float radius,
        DependencyProgress snapshot)
    {
        switch (snapshot.State)
        {
            case DependencyState.Ready:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.ToggleOn), 20);
                AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Check), new Vector4(1f, 1f, 1f, 1f),
                    0.34f);
                return;
            case DependencyState.Failed:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.Danger), 20);
                return;
            case DependencyState.Downloading:
            case DependencyState.Installing:
            case DependencyState.Checking:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.Accent), 20);
                return;
            default:
                drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.6f)), 20,
                    Metrics.Stroke.Thin);
                return;
        }
    }
}
