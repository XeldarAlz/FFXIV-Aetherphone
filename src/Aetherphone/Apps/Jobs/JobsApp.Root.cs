using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Jobs;

internal sealed partial class JobsApp
{
    private const float HeroRingRadius = 46f;
    private const float HeroRingThickness = 7f;
    private const float HeroTextGap = 18f;
    private const float HeroChevronSize = 5f;
    private const float HeroGlowCoverage = 0.7f;
    private const float HeroGlowStrength = 0.14f;
    private const float StatGlyph = 30f;
    private const float StatRingRadius = 15f;
    private const float StatRingThickness = 4f;
    private const float StatGlowStrength = 0.10f;
    private const float StatGlowCoverage = 0.6f;
    private const int GridColumns = 4;
    private const float GridPadY = 8f;
    private const float GridPadX = 6f;
    private const float CellTop = 10f;
    private const float CellRingRadius = 25f;
    private const float CellRingThickness = 4f;
    private const float CellLabelGap = 8f;
    private const float CellBottom = 10f;
    private const float CellWashInset = 3f;
    private const float CellTextInset = 4f;
    private const float ShelfHintPadY = 14f;
    private const float ReorderRadius = 11f;

    private readonly NavBarButton[] rootButtons = new NavBarButton[2];
    private Spring heroFill;

    private void DrawRoot(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            if (!snapshot.HasJobs)
            {
                cursorY += JobsArt.State(drawList, ui, origin, width, FontAwesomeIcon.UserClock, ui.Accent,
                    Loc.T(L.Jobs.LogInToView), Loc.T(L.Jobs.LogInBody), scale);
            }
            else
            {
                cursorY = DrawHero(drawList, origin, width, scale);
                cursorY = DrawStats(drawList, origin.X, cursorY, width, scale);
                cursorY = DrawShelves(drawList, origin.X, cursorY, width, scale);
                cursorY = DrawRoles(drawList, origin.X, cursorY, width, scale);
            }

            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        var count = 0;
        var categoriesIndex = -1;
        if (snapshot.HasJobs)
        {
            categoriesIndex = count;
            rootButtons[count] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.FolderPlus), Loc.T(L.Jobs.Categories));
            count++;
        }

        var colorIndex = count;
        rootButtons[count] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Palette), Loc.T(L.Jobs.BackgroundColor));
        count++;
        colorButtonRect = AppHeader.LargeTitleButtonRect(in navBar, colorIndex, count);
        if (categoriesIndex >= 0)
        {
            categoriesButtonRect = AppHeader.LargeTitleButtonRect(in navBar, categoriesIndex, count);
            UiAnchors.Report("jobs.categories", categoriesButtonRect);
        }

        var pressed = AppHeader.EndLargeTitle(in navBar, context, "jobs.nav", DisplayName, NavBarStyle.From(ui),
            rootButtons.AsSpan(0, count));
        if (pressed < 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        if (pressed == categoriesIndex)
        {
            menu.Toggle(CategoryMenuId, categoriesButtonRect);
            return;
        }

        menu.Toggle(ColorMenuId, colorButtonRect);
    }

    private float DrawHero(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (snapshot.ActiveJobIndex < 0)
        {
            return origin.Y;
        }

        var job = snapshot.Jobs[snapshot.ActiveJobIndex];
        var tint = JobsArt.Tint(job.Role);
        var pad = JobsArt.CardPad * scale;
        var ringRadius = HeroRingRadius * scale;
        var textLeft = origin.X + pad + ringRadius * 2f + HeroTextGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - HeroChevronSize * scale * 2f - textLeft);
        var showRested = job.IsCombat && !job.IsCapped && snapshot.RestedText.Length > 0;
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var nameHeight = Typography.LineHeight(TextStyles.Title2);
        var levelHeight = Typography.LineHeight(TextStyles.Headline);
        var lineGap = JobsArt.LineGap * scale;
        var blockHeight = labelHeight + nameHeight + levelHeight + labelHeight + lineGap * 3f +
                          (showRested ? labelHeight + lineGap : 0f);
        var height = MathF.Max(ringRadius * 2f, blockHeight) + pad * 2f;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        UiAnchors.Report("jobs.hero", rect);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID("jobs.hero"), pressed, PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, min, max, radius, true);
        Material.TopGlow(drawList, min, max, radius, tint, HeroGlowCoverage, HeroGlowStrength);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var center = new Vector2(min.X + pad + ringRadius, (min.Y + max.Y) * 0.5f);
        var fill = heroFill.Step(job.Fraction, Motion.Sheet, JobsArt.FrameDelta());
        JobsArt.JobRing(drawList, textures, center, ringRadius * press, HeroRingThickness * scale, fill, job,
            ui.TitleInk, IsPending(job), scale);
        JobsArt.Chevron(drawList, new Vector2(max.X - pad, (min.Y + max.Y) * 0.5f), HeroChevronSize * scale,
            ui.MutedInk, scale);

        var top = (min.Y + max.Y) * 0.5f - blockHeight * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(L.Jobs.CurrentJob), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        top += labelHeight + lineGap;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(job.Name, textWidth, TextStyles.Title2), ui.TitleInk, TextStyles.Title2);
        top += nameHeight + lineGap;
        var levelText = job.IsCapped ? Loc.T(L.Jobs.LevelCap) : job.LevelText;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(levelText, textWidth,
            TextStyles.Headline), tint, TextStyles.Headline);
        top += levelHeight + lineGap;
        var detail = job.IsCapped ? job.LevelText : job.ProgressText;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(detail, textWidth,
            TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (showRested)
        {
            top += labelHeight + lineGap;
            Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(snapshot.RestedText, textWidth,
                TextStyles.Footnote), Palette.Lighten(tint, 0.25f), TextStyles.Footnote);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            OpenDetail(job);
        }

        return rect.Max.Y;
    }

    private float DrawStats(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var gap = JobsArt.TileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var pad = JobsArt.CardPad * scale;
        var height = pad * 2f + StatGlyph * scale + gap + Typography.LineHeight(TextStyles.Footnote) +
                     Typography.LineHeight(TextStyles.Title3);
        var tileTop = top + gap;
        var summary = snapshot.Summary;
        DrawStatTile(drawList, new Rect(new Vector2(left, tileTop), new Vector2(left + tileWidth, tileTop + height)),
            AccentRing.Gold, FontAwesomeIcon.Crown, Loc.T(L.Jobs.AtCap), snapshot.CappedText,
            summary.CappedFraction, scale);
        DrawStatTile(drawList,
            new Rect(new Vector2(left + tileWidth + gap, tileTop), new Vector2(left + width, tileTop + height)),
            ui.Accent, FontAwesomeIcon.ChartBar, Loc.T(L.Jobs.LevelsEarned), snapshot.LevelsText,
            summary.LevelsFraction, scale);
        return tileTop + height;
    }

    private void DrawStatTile(ImDrawListPtr drawList, Rect rect, Vector4 tint, FontAwesomeIcon icon, string label,
        string value, float fraction, float scale)
    {
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, rect.Min, rect.Max, radius, true);
        Material.TopGlow(drawList, rect.Min, rect.Max, radius, tint, StatGlowCoverage, StatGlowStrength);
        var pad = JobsArt.CardPad * scale;
        var glyph = StatGlyph * scale;
        JobsArt.GlyphTile(drawList, new Vector2(rect.Min.X + pad + glyph * 0.5f, rect.Min.Y + pad + glyph * 0.5f),
            glyph, tint, icon);
        var ringRadius = StatRingRadius * scale;
        var ringCenter = new Vector2(rect.Max.X - pad - ringRadius, rect.Min.Y + pad + glyph * 0.5f);
        ProgressRing.Track(drawList, ringCenter, ringRadius, StatRingThickness * scale,
            Palette.WithAlpha(ui.TitleInk, 0.16f));
        ProgressRing.Fill(drawList, ringCenter, ringRadius, StatRingThickness * scale, fraction, tint);
        var textWidth = MathF.Max(1f, rect.Width - pad * 2f);
        var valueHeight = Typography.LineHeight(TextStyles.Title3);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueTop = rect.Max.Y - pad - valueHeight;
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, valueTop - labelHeight),
            Typography.FitText(label, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, valueTop),
            Typography.FitText(value, textWidth, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
    }

    private float SectionTop(ImDrawListPtr drawList, float left, float top, float width, string title, float scale)
    {
        var cursorY = top + JobsArt.SectionGap * scale;
        cursorY += JobsArt.SectionHeader(drawList, new Vector2(left, cursorY), width, title, ui.TitleInk);
        return cursorY + JobsArt.HeaderGap * scale;
    }

    private float DrawShelves(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var shelves = snapshot.Shelves;
        var cursorY = top;
        for (var shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            var shelf = shelves[shelfIndex];
            var reorderWidth = shelves.Length > 1 ? ReorderRadius * 4f * scale + JobsArt.TileGap * scale : 0f;
            var headerTop = cursorY + JobsArt.SectionGap * scale;
            var titleHeight = JobsArt.SectionHeader(drawList, new Vector2(left, headerTop), width - reorderWidth,
                shelf.Title, ui.TitleInk);
            if (shelves.Length > 1)
            {
                DrawShelfReorder(new Vector2(left + width, headerTop + titleHeight * 0.5f), shelf.CategoryIndex,
                    shelves.Length, scale);
            }

            cursorY = headerTop + titleHeight + JobsArt.HeaderGap * scale;
            cursorY = shelf.GearsetIndices.Length == 0
                ? DrawShelfHint(drawList, left, cursorY, width, scale)
                : DrawGearsetGrid(drawList, left, cursorY, width, shelf.GearsetIndices, scale);
        }

        return cursorY;
    }

    private void DrawShelfReorder(Vector2 rightCenter, int categoryIndex, int categoryCount, float scale)
    {
        var radius = ReorderRadius * scale;
        var down = new Vector2(rightCenter.X - radius, rightCenter.Y);
        var up = new Vector2(down.X - radius * 2f, rightCenter.Y);
        if (DrawReorderButton(up, radius, IconGlyph.Of(FontAwesomeIcon.ChevronUp), categoryIndex > 0,
                Loc.T(L.Jobs.MoveUp)))
        {
            categoryMoveIndex = categoryIndex;
            categoryMoveDelta = -1;
        }

        if (DrawReorderButton(down, radius, IconGlyph.Of(FontAwesomeIcon.ChevronDown),
                categoryIndex < categoryCount - 1, Loc.T(L.Jobs.MoveDown)))
        {
            categoryMoveIndex = categoryIndex;
            categoryMoveDelta = 1;
        }
    }

    private bool DrawReorderButton(Vector2 center, float radius, string glyph, bool enabled, string tooltip)
    {
        if (!enabled)
        {
            AppSkin.Icon(ImGui.GetWindowDrawList(), center, glyph,
                Palette.WithAlpha(ui.MutedInk, ui.MutedInk.W * 0.25f), 0.5f);
            return false;
        }

        if (!ui.IconButton(center, radius, glyph, ui.MutedInk, default, 0.5f, tooltip))
        {
            return false;
        }

        UiFeedback.Play(UiSound.Tap);
        return true;
    }

    private float DrawShelfHint(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var pad = JobsArt.CardPad * scale;
        var text = Loc.T(L.Jobs.EmptyShelf);
        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width - pad * 2f).Y;
        var max = new Vector2(left + width, top + textHeight + ShelfHintPadY * 2f * scale);
        ui.Card(drawList, new Vector2(left, top), max, Metrics.Radius.Grouped * scale, true);
        Typography.DrawWrappedLeft(new Vector2(left + pad, top + ShelfHintPadY * scale), text, ui.MutedInk,
            TextStyles.Footnote, width - pad * 2f);
        return max.Y;
    }

    private float DrawRoles(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = top;
        var starts = snapshot.RoleStarts;
        for (var role = 0; role < JobRoles.Count; role++)
        {
            var start = starts[role];
            var end = starts[role + 1];
            if (end <= start)
            {
                continue;
            }

            cursorY = SectionTop(drawList, left, cursorY, width, Loc.T(JobRoles.Title((JobRole)role)), scale);
            cursorY = DrawJobGrid(drawList, left, cursorY, width, start, end, scale);
        }

        return cursorY;
    }

    private float CellHeight(float scale) =>
        (CellTop + CellRingRadius * 2f + CellLabelGap + CellBottom) * scale +
        Typography.LineHeight(TextStyles.FootnoteEmphasized) + Typography.LineHeight(TextStyles.Footnote);

    private float GridHeight(int count, float scale)
    {
        var rows = (count + GridColumns - 1) / GridColumns;
        return rows * CellHeight(scale) + GridPadY * 2f * scale;
    }

    private Rect GridCell(float left, float top, float width, int slot, float scale)
    {
        var cellWidth = (width - GridPadX * 2f * scale) / GridColumns;
        var cellHeight = CellHeight(scale);
        var min = new Vector2(left + GridPadX * scale + cellWidth * (slot % GridColumns),
            top + GridPadY * scale + cellHeight * (slot / GridColumns));
        return new Rect(min, min + new Vector2(cellWidth, cellHeight));
    }

    private float DrawJobGrid(ImDrawListPtr drawList, float left, float top, float width, int start, int end,
        float scale)
    {
        var max = new Vector2(left + width, top + GridHeight(end - start, scale));
        ui.Card(drawList, new Vector2(left, top), max, Metrics.Radius.Grouped * scale, true);
        for (var jobIndex = start; jobIndex < end; jobIndex++)
        {
            var cell = GridCell(left, top, width, jobIndex - start, scale);
            if (jobIndex == 0)
            {
                UiAnchors.Report("jobs.tile", cell);
            }

            var job = snapshot.Jobs[jobIndex];
            var fill = tileFills[jobIndex].Step(job.Fraction, Motion.Sheet, JobsArt.FrameDelta());
            if (DrawJobCell(drawList, cell, job, fill, job.LevelText, job.IsCapped, IsPending(job), job.TooltipId,
                    scale))
            {
                OpenDetail(job);
            }
        }

        return max.Y;
    }

    private float DrawGearsetGrid(ImDrawListPtr drawList, float left, float top, float width, int[] gearsetIndices,
        float scale)
    {
        var max = new Vector2(left + width, top + GridHeight(gearsetIndices.Length, scale));
        ui.Card(drawList, new Vector2(left, top), max, Metrics.Radius.Grouped * scale, true);
        for (var slot = 0; slot < gearsetIndices.Length; slot++)
        {
            var gearset = snapshot.Gearsets[gearsetIndices[slot]];
            if (gearset.JobIndex < 0)
            {
                continue;
            }

            var job = snapshot.Jobs[gearset.JobIndex];
            var cell = GridCell(left, top, width, slot, scale);
            if (!DrawCell(drawList, cell, job, tileFills[gearset.JobIndex].Value, gearset.Name,
                    gearset.ItemLevelText, job.Name, gearset.IsActive, IsPending(gearset), gearset.PressId, scale))
            {
                continue;
            }

            if (gearset.IsActive)
            {
                OpenDetail(job);
                continue;
            }

            RequestEquip(gearset);
        }

        return max.Y;
    }

    private bool DrawJobCell(ImDrawListPtr drawList, Rect cell, JobRow job, float fill, string secondary,
        bool highlightSecondary, bool pending, string pressId, float scale)
    {
        var fits = Typography.Measure(job.Name, TextStyles.FootnoteEmphasized).X <=
                   cell.Width - CellTextInset * 2f * scale;
        return DrawCell(drawList, cell, job, fill, fits ? job.Name : job.Abbreviation, secondary,
            fits ? string.Empty : job.Name, job.IsActive, pending, pressId, scale, highlightSecondary);
    }

    private bool DrawCell(ImDrawListPtr drawList, Rect cell, JobRow job, float fill, string title, string secondary,
        string tooltip, bool active, bool pending, string pressId, float scale, bool highlightSecondary = false)
    {
        var hovered = UiInteract.Hover(cell.Min, cell.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID(pressId), pressed);
        if (hovered)
        {
            var inset = new Vector2(CellWashInset * scale, CellWashInset * scale);
            Squircle.Fill(drawList, cell.Min + inset, cell.Max - inset, Metrics.Radius.Md * scale,
                ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (tooltip.Length > 0)
            {
                HoverTooltip.Show(pressId, cell, tooltip, HoverLabelSide.Above);
            }
        }

        var ringRadius = CellRingRadius * scale;
        var ringCenter = new Vector2(cell.Center.X, cell.Min.Y + CellTop * scale + ringRadius);
        JobsArt.JobRing(drawList, textures, ringCenter, ringRadius * press, CellRingThickness * scale * press, fill,
            job, ui.TitleInk, pending, scale);
        if (active)
        {
            JobsArt.ActiveDot(drawList, ringCenter, ringRadius * press, ui.Accent, ui.Palette.BackdropBottom);
        }

        var textWidth = MathF.Max(1f, cell.Width - CellTextInset * 2f * scale);
        var titleTop = ringCenter.Y + ringRadius + CellLabelGap * scale;
        var titleHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, new Vector2(cell.Center.X, titleTop + titleHeight * 0.5f),
            Typography.FitText(title, textWidth, TextStyles.FootnoteEmphasized),
            job.IsLocked ? ui.MutedInk : ui.TitleInk, TextStyles.FootnoteEmphasized);
        var secondaryHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.DrawCentered(drawList, new Vector2(cell.Center.X, titleTop + titleHeight + secondaryHeight * 0.5f),
            Typography.FitText(secondary, textWidth, TextStyles.Footnote),
            highlightSecondary ? Palette.Lighten(JobsArt.Tint(job.Role), 0.2f) : ui.MutedInk, TextStyles.Footnote);
        return UiInteract.Click(cell.Min, cell.Max, hovered);
    }
}
