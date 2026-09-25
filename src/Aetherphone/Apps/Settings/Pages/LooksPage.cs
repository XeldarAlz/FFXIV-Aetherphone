using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class LooksPage : ISettingsPage
{
    private enum RowAction
    {
        None,
        Choose,
        Open,
    }

    private const float RowHeight = 66f;
    private const float ThumbHeight = 50f;
    private const float ThumbAspect = 0.62f;
    private const float ThumbRadius = 7f;
    private const float AccentDotRadius = 6f;
    private const float EditExtent = 14f;
    private const float CheckReserve = 26f;

    public string Title => Loc.T(L.Home.Looks);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Palette;
    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);
    private readonly HomeLookService looks;
    private readonly WallpaperLibrary wallpapers;
    private readonly ISettingsNavigator navigator;
    private readonly ConfirmService confirm;
    private readonly List<string> subtitles = new();

    public LooksPage(HomeLookService looks, WallpaperLibrary wallpapers, ISettingsNavigator navigator,
        ConfirmService confirm)
    {
        this.looks = looks;
        this.wallpapers = wallpapers;
        this.navigator = navigator;
        this.confirm = confirm;
        looks.CaptureActive();
    }

    public static string NameOf(HomeLook? look) =>
        look is null || look.Name.Length == 0 ? Loc.T(L.Home.LookDefaultName) : look.Name;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        var entries = looks.Looks;
        RefreshSubtitles(entries);
        var chosen = Guid.Empty;
        HomeLook? open = null;
        var create = false;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Home.Looks), theme);
            var card = GroupCard.Begin(theme, entries.Count, RowHeight);
            for (var index = 0; index < entries.Count; index++)
            {
                var look = entries[index];
                switch (DrawRow(card.NextRow(), look, subtitles[index], look.Id == looks.ActiveLookId, theme, scale))
                {
                    case RowAction.Choose:
                        chosen = look.Id;
                        break;
                    case RowAction.Open:
                        open = look;
                        break;
                }
            }

            card.End();
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            var actions = GroupCard.Begin(theme, 1);
            create = SettingsRow.Link(actions.NextRow(), FontAwesomeIcon.Plus, Tint, Loc.T(L.Home.LookNew),
                string.Empty, theme);
            actions.End();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Home.LooksHint), theme);
        }

        if (create)
        {
            var created = looks.CreateFromCurrent(Loc.T(L.Home.LookNewName));
            looks.Choose(created.Id);
            navigator.Open(new LookDetailPage(created, looks, navigator, confirm));
            return;
        }

        if (open is not null)
        {
            navigator.Open(new LookDetailPage(open, looks, navigator, confirm));
            return;
        }

        if (chosen != Guid.Empty)
        {
            looks.Choose(chosen);
        }
    }

    private void RefreshSubtitles(IReadOnlyList<HomeLook> entries)
    {
        if (entries.Count == subtitles.Count)
        {
            return;
        }

        subtitles.Clear();
        for (var index = 0; index < entries.Count; index++)
        {
            subtitles.Add(SubtitleOf(entries[index]));
        }
    }

    private static string SubtitleOf(HomeLook look)
    {
        var accent = ThemeCatalog.IsCustomAccent(look.AccentName)
            ? Loc.T(L.Settings.AccentCustom)
            : CatalogLabels.Accent(look.AccentName);
        return $"{accent} · {CatalogLabels.PhoneCase(look.PhoneCaseName)}";
    }

    private string PreviewWallpaperId(HomeLook look) =>
        look.ThemeMode switch
        {
            ThemeMode.Light => look.LightWallpaperId,
            ThemeMode.Dark => look.DarkWallpaperId,
            _ => wallpapers.Darkness >= 0.5f ? look.DarkWallpaperId : look.LightWallpaperId,
        };

    private RowAction DrawRow(Rect row, HomeLook look, string subtitle, bool active, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var thumbHeight = ThumbHeight * scale;
        var thumbMin = new Vector2(row.Min.X, row.Center.Y - thumbHeight * 0.5f);
        var thumb = new Rect(thumbMin, thumbMin + new Vector2(thumbHeight * ThumbAspect, thumbHeight));
        var editExtent = new Vector2(EditExtent * scale, EditExtent * scale);
        var editCenter = new Vector2(row.Max.X - editExtent.X, row.Center.Y);
        var overEdit = UiInteract.Hover(editCenter - editExtent, editCenter + editExtent);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered && !overEdit)
        {
            var pressed = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            Squircle.Fill(drawList, new Vector2(row.Min.X - 10f * scale, row.Min.Y + 3f * scale),
                new Vector2(row.Max.X + 10f * scale, row.Max.Y - 3f * scale), 8f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, pressed ? 0.10f : 0.05f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var radius = ThumbRadius * scale;
        WallpaperRenderer.DrawSingle(drawList, thumb, radius, wallpapers.Resolve(PreviewWallpaperId(look)),
            ThumbAspect, 1f, theme.SurfaceMuted);
        Squircle.Stroke(drawList, thumb.Min, thumb.Max, radius, ImGui.GetColorU32(theme.Separator), scale);
        var dotRadius = AccentDotRadius * scale;
        var dotCenter = new Vector2(thumb.Max.X - dotRadius * 0.5f, thumb.Max.Y - dotRadius * 0.5f);
        drawList.AddCircleFilled(dotCenter, dotRadius + 2f * scale, ImGui.GetColorU32(theme.GroupedCard));
        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(ThemeCatalog.ResolveAccent(look.AccentName)));

        var textLeft = thumb.Max.X + 14f * scale;
        var textRight = editCenter.X - editExtent.X - (active ? CheckReserve : 4f) * scale;
        var textWidth = MathF.Max(24f * scale, textRight - textLeft);
        var title = Typography.FitText(NameOf(look), textWidth, TextStyles.BodyEmphasized);
        var fittedSubtitle = Typography.FitText(subtitle, textWidth, TextStyles.Footnote);
        var titleSize = Typography.Measure(title, TextStyles.BodyEmphasized);
        var subtitleSize = Typography.Measure(fittedSubtitle, TextStyles.Footnote);
        var gap = 2f * scale;
        var top = row.Center.Y - (titleSize.Y + gap + subtitleSize.Y) * 0.5f;
        Typography.Draw(new Vector2(textLeft, top), title, theme.TextStrong, TextStyles.BodyEmphasized);
        Typography.Draw(new Vector2(textLeft, top + titleSize.Y + gap), fittedSubtitle, theme.TextMuted,
            TextStyles.Footnote);
        if (active)
        {
            var tip = new Vector2(editCenter.X - editExtent.X - 14f * scale, row.Center.Y + 5f * scale);
            var ink = ImGui.GetColorU32(theme.Accent);
            drawList.AddLine(tip - new Vector2(5f * scale, 5f * scale), tip, ink, 2f * scale);
            drawList.AddLine(tip, new Vector2(tip.X + 9f * scale, tip.Y - 11f * scale), ink, 2f * scale);
        }

        ProgressRing.CenterIcon(drawList, editCenter, FontAwesomeIcon.Pen, overEdit ? theme.Accent : theme.TextMuted,
            12f * scale);
        if (UiInteract.Click(editCenter - editExtent, editCenter + editExtent, overEdit))
        {
            return RowAction.Open;
        }

        if (UiInteract.Click(row.Min, row.Max, hovered && !overEdit))
        {
            return RowAction.Choose;
        }

        return RowAction.None;
    }
}
