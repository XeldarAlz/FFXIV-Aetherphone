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
    private const float DetailRingRadius = 56f;
    private const float DetailRingThickness = 9f;
    private const float DetailTextGap = 18f;
    private const float DetailGlowCoverage = 0.75f;
    private const float DetailGlowStrength = 0.16f;
    private const float BadgePadX = 9f;
    private const float BadgePadY = 3f;
    private const float BadgeFillAlpha = 0.2f;
    private const float BarHeight = 8f;
    private const float BarGap = 10f;
    private const float LegendDot = 4f;
    private const float LegendGap = 8f;
    private const float ActionHeight = 44f;
    private const float GearsetRowHeight = 64f;
    private const float GearsetIconSize = 40f;
    private const float GearsetIconRadius = 10f;
    private const float GearsetTextGap = 12f;
    private const float GearsetMenuRadius = 14f;
    private const float GearsetSpinnerRadius = 8f;
    private const float GearsetSpinnerThickness = 2f;
    private const float GearsetRowWashAlpha = 0.07f;
    private const float GearsetRowPressAlpha = 0.14f;
    private const float GearsetNameTop = 12f;
    private const float GearsetSubTop = 36f;

    private Spring detailRingFill;
    private Spring detailBarFill;
    private bool gearsetAnchorTaken;

    private void DrawDetail(Rect area, uint classJobId)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        UiAnchors.Report("jobs.detail", navBar.Body);
        var jobIndex = snapshot.IndexOfJob(classJobId);
        var title = jobIndex >= 0 ? snapshot.Jobs[jobIndex].Name : DisplayName;
        gearsetAnchorTaken = false;
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            if (jobIndex < 0)
            {
                cursorY += JobsArt.State(drawList, ui, origin, width, FontAwesomeIcon.UserClock, ui.Accent,
                    Loc.T(L.Jobs.LogInToView), Loc.T(L.Jobs.LogInBody), scale);
            }
            else
            {
                var job = snapshot.Jobs[jobIndex];
                cursorY = DrawDetailHero(drawList, origin, width, job, scale);
                cursorY = DrawExperienceCard(drawList, origin.X, cursorY, width, job, scale);
                cursorY = DrawDetailBody(drawList, origin.X, cursorY, width, job, scale);
            }

            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "jobs.detail.nav", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private float DrawDetailHero(ImDrawListPtr drawList, Vector2 origin, float width, JobRow job, float scale)
    {
        var tint = JobsArt.Tint(job.Role);
        var pad = JobsArt.CardPad * scale;
        var ringRadius = DetailRingRadius * scale;
        var textLeft = origin.X + pad + ringRadius * 2f + DetailTextGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - textLeft);
        var roleHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var levelHeight = Typography.LineHeight(TextStyles.Title2);
        var detailHeight = Typography.LineHeight(TextStyles.Footnote);
        var badgeHeight = job.IsActive ? detailHeight + BadgePadY * 2f * scale : 0f;
        var lineGap = JobsArt.LineGap * scale;
        var blockHeight = roleHeight + levelHeight + detailHeight + lineGap * 2f +
                          (job.IsActive ? badgeHeight + BarGap * scale : 0f);
        var height = MathF.Max(ringRadius * 2f, blockHeight) + pad * 2f;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, origin, max, radius, true);
        Material.TopGlow(drawList, origin, max, radius, tint, DetailGlowCoverage, DetailGlowStrength);

        var center = new Vector2(origin.X + pad + ringRadius, (origin.Y + max.Y) * 0.5f);
        var fill = detailRingFill.Step(job.Fraction, Motion.Sheet, JobsArt.FrameDelta());
        JobsArt.JobRing(drawList, textures, center, ringRadius, DetailRingThickness * scale, fill, job, ui.TitleInk,
            IsPending(job), scale);

        var top = center.Y - blockHeight * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(JobRoles.Title(job.Role)), textWidth, TextStyles.FootnoteEmphasized),
            Palette.Lighten(tint, 0.2f), TextStyles.FootnoteEmphasized);
        top += roleHeight + lineGap;
        var levelLine = job.IsLocked ? Loc.T(L.Jobs.Locked) : job.LevelText;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(levelLine, textWidth, TextStyles.Title2), ui.TitleInk, TextStyles.Title2);
        top += levelHeight + lineGap;
        var detail = job.IsCapped ? Loc.T(L.Jobs.LevelCap) : job.IsLocked ? job.Abbreviation : job.PercentText;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(detail, textWidth, TextStyles.Footnote), job.IsCapped ? tint : ui.MutedInk,
            TextStyles.Footnote);
        if (job.IsActive)
        {
            top += detailHeight + BarGap * scale;
            DrawBadge(drawList, new Vector2(textLeft, top), textWidth, Loc.T(L.Jobs.CurrentJob), scale);
        }

        return max.Y;
    }

    private void DrawBadge(ImDrawListPtr drawList, Vector2 topLeft, float maxWidth, string text, float scale)
    {
        var padX = BadgePadX * scale;
        var padY = BadgePadY * scale;
        var fitted = Typography.FitText(text, MathF.Max(1f, maxWidth - padX * 2f), TextStyles.FootnoteEmphasized);
        var size = Typography.Measure(fitted, TextStyles.FootnoteEmphasized);
        var max = topLeft + new Vector2(size.X + padX * 2f, size.Y + padY * 2f);
        drawList.AddRectFilled(topLeft, max, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, BadgeFillAlpha)),
            (max.Y - topLeft.Y) * 0.5f);
        Typography.Draw(drawList, topLeft + new Vector2(padX, padY), fitted, Palette.Lighten(ui.Accent, 0.3f),
            TextStyles.FootnoteEmphasized);
    }

    private float DrawExperienceCard(ImDrawListPtr drawList, float left, float top, float width, JobRow job,
        float scale)
    {
        if (job.IsLocked || job.IsCapped || job.ExperienceNeeded <= 0)
        {
            return top;
        }

        var tint = JobsArt.Tint(job.Role);
        var pad = JobsArt.CardPad * scale;
        var rested = job.IsCombat
            ? JobsRoster.RestedShare(snapshot.RestedExperience, job.Experience, job.ExperienceNeeded, false)
            : 0L;
        var headerHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var footnoteHeight = Typography.LineHeight(TextStyles.Footnote);
        var legendHeight = rested > 0 ? footnoteHeight + BarGap * scale : 0f;
        var cardTop = top + JobsArt.TileGap * scale;
        var height = pad * 2f + headerHeight + BarGap * scale + BarHeight * scale + BarGap * scale + footnoteHeight +
                     legendHeight;
        var max = new Vector2(left + width, cardTop + height);
        ui.Card(drawList, new Vector2(left, cardTop), max, Metrics.Radius.Widget * scale, true);
        UiAnchors.Report("jobs.experience", new Rect(new Vector2(left, cardTop), max));

        var innerLeft = left + pad;
        var innerWidth = width - pad * 2f;
        var cursorY = cardTop + pad;
        var percent = job.PercentText;
        var percentWidth = Typography.Measure(percent, TextStyles.FootnoteEmphasized).X;
        Typography.Draw(drawList, new Vector2(innerLeft, cursorY),
            Typography.FitText(job.ToGoText, MathF.Max(1f, innerWidth - percentWidth - BarGap * scale),
                TextStyles.SubheadlineEmphasized), ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList,
            new Vector2(innerLeft + innerWidth - percentWidth,
                cursorY + headerHeight - Typography.LineHeight(TextStyles.FootnoteEmphasized)), percent, tint,
            TextStyles.FootnoteEmphasized);
        cursorY += headerHeight + BarGap * scale;
        var fill = detailBarFill.Step(job.Fraction, Motion.Sheet, JobsArt.FrameDelta());
        JobsArt.Bar(drawList,
            new Rect(new Vector2(innerLeft, cursorY), new Vector2(innerLeft + innerWidth, cursorY + BarHeight * scale)),
            fill, rested / (float)job.ExperienceNeeded, tint, ui.TitleInk);
        cursorY += BarHeight * scale + BarGap * scale;
        Typography.Draw(drawList, new Vector2(innerLeft, cursorY),
            Typography.FitText(job.ProgressText, innerWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (rested <= 0)
        {
            return max.Y;
        }

        cursorY += footnoteHeight + BarGap * scale;
        var dot = LegendDot * scale;
        drawList.AddCircleFilled(new Vector2(innerLeft + dot, cursorY + footnoteHeight * 0.5f), dot,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.6f)), 12);
        var legendLeft = innerLeft + dot * 2f + LegendGap * scale;
        Typography.Draw(drawList, new Vector2(legendLeft, cursorY),
            Typography.FitText(snapshot.RestedText, MathF.Max(1f, innerLeft + innerWidth - legendLeft),
                TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        return max.Y;
    }

    private float DrawDetailBody(ImDrawListPtr drawList, float left, float top, float width, JobRow job, float scale)
    {
        if (job.IsLocked)
        {
            return top + JobsArt.TileGap * scale + JobsArt.State(drawList, ui,
                new Vector2(left, top + JobsArt.TileGap * scale), width, FontAwesomeIcon.Lock,
                JobsArt.Tint(job.Role), Loc.T(L.Jobs.LockedTitle), job.LockedText, scale);
        }

        if (job.GearsetIndices.Length == 0)
        {
            return top + JobsArt.TileGap * scale + JobsArt.State(drawList, ui,
                new Vector2(left, top + JobsArt.TileGap * scale), width, FontAwesomeIcon.Tshirt,
                JobsArt.Tint(job.Role), Loc.T(L.Jobs.NoGearsetTitle), Loc.T(L.Jobs.NoGearsetBody), scale);
        }

        var cursorY = DrawSwitchAction(left, top, width, job, scale);
        cursorY = SectionTop(drawList, left, cursorY, width, Loc.T(L.Jobs.Gearsets), scale);
        ImGui.SetCursorScreenPos(new Vector2(left, cursorY));
        var card = GroupCard.Begin(ui, job.GearsetIndices.Length, GearsetRowHeight);
        for (var index = 0; index < job.GearsetIndices.Length; index++)
        {
            DrawGearsetRow(drawList, card.NextRow(), snapshot.Gearsets[job.GearsetIndices[index]], scale);
        }

        card.End();
        return ImGui.GetCursorScreenPos().Y;
    }

    private float DrawSwitchAction(float left, float top, float width, JobRow job, float scale)
    {
        var best = snapshot.Gearsets[job.GearsetIndices[0]];
        if (job.IsActive && pendingGearsetId < 0)
        {
            return top;
        }

        var actionTop = top + JobsArt.TileGap * scale;
        var rect = new Rect(new Vector2(left, actionTop), new Vector2(left + width, actionTop + ActionHeight * scale));
        UiAnchors.Report("jobs.switch", rect);
        var pending = pendingGearsetId >= 0;
        var label = IsPending(job) ? Loc.T(L.Jobs.Switching) : best.SwitchText;
        if (ui.AccentPill(rect, label, !pending, TextStyles.Headline))
        {
            RequestEquip(best);
        }

        return rect.Max.Y;
    }

    private void DrawGearsetRow(ImDrawListPtr drawList, Rect contentRect, GearsetRow gearset, float scale)
    {
        var rowRect = new Rect(new Vector2(contentRect.Min.X - Metrics.Space.Lg * scale, contentRect.Min.Y),
            new Vector2(contentRect.Max.X + Metrics.Space.Lg * scale, contentRect.Max.Y));
        var menuRadius = GearsetMenuRadius * scale;
        var menuCenter = new Vector2(contentRect.Max.X - menuRadius, contentRect.Center.Y);
        var menuHalf = new Vector2(menuRadius, menuRadius);
        var menuRect = new Rect(menuCenter - menuHalf, menuCenter + menuHalf);
        if (!gearsetAnchorTaken)
        {
            gearsetAnchorTaken = true;
            UiAnchors.Report("jobs.gearset", rowRect);
            UiAnchors.Report("jobs.gearset.menu", menuRect);
        }

        var overMenu = UiInteract.Hover(menuRect.Min, menuRect.Max);
        var equippable = !gearset.IsActive && pendingGearsetId < 0;
        var hovered = equippable && !overMenu && UiInteract.Hover(rowRect.Min, rowRect.Max);
        if (hovered)
        {
            var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? GearsetRowPressAlpha : GearsetRowWashAlpha;
            drawList.AddRectFilled(rowRect.Min, rowRect.Max, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, alpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconSize = GearsetIconSize * scale;
        var iconMin = new Vector2(contentRect.Min.X, contentRect.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        GameIconTile.Draw(drawList, textures, gearset.IconId, iconMin, iconMax, GearsetIconRadius * scale, scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), edgeStroke: true);

        var trailingRight = menuCenter.X - menuRadius - GearsetTextGap * scale;
        var trailingWidth = 0f;
        if (IsPending(gearset))
        {
            var spinnerRadius = GearsetSpinnerRadius * scale;
            var spinnerCenter = new Vector2(trailingRight - spinnerRadius, contentRect.Center.Y);
            ProgressRing.Track(drawList, spinnerCenter, spinnerRadius, GearsetSpinnerThickness * scale,
                Palette.WithAlpha(ui.TitleInk, 0.14f));
            ProgressRing.Sweep(spinnerCenter, spinnerRadius, GearsetSpinnerThickness * scale, ui.Accent, 900.0, 1.6f,
                0.9f, drawList);
            trailingWidth = spinnerRadius * 2f;
        }
        else if (gearset.IsActive)
        {
            trailingWidth = DrawActiveBadge(drawList, new Vector2(trailingRight, contentRect.Center.Y),
                Loc.T(L.Jobs.Active), scale);
        }

        var textLeft = iconMax.X + GearsetTextGap * scale;
        var textWidth = MathF.Max(1f, trailingRight - trailingWidth - GearsetTextGap * scale - textLeft);
        Typography.Draw(drawList, new Vector2(textLeft, contentRect.Min.Y + GearsetNameTop * scale),
            Typography.FitText(gearset.Name, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        var subTop = contentRect.Min.Y + GearsetSubTop * scale;
        var itemLevel = Typography.FitText(gearset.ItemLevelText, textWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, subTop), itemLevel, ui.MutedInk, TextStyles.Footnote);
        if (gearset.MainHandMissing)
        {
            var warningLeft = textLeft + Typography.Measure(itemLevel, TextStyles.Footnote).X + GearsetTextGap * scale;
            Typography.Draw(drawList, new Vector2(warningLeft, subTop),
                Typography.FitText(Loc.T(L.Jobs.MainHandMissing), MathF.Max(1f, textLeft + textWidth - warningLeft),
                    TextStyles.Footnote), theme.Danger, TextStyles.Footnote);
        }

        if (ui.IconButton(menuCenter, menuRadius, IconGlyph.Of(FontAwesomeIcon.EllipsisH), ui.MutedInk, default, 0.5f))
        {
            UiFeedback.Play(UiSound.Tap);
            menuGearsetId = gearset.Id;
            menu.Toggle(RowMenuId, menuRect);
        }

        if (!overMenu && UiInteract.Click(rowRect.Min, rowRect.Max, hovered))
        {
            RequestEquip(gearset);
        }
    }

    private float DrawActiveBadge(ImDrawListPtr drawList, Vector2 rightCenter, string text, float scale)
    {
        var textSize = Typography.Measure(text, TextStyles.Caption2);
        var padX = BadgePadX * scale;
        var padY = BadgePadY * scale + scale;
        var width = textSize.X + padX * 2f;
        var height = textSize.Y + padY * 2f;
        var max = new Vector2(rightCenter.X, rightCenter.Y + height * 0.5f);
        var min = new Vector2(max.X - width, rightCenter.Y - height * 0.5f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, BadgeFillAlpha)),
            height * 0.5f);
        Typography.Draw(drawList, new Vector2(min.X + padX, rightCenter.Y - textSize.Y * 0.5f), text,
            Palette.Lighten(ui.Accent, 0.3f), TextStyles.Caption2);
        return width;
    }
}
