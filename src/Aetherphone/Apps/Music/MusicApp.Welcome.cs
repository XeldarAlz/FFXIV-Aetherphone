using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const string WelcomeShowId = "music.welcome.showWindowsMedia";
    private const string WelcomePublishId = "music.welcome.publishToWindowsMedia";
    private const string WelcomeSourceId = "music.welcome.source";
    private const string WelcomeContinueId = "music.welcome.continue";

    private bool NeedsWelcome => !configuration.MusicWelcomeShown && !NativeFileDialog.RunsUnderWine;

    private void DrawWelcome(Rect content, Rect screen, float scale)
    {
        pcSourceMenu.Gate();
        var buttonHeight = Button.LargeHeight * scale;
        var continueTop = content.Max.Y - Metrics.Space.Sm * scale - buttonHeight;
        var sourceTop = continueTop - Metrics.Space.Sm * scale - buttonHeight;
        var body = new Rect(content.Min, new Vector2(content.Max.X, sourceTop - Metrics.Space.Md * scale));
        using (AppSurface.Begin(body))
        {
            DrawWelcomeCopy(scale);
            DrawWelcomeToggles(scale);
        }

        var left = content.Min.X + AppSurface.SidePadding * scale;
        var right = content.Max.X - AppSurface.SidePadding * scale;
        var source = new Rect(new Vector2(left, sourceTop), new Vector2(right, sourceTop + buttonHeight));
        if (Button.Draw(source, Loc.T(L.Music.PcMedia.WelcomeSource), ui.Ink, ButtonStyle.Gray,
                enabled: configuration.ShowWindowsMedia, id: WelcomeSourceId))
        {
            pcSourceMenu.Toggle(PcSourceMenuId, source);
        }

        var proceed = new Rect(new Vector2(left, continueTop), new Vector2(right, continueTop + buttonHeight));
        if (Button.Draw(proceed, Loc.T(L.Music.PcMedia.WelcomeContinue), ui.Ink, id: WelcomeContinueId))
        {
            configuration.MusicWelcomeShown = true;
            configuration.Save();
        }

        DrawPcSourceMenu(screen);
    }

    private void DrawWelcomeCopy(float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        DrawWelcomeText(Loc.T(L.Music.PcMedia.WelcomeTitle), ui.TitleInk, TextStyles.Title1, width,
            Metrics.Space.Md * scale);
        DrawWelcomeText(Loc.T(L.Music.PcMedia.WelcomeIntro), ui.TitleInk, TextStyles.Body, width,
            Metrics.Space.Lg * scale);
        DrawWelcomeText(Loc.T(L.Music.PcMedia.WelcomePrivacy), ui.BodyInk, TextStyles.Subheadline, width,
            Metrics.Space.Md * scale);
        DrawWelcomeText(Loc.T(L.Music.PcMedia.WelcomeDirect), ui.BodyInk, TextStyles.Subheadline, width,
            Metrics.Space.Md * scale);
        DrawWelcomeText(Loc.T(L.Music.PcMedia.WelcomeSettings), ui.MutedInk, TextStyles.Subheadline, width,
            Metrics.Space.Lg * scale);
    }

    private void DrawWelcomeToggles(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var inset = Metrics.Space.Lg * scale;
        var labelWidth = MathF.Max(1f,
            width - inset * 2f - (Metrics.Size.ToggleWidth + Metrics.Space.Md) * scale);
        var showLabel = Loc.T(L.Music.PcMedia.ShowWindowsMedia);
        var publishLabel = Loc.T(L.Music.PcMedia.PublishToWindowsMedia);
        var showText = Typography.MeasureWrappedBlock(showLabel, TextStyles.BodyEmphasized, labelWidth).Y;
        var publishText = Typography.MeasureWrappedBlock(publishLabel, TextStyles.BodyEmphasized, labelWidth).Y;
        var showRow = WelcomeRowHeight(showText, scale);
        var publishRow = WelcomeRowHeight(publishText, scale);
        var card = GroupCard.Begin(ui, showRow + publishRow);
        var show = DrawWelcomeToggle(card.NextRow(showRow), WelcomeShowId, showLabel, showText, labelWidth,
            configuration.ShowWindowsMedia);
        var publish = DrawWelcomeToggle(card.NextRow(publishRow), WelcomePublishId, publishLabel, publishText,
            labelWidth, configuration.PublishToWindowsMedia);
        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxs * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + inset);
        DrawWelcomeText(Loc.T(L.Music.PcMedia.SettingsHint), ui.MutedInk, TextStyles.Footnote,
            MathF.Max(1f, width - inset * 2f), 0f);
        configuration.SetWindowsMedia(show, publish);
    }

    private bool DrawWelcomeToggle(Rect row, string id, string label, float labelHeight, float labelWidth, bool value)
    {
        var scale = UiScale.Current;
        var labelTop = row.Center.Y - (labelHeight - ImGui.GetStyle().ItemSpacing.Y) * 0.5f;
        Typography.DrawWrappedLeft(new Vector2(row.Min.X, labelTop), label, ui.TitleInk, TextStyles.BodyEmphasized,
            labelWidth);
        var size = new Vector2(Metrics.Size.ToggleWidth, Metrics.Size.ToggleHeight) * scale;
        var min = new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f);
        return Toggle.Draw(id, new Rect(min, min + size), value, theme);
    }

    private static float WelcomeRowHeight(float labelHeight, float scale) =>
        MathF.Max(GroupCard.DefaultRowHeight, labelHeight / scale + Metrics.Space.Md * 2f);

    private static void DrawWelcomeText(string text, Vector4 ink, in TextStyle style, float width, float gapBelow)
    {
        var height = Typography.DrawWrappedLeft(ImGui.GetCursorScreenPos(), text, ink, style, width);
        ImGui.Dummy(new Vector2(width, height + gapBelow));
    }
}
