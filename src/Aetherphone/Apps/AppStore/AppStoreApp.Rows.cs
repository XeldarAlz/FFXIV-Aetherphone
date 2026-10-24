using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.AppStore;

internal sealed partial class AppStoreApp
{
    private const float RowHeight = 72f;
    private const float RowIconSize = 50f;
    private const float RowInset = 14f;
    private const float RowTextGap = 12f;
    private const float RowLineGap = 2f;
    private const float StatePillWidth = 68f;
    private const float StatePillHeight = 28f;
    private const float PillCaptionGap = 3f;
    private const float PillInkLift = 0.30f;
    private const float DisabledAlpha = 0.45f;
    private const float RingRadius = 11f;
    private const float RingThickness = 2.5f;
    private const float RingStop = 3f;
    private const float RingTrackAlpha = 0.18f;
    private const float HairlineAlpha = 0.06f;
    private const float SectionTitleGap = 4f;
    private const float SectionGap = 28f;
    private const float BottomBreathing = 28f;
    private const string PillIdScope = "appstore.pill";
    private const string RowsIdScope = "appstore.rows";
    private const string UpdatesIdScope = "appstore.updates.rows";
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private void DrawIcon(ImDrawListPtr drawList, Vector2 center, float size, IPhoneApp app)
    {
        var scale = UiScale.Current;
        var half = size * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        var radius = size * Metrics.Radius.TileFactor;
        if (AppIconTile.TryDraw(drawList, app.Id, app.Accent, min, max, radius, 1f, true, scale))
        {
            return;
        }

        var surface = IconTile.Surface(app.Accent);
        Elevation.IconRest(drawList, min, max, radius, scale);
        IconTile.FillShaded(drawList, min, max, radius, surface);
        Material.EdgeSquircle(drawList, min, max, radius, scale);
        var ink = AppAccents.InkFor(app.Id);
        if (!AppIconArt.TryDraw(drawList, app.Id, center, size * 0.62f, ink, Palette.Mix(surface, ink, 0.28f)))
        {
            Typography.DrawCentered(drawList, center, app.Glyph, ink, TextStyles.Headline);
        }
    }

    private float DrawSectionHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title,
        bool seeAll, out bool seeAllTapped)
    {
        seeAllTapped = false;
        var bandHeight = CardSectionHeader.HeightUnits * UiScale.Current;
        var reserve = 0f;
        if (seeAll)
        {
            var label = Loc.T(L.Store.SeeAll);
            var size = Typography.Measure(label, TextStyles.Body);
            var min = new Vector2(origin.X + width - size.X, origin.Y + (bandHeight - size.Y) * 0.5f);
            var max = min + size;
            var hovered = UiInteract.Hover(min, max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            Typography.Draw(drawList, min, label, hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent,
                TextStyles.Body);
            seeAllTapped = UiInteract.Click(min, max, hovered);
            reserve = size.X + Metrics.Space.Md * UiScale.Current;
        }

        var height = CardSectionHeader.Draw(drawList, origin, width, title, ui.TitleInk, reserve);
        return origin.Y + height + SectionTitleGap * UiScale.Current;
    }

    private float DrawRowCard(ImDrawListPtr drawList, Vector2 origin, float width, IReadOnlyList<IPhoneApp> entries,
        int limit, float scale)
    {
        var count = Math.Min(entries.Count, limit);
        if (count == 0)
        {
            return origin.Y;
        }

        var cardMax = new Vector2(origin.X + width, origin.Y + count * RowHeight * scale);
        ui.Card(drawList, origin, cardMax, Metrics.Radius.Grouped * scale);
        using var scope = UiScope.PushId(RowsIdScope);
        for (var rowIndex = 0; rowIndex < count; rowIndex++)
        {
            var app = entries[rowIndex];
            var row = RowRect(origin, cardMax.X, rowIndex, scale);
            DrawRowDivider(drawList, origin, cardMax.X, rowIndex, scale);
            if (DrawAppRow(drawList, row, app, Loc.T(AppStoreCatalog.For(app.Id).Subtitle), scale))
            {
                OpenDetail(app.Id);
            }
        }

        return cardMax.Y;
    }

    private float DrawUpdateCard(ImDrawListPtr drawList, Vector2 origin, float width, IReadOnlyList<StoreUpdate> entries,
        int limit, float scale)
    {
        var count = Math.Min(entries.Count, limit);
        if (count == 0)
        {
            return origin.Y;
        }

        var cardMax = new Vector2(origin.X + width, origin.Y + count * RowHeight * scale);
        ui.Card(drawList, origin, cardMax, Metrics.Radius.Grouped * scale);
        using var scope = UiScope.PushId(UpdatesIdScope);
        for (var rowIndex = 0; rowIndex < count; rowIndex++)
        {
            var update = entries[rowIndex];
            var row = RowRect(origin, cardMax.X, rowIndex, scale);
            DrawRowDivider(drawList, origin, cardMax.X, rowIndex, scale);
            if (DrawAppRow(drawList, row, update.App, Loc.T(update.Highlight), scale))
            {
                OpenDetail(update.App.Id);
            }
        }

        return cardMax.Y;
    }

    private static Rect RowRect(Vector2 origin, float right, int rowIndex, float scale)
    {
        var top = origin.Y + rowIndex * RowHeight * scale;
        return new Rect(new Vector2(origin.X + RowInset * scale, top),
            new Vector2(right - RowInset * scale, top + RowHeight * scale));
    }

    private void DrawRowDivider(ImDrawListPtr drawList, Vector2 origin, float right, int rowIndex, float scale)
    {
        if (rowIndex == 0)
        {
            return;
        }

        var top = origin.Y + rowIndex * RowHeight * scale;
        drawList.AddLine(new Vector2(origin.X + (RowInset + RowIconSize + RowTextGap) * scale, top),
            new Vector2(right - RowInset * scale, top),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha)), 1f);
    }

    private bool DrawAppRow(ImDrawListPtr drawList, Rect row, IPhoneApp app, string subtitle, float scale)
    {
        var pill = new Rect(
            new Vector2(row.Max.X - StatePillWidth * scale, row.Center.Y - StatePillHeight * 0.5f * scale),
            new Vector2(row.Max.X, row.Center.Y + StatePillHeight * 0.5f * scale));
        var builtIn = !AppInstaller.CanUninstall(app.Id);
        if (builtIn)
        {
            var shift = (Typography.LineHeight(TextStyles.Caption2) + PillCaptionGap * scale) * 0.5f;
            pill = new Rect(pill.Min - new Vector2(0f, shift), pill.Max - new Vector2(0f, shift));
        }

        if (!rowAnchorTaken)
        {
            rowAnchorTaken = true;
            UiAnchors.Report("appstore.row",
                new Rect(row.Min, new Vector2(pill.Min.X - RowTextGap * scale, row.Max.Y)));
        }

        var overPill = UiInteract.Hover(pill.Min, pill.Max);
        var hovered = UiInteract.Hover(row.Min, row.Max) && !overPill;
        var press = PressFx.Scale(ImGui.GetID(app.Id), hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left),
            PressFx.CardPressedScale);
        if (hovered)
        {
            var wash = new Rect(new Vector2(row.Min.X - RowInset * 0.5f * scale, row.Min.Y + 2f * scale),
                new Vector2(row.Max.X + RowInset * 0.5f * scale, row.Max.Y - 2f * scale));
            Squircle.Fill(drawList, wash.Min, wash.Max, Metrics.Radius.Md * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconCenter = new Vector2(row.Min.X + RowIconSize * 0.5f * scale, row.Center.Y);
        DrawIcon(drawList, iconCenter, RowIconSize * scale * press, app);
        var textLeft = row.Min.X + (RowIconSize + RowTextGap) * scale;
        var textWidth = MathF.Max(1f, pill.Min.X - textLeft - RowTextGap * scale);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var nameY = row.Center.Y - (nameHeight + RowLineGap * scale + subtitleHeight) * 0.5f;
        var textHovering = UiInteract.Hover(new Vector2(textLeft, row.Min.Y), new Vector2(textLeft + textWidth, row.Max.Y));
        Marquee.DrawLeft(new MarqueeId("appstore.row.name.", app.Id), app.DisplayName, textLeft, nameY, textWidth,
            TextStyles.Headline, ui.TitleInk, textHovering);
        Marquee.DrawLeft(new MarqueeId("appstore.row.subtitle.", app.Id), subtitle, textLeft,
            nameY + nameHeight + RowLineGap * scale, textWidth, TextStyles.Footnote, ui.MutedInk, textHovering);
        DrawStatePill(drawList, pill, app, overPill, ui.Ink,
            Palette.Lighten(app.Accent, PillInkLift), scale);
        if (builtIn)
        {
            Typography.DrawCentered(drawList,
                new Vector2(pill.Center.X, pill.Max.Y + PillCaptionGap * scale +
                                           Typography.LineHeight(TextStyles.Caption2) * 0.5f),
                Loc.T(L.Store.BuiltIn), ui.MutedInk, TextStyles.Caption2);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void DrawStatePill(ImDrawListPtr drawList, Rect pill, IPhoneApp app, bool hovered, in ControlInk ink,
        Vector4 labelInk, float scale)
    {
        if (installing.TryGetValue(app.Id, out var progress))
        {
            DrawInstallProgress(drawList, pill.Center, progress, labelInk, scale);
            return;
        }

        var installed = installer.IsInstalled(app.Id);
        var self = string.Equals(app.Id, StoreAppId, StringComparison.Ordinal);
        var enabled = !(installed && self);
        var active = hovered && enabled;
        uint key;
        using (UiScope.PushId(PillIdScope))
        {
            key = ImGui.GetID(app.Id);
        }

        var face = Button.Surface(drawList, pill, ink, ButtonStyle.Gray, ButtonRole.Normal, enabled, active, key);
        var style = Button.LabelStyle(face.Face.Height);
        var label = Typography.FitText(Loc.T(installed ? L.Store.Open : L.Store.Get),
            MathF.Max(1f, face.Face.Width - face.Face.Height * 0.5f), style);
        Typography.DrawCentered(drawList, face.Face.Center, label,
            Palette.WithAlpha(labelInk, labelInk.W * (enabled ? 1f : DisabledAlpha)), style);
        if (!active)
        {
            return;
        }

        if (!UiInteract.Click(pill.Min, pill.Max, true))
        {
            return;
        }

        if (installed)
        {
            OpenApp(app.Id);
            return;
        }

        BeginInstall(app.Id);
    }

    private void DrawInstallProgress(ImDrawListPtr drawList, Vector2 center, float progress, Vector4 ink, float scale)
    {
        var radius = RingRadius * scale;
        var thickness = RingThickness * scale;
        ProgressRing.Track(center, radius, thickness, Palette.WithAlpha(ui.TitleInk, RingTrackAlpha));
        ProgressRing.Fill(center, radius, thickness, Math.Clamp(progress, 0f, 1f), ink);
        var stop = RingStop * scale;
        drawList.AddRectFilled(center - new Vector2(stop), center + new Vector2(stop), ImGui.GetColorU32(ink),
            1f * scale);
    }

    private static void Reserve(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
        ImGui.Dummy(new Vector2(width, BottomBreathing * UiScale.Current));
    }
}
