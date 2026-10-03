using Aetherphone.Apps.Music.NowPlaying;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float OutputTitleHeight = 40f;
    private const float OutputRowHeight = 50f;
    private const float OutputSessionHeight = 92f;
    private const float OutputCrossfadeHeight = 76f;
    private const float OutputPadX = 20f;
    private const float OutputIconColumn = 30f;
    private const float OutputIconScale = 0.95f;
    private const float OutputControlRadius = 16f;
    private const float OutputControlGlyph = 6f;
    private const float OutputControlSpacing = 38f;
    private const float OutputMutedAlpha = 0.62f;
    private const float OutputHairlineAlpha = 0.12f;
    private const float OutputHoverAlpha = 0.07f;
    private const float CrossfadeMaximumSeconds = 12f;
    private const int SleepOffIndex = 0;
    private const int SleepTrackEndIndex = 5;

    private readonly Sheet outputSheet = new();
    private readonly ActionSheet sleepMenu = new();
    private readonly ActionSheet.Item[] sleepItems = new ActionSheet.Item[SleepTrackEndIndex + 1];
    private readonly NowPlayingSlider crossfadeSlider = new();
    private int sleepChoice;

    private void DrawNowPlayingPopups(Rect screen)
    {
        using (InputShield.Engage(sleepMenu.CapturesPointer))
        {
            DrawOutputSheet(screen);
        }

        DrawSleepMenu(screen);
        DrawQueueMenu(screen);
    }

    private void DrawOutputSheet(Rect screen)
    {
        if (!outputSheet.CapturesPointer)
        {
            return;
        }

        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        var session = windowsMedia.IsSupported && windowsMedia.Current.HasSession;
        var height = (SheetMetrics.GrabberZone + OutputTitleHeight + OutputRowHeight * 5f + OutputCrossfadeHeight +
                      Metrics.Size.HomeIndicatorInset + (session ? OutputSessionHeight : 0f)) * scale;
        using var layer = ScreenLayer.Begin("music.output", screen, false);
        var frame = outputSheet.Begin(ImGui.GetWindowDrawList(), screen, theme, SheetDetents.Fitted(height),
            SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var drawList = frame.DrawList;
        var content = frame.Content;
        var ink = frame.Ink with { W = frame.Ink.W * frame.Opacity };
        var muted = ink with { W = ink.W * OutputMutedAlpha };
        var left = content.Min.X + OutputPadX * scale;
        var right = content.Max.X - OutputPadX * scale;
        var interactive = frame.Interactive;
        var y = content.Min.Y;
        var title = Loc.T(L.Music.NowPlaying.Output);
        var titleSize = Typography.Measure(title, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(content.Center.X - titleSize.X * 0.5f,
            y + (OutputTitleHeight * scale - titleSize.Y) * 0.5f), title, ink, TextStyles.Headline);
        y += OutputTitleHeight * scale;
        DrawOutputPhoneRow(drawList, left, right, y, ink, scale);
        y += OutputRowHeight * scale;
        if (session)
        {
            DrawOutputSession(drawList, left, right, y, ink, muted, interactive, scale);
            y += OutputSessionHeight * scale;
        }

        Hairline(drawList, left, right, y, ink);
        var jamValue = JamActive ? jam.DisplayCode : Loc.T(L.Music.Jam.Start);
        if (DrawOutputLinkRow(drawList, left, right, y, ink, muted, interactive, scale, FontAwesomeIcon.Users,
                Loc.T(L.Music.Jam.Title), jamValue))
        {
            OpenJamLobby();
        }

        y += OutputRowHeight * scale;
        Hairline(drawList, left, right, y, ink);
        var sleepValue = playback.SleepTimerActive ? SleepStatusText() : Loc.T(L.Common.Off);
        if (DrawOutputLinkRow(drawList, left, right, y, ink, muted, interactive, scale, FontAwesomeIcon.Moon,
                Loc.T(L.Music.NowPlaying.SleepTimer), sleepValue))
        {
            OpenSleepMenu();
        }

        y += OutputRowHeight * scale;
        Hairline(drawList, left, right, y, ink);
        DrawSoundCheckRow(drawList, left, right, y, ink, interactive, frame.Opacity, scale);
        y += OutputRowHeight * scale;
        Hairline(drawList, left, right, y, ink);
        DrawCrossfadeRow(drawList, left, right, y, ink, muted, interactive, scale, delta);
        y += OutputCrossfadeHeight * scale;
        Hairline(drawList, left, right, y, ink);
        if (DrawOutputLinkRow(drawList, left, right, y, ink, muted, interactive, scale, FontAwesomeIcon.Stop,
                Loc.T(L.Music.NowPlaying.StopPlaying), string.Empty))
        {
            outputSheet.Close();
            StopPlaying();
        }

        outputSheet.End(in frame);
    }

    private static void Hairline(ImDrawListPtr drawList, float left, float right, float y, Vector4 ink)
    {
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y),
            ImGui.GetColorU32(ink with { W = ink.W * OutputHairlineAlpha }), UiScale.Current);
    }

    private static void DrawOutputPhoneRow(ImDrawListPtr drawList, float left, float right, float top, Vector4 ink,
        float scale)
    {
        var centerY = top + OutputRowHeight * scale * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + OutputIconColumn * 0.5f * scale, centerY),
            IconGlyph.Of(FontAwesomeIcon.MobileAlt), ink, OutputIconScale);
        var label = Loc.T(L.Music.NowPlaying.ThisPhone);
        var labelLeft = left + (OutputIconColumn + Metrics.Space.Sm) * scale;
        var width = MathF.Max(1f, right - OutputIconColumn * scale - labelLeft);
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelHeight * 0.5f),
            Typography.FitText(label, width, TextStyles.Body), ink, TextStyles.Body);
        AppSkin.Icon(drawList, new Vector2(right - OutputIconColumn * 0.5f * scale, centerY),
            IconGlyph.Of(FontAwesomeIcon.Check), ink, OutputIconScale * 0.85f);
    }

    private void DrawOutputSession(ImDrawListPtr drawList, float left, float right, float top, Vector4 ink,
        Vector4 muted, bool interactive, float scale)
    {
        ref readonly var snapshot = ref windowsMedia.Current;
        var centerY = top + OutputSessionHeight * scale * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + OutputIconColumn * 0.5f * scale, centerY),
            IconGlyph.Of(FontAwesomeIcon.Desktop), ink, OutputIconScale);
        var radius = OutputControlRadius * scale;
        var spacing = OutputControlSpacing * scale;
        var nextCenter = new Vector2(right - radius, centerY);
        var playCenter = new Vector2(nextCenter.X - spacing, centerY);
        var previousCenter = new Vector2(playCenter.X - spacing, centerY);
        var textLeft = left + (OutputIconColumn + Metrics.Space.Sm) * scale;
        var width = MathF.Max(1f, previousCenter.X - radius - Metrics.Space.Sm * scale - textLeft);
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var artistHeight = Typography.LineHeight(TextStyles.Subheadline);
        var textTop = centerY - (captionHeight + titleHeight + artistHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(nowPlayingText.PlayingOn(snapshot.AppName), width, TextStyles.Footnote), muted,
            TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + captionHeight),
            Typography.FitText(snapshot.Title, width, TextStyles.Headline), ink, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + captionHeight + titleHeight),
            Typography.FitText(snapshot.Artist, width, TextStyles.Subheadline), muted, TextStyles.Subheadline);
        var glyph = OutputControlGlyph * scale;
        if (OutputControl(drawList, "music.output.previous", previousCenter, radius, ink,
                interactive && snapshot.CanPrevious))
        {
            windowsMedia.Previous();
        }

        MediaGlyph.Previous(drawList, previousCenter, glyph,
            ImGui.GetColorU32(snapshot.CanPrevious ? ink : muted));
        if (OutputControl(drawList, "music.output.play", playCenter, radius, ink,
                interactive && snapshot.CanPlayPause))
        {
            windowsMedia.TogglePlayPause();
        }

        var playInk = ImGui.GetColorU32(snapshot.CanPlayPause ? ink : muted);
        if (snapshot.IsPlaying)
        {
            MediaGlyph.Pause(drawList, playCenter, glyph, playInk);
        }
        else
        {
            MediaGlyph.Play(drawList, playCenter, glyph, playInk);
        }

        if (OutputControl(drawList, "music.output.next", nextCenter, radius, ink, interactive && snapshot.CanNext))
        {
            windowsMedia.Next();
        }

        MediaGlyph.Next(drawList, nextCenter, glyph, ImGui.GetColorU32(snapshot.CanNext ? ink : muted));
    }

    private static bool OutputControl(ImDrawListPtr drawList, string id, Vector2 center, float radius, Vector4 ink,
        bool enabled)
    {
        var hit = new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(center - hit, center + hit);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.ControlPressedScale);
        drawList.AddCircleFilled(center, radius * grow,
            ImGui.GetColorU32(ink with { W = ink.W * (hovered ? OutputHoverAlpha * 2f : OutputHoverAlpha) }));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(center - hit, center + hit, hovered);
    }

    private static bool DrawOutputLinkRow(ImDrawListPtr drawList, float left, float right, float top, Vector4 ink,
        Vector4 muted, bool interactive, float scale, FontAwesomeIcon icon, string label, string value)
    {
        var rowMin = new Vector2(left, top);
        var rowMax = new Vector2(right, top + OutputRowHeight * scale);
        var hovered = interactive && UiInteract.Hover(rowMin, rowMax);
        if (hovered)
        {
            Squircle.Fill(drawList, rowMin, rowMax, Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(ink with { W = ink.W * OutputHoverAlpha }));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var centerY = (rowMin.Y + rowMax.Y) * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + OutputIconColumn * 0.5f * scale, centerY),
            IconGlyph.Of(icon), ink, OutputIconScale);
        var chevronCenter = new Vector2(right - Metrics.Space.Sm * scale, centerY);
        AppSkin.Icon(drawList, chevronCenter, IconGlyph.Of(FontAwesomeIcon.ChevronRight), muted, VolumeIconScale);
        var valueSize = Typography.Measure(value, TextStyles.Body);
        var valueRight = chevronCenter.X - Metrics.Space.Lg * scale;
        Typography.Draw(drawList, new Vector2(valueRight - valueSize.X, centerY - valueSize.Y * 0.5f), value, muted,
            TextStyles.Body);
        var labelLeft = left + (OutputIconColumn + Metrics.Space.Sm) * scale;
        var labelWidth = MathF.Max(1f, valueRight - valueSize.X - Metrics.Space.Md * scale - labelLeft);
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelHeight * 0.5f),
            Typography.FitText(label, labelWidth, TextStyles.Body), ink,
            TextStyles.Body);
        return interactive && UiInteract.Click(rowMin, rowMax, hovered);
    }

    private void DrawSoundCheckRow(ImDrawListPtr drawList, float left, float right, float top, Vector4 ink,
        bool interactive, float opacity, float scale)
    {
        var centerY = top + OutputRowHeight * scale * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + OutputIconColumn * 0.5f * scale, centerY),
            IconGlyph.Of(FontAwesomeIcon.VolumeUp), ink, OutputIconScale);
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggle = new Rect(new Vector2(right - toggleWidth, centerY - toggleHeight * 0.5f),
            new Vector2(right, centerY + toggleHeight * 0.5f));
        var enabled = playback.SoundCheckEnabled;
        var next = Toggle.Draw("music.output.soundCheck", toggle, enabled, theme, opacity, interactive);
        if (next != enabled)
        {
            playback.SetSoundCheck(next);
        }

        var labelLeft = left + (OutputIconColumn + Metrics.Space.Sm) * scale;
        var labelWidth = MathF.Max(1f, toggle.Min.X - Metrics.Space.Md * scale - labelLeft);
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelHeight * 0.5f),
            Typography.FitText(Loc.T(L.Music.SoundCheck.Title), labelWidth, TextStyles.Body), ink, TextStyles.Body);
    }

    private void DrawCrossfadeRow(ImDrawListPtr drawList, float left, float right, float top, Vector4 ink,
        Vector4 muted, bool interactive, float scale, float delta)
    {
        var labelTop = top + Metrics.Space.Md * scale;
        var labelLeft = left + (OutputIconColumn + Metrics.Space.Sm) * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        AppSkin.Icon(drawList, new Vector2(left + OutputIconColumn * 0.5f * scale, labelTop + labelHeight * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.SlidersH), ink, OutputIconScale);
        var sliderY = labelTop + labelHeight + Metrics.Space.Lg * scale;
        var current = playback.CrossfadeSeconds / CrossfadeMaximumSeconds;
        var result = crossfadeSlider.Draw(drawList, labelLeft, right, sliderY, current, interactive,
            ink with { W = ink.W * 0.9f }, ink with { W = ink.W * 0.2f }, delta);
        var seconds = (int)MathF.Round(result.Value * CrossfadeMaximumSeconds);
        if (result.Released)
        {
            playback.SetCrossfade(seconds);
        }

        var value = nowPlayingText.Crossfade(seconds);
        var valueSize = Typography.Measure(value, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(right - valueSize.X, labelTop), value, muted, TextStyles.Body);
        var labelWidth = MathF.Max(1f, right - valueSize.X - Metrics.Space.Md * scale - labelLeft);
        Typography.Draw(drawList, new Vector2(labelLeft, labelTop),
            Typography.FitText(Loc.T(L.Music.NowPlaying.Crossfade), labelWidth, TextStyles.Body), ink,
            TextStyles.Body);
    }

    private void OpenSleepMenu()
    {
        if (!playback.SleepTimerActive)
        {
            sleepChoice = SleepOffIndex;
        }
        else if (playback.SleepAtTrackEnd)
        {
            sleepChoice = SleepTrackEndIndex;
        }

        sleepMenu.Open();
    }

    private void DrawSleepMenu(Rect screen)
    {
        if (!sleepMenu.CapturesPointer)
        {
            return;
        }

        sleepItems[SleepOffIndex] = new ActionSheet.Item(Loc.T(L.Common.Off), string.Empty, false,
            sleepChoice == SleepOffIndex, true);
        for (var presetIndex = 0; presetIndex < NowPlayingText.SleepPresets.Length; presetIndex++)
        {
            var itemIndex = presetIndex + 1;
            sleepItems[itemIndex] = new ActionSheet.Item(nowPlayingText.SleepPreset(presetIndex), string.Empty,
                false, sleepChoice == itemIndex, true);
        }

        sleepItems[SleepTrackEndIndex] = new ActionSheet.Item(Loc.T(L.Music.NowPlaying.SleepEndOfTrack),
            string.Empty, false, sleepChoice == SleepTrackEndIndex, true);
        var picked = sleepMenu.Draw(screen, ActionSheetStyle.From(ui), sleepItems, Loc.T(L.Common.Cancel), false,
            Loc.T(L.Music.NowPlaying.SleepTimer));
        if (picked < 0)
        {
            return;
        }

        sleepChoice = picked;
        if (picked == SleepOffIndex)
        {
            playback.CancelSleepTimer();
        }
        else if (picked == SleepTrackEndIndex)
        {
            playback.SetSleepAtTrackEnd();
        }
        else
        {
            playback.SetSleepTimer(NowPlayingText.SleepPresets[picked - 1]);
        }
    }
}
