using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const int DescriptionBufferLength = 300;
    private const int CategoryColumns = 4;
    private const int PreviewTitleLines = 3;
    private const float CategoryTileHeight = 76f;
    private const float CategoryTileGap = 8f;
    private const float CategoryGlyphTile = 34f;
    private const float CategoryGlyph = 16f;
    private const float DescriptionHeight = 92f;
    private const float RulerHeight = 34f;
    private const float RulerReadoutHeight = 30f;
    private const float RulerMinorTick = 6f;
    private const float RulerMajorTick = 12f;
    private const float RulerTrack = 4f;
    private const float RulerKnob = 9f;
    private const float RulerBlockGap = 14f;
    private const float StepperHeight = 56f;
    private const float StepperGlyphFraction = 0.8f;
    private const float ToggleTileHeight = 78f;
    private const int RulerMajorEvery = 4;

    private readonly CachedText[] createTexts = new CachedText[6];
    private int createCategory;
    private string createDescription = string.Empty;
    private SharedLocation? createLocation;
    private string createLocationSummary = string.Empty;
    private string createSpot = string.Empty;
    private int createLead = MusterSchedule.DefaultLeadMinutes;
    private int createDuration = MusterSchedule.DefaultDurationMinutes;
    private bool createLimit;
    private int createMaxAttendees = MusterDraft.DefaultAttendees;
    private bool createUnlistWhenFull;
    private bool createIsPublic = true;
    private bool createBusy;
    private bool createSucceeded;
    private CachedText descriptionTooLongText;
    private MusterCreateOutcome? createOutcome;
    private int createWorldId = -1;
    private int createDataCenterId;
    private uint activeRuler;
    private Action? decrementAttendees;
    private Action? incrementAttendees;

    private enum CreateText : byte
    {
        Counter,
        Status,
        Range,
        StartReadout,
        LastsReadout,
        Spots,
    }

    private void OpenCreate()
    {
        if (store.Mine is not null)
        {
            OpenManage();
            return;
        }

        if (createSucceeded)
        {
            createSucceeded = false;
            ResetCreateForm();
        }

        createOutcome = null;
        createBusy = false;
        router.Push(MusterRoute.Create(RootTitle()));
    }

    private void DrawCreate(in PhoneContext context, MusterRoute route)
    {
        if (createSucceeded)
        {
            createSucceeded = false;
            ResetCreateForm();
            router.Pop(false);
            activeTab = MusterTab.Plans;
            OpenManage(false);
            return;
        }

        var scale = UiScale.Current;
        var nowUnix = NowUnix();
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("muster.create"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawCreatePreview(drawList, origin.X, origin.Y, width, nowUnix, scale);
            cursorY = DrawCategoryGrid(drawList, origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
            cursorY = DrawCreateDetails(drawList, origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
            cursorY = DrawCreateWhere(drawList, origin.X, cursorY + MusterArt.CardGap * scale, width, scale);
            cursorY = DrawCreateWhen(drawList, origin.X, cursorY + MusterArt.SectionGap * scale, width, nowUnix,
                scale, out var dragging);
            if (dragging)
            {
                surface.CancelDrag();
            }

            cursorY = DrawCreateWho(drawList, origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
            cursorY = DrawCreateSubmit(origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
            MusterArt.Reserve(origin, width, cursorY + MusterArt.BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "muster.create.nav", Loc.T(L.Muster.NewMuster),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, route.BackTitle, back);
    }

    private float DrawCreatePreview(ImDrawListPtr drawList, float left, float top, float width, long nowUnix,
        float scale)
    {
        var minute = nowUnix / 60;
        var startUnix = minute * 60 + createLead * 60L;
        var endUnix = startUnix + createDuration * 60L;
        var hasDescription = MusterDraft.TrimmedLength(createDescription) > 0;
        var spot = createSpot.Length > 0 ? createSpot : createLocationSummary;
        var poster = new MusterPoster
        {
            Category = createCategory,
            Eyebrow = MusterLabels.Range(startUnix, endUnix, ref createTexts[(int)CreateText.Range]),
            Title = hasDescription ? createDescription : Loc.T(L.Muster.DescriptionLabel),
            TitleMuted = !hasDescription,
            Status = createLead == 0 ? Loc.T(L.Common.Live) : PreviewStatus(),
            Live = createLead == 0,
            HostName = string.Empty,
            HostWorld = string.Empty,
            HostFrameId = string.Empty,
            Identity = string.Empty,
            Place = spot,
            Count = createLimit ? SpotsText() : string.Empty,
        };
        var height = MusterArt.PosterHeight(in poster, width, PreviewTitleLines, scale);
        MusterArt.Poster(drawList, ImGui.GetID("muster.create.preview"), in poster, new Vector2(left, top), width,
            PreviewTitleLines, theme, images, lodestone, false, scale);
        return top + height;
    }

    private string PreviewStatus()
    {
        ref var cache = ref createTexts[(int)CreateText.Status];
        return cache.IsCurrent(createLead)
            ? cache.Value
            : cache.Store(createLead, Loc.T(L.Muster.StartsIn, MusterText.Span(createLead * 60L)));
    }

    private string SpotsText()
    {
        ref var cache = ref createTexts[(int)CreateText.Spots];
        return cache.IsCurrent(createMaxAttendees)
            ? cache.Value
            : cache.Store(createMaxAttendees, Loc.T(L.Muster.SpotsValue, createMaxAttendees));
    }

    private float DrawCategoryGrid(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, Loc.T(L.Muster.CategorySection), ui.TitleInk);
        var categories = MusterCategories.All;
        var gap = CategoryTileGap * scale;
        var tileWidth = (width - gap * (CategoryColumns - 1)) / CategoryColumns;
        var tileHeight = CategoryTileHeight * scale;
        var rows = (categories.Length + CategoryColumns - 1) / CategoryColumns;
        UiAnchors.Report("muster.create.category", new Rect(new Vector2(left, cursorY),
            new Vector2(left + width, cursorY + rows * tileHeight + (rows - 1) * gap)));
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            var column = index % CategoryColumns;
            var rowIndex = index / CategoryColumns;
            var min = new Vector2(left + column * (tileWidth + gap), cursorY + rowIndex * (tileHeight + gap));
            var rect = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            if (DrawCategoryTile(drawList, rect, category, category == createCategory, scale))
            {
                UiFeedback.Play(UiSound.Tap);
                createCategory = category;
            }
        }

        return cursorY + rows * tileHeight + (rows - 1) * gap;
    }

    private bool DrawCategoryTile(ImDrawListPtr drawList, Rect rect, int category, bool selected, float scale)
    {
        var key = KeyFor("category", category, string.Empty);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(key, pressed, Core.Animation.Motion.PressScaleControl);
        var amount = PressFx.Toward(key + 1u, selected ? 1f : 0f);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var tint = MusterArt.Tint(category);
        var rest = Surfaces.Fill(ui.TitleInk, hovered ? FillLevel.Secondary : FillLevel.Tertiary);
        var lit = Palette.WithAlpha(tint, hovered ? 0.24f : 0.18f);
        var radius = Metrics.Radius.Card * scale;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Vector4.Lerp(rest, lit, amount)));
        if (amount > 0.01f)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(tint with { W = 0.7f * amount }),
                Metrics.Stroke.Ring * scale);
        }

        var glyphTile = CategoryGlyphTile * scale;
        var center = new Vector2((min.X + max.X) * 0.5f, min.Y + Metrics.Space.Md * scale + glyphTile * 0.5f);
        var tileMin = center - new Vector2(glyphTile, glyphTile) * 0.5f;
        var tileMax = center + new Vector2(glyphTile, glyphTile) * 0.5f;
        IconTile.FillShaded(drawList, tileMin, tileMax, glyphTile * Metrics.Radius.TileFactor,
            MusterArt.Surface(category), 0.55f + 0.45f * amount);
        ProgressRing.CenterIcon(drawList, center, MusterCategories.Icon(category), AccentRing.Ink,
            CategoryGlyph * scale);
        var label = Typography.FitText(Loc.T(MusterCategories.Label(category)),
            MathF.Max(1f, max.X - min.X - Metrics.Space.Xs * scale), TextStyles.Caption1);
        var labelHeight = Typography.LineHeight(TextStyles.Caption1);
        Typography.DrawCentered(drawList,
            new Vector2(center.X, tileMax.Y + Metrics.Space.Xs * scale + labelHeight * 0.5f), label,
            selected ? ui.TitleInk : ui.BodyInk, TextStyles.Caption1);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private float DrawCreateDetails(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var rowHeight = MusterArt.FieldRowHeight * scale;
        var descriptionHeight = DescriptionHeight * scale;
        var max = new Vector2(left + width, top + descriptionHeight + rowHeight);
        ui.Card(drawList, new Vector2(left, top), max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var framePadding = ImGui.GetStyle().FramePadding;
        var counter = DescriptionCounter();
        var counterSize = Typography.Measure(counter, TextStyles.Caption1);
        var notesMin = new Vector2(left + pad - framePadding.X, top + Metrics.Space.Sm * scale);
        var notesSize = new Vector2(width - pad * 2f + framePadding.X * 2f,
            descriptionHeight - Metrics.Space.Sm * scale - counterSize.Y - Metrics.Space.Xs * scale);
        UiAnchors.Report("muster.create.description", new Rect(new Vector2(left, top),
            new Vector2(max.X, top + descriptionHeight)));
        if (createDescription.Length == 0)
        {
            Typography.Draw(drawList, notesMin + framePadding, Loc.T(L.Muster.DescriptionLabel), ui.MutedInk,
                TextStyles.Body);
        }

        ImGui.SetCursorScreenPos(notesMin);
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgHovered, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgActive, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        {
            var wrapWidth = notesSize.X - framePadding.X * 2f - Metrics.Space.Xxs * scale;
            SoftWrapField.Multiline("##musterDescription", ref createDescription, DescriptionBufferLength, notesSize,
                wrapWidth);
        }

        var over = createDescription.Length > MusterDraft.DescriptionMaxLength;
        Typography.Draw(drawList,
            new Vector2(max.X - pad - counterSize.X, top + descriptionHeight - counterSize.Y - Metrics.Space.Xs * scale),
            counter, over ? ui.Theme.Danger : ui.MutedInk, TextStyles.Caption1);
        var spotTop = top + descriptionHeight;
        FeedCell.Hairline(drawList, left + pad, max.X, spotTop, ui.Hairline);
        ImGui.SetCursorScreenPos(new Vector2(left + pad - framePadding.X,
            spotTop + rowHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - pad * 2f + framePadding.X * 2f);
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgHovered, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgActive, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (UiScope.PushColor(ImGuiCol.TextDisabled, ui.MutedInk))
        {
            var hint = Loc.T(L.Muster.MeetingSpot);
            Plugin.Fonts.NoticeText(hint);
            Plugin.Fonts.NoticeText(createSpot);
            ImGui.InputTextWithHint("##musterSpot", hint, ref createSpot, MusterDraft.SpotMaxLength);
        }

        return max.Y;
    }

    private string DescriptionCounter()
    {
        ref var cache = ref createTexts[(int)CreateText.Counter];
        var length = createDescription.Length;
        return cache.IsCurrent(length)
            ? cache.Value
            : cache.Store(length, Loc.T(L.Common.PhotoCounter, length, MusterDraft.DescriptionMaxLength));
    }

    private float DrawCreateWhere(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + MusterArt.FieldRowHeight * scale));
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var glyph = LocationGlyph * scale;
        var glyphCenter = new Vector2(left + pad + glyph * 0.5f, row.Center.Y);
        var textLeft = glyphCenter.X + glyph * 0.5f + MusterArt.TextGap * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        if (createLocation is null)
        {
            var hovered = MusterArt.RowWash(drawList, ui, row, scale);
            ProgressRing.CenterIcon(drawList, glyphCenter, FontAwesomeIcon.LocationArrow, ui.Accent, glyph);
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - lineHeight * 0.5f),
                Typography.FitText(Loc.T(L.Muster.UseMyLocation), MathF.Max(1f, row.Max.X - pad - textLeft),
                    TextStyles.Body), ui.Accent, TextStyles.Body);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                CaptureCreateLocation();
            }

            return row.Max.Y;
        }

        ProgressRing.CenterIcon(drawList, glyphCenter, FontAwesomeIcon.MapMarkerAlt, ui.Accent, glyph);
        var clearLabel = Loc.T(L.Muster.ClearLocation);
        var clearSize = Typography.Measure(clearLabel, TextStyles.Body);
        var clearRect = new Rect(new Vector2(row.Max.X - pad - clearSize.X - Metrics.Space.Sm * scale, row.Min.Y),
            row.Max);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - lineHeight * 0.5f),
            Typography.FitText(createLocationSummary, MathF.Max(1f, clearRect.Min.X - textLeft), TextStyles.Body),
            ui.TitleInk, TextStyles.Body);
        var clearHovered = UiInteract.Hover(clearRect.Min, clearRect.Max);
        Typography.Draw(drawList, new Vector2(row.Max.X - pad - clearSize.X, row.Center.Y - clearSize.Y * 0.5f),
            clearLabel, clearHovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent, TextStyles.Body);
        if (clearHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(clearRect.Min, clearRect.Max, clearHovered))
        {
            UiFeedback.Play(UiSound.ToggleOff);
            createLocation = null;
            createLocationSummary = string.Empty;
        }

        return row.Max.Y;
    }

    private void CaptureCreateLocation()
    {
        createLocation = LocationShare.Capture();
        if (createLocation is not { } location)
        {
            UiFeedback.Play(UiSound.Caution);
            createLocationSummary = string.Empty;
            return;
        }

        UiFeedback.Play(UiSound.ToggleOn);
        createLocationSummary = LocationShare.Summary(in location);
    }

    private float DrawCreateWhen(ImDrawListPtr drawList, float left, float top, float width, long nowUnix,
        float scale, out bool dragging)
    {
        var cursorY = top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, Loc.T(L.Muster.WhenSection), ui.TitleInk);
        var pad = Metrics.Space.Lg * scale;
        var blockHeight = (RulerReadoutHeight + RulerHeight) * scale;
        var max = new Vector2(left + width, cursorY + pad * 2f + blockHeight * 2f + RulerBlockGap * scale);
        ui.Card(drawList, new Vector2(left, cursorY), max, Metrics.Radius.Grouped * scale);
        UiAnchors.Report("muster.create.when", new Rect(new Vector2(left, cursorY), max));
        var minute = nowUnix / 60;
        var leadTop = cursorY + pad;
        DrawReadout(drawList, left + pad, max.X - pad, leadTop, Loc.T(L.Muster.StartLabel), StartReadout(minute),
            scale);
        var leadTrack = new Rect(new Vector2(left + pad, leadTop + RulerReadoutHeight * scale),
            new Vector2(max.X - pad, leadTop + blockHeight));
        var leadFraction = DrawRuler(drawList, ImGui.GetID("muster.create.lead"), leadTrack,
            MusterSchedule.LeadFraction(createLead), MusterSchedule.MaxLeadMinutes / MusterSchedule.StepMinutes,
            scale, out var leadDragging);
        var lead = MusterSchedule.LeadFromFraction(leadFraction);
        if (lead != createLead)
        {
            UiFeedback.Play(UiSound.Keystroke);
            createLead = lead;
        }

        var durationTop = leadTop + blockHeight + RulerBlockGap * scale;
        DrawReadout(drawList, left + pad, max.X - pad, durationTop, Loc.T(L.Muster.LastsLabel),
            LastsReadout(minute), scale);
        var durationTrack = new Rect(new Vector2(left + pad, durationTop + RulerReadoutHeight * scale),
            new Vector2(max.X - pad, durationTop + blockHeight));
        var durationFraction = DrawRuler(drawList, ImGui.GetID("muster.create.duration"), durationTrack,
            MusterSchedule.DurationFraction(createDuration),
            (MusterSchedule.MaxDurationMinutes - MusterSchedule.MinDurationMinutes) / MusterSchedule.StepMinutes, scale,
            out var durationDragging);
        var duration = MusterSchedule.DurationFromFraction(durationFraction);
        if (duration != createDuration)
        {
            UiFeedback.Play(UiSound.Keystroke);
            createDuration = duration;
        }

        dragging = leadDragging || durationDragging;
        return max.Y;
    }

    private void DrawReadout(ImDrawListPtr drawList, float left, float right, float top, string label, string value,
        float scale)
    {
        var centerY = top + RulerReadoutHeight * 0.5f * scale;
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(left, centerY - labelSize.Y * 0.5f), label, ui.TitleInk,
            TextStyles.Body);
        var fitted = Typography.FitText(value, MathF.Max(1f, right - left - labelSize.X - Metrics.Space.Md * scale),
            TextStyles.BodyEmphasized);
        var valueSize = Typography.Measure(fitted, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(right - valueSize.X, centerY - valueSize.Y * 0.5f), fitted, ui.Accent,
            TextStyles.BodyEmphasized);
    }

    private string StartReadout(long minute)
    {
        if (createLead == 0)
        {
            return Loc.T(L.Muster.Now);
        }

        ref var cache = ref createTexts[(int)CreateText.StartReadout];
        var key = minute * 1000 + createLead;
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var startUnix = minute * 60 + createLead * 60L;
        return cache.Store(key,
            Loc.T(L.Muster.StartsValue, MusterText.Span(createLead * 60L), TimeText.Clock(startUnix)));
    }

    private string LastsReadout(long minute)
    {
        ref var cache = ref createTexts[(int)CreateText.LastsReadout];
        var key = (minute * 1000 + createLead) * 1000 + createDuration;
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var endUnix = minute * 60 + (createLead + createDuration) * 60L;
        return cache.Store(key,
            Loc.T(L.Muster.LastsValue, MusterText.Span(createDuration * 60L), TimeText.Clock(endUnix)));
    }

    private float DrawRuler(ImDrawListPtr drawList, uint key, Rect track, float fraction, int steps, float scale,
        out bool dragging)
    {
        var knob = RulerKnob * scale;
        var hitMin = new Vector2(track.Min.X - knob, track.Min.Y);
        var hitMax = new Vector2(track.Max.X + knob, track.Max.Y);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        if (hovered && activeRuler == 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            activeRuler = key;
        }

        dragging = activeRuler == key && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (activeRuler == key && !dragging)
        {
            activeRuler = 0;
        }

        var width = MathF.Max(1f, track.Width);
        var value = Math.Clamp(fraction, 0f, 1f);
        if (dragging)
        {
            value = Math.Clamp((ImGui.GetMousePos().X - track.Min.X) / width, 0f, 1f);
            UiInteract.ReportGestureSurface();
            UiInteract.CancelPendingTap();
        }

        var centerY = track.Center.Y;
        var tickInk = ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.55f));
        var stepCount = Math.Max(1, steps);
        for (var index = 0; index <= stepCount; index++)
        {
            var x = track.Min.X + width * index / stepCount;
            var tick = (index % RulerMajorEvery == 0 ? RulerMajorTick : RulerMinorTick) * scale * 0.5f;
            drawList.AddLine(new Vector2(x, centerY - tick), new Vector2(x, centerY + tick), tickInk,
                Metrics.Stroke.Hairline);
        }

        var snapped = MathF.Round(value * stepCount) / stepCount;
        var knobX = track.Min.X + width * snapped;
        var railHalf = RulerTrack * 0.5f * scale;
        drawList.AddRectFilled(new Vector2(track.Min.X, centerY - railHalf), new Vector2(knobX, centerY + railHalf),
            ImGui.GetColorU32(ui.Accent), railHalf);
        var engaged = PressFx.Toward(key, hovered || dragging ? 1f : 0f);
        var knobRadius = knob * (1f + 0.2f * engaged);
        drawList.AddCircleFilled(new Vector2(knobX, centerY + 1.5f * scale), knobRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)), 24);
        drawList.AddCircleFilled(new Vector2(knobX, centerY), knobRadius, ImGui.GetColorU32(AccentRing.Ink), 24);
        if (hovered || dragging)
        {
            UiInteract.ReportGestureSurface();
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return value;
    }

    private float DrawCreateWho(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, Loc.T(L.Muster.WhoSection), ui.TitleInk);
        var gap = CategoryTileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var tileHeight = ToggleTileHeight * scale;
        var publicRect = new Rect(new Vector2(left, cursorY), new Vector2(left + tileWidth, cursorY + tileHeight));
        if (MusterArt.ToggleTile(drawList, ui, ImGui.GetID("muster.create.public"), publicRect,
                FontAwesomeIcon.GlobeAmericas, Loc.T(L.Muster.ListPublicly), createIsPublic, scale))
        {
            createIsPublic = !createIsPublic;
        }

        var limitRect = new Rect(new Vector2(left + tileWidth + gap, cursorY),
            new Vector2(left + width, cursorY + tileHeight));
        if (MusterArt.ToggleTile(drawList, ui, ImGui.GetID("muster.create.limit"), limitRect,
                FontAwesomeIcon.UserFriends, Loc.T(L.Muster.LimitAttendance), createLimit, scale))
        {
            createLimit = !createLimit;
        }

        cursorY += tileHeight;
        if (createLimit)
        {
            cursorY = DrawSpotsStepper(drawList, left, cursorY + gap, width, scale);
            var unlistRect = new Rect(new Vector2(left, cursorY + gap),
                new Vector2(left + width, cursorY + gap + tileHeight));
            if (MusterArt.ToggleTile(drawList, ui, ImGui.GetID("muster.create.unlist"), unlistRect,
                    FontAwesomeIcon.EyeSlash, Loc.T(L.Muster.UnlistWhenFull), createUnlistWhenFull, scale))
            {
                createUnlistWhenFull = !createUnlistWhenFull;
            }

            cursorY = unlistRect.Max.Y;
        }

        var hintTop = cursorY + Metrics.Space.Sm * scale;
        var hintHeight = Typography.DrawWrappedLeft(new Vector2(left + Metrics.Space.Lg * scale, hintTop),
            Loc.T(L.Muster.PublicHint), ui.MutedInk, TextStyles.Footnote, width - Metrics.Space.Lg * 2f * scale);
        return hintTop + hintHeight;
    }

    private float DrawSpotsStepper(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        decrementAttendees ??= () => SetMaxAttendees(createMaxAttendees - 1);
        incrementAttendees ??= () => SetMaxAttendees(createMaxAttendees + 1);
        var max = new Vector2(left + width, top + StepperHeight * scale);
        ui.Card(drawList, new Vector2(left, top), max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var radius = RoundButton.RegularRadius * scale;
        var centerY = (top + max.Y) * 0.5f;
        var glyphHeight = radius * StepperGlyphFraction;
        if (RoundButton.FontIcon(drawList, "muster.create.fewer", new Vector2(left + pad + radius, centerY), radius,
                FontAwesomeIcon.Minus, glyphHeight, ui.Ink, ButtonStyle.Tinted,
                enabled: createMaxAttendees > MusterDraft.MinAttendees))
        {
            decrementAttendees();
        }

        if (RoundButton.FontIcon(drawList, "muster.create.more", new Vector2(max.X - pad - radius, centerY), radius,
                FontAwesomeIcon.Plus, glyphHeight, ui.Ink, ButtonStyle.Tinted,
                enabled: createMaxAttendees < MusterDraft.MaxAttendees))
        {
            incrementAttendees();
        }

        Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, centerY), SpotsText(), ui.TitleInk,
            TextStyles.Headline);
        return max.Y;
    }

    private void SetMaxAttendees(int value)
    {
        var next = MusterDraft.ClampAttendees(value);
        if (next == createMaxAttendees)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        createMaxAttendees = next;
    }

    private float DrawCreateSubmit(float left, float top, float width, float scale)
    {
        var dataCenterId = CreateDataCenterId(out _);
        var issue = MusterDraft.Validate(createDescription, createSpot, createLocation is not null, dataCenterId);
        var cursorY = top;
        var message = createOutcome is { } outcome ? OutcomeText(outcome) : IssueText(issue);
        if (message.Length > 0)
        {
            var ink = createOutcome is not null || issue == MusterDraftIssue.DescriptionTooLong
                ? ui.Theme.Danger
                : ui.MutedInk;
            var height = Typography.DrawWrappedLeft(new Vector2(left + Metrics.Space.Lg * scale, cursorY), message, ink,
                TextStyles.Footnote, width - Metrics.Space.Lg * 2f * scale);
            cursorY += height + Metrics.Space.Md * scale;
        }

        var rect = new Rect(new Vector2(left, cursorY), new Vector2(left + width, cursorY + MusterArt.PillHeight * scale));
        UiAnchors.Report("muster.create.submit", rect);
        if (createBusy)
        {
            ui.PaintAccentPill(rect, string.Empty, false, false, TextStyles.Headline);
            LoadingPulse.Spinner(rect.Center, 9f * scale, AccentRing.Ink);
        }
        else if (MusterArt.Action(ui, rect, Loc.T(L.Muster.CallIt), issue == MusterDraftIssue.None))
        {
            SubmitCreate();
        }

        return rect.Max.Y;
    }

    private string IssueText(MusterDraftIssue issue) =>
        issue switch
        {
            MusterDraftIssue.NeedDescription => Loc.T(L.Muster.NeedDescription),
            MusterDraftIssue.DescriptionTooLong => DescriptionTooLongText(),
            MusterDraftIssue.NeedWhere => Loc.T(L.Muster.NeedWhere),
            MusterDraftIssue.NeedDataCenter => Loc.T(L.Muster.NeedDataCenter),
            _ => string.Empty,
        };

    private string DescriptionTooLongText() =>
        descriptionTooLongText.IsCurrent(MusterDraft.DescriptionMaxLength)
            ? descriptionTooLongText.Value
            : descriptionTooLongText.Store(MusterDraft.DescriptionMaxLength,
                Loc.T(L.Muster.DescriptionTooLong, MusterDraft.DescriptionMaxLength));

    private static string OutcomeText(MusterCreateOutcome outcome) =>
        outcome switch
        {
            MusterCreateOutcome.AlreadyHosting => Loc.T(L.Muster.ErrorAlreadyHosting),
            MusterCreateOutcome.Invalid => Loc.T(L.Muster.ErrorInvalid),
            MusterCreateOutcome.RateLimited => Loc.T(L.Muster.ErrorRateLimited),
            _ => Loc.T(L.Muster.ErrorFailed),
        };

    private int CreateDataCenterId(out int worldId)
    {
        worldId = createLocation is { } location ? (int)location.WorldId : store.CurrentWorldId;
        if (worldId != createWorldId)
        {
            createWorldId = worldId;
            createDataCenterId = MusterWorlds.DataCenterIdForWorld((uint)worldId);
        }

        return createDataCenterId;
    }

    private void SubmitCreate()
    {
        var location = createLocation;
        var dataCenterId = CreateDataCenterId(out var worldId);
        if (dataCenterId == 0)
        {
            return;
        }

        var request = new CreateMusterRequest(
            createCategory,
            createDescription.Trim(),
            (int)(location?.TerritoryId ?? 0u),
            (int)(location?.MapId ?? 0u),
            location?.MapX ?? 0f,
            location?.MapY ?? 0f,
            worldId,
            location?.Ward ?? 0,
            location?.Plot ?? 0,
            location?.Room ?? 0,
            createSpot.Trim(),
            MusterCategories.RegionBitForWorld((uint)worldId),
            dataCenterId,
            MusterSchedule.ClampLead(createLead),
            MusterSchedule.ClampDuration(createDuration),
            createLimit ? createMaxAttendees : 0,
            createLimit && createUnlistWhenFull,
            createIsPublic);
        createBusy = true;
        createOutcome = null;
        store.Create(request, outcome =>
        {
            createBusy = false;
            if (outcome == MusterCreateOutcome.Created)
            {
                UiFeedback.Play(UiSound.Success);
                createSucceeded = true;
                return;
            }

            UiFeedback.Play(UiSound.Caution);
            createOutcome = outcome;
        });
    }

    private void ResetCreateForm()
    {
        createCategory = MusterCategories.Social;
        createDescription = string.Empty;
        createLocation = null;
        createLocationSummary = string.Empty;
        createSpot = string.Empty;
        createLead = MusterSchedule.DefaultLeadMinutes;
        createDuration = MusterSchedule.DefaultDurationMinutes;
        createLimit = false;
        createMaxAttendees = MusterDraft.DefaultAttendees;
        createUnlistWhenFull = false;
        createIsPublic = true;
        createBusy = false;
        createOutcome = null;
    }
}
