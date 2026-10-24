using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int GuideSlides = 7;
    private const int GuideTrackCount = 2;
    private const int GuidePodiumCount = 3;
    private const int GuideLibraryColumns = 3;
    private const int GuideLibraryRows = 2;
    private const int GuideHeatIndex = 2;
    private const int GuideHeatCount = 5;
    private const long GuideLikes = 128;
    private const float GuidePhotoAspect = 0.75f;
    private const float GuideTextDelay = 0.04f;
    private const float GuideStageDelay = 0.14f;
    private const float GuideCardGapUnits = 12f;
    private const float GuideCardRadiusUnits = 18f;
    private const float GuidePlateUnits = 48f;
    private const float GuidePlateIconShare = 0.42f;
    private const float GuideScoreUnits = 56f;
    private const float GuideScorePitch = 1.25f;
    private const float GuideSwipeUnits = 56f;
    private const float GuideFilterUnits = 30f;
    private const float GuideSearchUnits = 44f;
    private const float GuideDimAlpha = 0.3f;
    private const float GuideRevealDelay = 0.1f;
    private const float GuideRevealStagger = 0.24f;
    private const float GuideRunnerUpShare = 0.86f;
    private const float GuideThirdShare = 0.74f;
    private const float GuideShardSpread = 1.4f;
    private const float GuideShardSpeed = 340f;
    private const float GuideMarkUnits = 72f;
    private const float GuideMarkShare = 0.3f;
    private const float GuideThemeShare = 0.58f;
    private const float GuideSideShare = 0.82f;
    private const float GuideSideShift = 0.42f;
    private const float GuidePillShare = 0.55f;
    private const float GuidePhotoFloorShare = 0.3f;
    private const float GuideTallAspect = 0.56f;
    private const float GuidePodiumFirstShare = 0.46f;
    private const float GuidePodiumSideShare = 0.36f;
    private const float GuidePodiumOverlapShare = 0.09f;
    private const float GuideCollageShare = 0.6f;
    private const float GuideVeilAlpha = 0.72f;

    private static readonly TextStyle GuideBodyStyle = new(TextStyles.Title3.Scale, FontWeight.Regular);

    private static readonly LocString[] GuideTitles =
    {
        L.Prism.GuideIntroTitle, L.Prism.GuideJudgeTitle, L.Prism.GuideRewardsTitle, L.Prism.GuideTryTitle,
        L.Prism.GuideBuildTitle, L.Prism.GuideLibraryTitle, L.Prism.ThemeLabel,
    };

    private static readonly LocString[] GuideBodies =
    {
        L.Prism.GuideWelcomeBody, L.Prism.GuideJudgeBody, L.Prism.GuideRewardsBody, L.Prism.GuideTryBody,
        L.Prism.GuideBuildBody, L.Prism.GuideLibraryBody, L.Prism.GuideThemeBody,
    };

    private static readonly PrismPicture[] GuideTrackPictures = { PrismPicture.TrackModded, PrismPicture.TrackVanilla };
    private static readonly string[] GuideTrackIds = { PrismTracks.Modded, PrismTracks.Vanilla };
    private static readonly PrismFact[] GuideTrackFacts = { PrismFact.Modded, PrismFact.Vanilla };
    private static readonly int[] GuidePodiumRanks = { 2, 3, 1 };
    private static readonly PrismPicture[] GuidePodiumPictures =
    {
        PrismPicture.GuidePodiumFirst, PrismPicture.GuidePodiumSecond, PrismPicture.GuidePodiumThird,
    };
    private static readonly PrismFact[] GuideFilters =
    {
        PrismFact.FitsMe, PrismFact.Race, PrismFact.Male, PrismFact.Female, PrismFact.Modded, PrismFact.Vanilla,
    };

    private static readonly int[] GuideLibraryTags =
    {
        FactBit(PrismFact.Modded) | FactBit(PrismFact.Female) | FactBit(PrismFact.Race) | FactBit(PrismFact.FitsMe),
        FactBit(PrismFact.Modded) | FactBit(PrismFact.Female) | FactBit(PrismFact.Male),
        FactBit(PrismFact.Vanilla) | FactBit(PrismFact.Female) | FactBit(PrismFact.Race) | FactBit(PrismFact.FitsMe),
        FactBit(PrismFact.Vanilla) | FactBit(PrismFact.Male),
        FactBit(PrismFact.Modded) | FactBit(PrismFact.Female) | FactBit(PrismFact.Race),
        FactBit(PrismFact.Vanilla) | FactBit(PrismFact.Female) | FactBit(PrismFact.Race),
    };

    private static readonly PrismPicture[] GuideExploreSidePictures =
    {
        PrismPicture.GuideExploreSecond, PrismPicture.GuideExploreThird,
    };

    private static readonly PrismPicture[] GuideLibraryPictures =
    {
        PrismPicture.GuideLibraryFirst, PrismPicture.GuideLibrarySecond, PrismPicture.GuideLibraryThird,
        PrismPicture.GuideLibraryFourth, PrismPicture.GuideLibraryFifth, PrismPicture.GuideLibrarySixth,
    };

    private static readonly uint[] GuideGear = { 19139, 0, 11102, 17492, 11116, 17493, 11137, 16039, 12364, 0, 0, 0 };

    private readonly CachedText[] prizeTexts = new CachedText[PrismArt.PodiumRanks];
    private readonly GearDraft guideGear = new();
    private readonly PrismCountdown guideCountdown = new();
    private readonly Spring[] guideFilterFill = new Spring[GuideFilters.Length];
    private readonly Spring[] guideTileFade = new Spring[GuideLibraryTags.Length];
    private CachedText guideHeatText;
    private double guideLikedAt;
    private double guideTriedAt;
    private double guideTrackAt;
    private double guideRevealAt;
    private float guideScoredAt;
    private int guideScore;
    private int guidePage;
    private int guideTrack;
    private int guideFilters;
    private bool guideLiked;
    private bool guideSaved;
    private bool guideWorn;
    private bool guideRevealed;
    private bool guidePodiumBurst;
    private bool guideTouched;
    private bool guideSwipePressed;
    private bool guideSwiping;
    private Vector2 guideSwipeStart;
    private PrismTab guideReturn = PrismTab.Discover;

    private static int FactBit(PrismFact fact) => 1 << (int)fact;

    private void StartGuide(PrismTab returnTab)
    {
        guidePage = 0;
        guideReturn = returnTab;
        guideLiked = false;
        guideSaved = false;
        guideWorn = false;
        guideRevealed = false;
        guidePodiumBurst = false;
        guideTouched = false;
        guideSwipePressed = false;
        guideSwiping = false;
        guideTrack = -1;
        guideScore = 0;
        guideFilters = 0;
        guideScoredAt = 0f;
        Array.Clear(guideGear.Items);
        Array.Clear(guideGear.Shown);
        for (var index = 0; index < guideTileFade.Length; index++)
        {
            guideTileFade[index].SnapTo(1f);
        }

        for (var index = 0; index < guideFilterFill.Length; index++)
        {
            guideFilterFill[index].SnapTo(0f);
        }

        pillLabel = string.Empty;
        Go(PrismScreen.Guide);
    }

    private void TurnGuidePage(int delta)
    {
        var next = guidePage + delta;
        if (next < 0 || next >= GuideSlides)
        {
            FinishGuide(false);
            return;
        }

        guidePage = next;
        guideTouched = false;
        Go(PrismScreen.Guide, delta);
    }

    private void FinishGuide(bool toTheme)
    {
        OpenTab(toTheme ? PrismTab.Week : guideReturn);
    }

    private bool GuideThemeOpen => store.Week is { Phase: PrismPhases.Entering, Title.Length: > 0 };

    private bool GuideLastPage => guidePage == GuideSlides - 1;

    private void TouchGuide()
    {
        guideTouched = true;
    }

    private string GuideTitle() => Loc.T(GuideLastPage && !GuideThemeOpen
        ? L.Prism.GuideExploreTitle
        : GuideTitles[guidePage]);

    private string GuideBody() => Loc.T(GuideLastPage && !GuideThemeOpen
        ? L.Prism.GuideIntroBody
        : GuideBodies[guidePage]);

    private void DrawGuide(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        onPhoto = false;
        PrismArt.OverPhoto(false);
        var left = content.Min.X;
        var width = content.Width;
        var tap = Metrics.Size.TapTarget * scale;
        var rowTop = content.Min.Y + (StoryBarGapUnits + StoryRowGapUnits) * scale - Metrics.Space.Xs * scale;
        var textTop = rowTop + tap + Metrics.Space.Md * scale;
        var title = GuideTitle();
        var body = GuideBody();
        var bodyTop = textTop + PrismType.HeroHeight(title, width) + Metrics.Space.Sm * scale;
        var bodyHeight = Typography.MeasureWrappedBlock(body, GuideBodyStyle, width).Y;
        var bottom = content.Max.Y - PrismArt.BottomMargin(scale);
        var button = new Rect(new Vector2(left, bottom - Button.LargeHeight * scale),
            new Vector2(content.Max.X, bottom));
        var stage = new Rect(new Vector2(left, bodyTop + bodyHeight + Metrics.Space.Xl * scale),
            new Vector2(content.Max.X, button.Min.Y - Metrics.Space.Xl * scale));

        var textMark = PrismMotion.Mark(drawList);
        PrismType.DrawHero(drawList, new Vector2(left, textTop), title, width, PrismArt.Fore);
        Typography.DrawWrappedLeft(new Vector2(left, bodyTop), body, PrismArt.Fore, GuideBodyStyle, width);
        PrismMotion.Rise(drawList, textMark, PrismMotion.At(screenAge, GuideTextDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);

        if (stage.Height > 0f)
        {
            var stageMark = PrismMotion.Mark(drawList);
            DrawGuideSlide(drawList, stage, scale);
            PrismMotion.Rise(drawList, stageMark, PrismMotion.At(screenAge, GuideStageDelay, Motion.PageSettle),
                PrismMotion.RiseUnits * scale);
        }

        if (DrawGuideNext(drawList, button, scale) || DrawGuideTop(drawList, content, rowTop, scale))
        {
            return;
        }

        SwipeGuide(content, scale);
    }

    private bool DrawGuideNext(ImDrawListPtr drawList, Rect button, float scale)
    {
        var themeOpen = GuideThemeOpen;
        var label = !GuideLastPage ? Loc.T(L.Common.Next)
            : themeOpen ? Loc.T(L.Prism.EnterThisWeek)
            : Loc.T(L.Prism.GetStarted);
        if (!DrawButton(drawList, button, label, ButtonStyle.Solid, scale, id: "prism.guide.next"))
        {
            return false;
        }

        if (GuideLastPage)
        {
            FinishGuide(themeOpen);
        }
        else
        {
            TurnGuidePage(1);
        }

        return true;
    }

    private bool DrawGuideTop(ImDrawListPtr drawList, Rect content, float rowTop, float scale)
    {
        PrismArt.StoryBars(drawList, content.Min.X, content.Max.X, content.Min.Y + StoryBarGapUnits * scale,
            GuideSlides, guidePage, 1f, scale);
        var tap = Metrics.Size.TapTarget * scale;
        var back = new Rect(new Vector2(content.Min.X, rowTop), new Vector2(content.Min.X + tap, rowTop + tap));
        HoverTooltip.Show("prism.guide.back", back, Loc.T(L.Prism.Back));
        if (BackButton.Draw("prism.guide.back", back.Center, BackChevronUnits * scale, PrismArt.Fore,
                UiInteract.Hover(back.Min, back.Max), scale))
        {
            TurnGuidePage(-1);
            return true;
        }

        if (GuideLastPage)
        {
            return false;
        }

        var skip = Loc.T(L.Prism.Skip);
        if (!DrawButton(drawList,
                PlainRect(new Vector2(content.Max.X, back.Center.Y), skip, content.Width * 0.5f, 1f, scale), skip,
                ButtonStyle.Plain, scale))
        {
            return false;
        }

        FinishGuide(false);
        return true;
    }

    private void SwipeGuide(Rect content, float scale)
    {
        var mouse = ImGui.GetMousePos();
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(content.Min, content.Max))
        {
            guideSwipePressed = true;
            guideSwiping = false;
            guideSwipeStart = mouse;
        }

        if (!guideSwipePressed)
        {
            return;
        }

        var move = mouse - guideSwipeStart;
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (!guideSwiping && MathF.Abs(move.X) > PagerSlopUnits * scale && MathF.Abs(move.X) > MathF.Abs(move.Y))
            {
                guideSwiping = true;
                UiInteract.CancelPendingTap();
            }

            return;
        }

        var swiped = guideSwiping && MathF.Abs(move.X) >= GuideSwipeUnits * scale;
        guideSwipePressed = false;
        guideSwiping = false;
        if (!swiped)
        {
            return;
        }

        var delta = move.X < 0f ? 1 : -1;
        var next = guidePage + delta;
        if (next < 0 || next >= GuideSlides)
        {
            return;
        }

        TurnGuidePage(delta);
    }

    private void DrawGuideSlide(ImDrawListPtr drawList, Rect stage, float scale)
    {
        switch (guidePage)
        {
            case 0:
                DrawGuideTracks(drawList, stage, scale);
                break;
            case 1:
                DrawGuideJudging(drawList, stage, scale);
                break;
            case 2:
                DrawGuidePodium(drawList, stage, scale);
                break;
            case 3:
                DrawGuideTryOn(drawList, stage, scale);
                break;
            case 4:
                DrawGuideWardrobe(drawList, stage, scale);
                break;
            case 5:
                DrawGuideLibrary(drawList, stage, scale);
                break;
            default:
                DrawGuideFinal(drawList, stage, scale);
                break;
        }
    }

    private static float GuideHintLine(float scale) =>
        Typography.LineHeight(PrismType.Caption) + Metrics.Space.Md * scale;

    private void DrawGuideHint(ImDrawListPtr drawList, float centerX, float top, float scale)
    {
        if (guideTouched)
        {
            return;
        }

        var hint = Loc.T(L.Prism.GuideTapHint);
        var size = Typography.Measure(hint, PrismType.Caption);
        var glyph = size.Y;
        var gap = Metrics.Space.Xs * scale;
        var left = centerX - (glyph + gap + size.X) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(left + glyph * 0.5f, top + size.Y * 0.5f), PhoneIcons.Sparkles,
            PrismArt.Muted, glyph);
        Typography.Draw(drawList, new Vector2(left + glyph + gap, top), hint, PrismArt.Muted, PrismType.Caption);
    }

    private static float GuideCenterTop(Rect stage, float blockHeight) =>
        stage.Min.Y + MathF.Max(0f, (stage.Height - blockHeight) * 0.5f);

    private static Rect GuidePhotoRect(Rect stage, float top, float maxHeight)
    {
        var height = MathF.Max(0f, maxHeight);
        var width = MathF.Min(stage.Width, height * GuidePhotoAspect);
        height = width / GuidePhotoAspect;
        return new Rect(new Vector2(stage.Center.X - width * 0.5f, top),
            new Vector2(stage.Center.X + width * 0.5f, top + height));
    }

    private static void DrawGuideCard(ImDrawListPtr drawList, Rect card, PrismPicture picture, float alpha,
        float scale) =>
        DrawGuideCard(drawList, card, picture, alpha, GuideCardRadiusUnits * scale, scale);

    private static void DrawGuideCard(ImDrawListPtr drawList, Rect card, PrismPicture picture, float alpha,
        float radius, float scale)
    {
        if (card.Width <= 0f || card.Height <= 0f)
        {
            return;
        }

        if (!PrismStock.DrawRounded(drawList, card, picture, alpha, radius))
        {
            PrismArt.Placeholder(drawList, card, false, scale, alpha);
        }
    }

    private static bool GuideCardTapped(Rect card)
    {
        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private void BurstFrom(Vector2 origin)
    {
        shards.Burst(origin, -MathF.PI * 0.5f, GuideShardSpread, GuideShardSpeed, ShardLifeSeconds);
    }

    private void DrawGuideTracks(ImDrawListPtr drawList, Rect stage, float scale)
    {
        var gap = GuideCardGapUnits * scale;
        var hint = GuideHintLine(scale);
        var width = (stage.Width - gap) * 0.5f;
        var height = MathF.Min(width / GuideTallAspect, stage.Height - hint);
        if (height <= 0f)
        {
            return;
        }

        width = MathF.Min(width, height * GuideTallAspect);
        var top = GuideCenterTop(stage, height + hint);
        var radius = GuideCardRadiusUnits * scale;
        var lit = PrismMotion.At((float)(ImGui.GetTime() - guideTrackAt), 0f, Motion.Appear);
        var left = stage.Center.X - width - gap * 0.5f;
        for (var index = 0; index < GuideTrackCount; index++)
        {
            var min = new Vector2(left + index * (width + gap), top);
            var card = new Rect(min, min + new Vector2(width, height));
            var picked = guideTrack == index;
            var alpha = guideTrack < 0 || picked ? 1f : 1f + (GuideDimAlpha - 1f) * lit;
            DrawGuideCard(drawList, card, GuideTrackPictures[index], alpha, scale);
            DrawGuideTrackPlate(drawList, card, index, scale);
            if (picked)
            {
                PrismLight.Edge(drawList, card, radius, index == 0 ? PrismGlow.Spectrum : PrismGlow.Pearl,
                    PrismLight.EdgeAlpha * lit, scale);
            }

            if (!GuideCardTapped(card))
            {
                continue;
            }

            guideTrack = index;
            guideTrackAt = ImGui.GetTime();
            TouchGuide();
            UiFeedback.Play(UiSound.Tap);
            BurstFrom(new Vector2(card.Center.X, card.Max.Y - GuidePlateUnits * scale));
        }

        DrawGuideHint(drawList, stage.Center.X, top + height + Metrics.Space.Md * scale, scale);
    }

    private static void DrawGuideTrackPlate(ImDrawListPtr drawList, Rect card, int index, float scale)
    {
        var inset = Metrics.Space.Sm * scale;
        var plate = new Rect(new Vector2(card.Min.X + inset, card.Max.Y - inset - GuidePlateUnits * scale),
            new Vector2(card.Max.X - inset, card.Max.Y - inset));
        PrismArt.PhotoPanel(drawList, plate, Metrics.Radius.Md * scale);
        var glyph = plate.Height * GuidePlateIconShare;
        var pad = Metrics.Space.Sm * scale;
        PrismArt.FactIcon(drawList, new Vector2(plate.Min.X + pad + glyph * 0.5f, plate.Center.Y), glyph,
            GuideTrackFacts[index], PrismArt.PhotoFore);
        var textLeft = plate.Min.X + pad + glyph + Metrics.Space.Xs * scale;
        var name = Typography.FitText(PrismTracks.Name(GuideTrackIds[index]),
            MathF.Max(1f, plate.Max.X - pad - textLeft), PrismType.Headline);
        Typography.Draw(drawList,
            new Vector2(textLeft, plate.Center.Y - Typography.LineHeight(PrismType.Headline) * 0.5f), name,
            PrismArt.PhotoFore, PrismType.Headline);
    }

    private string GuideHeat()
    {
        var key = HashCode.Combine(GuideHeatIndex, GuideHeatCount, Loc.Current.Code);
        return guideHeatText.IsCurrent(key)
            ? guideHeatText.Value
            : guideHeatText.Store(key, Loc.T(L.Prism.HeatOf, GuideHeatIndex, GuideHeatCount));
    }

    private void DrawGuideJudging(ImDrawListPtr drawList, Rect stage, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var captionLine = Typography.LineHeight(PrismType.Caption);
        var gap = Metrics.Space.Sm * scale;
        var size = MathF.Min(GuideScoreUnits * scale, stage.Width / (PrismArt.Stars * GuideScorePitch));
        var reserved = captionLine + gap + Metrics.Space.Md * scale + size + gap + labelLine + GuideHintLine(scale);
        var photo = GuidePhotoRect(stage, stage.Min.Y + captionLine + gap, stage.Height - reserved);
        if (photo.Height <= 0f)
        {
            return;
        }

        var heat = GuideHeat();
        Typography.Draw(drawList,
            new Vector2(stage.Center.X - Typography.Measure(heat, PrismType.Caption).X * 0.5f, stage.Min.Y), heat,
            PrismArt.Muted, PrismType.Caption);
        DrawGuideCard(drawList, photo, PrismPicture.GuideJudging, 1f, scale);
        var rowTop = photo.Max.Y + Metrics.Space.Md * scale;
        var row = new Rect(new Vector2(stage.Min.X, rowTop), new Vector2(stage.Max.X, rowTop + size));
        var picked = DrawScoreButtons(drawList, row, guideScore,
            PrismMotion.At(screenAge, guideScoredAt, Motion.Release), true, scale);
        if (picked > 0)
        {
            guideScore = picked;
            guideScoredAt = screenAge;
            TouchGuide();
        }

        var anchors = row.Max.Y + gap;
        PrismType.Draw(drawList, new Vector2(row.Min.X, anchors), Loc.T(L.Prism.ScoreLow), PrismArt.Muted,
            PrismFace.Label, labelPixels);
        PrismType.DrawRight(drawList, new Vector2(row.Max.X, anchors), Loc.T(L.Prism.ScoreHigh), PrismArt.Muted,
            PrismFace.Label, labelPixels);
        DrawGuideHint(drawList, stage.Center.X, anchors + labelLine + Metrics.Space.Md * scale, scale);
    }

    private void DrawGuidePodium(ImDrawListPtr drawList, Rect stage, float scale)
    {
        var plate = Typography.LineHeight(PrismType.Title) + Typography.LineHeight(PrismType.Caption) +
                    Metrics.Space.Xs * scale;
        var hint = GuideHintLine(scale);
        var firstWidth = stage.Width * GuidePodiumFirstShare;
        var tallest = MathF.Min(firstWidth / GuideTallAspect, stage.Height - plate - hint);
        if (tallest <= 0f)
        {
            return;
        }

        firstWidth = tallest * GuideTallAspect;
        var sideWidth = firstWidth * GuidePodiumSideShare / GuidePodiumFirstShare;
        var overlap = stage.Width * GuidePodiumOverlapShare;
        var top = GuideCenterTop(stage, tallest + plate + hint);
        var baseline = top + tallest;
        var podium = new Rect(new Vector2(stage.Min.X, top), new Vector2(stage.Max.X, baseline + plate));
        if (!guideRevealed && GuideCardTapped(podium))
        {
            guideRevealed = true;
            guidePodiumBurst = false;
            guideRevealAt = ImGui.GetTime();
            TouchGuide();
            UiFeedback.Play(UiSound.Success);
        }

        var age = (float)(ImGui.GetTime() - guideRevealAt);
        var radius = GuideCardRadiusUnits * scale;
        var reach = firstWidth * 0.5f + sideWidth * 0.5f - overlap;
        for (var slot = 0; slot < GuidePodiumCount; slot++)
        {
            var rank = GuidePodiumRanks[slot];
            var width = rank == 1 ? firstWidth : sideWidth;
            var height = rank == 1 ? tallest
                : width / GuideTallAspect * (rank == 2 ? 1f : GuideThirdShare / GuideRunnerUpShare);
            var middle = stage.Center.X + (rank == 2 ? -reach : rank == 3 ? reach : 0f);
            var card = new Rect(new Vector2(middle - width * 0.5f, baseline - height),
                new Vector2(middle + width * 0.5f, baseline));
            var delay = GuideRevealDelay + GuideRevealStagger * (GuidePodiumCount - rank);
            var lit = guideRevealed ? PrismMotion.At(age, delay, Motion.Sheet) : 0f;
            DrawGuideCard(drawList, card, GuidePodiumPictures[rank - 1], 1f, scale);
            if (lit < PrismMotion.Settled)
            {
                Squircle.Fill(drawList, card.Min, card.Max, radius,
                    ImGui.GetColorU32(PrismArt.Page with { W = GuideVeilAlpha * (1f - lit) }));
            }

            if (lit > PrismMotion.GrowEpsilon)
            {
                PrismLight.Edge(drawList, card, radius, rank == 1 ? PrismGlow.Spectrum : PrismGlow.Pearl,
                    (rank == 1 ? PrismLight.EdgeAlpha : PlacedPearlAlpha) * lit, scale);
            }

            if (rank == 1 && guideRevealed && !guidePodiumBurst && age >= delay)
            {
                guidePodiumBurst = true;
                BurstFrom(new Vector2(card.Center.X, card.Min.Y + card.Height * 0.25f));
            }

            var plateCenter = rank == 1 ? middle : middle + (rank == 2 ? -overlap : overlap) * 0.5f;
            DrawGuidePodiumPlate(drawList, new Vector2(plateCenter, baseline + Metrics.Space.Xs * scale),
                rank == 1 ? width : width - overlap, rank, lit);
        }

        DrawGuideHint(drawList, stage.Center.X, baseline + plate + Metrics.Space.Md * scale, scale);
    }

    private void DrawGuidePodiumPlate(ImDrawListPtr drawList, Vector2 topCenter, float width, int rank, float lit)
    {
        var numeral = Plain[rank];
        var numeralSize = Typography.Measure(numeral, PrismType.Title);
        Typography.Draw(drawList, new Vector2(topCenter.X - numeralSize.X * 0.5f, topCenter.Y), numeral,
            PrismArt.Fore, PrismType.Title);
        var prize = PrizeLine(store.Week?.Prizes?.Final, rank - 1);
        if (prize.Length == 0 || lit <= PrismMotion.GrowEpsilon)
        {
            return;
        }

        var line = Typography.LineHeight(PrismType.Caption);
        var glyph = line * CurrencyGlyph.GlyphFraction;
        var reserve = CurrencyGlyph.Reserve(line);
        var fitted = Typography.FitText(prize, MathF.Max(1f, width - reserve), PrismType.Caption);
        var total = reserve + Typography.Measure(fitted, PrismType.Caption).X;
        var left = topCenter.X - total * 0.5f;
        var top = topCenter.Y + numeralSize.Y;
        var mark = PrismMotion.Mark(drawList);
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, new Vector2(left + glyph * 0.5f, top + line * 0.5f), glyph);
        Typography.Draw(drawList, new Vector2(left + reserve, top), fitted, PrismArt.Fore, PrismType.Caption);
        if (lit < PrismMotion.Settled)
        {
            PrismMotion.Shift(drawList, mark, Vector2.Zero, lit);
        }
    }

    private void DrawGuideTryOn(ImDrawListPtr drawList, Rect stage, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var photo = GuidePhotoRect(stage, stage.Min.Y,
            stage.Height - tap - Metrics.Space.Md * scale - GuideHintLine(scale));
        if (photo.Height <= 0f)
        {
            return;
        }

        DrawGuideCard(drawList, photo, PrismPicture.GuideTryOn, 1f, scale);
        var tried = (float)(ImGui.GetTime() - guideTriedAt);
        if (guideWorn)
        {
            drawList.PushClipRect(photo.Min, photo.Max, true);
            PrismArt.TryOnMoment(drawList, photo, PhoneIcons.Wand, Loc.T(L.Prism.OnYourCharacter), tried, scale);
            drawList.PopClipRect();
        }

        var liked = (float)(ImGui.GetTime() - guideLikedAt);
        if (guideLiked && liked < LikeBloomSeconds)
        {
            DrawLikeBloom(drawList, photo, liked, scale);
        }

        var rowTop = photo.Max.Y + Metrics.Space.Md * scale;
        DrawGuideActions(drawList, new Rect(new Vector2(stage.Min.X, rowTop), new Vector2(stage.Max.X, rowTop + tap)),
            tried, scale);
        DrawGuideHint(drawList, stage.Center.X, rowTop + tap + Metrics.Space.Md * scale, scale);
    }

    private void DrawGuideActions(ImDrawListPtr drawList, Rect row, float tried, float scale)
    {
        var radius = row.Height * 0.5f;
        var heart = new Vector2(row.Min.X + radius, row.Center.Y);
        var stamp = PrismMotion.Mark(drawList);
        if (RoundButton.Icon(drawList, heart, radius, guideLiked ? PhoneIcons.HeartFilled : PhoneIcons.Heart,
                controls, ButtonStyle.Bare, Loc.T(L.Prism.Like), HoverLabelSide.Below))
        {
            guideLiked = !guideLiked;
            guideLikedAt = ImGui.GetTime();
            TouchGuide();
        }

        var age = (float)(ImGui.GetTime() - guideLikedAt);
        if (guideLiked && age < LikeBloomSeconds)
        {
            PrismMotion.Grow(drawList, stamp, heart,
                1f + LikeStamp * (1f - PrismMotion.At(age, 0f, Motion.PageSettle)));
            DrawLikeRing(drawList, heart, radius * 0.5f, age, scale);
        }

        var count = NumberText.Compact(GuideLikes + (guideLiked ? 1 : 0));
        Typography.Draw(drawList,
            new Vector2(heart.X + radius, row.Center.Y - Typography.LineHeight(PrismType.Body) * 0.5f), count,
            PrismArt.Fore, PrismType.Body);
        if (RoundButton.Icon(drawList, new Vector2(row.Max.X - radius, row.Center.Y), radius,
                guideSaved ? PhoneIcons.BookmarkFilled : PhoneIcons.Bookmark, controls, ButtonStyle.Bare,
                Loc.T(guideSaved ? L.Prism.Saved : L.Prism.Save), HoverLabelSide.Below))
        {
            guideSaved = !guideSaved;
            TouchGuide();
        }

        var tryOn = Loc.T(L.Prism.TryOnGlam);
        var wearing = Loc.T(L.Prism.Wearing);
        var cap = row.Width * GuidePillShare;
        var check = Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xs * scale;
        var width = MathF.Min(ButtonWidth([tryOn, wearing], cap, scale) + check, cap);
        var pill = new Rect(new Vector2(row.Center.X - width * 0.5f, row.Min.Y),
            new Vector2(row.Center.X + width * 0.5f, row.Max.Y));
        var settling = guideWorn && tried < PrismArt.TryOnSeconds;
        if (guideWorn && !settling)
        {
            if (DrawWornLabel(drawList, pill, wearing, PrismMotion.At(tried, PrismArt.TryOnSeconds, Motion.Appear),
                    scale))
            {
                guideWorn = false;
                guideTriedAt = ImGui.GetTime();
            }

            return;
        }

        PrismLight.Primary(drawList, pill, !settling, scale);
        if (!DrawPill(drawList, pill, tryOn, false, false, settling, scale))
        {
            return;
        }

        guideWorn = true;
        guideTriedAt = ImGui.GetTime();
        TouchGuide();
        UiFeedback.Play(UiSound.Success);
        shards.Burst(new Vector2(pill.Min.X + pill.Width * TryOnShardShare, pill.Min.Y), TryOnShardAngle,
            TryOnShardSpread, TryOnShardSpeed, ShardLifeSeconds);
    }

    private static bool DrawWornLabel(ImDrawListPtr drawList, Rect rect, string label, float alpha, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var ink = hovered ? PrismArt.Muted : PrismArt.Fore;
        var glyph = Typography.LineHeight(PrismType.Caption);
        var gap = Metrics.Space.Xs * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, rect.Width - glyph - gap), PrismType.Headline);
        var size = Typography.Measure(fitted, PrismType.Headline);
        var textLeft = rect.Center.X - (glyph + gap + size.X) * 0.5f + glyph + gap;
        var mark = PrismMotion.Mark(drawList);
        PhoneIcon.Draw(drawList, new Vector2(textLeft - gap - glyph * 0.5f, rect.Center.Y), PhoneIcons.Check, ink,
            glyph);
        Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - size.Y * 0.5f), fitted, ink,
            PrismType.Headline);
        if (alpha < PrismMotion.Settled)
        {
            PrismMotion.Shift(drawList, mark, Vector2.Zero, alpha);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void DrawGuideWardrobe(ImDrawListPtr drawList, Rect stage, float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var gridWidth = stage.Width - pad * 2f;
        var panelHeight = GearGridHeight(guideGear.Items.Length, gridWidth, scale) + pad * 2f;
        var fillHeight = Metrics.Size.TapTarget * scale;
        var hint = GuideHintLine(scale);
        var panelTop = stage.Max.Y - hint - fillHeight - Metrics.Space.Md * scale - panelHeight;
        if (panelTop < stage.Min.Y)
        {
            return;
        }

        var photo = GuidePhotoRect(stage, stage.Min.Y, panelTop - Metrics.Space.Md * scale - stage.Min.Y);
        if (photo.Height >= stage.Height * GuidePhotoFloorShare)
        {
            DrawGuideCard(drawList, photo, PrismPicture.GuideWardrobe, 1f, scale);
        }

        var panel = new Rect(new Vector2(stage.Min.X, panelTop), new Vector2(stage.Max.X, panelTop + panelHeight));
        Squircle.Fill(drawList, panel.Min, panel.Max, Metrics.Radius.Lg * scale,
            ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Quaternary)));
        DrawGearGrid(drawList, guideGear, panel.Min.X + pad, panel.Min.Y + pad, gridWidth, scale);
        var fill = new Rect(new Vector2(stage.Min.X, panel.Max.Y + Metrics.Space.Md * scale),
            new Vector2(stage.Max.X, panel.Max.Y + Metrics.Space.Md * scale + fillHeight));
        var filled = DrawButton(drawList, fill, Loc.T(L.Prism.FillFromWearing), ButtonStyle.Gray, scale,
            id: "prism.guide.fill", tapSound: UiSound.Success);
        if (filled)
        {
            Array.Fill(guideGear.Shown, true);
            BurstFrom(new Vector2(panel.Center.X, panel.Min.Y));
        }

        if (FillGuideGear())
        {
            TouchGuide();
            if (!filled)
            {
                UiFeedback.Play(UiSound.Tap);
            }
        }

        DrawGuideHint(drawList, stage.Center.X, fill.Max.Y + Metrics.Space.Md * scale, scale);
    }

    private bool FillGuideGear()
    {
        var added = false;
        for (var index = 0; index < GuideGear.Length; index++)
        {
            if (!guideGear.Shown[index] || guideGear.Items[index] != 0 || GuideGear[index] == 0)
            {
                continue;
            }

            guideGear.Items[index] = GuideGear[index];
            added = true;
        }

        return added;
    }

    private void DrawGuideLibrary(ImDrawListPtr drawList, Rect stage, float scale)
    {
        var search = GuideSearchUnits * scale;
        var bar = new Rect(stage.Min, new Vector2(stage.Max.X, stage.Min.Y + search));
        Squircle.Fill(drawList, bar.Min, bar.Max, search * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary)));
        var glyph = PrismArt.InlineIconUnits * scale;
        var inset = Metrics.Space.Md * scale;
        PhoneIcon.Draw(drawList, new Vector2(bar.Min.X + inset + glyph * 0.5f, bar.Center.Y), PhoneIcons.Search,
            PrismArt.Muted, glyph);
        var textLeft = bar.Min.X + inset + glyph + Metrics.Space.Sm * scale;
        Typography.Draw(drawList,
            new Vector2(textLeft, bar.Center.Y - Typography.LineHeight(PrismType.Body) * 0.5f),
            Typography.FitText(Loc.T(L.Prism.SearchHint), MathF.Max(1f, bar.Max.X - inset - textLeft), PrismType.Body),
            PrismArt.Muted, PrismType.Body);

        var tap = Metrics.Size.TapTarget * scale;
        var filterTop = bar.Max.Y + Metrics.Space.Sm * scale;
        DrawGuideFilters(drawList,
            new Rect(new Vector2(stage.Min.X, filterTop), new Vector2(stage.Max.X, filterTop + tap)), scale);
        var gap = Metrics.Space.Xs * scale;
        var gridTop = filterTop + tap + Metrics.Space.Sm * scale;
        var available = stage.Max.Y - GuideHintLine(scale) - gridTop;
        if (available <= 0f)
        {
            return;
        }

        var width = MathF.Min((stage.Width - gap * (GuideLibraryColumns - 1)) / GuideLibraryColumns,
            (available - gap * (GuideLibraryRows - 1)) / GuideLibraryRows * GuidePhotoAspect);
        if (width <= 0f)
        {
            return;
        }

        var height = width / GuidePhotoAspect;
        var gridLeft = stage.Center.X - (width * GuideLibraryColumns + gap * (GuideLibraryColumns - 1)) * 0.5f;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        for (var index = 0; index < GuideLibraryTags.Length; index++)
        {
            var matches = (GuideLibraryTags[index] & guideFilters) == guideFilters;
            var alpha = guideTileFade[index].Step(matches ? 1f : GuideDimAlpha, Motion.Appear, delta);
            var min = new Vector2(gridLeft + index % GuideLibraryColumns * (width + gap),
                gridTop + index / GuideLibraryColumns * (height + gap));
            DrawGuideCard(drawList, new Rect(min, min + new Vector2(width, height)), GuideLibraryPictures[index],
                Math.Clamp(alpha, GuideDimAlpha, 1f), Metrics.Radius.Md * scale, scale);
        }

        DrawGuideHint(drawList, stage.Center.X,
            gridTop + height * GuideLibraryRows + gap * (GuideLibraryRows - 1) + Metrics.Space.Md * scale, scale);
    }

    private void DrawGuideFilters(ImDrawListPtr drawList, Rect row, float scale)
    {
        var size = GuideFilterUnits * scale;
        var pitch = row.Width / GuideFilters.Length;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        for (var index = 0; index < GuideFilters.Length; index++)
        {
            var fact = GuideFilters[index];
            var bit = FactBit(fact);
            var center = new Vector2(row.Min.X + pitch * (index + 0.5f), row.Center.Y);
            var half = new Vector2(row.Height * 0.5f, row.Height * 0.5f);
            var hovered = UiInteract.Hover(center - half, center + half);
            var fill = guideFilterFill[index].Step((guideFilters & bit) != 0 ? 1f : 0f, Motion.Appear, delta);
            PrismArt.FactToggle(drawList, center, size, fact, Math.Clamp(fill, 0f, 1f), hovered ? 1f : 0f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (!UiInteract.Click(center - half, center + half, hovered))
            {
                continue;
            }

            var on = (guideFilters & bit) == 0;
            guideFilters = on ? (guideFilters | bit) & ~ExclusiveBit(fact) : guideFilters & ~bit;
            TouchGuide();
            UiFeedback.Play(on ? UiSound.ToggleOn : UiSound.ToggleOff);
        }
    }

    private static int ExclusiveBit(PrismFact fact) => fact switch
    {
        PrismFact.Male => FactBit(PrismFact.Female),
        PrismFact.Female => FactBit(PrismFact.Male),
        PrismFact.Modded => FactBit(PrismFact.Vanilla),
        PrismFact.Vanilla => FactBit(PrismFact.Modded),
        _ => 0,
    };

    private void DrawGuideFinal(ImDrawListPtr drawList, Rect stage, float scale)
    {
        if (store.Week is { Phase: PrismPhases.Entering, Title.Length: > 0 } week)
        {
            DrawGuideTheme(drawList, stage, week, scale);
            return;
        }

        DrawGuideCollage(drawList, stage, scale);
    }

    private void DrawGuideTheme(ImDrawListPtr drawList, Rect stage, PrismWeekDto week, float scale)
    {
        var side = MathF.Min(stage.Height, stage.Width);
        var cardTop = GuideCenterTop(stage, side);
        var card = new Rect(new Vector2(stage.Min.X, cardTop), new Vector2(stage.Max.X, cardTop + side));
        PrismLight.Week(drawList, card, week.Id, PrismArt.Page, PrismArt.Fore, GuideMarkUnits * scale,
            GuideMarkShare);
        Squircle.Stroke(drawList, card.Min, card.Max, GuideCardRadiusUnits * scale,
            ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary)), MathF.Max(1f, scale));
        var pad = Metrics.Space.Lg * scale;
        var room = card.Width - pad * 2f;
        var style = FitStyle(week.Title, room, PrismType.Title);
        var title = Typography.FitText(week.Title, room, style);
        var titleSize = Typography.Measure(title, style);
        var titleTop = card.Min.Y + card.Height * GuideThemeShare - titleSize.Y;
        Typography.Draw(drawList, new Vector2(card.Center.X - titleSize.X * 0.5f, titleTop), title, PrismArt.Fore,
            style);
        var countdownTop = titleTop + titleSize.Y + Metrics.Space.Md * scale;
        var countdown = new Rect(new Vector2(card.Min.X + pad, countdownTop),
            new Vector2(card.Max.X - pad, countdownTop + PrismCountdown.Height(scale)));
        if (countdown.Max.Y <= card.Max.Y)
        {
            guideCountdown.Draw(drawList, countdown, PrismCountdown.Deadline(week), scale);
        }

        if (GuideCardTapped(card))
        {
            TouchGuide();
            UiFeedback.Play(UiSound.Tap);
            BurstFrom(new Vector2(card.Center.X, titleTop));
        }
    }

    private void DrawGuideCollage(ImDrawListPtr drawList, Rect stage, float scale)
    {
        var height = MathF.Min(stage.Height, stage.Width * GuideCollageShare / GuidePhotoAspect);
        var center = GuidePhotoRect(stage, GuideCenterTop(stage, height), height);
        if (center.Height <= 0f)
        {
            return;
        }

        var sideHeight = center.Height * GuideSideShare;
        var sideWidth = sideHeight * GuidePhotoAspect;
        var shift = center.Width * GuideSideShift;
        var sideTop = center.Min.Y + (center.Height - sideHeight) * 0.5f;
        for (var side = 0; side < 2; side++)
        {
            var middle = side == 0
                ? MathF.Max(stage.Min.X + sideWidth * 0.5f, center.Center.X - shift - sideWidth * 0.5f)
                : MathF.Min(stage.Max.X - sideWidth * 0.5f, center.Center.X + shift + sideWidth * 0.5f);
            DrawGuideCard(drawList,
                new Rect(new Vector2(middle - sideWidth * 0.5f, sideTop),
                    new Vector2(middle + sideWidth * 0.5f, sideTop + sideHeight)), GuideExploreSidePictures[side],
                GuideDimAlpha + (1f - GuideDimAlpha) * 0.5f, scale);
        }

        DrawGuideCard(drawList, center, PrismPicture.GuideExploreFirst, 1f, scale);
    }

    private string PrizeLine(long[]? prizes, int index)
    {
        if (prizes is null || index >= prizes.Length || prizes[index] <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(prizes[index], Loc.Current.Code);
        return prizeTexts[index].IsCurrent(key)
            ? prizeTexts[index].Value
            : prizeTexts[index].Store(key, string.Concat("+", prizes[index].ToString("N0", Loc.Culture)));
    }
}
