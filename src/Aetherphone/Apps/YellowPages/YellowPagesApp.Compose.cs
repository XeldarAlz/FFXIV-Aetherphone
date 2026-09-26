using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.YellowPages;

internal enum ComposeStep : byte
{
    Kind,
    Photos,
    Details,
    Options,
}

internal sealed partial class YellowPagesApp
{
    private const int TitleMaxLength = 80;
    private const int BodyMaxLength = 1000;
    private const int BodyBufferLength = 1200;
    private const int NoteMaxLength = 80;
    private const int TagMaxLength = 24;
    private const int MaxTags = 8;
    private const int LinkMaxLength = 200;
    private const int RequirementsMaxLength = 200;
    private const int MinutesPerDay = 1440;
    private const int DefaultOpenMinute = 1200;
    private const int DefaultCloseMinute = 1380;
    private const int MinOpenMinutes = 15;
    private const int ComposeStepCount = 4;
    private const float StepBarHeight = 4f;
    private const float StepBarGap = 6f;
    private const float StepBarMaxWidth = 220f;
    private const float StepRowHeight = 22f;
    private const float KindCardHeight = 84f;
    private const float KindTileSide = 42f;
    private const float DirectionTileHeight = 64f;
    private const float FieldCardRounding = 14f;
    private const float FieldCardPad = 14f;
    private const float FieldRowHeight = 46f;
    private const float FieldLabelWidth = 96f;
    private const float BodyFieldHeight = 120f;
    private const float TagsFieldHeight = 40f;
    private const float TagChipHeight = 28f;
    private const float DayCellHeight = 38f;
    private const float TimeFieldHeight = 42f;
    private const float AccentDotRadius = 14f;
    private const float AccentRowHeight = 52f;
    private const float ToggleRowHeight = 52f;
    private const float PublishHeight = 50f;
    private const float ComposePaneFraction = 0.5f;
    private const float ComposeGridGap = 6f;
    private const float KeptStripHeight = 64f;
    private const float ComposeAspectRatio = 1.3333f;
    private const int ComposeCounterWarning = 50;

    private static readonly int[] WeekDays = { 1, 2, 3, 4, 5, 6, 0 };
    private static readonly TextStyle FieldLabelStyle = new(0.9f, FontWeight.Regular);
    private static readonly TextStyle FieldHintStyle = TextStyles.Footnote;
    private static readonly TextStyle KindTitleStyle = TextStyles.Headline;
    private static readonly TextStyle KindHintStyle = TextStyles.Footnote;
    private static readonly TextStyle ComposeActionStyle = TextStyles.Headline;
    private static readonly TextStyle CounterStyle = TextStyles.Caption1;

    private readonly List<string> composeTags = new();
    private readonly List<string> composeKeptUrls = new();
    private readonly string[] priceModeLabels = new string[3];
    private readonly string[] kindChipLabels = new string[AdCategories.Count];
    private readonly bool[] kindChipActive = new bool[AdCategories.Count];
    private readonly ChipRail composeCategoryRail = new();
    private ComposeStep composeStep;
    private string? editingAdId;
    private int composeArchetype = -1;
    private bool composeWanted;
    private int composeCategory;
    private string composeTitle = string.Empty;
    private string composeBody = string.Empty;
    private string composeTagDraft = string.Empty;
    private SharedLocation? composeLocation;
    private string composeAddressNote = string.Empty;
    private readonly bool[] composeDays = new bool[7];
    private int composeOpenMinute = DefaultOpenMinute;
    private int composeCloseMinute = DefaultCloseMinute;
    private int composePriceMode;
    private string composePriceText = string.Empty;
    private string composeTurnaround = string.Empty;
    private string composeSlotsLine = string.Empty;
    private string composeRequirements = string.Empty;
    private string composeLink = string.Empty;
    private bool composeAllowInquiries = true;
    private bool composeAfterDark;
    private int composeAccent;
    private bool composeBusy;
    private bool composeSucceeded;
    private AdCreateOutcome? composeOutcome;
    private int composeCounterLength = -1;
    private string composeCounter = string.Empty;
    private bool composeTitleFocus;
    private AdDto? composePreview;
    private int composePreviewHash;

    private PhotoComposeStyle ComposeStyle => new(Accent, Ink.MutedInk, theme.SurfaceMuted, theme.SurfaceMuted, true);

    private PhotoEditPanelStyle ComposeEditStyle =>
        PhotoEditPanelStyle.ForComposer(ComposeStyle, Ink.TitleInk, theme.SurfaceMuted);

    private void StartCompose()
    {
        ResetComposeForm();
        composeSession.Open(false);
        router.Push(YellowPagesRoute.Compose);
    }

    public void StartEdit(AdDto ad)
    {
        ResetComposeForm();
        composeSession.Open(false);
        editingAdId = ad.Id;
        composeStep = ComposeStep.Details;
        composeArchetype = ad.Archetype;
        composeWanted = ad.Wanted;
        composeCategory = ad.Category;
        composeTitle = ad.Title;
        composeBody = ad.Body;
        composeTags.AddRange(ad.Tags);
        composeAddressNote = ad.AddressNote;
        composeAllowInquiries = ad.AllowInquiries;
        composeAfterDark = ad.AfterDark;
        composeAccent = ad.Accent;
        composeKeptUrls.AddRange(ad.MediaUrls);
        if (ad.TerritoryId > 0 || ad.Ward > 0)
        {
            composeLocation = AdText.Location(ad);
        }

        if (AdCategories.IsLinkOnly(ad.Category))
        {
            composeLink = ad.LinkUrl;
        }
        else if (ad.Archetype == AdArchetypes.Service)
        {
            composePriceMode = ad.PriceMode;
            composePriceText = ad.PriceGil > 0 ? ad.PriceGil.ToString(Loc.Culture) : string.Empty;
            composeTurnaround = ad.Turnaround;
        }
        else if (ad.Archetype == AdArchetypes.Call)
        {
            composeSlotsLine = ad.SlotsLine;
            composeRequirements = ad.Requirements;
        }

        if (ad.Schedule.Length > 0)
        {
            AdText.ToLocalSlot(ad.Schedule[0], out _, out var localStartMinute);
            composeOpenMinute = localStartMinute;
            composeCloseMinute = (localStartMinute + ad.Schedule[0].DurationMinutes) % MinutesPerDay;
            for (var index = 0; index < ad.Schedule.Length; index++)
            {
                AdText.ToLocalSlot(ad.Schedule[index], out var localDay, out _);
                composeDays[localDay] = true;
            }
        }

        if (router.Current.Screen != YellowPagesScreen.Compose)
        {
            router.Push(YellowPagesRoute.Compose);
        }
    }

    private void DrawCompose(Rect area)
    {
        if (composeSucceeded)
        {
            composeSucceeded = false;
            var wasEditing = editingAdId is not null;
            ResetComposeForm();
            router.Pop(false);
            if (!wasEditing)
            {
                activeTab = YellowPagesTab.Mine;
            }

            store.SyncNow();
            return;
        }

        composeSession.ConsumePendingImport();
        var scale = UiScale.Current;
        DrawComposeHeader(area, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        top = DrawStepBars(area, top, scale);
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        switch (composeStep)
        {
            case ComposeStep.Photos:
                DrawComposePhotos(body, scale);
                break;
            case ComposeStep.Details:
                DrawComposeDetails(body, scale);
                break;
            case ComposeStep.Options:
                DrawComposeOptions(body, scale);
                break;
            default:
                DrawComposeKind(body, scale);
                break;
        }
    }

    private void DrawComposeHeader(Rect area, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var title = Loc.T(editingAdId is null ? L.YellowPages.NewAd : L.YellowPages.EditAdTitle);
        var closing = composeStep == ComposeStep.Kind;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var chipRadius = SocialChrome.BackChipRadius * scale;
        var chipCenter = new Vector2(area.Min.X + 12f * scale + chipRadius, rowCenterY);
        if (closing)
        {
            if (DrawHeaderIcon(drawList, chipCenter, PhoneIcons.X, Loc.T(L.Common.Cancel)))
            {
                router.Pop();
            }
        }
        else if (SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, Ink))
        {
            ComposeBack();
        }

        var actionLabel = ComposeActionLabel();
        var actionEnabled = ComposeActionEnabled();
        var actionSize = Typography.Measure(actionLabel, ComposeActionStyle);
        var actionRect = new Rect(new Vector2(area.Max.X - CellPadX * scale - actionSize.X - 8f * scale, rowCenterY - 16f * scale),
            new Vector2(area.Max.X - CellPadX * scale + 8f * scale, rowCenterY + 16f * scale));
        var hovered = actionEnabled && UiInteract.Hover(actionRect.Min, actionRect.Max);
        if (composeBusy)
        {
            LoadingPulse.Spinner(actionRect.Center, 8f * scale, Ink.Accent);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(actionRect.Min.X + 8f * scale, rowCenterY - actionSize.Y * 0.5f), actionLabel,
                actionEnabled ? (hovered ? Ink.TitleInk : Ink.AccentLink) : Ink.FaintInk, ComposeActionStyle);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (actionEnabled && UiInteract.Click(actionRect.Min, actionRect.Max, hovered))
            {
                ComposeAdvance();
            }
        }

        var reserve = actionSize.X + 24f * scale;
        SocialChrome.DrawScreenHeader(area, title, Ink, back, ScreenTitleStyle, reserve / scale, string.Empty, false, true);
        DrawHairline(drawList, area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale);
    }

    private string ComposeActionLabel()
    {
        return composeStep switch
        {
            ComposeStep.Options => Loc.T(editingAdId is null ? L.YellowPages.PublishAd : L.YellowPages.SaveChanges),
            ComposeStep.Photos when composeSession.Stage == PhotoComposeStage.Pick && !composeSession.HasSelection
                && composeKeptUrls.Count == 0 => Loc.T(L.YellowPages.SkipPhotos),
            _ => Loc.T(L.Common.Next),
        };
    }

    private bool ComposeActionEnabled()
    {
        if (composeBusy)
        {
            return false;
        }

        return composeStep switch
        {
            ComposeStep.Kind => composeArchetype >= 0,
            ComposeStep.Details => ComposeDetailsValid(out _),
            ComposeStep.Options => ComposeDetailsValid(out _),
            _ => true,
        };
    }

    private void ComposeAdvance()
    {
        switch (composeStep)
        {
            case ComposeStep.Kind:
                EnsureCategoryForArchetype();
                composeStep = ComposeStep.Photos;
                break;
            case ComposeStep.Photos:
                if (composeSession.Stage == PhotoComposeStage.Pick)
                {
                    if (composeSession.HasSelection)
                    {
                        composeSession.BeginEdit();
                    }
                    else
                    {
                        composeStep = ComposeStep.Details;
                        composeTitleFocus = true;
                    }
                }
                else if (composeSession.Stage == PhotoComposeStage.Edit)
                {
                    composeSession.EditAdvance();
                    composeStep = ComposeStep.Details;
                    composeTitleFocus = true;
                }
                else
                {
                    composeStep = ComposeStep.Details;
                }

                break;
            case ComposeStep.Details:
                composeStep = ComposeStep.Options;
                break;
            default:
                SubmitCompose();
                break;
        }
    }

    private void ComposeBack()
    {
        switch (composeStep)
        {
            case ComposeStep.Photos:
                if (composeSession.Stage == PhotoComposeStage.Edit)
                {
                    composeSession.EditBack();
                }
                else
                {
                    composeStep = ComposeStep.Kind;
                }

                break;
            case ComposeStep.Details:
                composeStep = ComposeStep.Photos;
                if (composeSession.Stage == PhotoComposeStage.Caption)
                {
                    composeSession.CaptionBack();
                }

                break;
            case ComposeStep.Options:
                composeStep = ComposeStep.Details;
                break;
            default:
                router.Pop();
                break;
        }
    }

    private float DrawStepBars(Rect area, float top, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var trackWidth = MathF.Min(area.Width - 64f * scale, StepBarMaxWidth * scale);
        var gap = StepBarGap * scale;
        var segmentWidth = (trackWidth - gap * (ComposeStepCount - 1)) / ComposeStepCount;
        var height = StepBarHeight * scale;
        var left = area.Center.X - trackWidth * 0.5f;
        var barTop = top + (StepRowHeight * scale - height) * 0.5f;
        var activeStep = (int)composeStep;
        for (var index = 0; index < ComposeStepCount; index++)
        {
            var min = new Vector2(left + index * (segmentWidth + gap), barTop);
            var max = new Vector2(min.X + segmentWidth, barTop + height);
            var fill = index <= activeStep ? Ink.AccentLink : Palette.WithAlpha(Ink.MutedInk, 0.22f);
            Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        }

        return top + StepRowHeight * scale;
    }

    private void DrawComposeKind(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            ui.SectionHeading(Loc.T(L.YellowPages.WhatPosting));
            DrawKindCard(AdArchetypes.Place, FontAwesomeIcon.Cocktail, Loc.T(L.YellowPages.ArchetypePlace),
                Loc.T(L.YellowPages.ArchetypePlaceHint), scale);
            DrawKindCard(AdArchetypes.Service, FontAwesomeIcon.Hammer, Loc.T(L.YellowPages.ArchetypeService),
                Loc.T(L.YellowPages.ArchetypeServiceHint), scale);
            DrawKindCard(AdArchetypes.Call, FontAwesomeIcon.Flag, Loc.T(L.YellowPages.ArchetypeCall),
                Loc.T(L.YellowPages.ArchetypeCallHint), scale);
            if (composeArchetype is AdArchetypes.Service or AdArchetypes.Call)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
                ui.SectionHeading(Loc.T(L.YellowPages.DirectionSection));
                ui.HelpText(Loc.T(composeArchetype == AdArchetypes.Service
                    ? L.YellowPages.DirectionHintService
                    : L.YellowPages.DirectionHintCall));
                DrawDirectionTiles(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            ui.HelpText(Loc.T(L.YellowPages.PostRules));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawKindCard(int archetype, FontAwesomeIcon icon, string title, string hint, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FieldCardPad * scale;
        var tileSide = KindTileSide * scale;
        var textLeft = origin.X + pad + tileSide + 12f * scale;
        var hintWidth = origin.X + width - pad - textLeft;
        var hintTop = origin.Y + pad + Typography.LineHeight(KindTitleStyle) + 3f * scale;
        var hintHeight = Typography.MeasureWrappedBlock(hint, KindHintStyle, hintWidth).Y;
        var height = MathF.Max(KindCardHeight * scale, hintTop - origin.Y + hintHeight + pad);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 18f * scale;
        var selected = composeArchetype == archetype;
        var hovered = UiInteract.Hover(min, max);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(selected ? Ink.AccentWash : hovered ? Ink.ChipHover : Ink.ChipFill));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(selected ? Palette.WithAlpha(Ink.AccentLink, 0.75f) : Ink.ChipStroke), selected ? 1.4f : 1f);
        var tileMin = new Vector2(origin.X + pad, origin.Y + pad);
        YellowPagesKit.Tile(drawList, tileMin, tileMin + new Vector2(tileSide, tileSide), Ink.Accent, icon, 12f * scale, 1f);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + pad), title, Ink.TitleInk, KindTitleStyle);
        Typography.DrawWrappedLeft(new Vector2(textLeft, hintTop), hint, Ink.MutedInk, KindHintStyle, hintWidth);
        if (selected)
        {
            PhoneIcon.Draw(drawList, new Vector2(max.X - pad - 8f * scale, origin.Y + pad + 8f * scale),
                PhoneIcons.CircleCheckFilled, Ink.AccentLink, 18f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            composeArchetype = archetype;
            if (archetype == AdArchetypes.Place)
            {
                composeWanted = false;
            }

            EnsureCategoryForArchetype();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawDirectionTiles(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = Metrics.Space.Sm * scale;
        var tileWidth = (width - gap) * 0.5f;
        var height = DirectionTileHeight * scale;
        if (DrawDirectionTile(drawList, new Rect(origin, new Vector2(origin.X + tileWidth, origin.Y + height)),
                Loc.T(L.YellowPages.OfferingTile), Loc.T(composeArchetype == AdArchetypes.Service
                    ? L.YellowPages.OfferingServiceHint
                    : L.YellowPages.OfferingCallHint), !composeWanted, scale))
        {
            composeWanted = false;
            EnsureCategoryForArchetype();
        }

        var wantedMin = new Vector2(origin.X + tileWidth + gap, origin.Y);
        if (DrawDirectionTile(drawList, new Rect(wantedMin, new Vector2(wantedMin.X + tileWidth, origin.Y + height)),
                Loc.T(L.YellowPages.WantedTile), Loc.T(composeArchetype == AdArchetypes.Service
                    ? L.YellowPages.WantedServiceHint
                    : L.YellowPages.WantedCallHint), composeWanted, scale))
        {
            composeWanted = true;
            EnsureCategoryForArchetype();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private bool DrawDirectionTile(ImDrawListPtr drawList, Rect rect, string title, string hint, bool active,
        float scale)
    {
        var rounding = 16f * scale;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (active)
        {
            AccentPill.Paint(drawList, rect.Min, rect.Max, rounding, hovered, Ink.Accent, Ink.AccentDeep,
                Ink.AccentShadow);
        }
        else
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(hovered ? Ink.ChipHover : Ink.ChipFill));
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(Ink.ChipStroke), 1f);
        }

        var pad = 12f * scale;
        var titleInk = active ? Ink.White : Ink.TitleInk;
        var hintInk = active ? Palette.WithAlpha(Ink.White, 0.78f) : Ink.MutedInk;
        var textWidth = rect.Width - pad * 2f;
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, rect.Min.Y + pad),
            Typography.FitText(title, textWidth, TextStyles.SubheadlineEmphasized), titleInk, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, rect.Min.Y + pad + Typography.LineHeight(TextStyles.SubheadlineEmphasized) + 3f * scale),
            Typography.FitText(hint, textWidth, TextStyles.Caption1), hintInk, TextStyles.Caption1);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void EnsureCategoryForArchetype()
    {
        if (composeArchetype < 0)
        {
            return;
        }

        var categories = AdCategories.ForArchetype(composeArchetype);
        if (Array.IndexOf(categories, composeCategory) >= 0 && (!composeWanted || AdCategories.SupportsWanted(composeCategory)))
        {
            return;
        }

        for (var index = 0; index < categories.Length; index++)
        {
            if (!composeWanted || AdCategories.SupportsWanted(categories[index]))
            {
                composeCategory = categories[index];
                return;
            }
        }
    }

    private void DrawComposePhotos(Rect body, float scale)
    {
        if (composeSession.Stage == PhotoComposeStage.Edit)
        {
            composeSession.DrawEditCanvas(body, scale, ComposeAspectRatio, ComposeStyle, false, !composeBusy);
            composeSession.DrawComposerFooter(body, scale, ComposeEditStyle, !composeBusy);
            return;
        }

        var top = body.Min.Y;
        if (composeKeptUrls.Count > 0)
        {
            top = DrawKeptPhotoStrip(body, top, scale);
        }

        var paneHeight = MathF.Min(body.Width, (body.Max.Y - top) * ComposePaneFraction);
        var pane = new Rect(new Vector2(body.Min.X, top), new Vector2(body.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, ComposeStyle, ComposeAspectRatio, false, !composeBusy);
        var gridTop = pane.Max.Y + ComposeGridGap * scale;
        if (composeSession.ShowsAspectRail)
        {
            gridTop = composeSession.DrawAspectRail(body, pane.Max.Y, scale, ui, !composeBusy);
        }

        var gridRect = new Rect(new Vector2(body.Min.X, gridTop), body.Max);
        using (AppSurface.BeginEdgeToEdge(gridRect))
        {
            composeSession.DrawPickGrid(gridRect, scale, ComposeStyle, true, Loc.T(L.Common.ImportFromPc),
                Loc.T(L.Feedback.AddPhotos));
        }
    }

    private float DrawKeptPhotoStrip(Rect body, float top, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var height = KeptStripHeight * scale;
        var side = height - 12f * scale;
        var gap = Metrics.Space.Sm * scale;
        var cursorX = body.Min.X + CellPadX * scale;
        var rounding = 10f * scale;
        var removeIndex = -1;
        for (var index = 0; index < composeKeptUrls.Count; index++)
        {
            var min = new Vector2(cursorX, top + 6f * scale);
            var max = min + new Vector2(side, side);
            var texture = images.Get(composeKeptUrls[index]);
            if (texture is null)
            {
                Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            }
            else
            {
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                Squircle.FillImage(drawList, min, max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
            }

            var badgeRadius = 8.5f * scale;
            var badgeCenter = new Vector2(max.X - badgeRadius - 2f * scale, min.Y + badgeRadius + 2f * scale);
            var badgeExtent = new Vector2(badgeRadius, badgeRadius);
            var badgeHovered = UiInteract.Hover(badgeCenter - badgeExtent, badgeCenter + badgeExtent);
            drawList.AddCircleFilled(badgeCenter, badgeRadius,
                ImGui.GetColorU32(badgeHovered ? YellowPagesKit.OverlayHover : YellowPagesKit.OverlayFill), 20);
            PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.X, YellowPagesKit.White, 10f * scale);
            if (badgeHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(badgeCenter - badgeExtent, badgeCenter + badgeExtent, badgeHovered))
            {
                removeIndex = index;
            }

            cursorX += side + gap;
        }

        if (removeIndex >= 0)
        {
            composeKeptUrls.RemoveAt(removeIndex);
        }

        return top + height;
    }

    private void DrawComposeDetails(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            ui.SectionHeading(Loc.T(L.YellowPages.CategorySection));
            DrawComposeCategoryRail(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawTitleAndBodyCard(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawTagsCard(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            switch (composeArchetype)
            {
                case AdArchetypes.Place:
                    DrawComposePlace(scale);
                    break;
                case AdArchetypes.Service:
                    DrawComposeService(scale);
                    break;
                default:
                    DrawComposeCall(scale);
                    break;
            }

            DrawComposeValidation(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawComposeCategoryRail(float scale)
    {
        var categories = AdCategories.ForArchetype(composeArchetype < 0 ? AdArchetypes.Place : composeArchetype);
        var count = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            if (composeWanted && !AdCategories.SupportsWanted(categories[index]))
            {
                continue;
            }

            kindChipLabels[count] = Loc.T(AdCategories.Label(categories[index]));
            kindChipActive[count] = categories[index] == composeCategory;
            count++;
        }

        var tapped = composeCategoryRail.Draw(ui, kindChipLabels.AsSpan(0, count), kindChipActive.AsSpan(0, count));
        if (tapped < 0)
        {
            return;
        }

        var seen = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            if (composeWanted && !AdCategories.SupportsWanted(categories[index]))
            {
                continue;
            }

            if (seen == tapped)
            {
                composeCategory = categories[index];
                return;
            }

            seen++;
        }
    }

    private void DrawTitleAndBodyCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FieldCardPad * scale;
        var rowHeight = FieldRowHeight * scale;
        var bodyLabelHeight = Typography.LineHeight(FieldLabelStyle);
        var bodyBlock = pad + bodyLabelHeight + 6f * scale + BodyFieldHeight * scale + pad;
        var height = rowHeight + bodyBlock;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, card.Min, card.Max, FieldCardRounding * scale, true);
        var labelWidth = FieldLabelWidth * scale;
        DrawFieldLabel(drawList, card.Min.X + pad, card.Min.Y, rowHeight, labelWidth, Loc.T(L.YellowPages.TitleLabel));
        if (composeTitleFocus)
        {
            ImGui.SetKeyboardFocusHere();
            composeTitleFocus = false;
        }

        DrawFieldInput("##adTitle", card.Min.X + pad + labelWidth, card.Max.X - pad, card.Min.Y, rowHeight,
            ref composeTitle, TitleMaxLength, Loc.T(L.YellowPages.TitleHint));
        DrawHairline(drawList, card.Min.X + pad, card.Max.X, card.Min.Y + rowHeight);
        var bodyLabelTop = card.Min.Y + rowHeight + pad;
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, bodyLabelTop), Loc.T(L.YellowPages.BodyLabel),
            Ink.MutedInk, FieldLabelStyle);
        SyncComposeCounter(composeBody.Length);
        var counterSize = Typography.Measure(composeCounter, CounterStyle);
        var counterInk = composeBody.Length >= BodyMaxLength - ComposeCounterWarning ? Ink.Danger : Ink.FaintInk;
        Typography.Draw(drawList, new Vector2(card.Max.X - pad - counterSize.X, bodyLabelTop + (bodyLabelHeight - counterSize.Y) * 0.5f),
            composeCounter, counterInk, CounterStyle);
        var fieldTop = bodyLabelTop + bodyLabelHeight + 6f * scale;
        var fieldWidth = width - pad * 2f;
        ImGui.SetCursorScreenPos(new Vector2(card.Min.X + pad, fieldTop));
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, Ink.TitleInk))
        {
            var wrapWidth = fieldWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
            SoftWrapField.Multiline("##adBody", ref composeBody, BodyBufferLength,
                new Vector2(fieldWidth, BodyFieldHeight * scale), wrapWidth);
        }

        if (composeBody.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(card.Min.X + pad, fieldTop) + ImGui.GetStyle().FramePadding,
                Typography.FitText(Loc.T(L.YellowPages.BodyHint), fieldWidth, TextStyles.Body), Ink.MutedInk,
                TextStyles.Body);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void SyncComposeCounter(int length)
    {
        if (composeCounterLength == length)
        {
            return;
        }

        composeCounterLength = length;
        composeCounter = string.Concat(length.ToString(Loc.Culture), "/", BodyMaxLength.ToString(Loc.Culture));
    }

    private static void DrawFieldLabel(ImDrawListPtr drawList, float left, float rowTop, float rowHeight, float maxWidth,
        string label)
    {
        var fitted = Typography.FitText(label, MathF.Max(1f, maxWidth - 8f * UiScale.Current), FieldLabelStyle);
        var size = Typography.Measure(fitted, FieldLabelStyle);
        Typography.Draw(drawList, new Vector2(left, rowTop + (rowHeight - size.Y) * 0.5f), fitted, Ink.MutedInk,
            FieldLabelStyle);
    }

    private bool DrawFieldInput(string id, float left, float right, float rowTop, float rowHeight, ref string value,
        int maxLength, string hint, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        ImGui.SetCursorScreenPos(new Vector2(left, rowTop + rowHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, right - left));
        Plugin.Fonts.NoticeText(value);
        bool changed;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, Ink.TitleInk))
        {
            changed = ImGui.InputText(id, ref value, maxLength, flags);
        }

        if (value.Length == 0 && hint.Length > 0)
        {
            var hintPosition = new Vector2(left + ImGui.GetStyle().FramePadding.X,
                rowTop + rowHeight * 0.5f - Typography.LineHeight(TextStyles.Body) * 0.5f);
            Typography.Draw(ImGui.GetWindowDrawList(), hintPosition,
                Typography.FitText(hint, right - left - ImGui.GetStyle().FramePadding.X * 2f, TextStyles.Body),
                Ink.FaintInk, TextStyles.Body);
        }

        return changed;
    }

    private void DrawFieldCard(string label, string id, ref string value, int maxLength, string hint, float scale,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FieldCardPad * scale;
        var rowHeight = FieldRowHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + rowHeight));
        ui.Card(drawList, card.Min, card.Max, FieldCardRounding * scale, true);
        var labelWidth = FieldLabelWidth * scale;
        DrawFieldLabel(drawList, card.Min.X + pad, card.Min.Y, rowHeight, labelWidth, label);
        DrawFieldInput(id, card.Min.X + pad + labelWidth, card.Max.X - pad, card.Min.Y, rowHeight, ref value, maxLength,
            hint, flags);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Sm * scale));
    }

    private void DrawMultilineCard(string label, string id, ref string value, int maxLength, float fieldHeight,
        string hint, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FieldCardPad * scale;
        var labelHeight = Typography.LineHeight(FieldLabelStyle);
        var height = pad + labelHeight + 6f * scale + fieldHeight * scale + pad;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, card.Min, card.Max, FieldCardRounding * scale, true);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), label, Ink.MutedInk, FieldLabelStyle);
        var fieldTop = card.Min.Y + pad + labelHeight + 6f * scale;
        var fieldWidth = width - pad * 2f;
        ImGui.SetCursorScreenPos(new Vector2(card.Min.X + pad, fieldTop));
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, Ink.TitleInk))
        {
            var wrapWidth = fieldWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
            SoftWrapField.Multiline(id, ref value, maxLength, new Vector2(fieldWidth, fieldHeight * scale), wrapWidth);
        }

        if (value.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(card.Min.X + pad, fieldTop) + ImGui.GetStyle().FramePadding,
                Typography.FitText(hint, fieldWidth, TextStyles.Body), Ink.FaintInk, TextStyles.Body);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawTagsCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FieldCardPad * scale;
        var rowHeight = FieldRowHeight * scale;
        var chipHeight = TagChipHeight * scale;
        var chipGap = Metrics.Space.Xs * scale;
        var innerWidth = width - pad * 2f;
        var chipRows = TagChipRows(innerWidth, scale);
        var chipsHeight = chipRows > 0 ? chipRows * (chipHeight + chipGap) + 4f * scale : 0f;
        var height = rowHeight + chipsHeight;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, card.Min, card.Max, FieldCardRounding * scale, true);
        var labelWidth = FieldLabelWidth * scale;
        DrawFieldLabel(drawList, card.Min.X + pad, card.Min.Y, rowHeight, labelWidth, Loc.T(L.YellowPages.TagsLabel));
        var full = composeTags.Count >= MaxTags;
        if (!full)
        {
            var submitted = DrawFieldInput("##adTagDraft", card.Min.X + pad + labelWidth, card.Max.X - pad, card.Min.Y,
                rowHeight, ref composeTagDraft, TagMaxLength, Loc.T(L.YellowPages.TagsHint),
                ImGuiInputTextFlags.EnterReturnsTrue);
            if (submitted || composeTagDraft.Contains(','))
            {
                CommitTagDraft();
            }
        }
        else
        {
            var fullLabel = Loc.T(L.YellowPages.TagsFull, MaxTags);
            Typography.Draw(drawList, new Vector2(card.Min.X + pad + labelWidth + ImGui.GetStyle().FramePadding.X,
                    card.Min.Y + rowHeight * 0.5f - Typography.LineHeight(FieldHintStyle) * 0.5f),
                fullLabel, Ink.FaintInk, FieldHintStyle);
        }

        if (composeTags.Count > 0)
        {
            DrawHairline(drawList, card.Min.X + pad, card.Max.X, card.Min.Y + rowHeight);
            var cursorX = card.Min.X + pad;
            var lineTop = card.Min.Y + rowHeight + 6f * scale;
            var removeIndex = -1;
            for (var index = 0; index < composeTags.Count; index++)
            {
                var label = composeTags[index];
                var chipWidth = TagChipWidth(label, scale);
                if (cursorX + chipWidth > card.Max.X - pad && cursorX > card.Min.X + pad)
                {
                    cursorX = card.Min.X + pad;
                    lineTop += chipHeight + chipGap;
                }

                if (DrawTagChip(drawList, new Vector2(cursorX, lineTop), label, chipWidth, chipHeight, scale))
                {
                    removeIndex = index;
                }

                cursorX += chipWidth + chipGap;
            }

            if (removeIndex >= 0)
            {
                composeTags.RemoveAt(removeIndex);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private int TagChipRows(float innerWidth, float scale)
    {
        if (composeTags.Count == 0)
        {
            return 0;
        }

        var rows = 1;
        var cursorX = 0f;
        var gap = Metrics.Space.Xs * scale;
        for (var index = 0; index < composeTags.Count; index++)
        {
            var chipWidth = TagChipWidth(composeTags[index], scale);
            if (cursorX + chipWidth > innerWidth && cursorX > 0f)
            {
                cursorX = 0f;
                rows++;
            }

            cursorX += chipWidth + gap;
        }

        return rows;
    }

    private static float TagChipWidth(string label, float scale) =>
        Typography.Measure(label, TextStyles.Footnote).X + 40f * scale;

    private static bool DrawTagChip(ImDrawListPtr drawList, Vector2 min, string label, float width, float height,
        float scale)
    {
        var max = min + new Vector2(width, height);
        var hovered = UiInteract.Hover(min, max);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(hovered ? Ink.ChipHover : Ink.AccentWash));
        var size = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(min.X + 12f * scale, min.Y + (height - size.Y) * 0.5f), label,
            Ink.AccentLink, TextStyles.Footnote);
        PhoneIcon.Draw(drawList, new Vector2(max.X - 14f * scale, min.Y + height * 0.5f), PhoneIcons.X, Ink.MutedInk,
            11f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void CommitTagDraft()
    {
        var parts = composeTagDraft.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < parts.Length && composeTags.Count < MaxTags; index++)
        {
            var tag = parts[index].TrimStart('#').ToLowerInvariant();
            if (tag.Length == 0 || composeTags.Contains(tag))
            {
                continue;
            }

            composeTags.Add(tag.Length > TagMaxLength ? tag[..TagMaxLength] : tag);
        }

        composeTagDraft = string.Empty;
    }

    private void DrawComposePlace(float scale)
    {
        ui.SectionHeading(Loc.T(L.YellowPages.WhereSection));
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = FieldRowHeight * scale;
        if (composeLocation is { } location)
        {
            var pad = FieldCardPad * scale;
            var summary = LocationShare.Summary(in location);
            var clearLabel = Loc.T(L.YellowPages.ClearLocation);
            var clearWidth = Typography.Measure(clearLabel, TextStyles.FootnoteEmphasized).X + 26f * scale;
            var summaryHeight = Typography.MeasureWrappedBlock(summary, TextStyles.Subheadline,
                width - pad * 2f - clearWidth - 10f * scale).Y;
            var height = MathF.Max(rowHeight, summaryHeight + pad * 2f);
            var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            ui.Card(drawList, card.Min, card.Max, FieldCardRounding * scale, true);
            Typography.DrawWrappedLeft(new Vector2(card.Min.X + pad, card.Min.Y + pad), summary, Ink.BodyInk,
                TextStyles.Subheadline, width - pad * 2f - clearWidth - 10f * scale);
            var clearRect = new Rect(new Vector2(card.Max.X - pad - clearWidth, card.Center.Y - 14f * scale),
                new Vector2(card.Max.X - pad, card.Center.Y + 14f * scale));
            if (SocialPill.Flat(drawList, clearRect, clearLabel, Ink.ChipFill, Ink.ChipHover, Ink.ChipStroke,
                    Ink.TitleInk, TextStyles.FootnoteEmphasized, clearRect.Height * 0.5f))
            {
                composeLocation = null;
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        }
        else
        {
            var captureRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + rowHeight));
            if (SocialPill.Flat(drawList, captureRect, Loc.T(L.YellowPages.UseMyLocation), Ink.ButtonFill, Ink.ButtonHover,
                    Ink.ChipStroke, Ink.TitleInk, TextStyles.SubheadlineEmphasized, FieldCardRounding * scale))
            {
                composeLocation = LocationShare.Capture();
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Sm * scale));
        }

        DrawFieldCard(Loc.T(L.YellowPages.AddressNoteLabel), "##adAddressNote", ref composeAddressNote, NoteMaxLength,
            Loc.T(L.YellowPages.AddressNoteHint), scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        ui.SectionHeading(Loc.T(L.YellowPages.ScheduleSection));
        ui.HelpText(Loc.T(L.YellowPages.ScheduleHint));
        DrawDayRow(scale);
        composeOpenMinute = DrawTimeField(Loc.T(L.YellowPages.OpensLabel), composeOpenMinute, scale);
        composeCloseMinute = DrawTimeField(Loc.T(L.YellowPages.ClosesLabel), composeCloseMinute, scale);
        DrawOpenForRow(scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawDayRow(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = 4f * scale;
        var cellWidth = (width - gap * (WeekDays.Length - 1)) / WeekDays.Length;
        var height = DayCellHeight * scale;
        var dayNames = Loc.Culture.DateTimeFormat.AbbreviatedDayNames;
        for (var index = 0; index < WeekDays.Length; index++)
        {
            var day = WeekDays[index];
            var min = new Vector2(origin.X + index * (cellWidth + gap), origin.Y);
            var max = new Vector2(min.X + cellWidth, origin.Y + height);
            var active = composeDays[day];
            var hovered = UiInteract.Hover(min, max);
            var rounding = 11f * scale;
            if (active)
            {
                AccentPill.Paint(drawList, min, max, rounding, hovered, Ink.Accent, Ink.AccentDeep, Ink.AccentShadow);
            }
            else
            {
                Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(hovered ? Ink.ChipHover : Ink.ChipFill));
                Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Ink.ChipStroke), 1f);
            }

            var label = Typography.FitText(dayNames[day], cellWidth - 6f * scale, TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), label,
                active ? Ink.White : Ink.TitleInk, TextStyles.FootnoteEmphasized);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                composeDays[day] = !composeDays[day];
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private int DrawTimeField(string label, int minuteOfDay, float scale)
    {
        ui.SectionLabel(label);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = TimeFieldHeight * scale;
        var edited = TimeOfDayField.Draw(ui, new Rect(origin, new Vector2(origin.X + width, origin.Y + height)),
            minuteOfDay, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        return edited;
    }

    private void DrawOpenForRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var label = Loc.T(L.YellowPages.DurationLabel);
        var value = DurationText(ComposeOpenMinutes());
        var valueSize = Typography.Measure(value, TextStyles.FootnoteEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(), origin, label, Ink.MutedInk, TextStyles.Footnote);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + width - valueSize.X, origin.Y), value,
            Ink.TitleInk, TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, valueSize.Y + Metrics.Space.Xs * scale));
    }

    private int ComposeOpenMinutes()
    {
        var span = composeCloseMinute - composeOpenMinute;
        return span > 0 ? span : span + MinutesPerDay;
    }

    private static string DurationText(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;
        if (hours == 0)
        {
            return Loc.T(L.YellowPages.DurationMinutes, rest);
        }

        return rest == 0
            ? Loc.T(L.YellowPages.DurationHours, hours)
            : Loc.T(L.YellowPages.DurationHoursMinutes, hours, rest);
    }

    private bool HasComposeDays()
    {
        for (var index = 0; index < composeDays.Length; index++)
        {
            if (composeDays[index])
            {
                return true;
            }
        }

        return false;
    }

    private void DrawComposeService(float scale)
    {
        if (AdCategories.IsLinkOnly(composeCategory))
        {
            ui.SectionHeading(Loc.T(L.YellowPages.ModLinkLabel));
            DrawFieldCard(Loc.T(L.YellowPages.LinkLabel), "##adModLink", ref composeLink, LinkMaxLength, "https://", scale);
            ui.HelpText(Loc.T(L.YellowPages.ModLinkHint));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            return;
        }

        ui.SectionHeading(Loc.T(composeWanted ? L.YellowPages.BudgetSection : L.YellowPages.PriceSection));
        priceModeLabels[0] = Loc.T(composeWanted ? L.YellowPages.BudgetOpen : L.YellowPages.PriceAsk);
        priceModeLabels[1] = Loc.T(composeWanted ? L.YellowPages.BudgetFixedLabel : L.YellowPages.PriceFixed);
        priceModeLabels[2] = Loc.T(composeWanted ? L.YellowPages.BudgetUpToLabel : L.YellowPages.PriceFromLabel);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + DirectionRowHeight * scale));
        var picked = SegmentStrip.Draw("yellowpages.compose.price", row, priceModeLabels, composePriceMode,
            AppPalettes.YellowPages, 34f);
        if (picked >= 0)
        {
            composePriceMode = picked;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, DirectionRowHeight * scale + Metrics.Space.Xs * scale));
        if (composePriceMode != AdPriceModes.Ask)
        {
            DrawFieldCard(Loc.T(L.YellowPages.PriceGilLabel), "##adPriceGil", ref composePriceText, 15, "0", scale,
                ImGuiInputTextFlags.CharsDecimal);
        }

        DrawFieldCard(Loc.T(L.YellowPages.TurnaroundLabel), "##adTurnaround", ref composeTurnaround, NoteMaxLength,
            Loc.T(L.YellowPages.TurnaroundHint), scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawComposeCall(float scale)
    {
        ui.SectionHeading(Loc.T(composeWanted ? L.YellowPages.WantedCallSection : L.YellowPages.CallSection));
        DrawFieldCard(Loc.T(composeWanted ? L.YellowPages.YourRoleLabel : L.YellowPages.SlotsLabel), "##adSlots",
            ref composeSlotsLine, NoteMaxLength,
            Loc.T(composeWanted ? L.YellowPages.YourRoleHint : L.YellowPages.SlotsHint), scale);
        DrawMultilineCard(Loc.T(composeWanted ? L.YellowPages.LookingForLabel : L.YellowPages.RequirementsLabel),
            "##adRequirements", ref composeRequirements, RequirementsMaxLength, 76f,
            Loc.T(composeWanted ? L.YellowPages.LookingForHint : L.YellowPages.RequirementsHint), scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawComposeValidation(float scale)
    {
        if (ComposeDetailsValid(out var hint) || hint.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Typography.DrawWrappedLeft(origin, hint, Ink.MutedInk, FieldHintStyle, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private bool ComposeDetailsValid(out string hint)
    {
        hint = string.Empty;
        if (composeArchetype < 0)
        {
            return false;
        }

        if (TrimmedLength(composeTitle) == 0)
        {
            hint = Loc.T(L.YellowPages.NeedTitle);
            return false;
        }

        if (TrimmedLength(composeBody) == 0)
        {
            hint = Loc.T(L.YellowPages.NeedBody);
            return false;
        }

        if (composeBody.Length > BodyMaxLength)
        {
            hint = Loc.T(L.YellowPages.NeedShorterBody, BodyMaxLength);
            return false;
        }

        if (AdCategories.IsLinkOnly(composeCategory) && TrimmedLength(composeLink) == 0)
        {
            hint = Loc.T(L.YellowPages.NeedModLink);
            return false;
        }

        if (composeArchetype == AdArchetypes.Place && HasComposeDays() && ComposeOpenMinutes() < MinOpenMinutes)
        {
            hint = Loc.T(L.YellowPages.NeedOpenWindow, MinOpenMinutes);
            return false;
        }

        if (ResolveComposeDataCenter(out _) == 0)
        {
            hint = Loc.T(L.YellowPages.NeedDataCenter);
            return false;
        }

        return true;
    }

    private void DrawComposeOptions(Rect body, float scale)
    {
        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawSectionLabel(Loc.T(L.YellowPages.PreviewSection));
            var preview = ComposePreview();
            AdCard.Draw(preview, new AdCardContext(images, NowUnix(), false, wallpaperImages));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            DrawSectionLabel(Loc.T(L.YellowPages.CoverAccent));
            DrawAccentRow(scale);
            DrawSectionLabel(Loc.T(L.YellowPages.OptionsSection));
            DrawComposeToggles(scale);
            DrawComposeOutcome(scale);
            DrawPublishButton(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawAccentRow(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var count = AdAccents.Count;
        var radius = AccentDotRadius * scale;
        var usable = width - pad * 2f;
        var pitch = usable / count;
        var centerY = origin.Y + AccentRowHeight * scale * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var center = new Vector2(origin.X + pad + pitch * (index + 0.5f), centerY);
            var accent = AdAccents.For(index);
            var extent = new Vector2(radius, radius);
            var hovered = UiInteract.Hover(center - extent, center + extent);
            var selected = composeAccent == index;
            var dotRadius = MathF.Min(radius - 3f * scale, pitch * 0.5f - 3f * scale);
            drawList.AddCircleFilled(center, dotRadius, ImGui.GetColorU32(accent), 32);
            if (selected)
            {
                drawList.AddCircle(center, dotRadius + 3f * scale, ImGui.GetColorU32(Ink.White), 32, 2f * scale);
                PhoneIcon.Draw(drawList, center, PhoneIcons.Check, Ink.White, dotRadius * 1.1f);
            }
            else if (hovered)
            {
                drawList.AddCircle(center, dotRadius + 2f * scale, ImGui.GetColorU32(Palette.WithAlpha(Ink.White, 0.6f)),
                    32, 1.2f * scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(center - extent, center + extent, hovered))
            {
                composeAccent = index;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, AccentRowHeight * scale));
        var hintOrigin = ImGui.GetCursorScreenPos();
        var hintHeight = Typography.DrawWrappedLeft(new Vector2(hintOrigin.X + pad, hintOrigin.Y),
            Loc.T(L.YellowPages.CoverAccentHint), Ink.MutedInk, FieldHintStyle, width - pad * 2f);
        ImGui.SetCursorScreenPos(hintOrigin);
        ImGui.Dummy(new Vector2(width, hintHeight + Metrics.Space.Md * scale));
    }

    private void DrawComposeToggles(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var rowHeight = ToggleRowHeight * scale;
        var card = new Rect(new Vector2(origin.X + pad, origin.Y), new Vector2(origin.X + width - pad, origin.Y + rowHeight * 2f));
        ui.Card(drawList, card.Min, card.Max, FieldCardRounding * scale);
        var firstRow = new Rect(card.Min, new Vector2(card.Max.X, card.Min.Y + rowHeight));
        composeAllowInquiries = DrawToggleRow(drawList, firstRow, "yellowpages.compose.inquiries", PhoneIcons.MessageCircle,
            Loc.T(L.YellowPages.AllowInquiriesToggle), composeAllowInquiries, scale);
        DrawHairline(drawList, card.Min.X + pad + 30f * scale, card.Max.X, firstRow.Max.Y);
        var secondRow = new Rect(new Vector2(card.Min.X, firstRow.Max.Y), card.Max);
        composeAfterDark = DrawToggleRow(drawList, secondRow, "yellowpages.compose.afterdark", PhoneIcons.Moon,
            Loc.T(L.YellowPages.AfterDarkToggle), composeAfterDark, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight * 2f + Metrics.Space.Xs * scale));
        var hintOrigin = ImGui.GetCursorScreenPos();
        var hint = composeAfterDark ? Loc.T(L.YellowPages.AfterDarkHint) : Loc.T(L.YellowPages.AllowInquiriesHint);
        var hintHeight = Typography.DrawWrappedLeft(new Vector2(hintOrigin.X + pad, hintOrigin.Y), hint, Ink.MutedInk,
            FieldHintStyle, width - pad * 2f);
        ImGui.SetCursorScreenPos(hintOrigin);
        ImGui.Dummy(new Vector2(width, hintHeight + Metrics.Space.Md * scale));
    }

    private bool DrawToggleRow(ImDrawListPtr drawList, Rect row, string id, string glyph, string label, bool value,
        float scale)
    {
        var pad = CellPadX * scale;
        var glyphCenter = new Vector2(row.Min.X + pad + 10f * scale, row.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, glyph, Ink.TitleInk, 20f * scale);
        var toggleMax = new Vector2(row.Max.X - pad, row.Center.Y + Metrics.Size.ToggleHeight * 0.5f * scale);
        var toggleMin = new Vector2(toggleMax.X - Metrics.Size.ToggleWidth * scale,
            row.Center.Y - Metrics.Size.ToggleHeight * 0.5f * scale);
        var labelLeft = glyphCenter.X + 22f * scale;
        var fitted = Typography.FitText(label, toggleMin.X - 12f * scale - labelLeft, TextStyles.Body);
        var size = Typography.Measure(fitted, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - size.Y * 0.5f), fitted, Ink.TitleInk,
            TextStyles.Body);
        return Toggle.Draw(id, new Rect(toggleMin, toggleMax), value, theme);
    }

    private void DrawComposeOutcome(float scale)
    {
        if (composeOutcome is not { } outcome)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y), OutcomeText(outcome), Ink.Danger,
            TextStyles.FootnoteEmphasized, width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawPublishButton(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var rect = new Rect(new Vector2(origin.X + pad, origin.Y), new Vector2(origin.X + width - pad, origin.Y + PublishHeight * scale));
        var label = Loc.T(editingAdId is null ? L.YellowPages.PublishAd : L.YellowPages.SaveChanges);
        if (composeBusy)
        {
            LoadingPulse.Spinner(rect.Center, 10f * scale, Ink.Accent);
        }
        else if (SocialPill.Accent(drawList, rect, label, Ink, ComposeActionStyle, rect.Height * 0.5f,
                     ComposeDetailsValid(out _)))
        {
            SubmitCompose();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, PublishHeight * scale));
    }

    private static string OutcomeText(AdCreateOutcome outcome) =>
        outcome switch
        {
            AdCreateOutcome.TooMany => Loc.T(L.YellowPages.ErrorTooMany),
            AdCreateOutcome.Invalid => Loc.T(L.YellowPages.ErrorInvalid),
            AdCreateOutcome.RateLimited => Loc.T(L.YellowPages.ErrorRateLimited),
            _ => Loc.T(L.YellowPages.ErrorFailed),
        };

    private int ResolveComposeDataCenter(out uint worldId)
    {
        worldId = composeLocation?.WorldId ?? gameData.LocalCurrentWorldId;
        return MusterWorlds.DataCenterIdForWorld(worldId);
    }

    private AdScheduleSlot[]? BuildSchedule()
    {
        if (composeArchetype != AdArchetypes.Place)
        {
            return null;
        }

        var duration = ComposeOpenMinutes();
        var slots = new List<AdScheduleSlot>(7);
        for (var day = 0; day < composeDays.Length; day++)
        {
            if (composeDays[day])
            {
                slots.Add(AdText.ToUtcSlot(day, composeOpenMinute, duration));
            }
        }

        return slots.Count > 0 ? slots.ToArray() : null;
    }

    private long ParsePriceGil()
    {
        if (composePriceMode == AdPriceModes.Ask)
        {
            return 0L;
        }

        var digits = 0L;
        var seen = false;
        for (var index = 0; index < composePriceText.Length && digits < 100_000_000_000L; index++)
        {
            var character = composePriceText[index];
            if (character is >= '0' and <= '9')
            {
                digits = digits * 10L + (character - '0');
                seen = true;
            }
        }

        return seen ? digits : 0L;
    }

    private CreateAdRequest BuildRequest(out int dataCenterId)
    {
        var location = composeLocation;
        dataCenterId = ResolveComposeDataCenter(out var worldId);
        if (composeTagDraft.Length > 0)
        {
            CommitTagDraft();
        }

        return new CreateAdRequest(
            composeCategory,
            composeTitle.Trim(),
            composeBody.Trim(),
            composeTags.Count > 0 ? composeTags.ToArray() : null,
            MusterCategories.RegionBitForWorld(worldId),
            dataCenterId,
            (int)worldId,
            (int)(location?.TerritoryId ?? 0u),
            (int)(location?.MapId ?? 0u),
            location?.MapX ?? 0f,
            location?.MapY ?? 0f,
            location?.Ward ?? 0,
            location?.Plot ?? 0,
            composeAddressNote.Trim(),
            BuildSchedule(),
            composePriceMode,
            ParsePriceGil(),
            composeTurnaround.Trim(),
            composeSlotsLine.Trim(),
            composeRequirements.Trim(),
            composeAfterDark,
            null,
            composeLink.Trim(),
            composeAllowInquiries,
            composeWanted && composeArchetype != AdArchetypes.Place && AdCategories.SupportsWanted(composeCategory),
            composeAccent);
    }

    private AdDto ComposePreview()
    {
        var firstPhoto = composeKeptUrls.Count > 0
            ? composeKeptUrls[0]
            : composeSession.HasSelection ? composeSession.FirstSelected : null;
        var hash = HashCode.Combine(
            HashCode.Combine(composeTitle, composeBody, composeCategory, composeWanted, composeAccent, composePriceMode,
                composePriceText, composeTags.Count),
            HashCode.Combine(composeSlotsLine, composeAfterDark, composeAllowInquiries, composeKeptUrls.Count,
                composeSession.SelectedCount, firstPhoto, composeTurnaround, composeLink),
            HashCode.Combine(composeAddressNote, composeLocation.HasValue, composeRequirements));
        if (composePreview is not null && composePreviewHash == hash)
        {
            return composePreview;
        }

        var request = BuildRequest(out _);
        var me = inquiries.MyUserId;
        var photoCount = composeKeptUrls.Count + composeSession.SelectedCount;
        var mediaUrls = new string[photoCount];
        for (var index = 0; index < composeKeptUrls.Count; index++)
        {
            mediaUrls[index] = composeKeptUrls[index];
        }

        for (var index = composeKeptUrls.Count; index < photoCount; index++)
        {
            mediaUrls[index] = firstPhoto ?? string.Empty;
        }

        var nowUnix = NowUnix();
        composePreviewHash = hash;
        composePreview = new AdDto(editingAdId ?? "preview", me, store.MyDisplayName, store.MyHandle, string.Empty,
            composeArchetype, request.Category, request.Title.Length > 0 ? request.Title : Loc.T(L.YellowPages.TitleLabel),
            request.Body, request.Tags ?? Array.Empty<string>(), request.Region, request.DataCenterId, request.WorldId,
            request.TerritoryId, request.MapId, request.MapX, request.MapY, request.Ward, request.Plot,
            request.AddressNote ?? string.Empty, request.Schedule ?? Array.Empty<AdScheduleSlot>(), 0L,
            request.PriceMode, request.PriceGil, request.Turnaround ?? string.Empty, request.SlotsLine ?? string.Empty,
            request.Requirements ?? string.Empty, request.LinkUrl ?? string.Empty, request.AfterDark, firstPhoto,
            mediaUrls, 0, false, AdStatuses.Live, nowUnix, nowUnix, nowUnix + 7L * 86400L, request.AllowInquiries,
            0, null, string.Empty, null, request.Wanted, request.Accent);
        return composePreview;
    }

    private void SubmitCompose()
    {
        if (!ComposeDetailsValid(out _) || composeBusy)
        {
            return;
        }

        var request = BuildRequest(out var dataCenterId);
        if (dataCenterId == 0)
        {
            return;
        }

        composeBusy = true;
        composeOutcome = null;
        var photos = new AdPhotoBatch(composeSession.SelectedArray(), composeSession.CropsArray(),
            composeSession.AspectsArray(), composeSession.EditsArray());
        void Done(AdCreateOutcome outcome)
        {
            composeBusy = false;
            if (outcome == AdCreateOutcome.Created)
            {
                composeSucceeded = true;
            }
            else
            {
                composeOutcome = outcome;
            }
        }

        if (editingAdId is { } adId)
        {
            store.Update(adId, request, composeKeptUrls.ToArray(), photos, Done);
        }
        else
        {
            store.Create(request, photos, Done);
        }
    }

    private void ResetComposeForm()
    {
        composeStep = ComposeStep.Kind;
        editingAdId = null;
        composeKeptUrls.Clear();
        composeTags.Clear();
        composeTagDraft = string.Empty;
        composeArchetype = -1;
        composeWanted = false;
        composeCategory = AdCategories.VenueNight;
        composeTitle = string.Empty;
        composeBody = string.Empty;
        composeLocation = null;
        composeAddressNote = string.Empty;
        Array.Clear(composeDays);
        composeOpenMinute = DefaultOpenMinute;
        composeCloseMinute = DefaultCloseMinute;
        composePriceMode = 0;
        composePriceText = string.Empty;
        composeTurnaround = string.Empty;
        composeSlotsLine = string.Empty;
        composeRequirements = string.Empty;
        composeLink = string.Empty;
        composeAllowInquiries = true;
        composeAfterDark = false;
        composeAccent = 0;
        composeBusy = false;
        composeOutcome = null;
        composeCounterLength = -1;
        composeTitleFocus = false;
        composePreview = null;
        composePreviewHash = 0;
        composeCategoryRail.Reset();
    }
}
