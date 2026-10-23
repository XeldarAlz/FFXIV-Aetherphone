using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int GuideCopyLines = 2;
    private const float GuideControlsDelay = 0.06f;
    private const float GuideCopyDelay = 0.12f;
    private const float GuideFillDelay = 0.45f;
    private const float GuidePhotoDelay = 0.18f;
    private const float GuideZoom = 0.06f;
    private const float GuideArrowGlyphShare = 0.4f;
    private const float ArrowHoverShare = 0.15f;
    private const float GuideDiptychGapUnits = 2f;
    private const float GuidePieceUnits = 32f;
    private const float GuidePiecesShare = 0.72f;
    private const float GuideScoreUnits = 48f;
    private const float GuideScorePitch = 1.2f;
    private const float GuideNumeralUnits = 120f;
    private const float GuideCountdownSeconds = 1.2f;
    private const float GuideTextShadeUnits = 300f;
    private const float GuideAppIconUnits = 64f;
    private static readonly Vector4 GuideShadeBlack = new(0f, 0f, 0f, 0.85f);
    private const int GuideChosenScore = 4;
    private const long GuideLikes = 128;

    private static readonly LocString[] GuideTitles =
    {
        L.Prism.GuideIntroTitle, L.Prism.GuideTryTitle, L.Prism.GuidePostTitle, L.Prism.GuideWardrobeTitle,
        L.Prism.GuideEnterTitle, L.Prism.JudgeHeat, L.Prism.GuideRevealTitle,
    };

    private static readonly LocString[] GuideBodies =
    {
        L.Prism.GuideIntroBody, L.Prism.GuideTryBody, L.Prism.GuidePostBody, L.Prism.GuideWardrobeBody,
        L.Prism.GuideEnterBody, L.Prism.GuideJudgeBody, L.Prism.GuideRevealBody,
    };

    private static readonly int[] GuidePhotos = { 3, 11, 8, 4, 2, 1 };
    private static readonly int[] GuideCountdown = { 10, 0, 7 };
    private static readonly uint[] GuideGear = { 19139, 0, 11102, 17492, 11116, 17493, 11137, 16039, 12364, 0, 0, 0 };
    private static readonly uint[] GuidePieces = { 6045, 6046, 6047, 6048 };
    private static readonly uint[] GuideOutfit = { 11102, 17492, 17493, 11137 };
    private static readonly int[] GuideOutfitSlots = { 2, 3, 5, 6 };
    private static readonly string PiecesGlyph = IconGlyph.Of(FontAwesomeIcon.LayerGroup);
    private static readonly string NextGlyph = IconGlyph.Of(FontAwesomeIcon.ArrowRight);
    private static readonly string DoneGlyph = IconGlyph.Of(FontAwesomeIcon.Check);

    private readonly CachedText[] prizeTexts = new CachedText[PrismArt.PodiumRanks];
    private readonly GearDraft guideGear = new();
    private CachedText guidePiecesText;
    private Spring guidePiecesReveal;
    private double guideLikedAt;
    private double guideTriedAt;
    private float guideScoredAt;
    private int guideScore;
    private int guidePage;
    private bool guideLiked;
    private bool guideSaved;
    private bool guideWorn;
    private bool guidePiecesOpen;
    private PrismTab guideReturn = PrismTab.Discover;

    private void StartGuide(PrismTab returnTab)
    {
        guidePage = 0;
        guideReturn = returnTab;
        guideLiked = false;
        guideSaved = false;
        guideWorn = false;
        guidePiecesOpen = false;
        guidePiecesReveal.SnapTo(0f);
        guideScore = GuideChosenScore;
        guideScoredAt = GuideFillDelay;
        GuideGear.CopyTo(guideGear.Items, 0);
        Array.Clear(guideGear.Shown);
        pillLabel = string.Empty;
        Go(PrismScreen.Guide);
    }

    private void TurnGuidePage(int delta)
    {
        var next = guidePage + delta;
        if (next < 0 || next >= GuideTitles.Length)
        {
            FinishGuide();
            return;
        }

        guidePage = next;
        guideScoredAt = GuideFillDelay;
        Go(PrismScreen.Guide, delta);
    }

    private void FinishGuide()
    {
        OpenTab(guideReturn);
    }

    private void DrawGuide(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        onPhoto = true;
        PrismArt.OverPhoto(true);
        var left = content.Min.X;
        var right = content.Max.X;
        var tap = Metrics.Size.TapTarget * scale;
        var title = Loc.T(GuideTitles[guidePage]);
        var copy = Loc.T(GuideBodies[guidePage]);
        var titleWidth = content.Width - tap - Metrics.Space.Md * scale;
        var copyHeight = MathF.Max(Typography.MeasureWrappedBlock(copy, PrismType.Body, titleWidth).Y,
            Typography.LineHeight(PrismType.Body) * GuideCopyLines);
        var titleHeight = PrismType.HeroHeight(title, titleWidth);
        var copyTop = content.Max.Y - PrismArt.BottomMargin(scale) - copyHeight;
        var titleTop = copyTop - Metrics.Space.Xs * scale - titleHeight;
        var rowTop = content.Min.Y + (StoryBarGapUnits + StoryRowGapUnits) * scale - Metrics.Space.Xs * scale;
        var stage = new Rect(new Vector2(left, rowTop + tap + Metrics.Space.Lg * scale),
            new Vector2(right, titleTop - Metrics.Space.Xl * scale));
        DrawGuideSlide(drawList, full, stage, scale);

        var text = PrismMotion.Mark(drawList);
        if (guidePage == 0)
        {
            var icon = GuideAppIconUnits * scale;
            var iconMax = new Vector2(left + icon, titleTop - Metrics.Space.Md * scale);
            AppIconTile.TryDraw(drawList, Id, AppAccents.For(Id), iconMax - new Vector2(icon, icon), iconMax,
                icon * Metrics.Radius.TileFactor, 1f, true, scale);
        }

        PrismType.DrawHero(drawList, new Vector2(left, titleTop), title, titleWidth, PrismArt.Fore);
        Typography.DrawWrappedLeft(new Vector2(left, copyTop), copy, PrismArt.Muted, PrismType.Body, titleWidth);
        PrismMotion.Rise(drawList, text, PrismMotion.At(screenAge, GuideCopyDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
        var last = guidePage == GuideTitles.Length - 1;
        if (DrawGuideArrow(drawList, new Vector2(right - tap * 0.5f, titleTop + titleHeight * 0.5f), last, scale))
        {
            TurnGuidePage(1);
            return;
        }

        DrawGuideTop(drawList, content, rowTop, last, scale);
    }

    private void DrawGuideTop(ImDrawListPtr drawList, Rect content, float rowTop, bool last, float scale)
    {
        PrismArt.StoryBars(drawList, content.Min.X, content.Max.X, content.Min.Y + StoryBarGapUnits * scale,
            GuideTitles.Length, guidePage, 1f, scale);
        var tap = Metrics.Size.TapTarget * scale;
        var back = new Rect(new Vector2(content.Min.X, rowTop), new Vector2(content.Min.X + tap, rowTop + tap));
        HoverTooltip.Show("prism.guide.back", back, Loc.T(L.Prism.Back));
        if (BackButton.Draw("prism.guide.back", back.Center, BackChevronUnits * scale, PrismArt.Fore,
                UiInteract.Hover(back.Min, back.Max), scale))
        {
            TurnGuidePage(-1);
            return;
        }

        var skip = Loc.T(L.Prism.Skip);
        if (!last && DrawButton(drawList,
                PlainRect(new Vector2(content.Max.X, back.Center.Y), skip, content.Width * 0.5f, 1f, scale), skip,
                ButtonStyle.Plain, scale, ink: photoControls))
        {
            FinishGuide();
        }
    }

    private bool DrawGuideArrow(ImDrawListPtr drawList, Vector2 center, bool last, float scale) =>
        DrawRoundAction(drawList, center, last ? DoneGlyph : NextGlyph,
            Loc.T(last ? L.Prism.GetStarted : L.Common.Next), true, string.Empty, photoControls, scale, false);

    private bool DrawRoundAction(ImDrawListPtr drawList, Vector2 center, string glyph, string label, bool enabled,
        string busy, in ControlInk ink, float scale, bool labelled = true)
    {
        var radius = Metrics.Size.TapTarget * 0.5f * scale;
        var rect = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var key = ImGui.GetID(label);
        var over = UiInteract.Hover(rect.Min, rect.Max);
        var hovered = enabled && over;
        var face = RoundButton.Surface(drawList, rect, ink, ButtonStyle.Solid, enabled, hovered, key,
            hovered ? Vector4.Lerp(PrismArt.Fore, PrismArt.OnFore, ArrowHoverShare) : PrismArt.Fore);
        PhoneIcon.Draw(drawList, face.Face.Center, glyph, PrismArt.OnFore, face.Face.Width * GuideArrowGlyphShare);
        if (busy.Length > 0)
        {
            ProgressRing.Sweep(center, radius - Metrics.Stroke.Ring * scale, Metrics.Stroke.Ring * scale,
                PrismArt.OnFore, HeaderSweepMs, HeaderSweepArc, 1f, drawList);
        }

        if (labelled || busy.Length > 0)
        {
            HoverTooltip.Show(key, rect, busy.Length > 0 ? busy : label, over, HoverLabelSide.Above);
        }

        return hovered && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void DrawGuideSlide(ImDrawListPtr drawList, Rect full, Rect stage, float scale)
    {
        switch (guidePage)
        {
            case 0:
                DrawGuidePhoto(drawList, full, GuidePhotos[0], 1f, 0f, scale);
                PrismArt.Refraction(drawList, full, PrismMotion.At(screenAge, GuidePhotoDelay, Motion.Sheet));
                GuideShade(drawList, full, stage.Max.Y, scale);
                break;
            case 1:
                DrawTrySlide(drawList, full, stage, scale);
                break;
            case 2:
                DrawPostSlide(drawList, full, stage, scale);
                break;
            case 3:
                DrawWardrobeSlide(drawList, full, stage, scale);
                break;
            case 4:
                DrawEnterSlide(drawList, full, stage, scale);
                break;
            case 5:
                DrawJudgeSlide(drawList, full, stage, scale);
                break;
            default:
                DrawRevealSlide(drawList, full, stage, scale);
                break;
        }
    }

    private void DrawGuidePhoto(ImDrawListPtr drawList, Rect frame, int index, float alpha, float start, float scale)
    {
        drawList.PushClipRect(frame.Min, frame.Max, true);
        var mark = PrismMotion.Mark(drawList);
        if (!PrismStock.Draw(drawList, frame, index, alpha))
        {
            PrismArt.Placeholder(drawList, frame, false, scale, alpha);
        }

        PrismMotion.Grow(drawList, mark, frame.Center,
            1f + GuideZoom * (1f - PrismMotion.At(screenAge, start, Motion.Sheet)));
        drawList.PopClipRect();
    }

    private static void GuideShade(ImDrawListPtr drawList, Rect full, float top, float scale)
    {
        PrismArt.TopFog(drawList, full, scale);
        PrismArt.Scrim(drawList, full, full.Max.Y - top, scale);
        var clear = ImGui.GetColorU32(GuideShadeBlack with { W = 0f });
        var black = ImGui.GetColorU32(GuideShadeBlack);
        drawList.AddRectFilledMultiColor(
            new Vector2(full.Min.X, MathF.Max(full.Min.Y, full.Max.Y - GuideTextShadeUnits * scale)), full.Max,
            clear, clear, black, black);
    }

    private void GuideRise(ImDrawListPtr drawList, int mark, float scale) =>
        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, GuideControlsDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);

    private void DrawTrySlide(ImDrawListPtr drawList, Rect full, Rect stage, float scale)
    {
        DrawGuidePhoto(drawList, full, GuidePhotos[1], 1f, 0f, scale);
        var rowTop = stage.Max.Y - Metrics.Size.TapTarget * scale;
        var chipBottom = rowTop - Metrics.Space.Md * scale;
        var chipTop = chipBottom - PrismArt.ChipUnits * scale;
        GuideShade(drawList, full, chipTop, scale);
        var tried = (float)(ImGui.GetTime() - guideTriedAt);
        if (guideWorn)
        {
            PrismArt.TryOnMoment(drawList, full, PhoneIcons.Wand, Loc.T(L.Prism.OnYourCharacter), tried, scale);
        }

        var liked = (float)(ImGui.GetTime() - guideLikedAt);
        if (guideLiked && liked < LikeBloomSeconds)
        {
            DrawLikeBloom(drawList, new Rect(new Vector2(full.Min.X, stage.Min.Y), new Vector2(full.Max.X, chipTop)),
                liked, scale);
        }

        var mark = PrismMotion.Mark(drawList);
        DrawGuidePieces(drawList, stage.Min.X, chipBottom, stage.Width, scale);
        DrawGuideActions(drawList, stage, rowTop, tried, scale);
        GuideRise(drawList, mark, scale);
    }

    private void DrawGuideActions(ImDrawListPtr drawList, Rect stage, float rowTop, float tried, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var radius = tap * 0.5f;
        var middle = rowTop + radius;
        var heart = new Vector2(stage.Min.X + radius * 0.5f, middle);
        var stamp = PrismMotion.Mark(drawList);
        if (RoundButton.Icon(drawList, heart, radius, guideLiked ? PhoneIcons.HeartFilled : PhoneIcons.Heart,
                photoControls, ButtonStyle.Bare, Loc.T(L.Prism.Like), HoverLabelSide.Below))
        {
            guideLiked = !guideLiked;
            guideLikedAt = ImGui.GetTime();
        }

        var age = (float)(ImGui.GetTime() - guideLikedAt);
        if (guideLiked && age < LikeBloomSeconds)
        {
            PrismMotion.Grow(drawList, stamp, heart,
                1f + LikeStamp * (1f - PrismMotion.At(age, 0f, Motion.PageSettle)));
            DrawLikeRing(drawList, heart, radius * 0.5f, age, scale);
        }

        var count = NumberText.Compact(GuideLikes + (guideLiked ? 1 : 0));
        var countSize = Typography.Measure(count, PrismType.Body);
        Typography.Draw(drawList,
            new Vector2(heart.X + radius * 0.5f + Metrics.Space.Xs * scale, middle - countSize.Y * 0.5f), count,
            PrismArt.Fore, PrismType.Body);
        var saveOffset = radius * 1.5f + Metrics.Space.Xs * 2f * scale +
                         Typography.Measure(LikeSlotTemplate, PrismType.Body).X;
        if (RoundButton.Icon(drawList, new Vector2(stage.Min.X + saveOffset, middle), radius,
                guideSaved ? PhoneIcons.BookmarkFilled : PhoneIcons.Bookmark, photoControls, ButtonStyle.Bare,
                Loc.T(guideSaved ? L.Prism.Saved : L.Prism.Save), HoverLabelSide.Below))
        {
            guideSaved = !guideSaved;
        }

        var tryOn = Loc.T(L.Prism.TryOnGlam);
        var wearing = Loc.T(L.Prism.Wearing);
        var cap = stage.Width - saveOffset - radius - Metrics.Space.Md * scale;
        var check = Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xs * scale;
        var width = MathF.Min(ButtonWidth([tryOn, wearing], cap, scale) + check, cap);
        var settling = guideWorn && tried < PrismArt.TryOnSeconds;
        var worn = guideWorn && !settling;
        var pill = new Rect(new Vector2(stage.Max.X - width, rowTop), new Vector2(stage.Max.X, rowTop + tap));
        if (worn)
        {
            if (DrawWornLabel(drawList, pill, wearing,
                    PrismMotion.At(tried, PrismArt.TryOnSeconds, Motion.Appear), scale))
            {
                guideWorn = false;
                guideTriedAt = ImGui.GetTime();
            }

            return;
        }

        if (DrawPill(drawList, pill, tryOn, false, false, settling, scale))
        {
            guideWorn = true;
            guidePiecesOpen = false;
            guideTriedAt = ImGui.GetTime();
        }
    }

    private static bool DrawWornLabel(ImDrawListPtr drawList, Rect rect, string label, float alpha, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var ink = hovered ? PrismArt.Muted : PrismArt.Fore;
        var glyph = Typography.LineHeight(PrismType.Caption);
        var gap = Metrics.Space.Xs * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, rect.Width - glyph - gap), PrismType.Headline);
        var size = Typography.Measure(fitted, PrismType.Headline);
        var textLeft = rect.Max.X - size.X;
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

    private void DrawGuidePieces(ImDrawListPtr drawList, float left, float chipBottom, float width, float scale)
    {
        if (PrismArt.PhotoChip(drawList, new Vector2(left, chipBottom), width * 0.5f, PiecesGlyph, GuidePieceCount(),
                guidePiecesOpen, scale))
        {
            guidePiecesOpen = !guidePiecesOpen;
        }

        var reveal = Math.Clamp(guidePiecesReveal.Step(guidePiecesOpen ? 1f : 0f, Motion.Appear,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds)), 0f, 1f);
        if (reveal <= PrismMotion.GrowEpsilon)
        {
            return;
        }

        var pad = Metrics.Space.Sm * scale;
        var icon = GuidePieceUnits * scale;
        var gap = Metrics.Space.Xs * scale;
        var bottom = chipBottom - (PrismArt.ChipUnits + Metrics.Space.Sm) * scale;
        var card = new Rect(
            new Vector2(left, bottom - pad * 2f - icon * GuidePieces.Length - gap * (GuidePieces.Length - 1)),
            new Vector2(left + width * GuidePiecesShare, bottom));
        var mark = PrismMotion.Mark(drawList);
        drawList.AddRectFilled(card.Min, card.Max, CardFill, Metrics.Radius.Md * scale);
        var line = Typography.LineHeight(PrismType.Body);
        for (var index = 0; index < GuidePieces.Length; index++)
        {
            if (!GearItem(GuidePieces[index], out var name, out var iconId))
            {
                continue;
            }

            var tile = new Vector2(card.Min.X + pad, card.Min.Y + pad + index * (icon + gap));
            GameIconTile.Draw(drawList, textures, iconId, tile, tile + new Vector2(icon, icon),
                Metrics.Radius.Sm * scale, scale);
            var textLeft = tile.X + icon + Metrics.Space.Sm * scale;
            Typography.Draw(drawList, new Vector2(textLeft, tile.Y + (icon - line) * 0.5f),
                Typography.FitText(name, MathF.Max(1f, card.Max.X - pad - textLeft), PrismType.Body),
                PrismArt.PhotoFore, PrismType.Body);
        }

        if (reveal < PrismMotion.Settled)
        {
            PrismMotion.Shift(drawList, mark, new Vector2(0f, PieceSlideUnits * scale * (1f - reveal)), reveal);
        }
    }

    private string GuidePieceCount()
    {
        var key = HashCode.Combine(GuidePieces.Length, Loc.Current.Code);
        return guidePiecesText.IsCurrent(key)
            ? guidePiecesText.Value
            : guidePiecesText.Store(key, Loc.Plural(L.Prism.PieceCount, GuidePieces.Length));
    }

    private void DrawPostSlide(ImDrawListPtr drawList, Rect full, Rect stage, float scale)
    {
        var half = (full.Width - GuideDiptychGapUnits * scale) * 0.5f;
        DrawGuidePhoto(drawList, new Rect(full.Min, new Vector2(full.Min.X + half, full.Max.Y)), GuidePhotos[2], 1f,
            0f, scale);
        DrawGuidePhoto(drawList, new Rect(new Vector2(full.Max.X - half, full.Min.Y), full.Max), GuidePhotos[2] + 1,
            PrismMotion.At(screenAge, GuidePhotoDelay, Motion.Appear), GuidePhotoDelay, scale);
        var pad = Metrics.Space.Md * scale;
        var gridWidth = stage.Width - pad * 2f;
        var panel = new Rect(
            new Vector2(stage.Min.X, stage.Max.Y - GearGridHeight(guideGear.Items.Length, gridWidth, scale) - pad * 2f),
            stage.Max);
        GuideShade(drawList, full, panel.Min.Y, scale);
        var mark = PrismMotion.Mark(drawList);
        PrismArt.PhotoPanel(drawList, panel, Metrics.Radius.Lg * scale);
        DrawGearGrid(drawList, guideGear, panel.Min.X + pad, panel.Min.Y + pad, gridWidth, scale);
        for (var index = 0; index < GuideGear.Length; index++)
        {
            if (guideGear.Shown[index] && guideGear.Items[index] == 0)
            {
                guideGear.Items[index] = GuideGear[index];
            }
        }

        GuideRise(drawList, mark, scale);
    }

    private void DrawWardrobeSlide(ImDrawListPtr drawList, Rect full, Rect stage, float scale)
    {
        DrawGuidePhoto(drawList, full, GuidePhotos[3], 1f, 0f, scale);
        var pad = Metrics.Space.Md * scale;
        var gap = Metrics.Space.Sm * scale;
        var icon = GearIconUnits * scale;
        var captionLine = Typography.LineHeight(PrismType.Caption);
        var nameLine = Typography.LineHeight(PrismType.Headline);
        var row = MathF.Max(icon, captionLine + nameLine);
        var card = new Rect(
            new Vector2(stage.Min.X,
                stage.Max.Y - pad * 2f - captionLine - (row + gap) * GuideOutfit.Length),
            stage.Max);
        GuideShade(drawList, full, card.Min.Y, scale);
        var mark = PrismMotion.Mark(drawList);
        PrismArt.PhotoPanel(drawList, card, Metrics.Radius.Lg * scale);
        var left = card.Min.X + pad;
        var glyph = PrismArt.InlineIconUnits * scale;
        PhoneIcon.Draw(drawList, new Vector2(left + glyph * 0.5f, card.Min.Y + pad + captionLine * 0.5f),
            PhoneIcons.Shirt, PrismArt.Muted, glyph);
        Typography.Draw(drawList, new Vector2(left + glyph + Metrics.Space.Xs * scale, card.Min.Y + pad),
            Loc.T(L.Prism.InWardrobe), PrismArt.Muted, PrismType.Caption);
        var textLeft = left + icon + Metrics.Space.Md * scale;
        var room = MathF.Max(1f, card.Max.X - pad - textLeft);
        for (var index = 0; index < GuideOutfit.Length; index++)
        {
            if (!GearItem(GuideOutfit[index], out var name, out var iconId))
            {
                continue;
            }

            var top = card.Min.Y + pad + captionLine + gap + index * (row + gap);
            var rowMark = PrismMotion.Mark(drawList);
            var tile = new Vector2(left, top + (row - icon) * 0.5f);
            GameIconTile.Draw(drawList, textures, iconId, tile, tile + new Vector2(icon, icon),
                Metrics.Radius.Sm * scale, scale);
            var textTop = top + (row - captionLine - nameLine) * 0.5f;
            Typography.Draw(drawList, new Vector2(textLeft, textTop),
                Typography.FitText(Loc.T(SlotNames[GuideOutfitSlots[index]]), room, PrismType.Caption),
                PrismArt.Muted, PrismType.Caption);
            Typography.Draw(drawList, new Vector2(textLeft, textTop + captionLine),
                Typography.FitText(name, room, PrismType.Headline), PrismArt.Fore, PrismType.Headline);
            PrismMotion.Rise(drawList, rowMark,
                PrismMotion.At(screenAge, GuidePhotoDelay + index * PrismMotion.Stagger, Motion.PageSettle),
                PrismMotion.RiseUnits * scale);
        }

        GuideRise(drawList, mark, scale);
    }

    private void DrawEnterSlide(ImDrawListPtr drawList, Rect full, Rect stage, float scale)
    {
        DrawGuidePhoto(drawList, full, GuidePhotos[4], 1f, 0f, scale);
        if (store.Week is not { Title.Length: > 0 } week)
        {
            GuideShade(drawList, full, stage.Max.Y, scale);
            return;
        }

        var pad = Metrics.Space.Md * scale;
        var captionLine = Typography.LineHeight(PrismType.Caption);
        var card = new Rect(
            new Vector2(stage.Min.X,
                stage.Max.Y - pad * 2f - captionLine - Typography.LineHeight(PrismType.Headline)),
            stage.Max);
        GuideShade(drawList, full, card.Min.Y, scale);
        var mark = PrismMotion.Mark(drawList);
        PrismArt.PhotoPanel(drawList, card, Metrics.Radius.Lg * scale);
        var room = card.Width - pad * 2f;
        Typography.Draw(drawList, card.Min + new Vector2(pad, pad),
            Typography.FitText(Loc.T(L.Prism.ThemeLabel), room, PrismType.Caption), PrismArt.Muted,
            PrismType.Caption);
        Typography.Draw(drawList, card.Min + new Vector2(pad, pad + captionLine),
            Typography.FitText(week.Title, room, PrismType.Headline), PrismArt.Fore, PrismType.Headline);
        GuideRise(drawList, mark, scale);
    }

    private void DrawJudgeSlide(ImDrawListPtr drawList, Rect full, Rect stage, float scale)
    {
        DrawGuidePhoto(drawList, full, GuidePhotos[5], 1f, 0f, scale);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var gap = Metrics.Space.Sm * scale;
        var size = MathF.Min(GuideScoreUnits * scale, stage.Width / (PrismArt.Stars * GuideScorePitch));
        var top = stage.Max.Y - labelLine - gap - size;
        var row = new Rect(new Vector2(stage.Min.X, top), new Vector2(stage.Max.X, top + size));
        GuideShade(drawList, full, top, scale);
        var mark = PrismMotion.Mark(drawList);
        var picked = DrawScoreButtons(drawList, row, guideScore,
            PrismMotion.At(screenAge, guideScoredAt, Motion.Release), true, scale);
        if (picked > 0)
        {
            guideScore = picked;
            guideScoredAt = screenAge;
        }

        var anchors = row.Max.Y + gap;
        PrismType.Draw(drawList, new Vector2(row.Min.X, anchors), Loc.T(L.Prism.ScoreLow), PrismArt.Muted,
            PrismFace.Label, labelPixels);
        PrismType.DrawRight(drawList, new Vector2(row.Max.X, anchors), Loc.T(L.Prism.ScoreHigh), PrismArt.Muted,
            PrismFace.Label, labelPixels);
        GuideRise(drawList, mark, scale);
    }

    private void DrawRevealSlide(ImDrawListPtr drawList, Rect full, Rect stage, float scale)
    {
        var step = Math.Clamp((int)(screenAge / GuideCountdownSeconds), 0, GuideCountdown.Length - 1);
        var start = step * GuideCountdownSeconds;
        if (step > 0)
        {
            DrawGuidePhoto(drawList, full, GuideCountdown[step - 1], 1f, start - GuideCountdownSeconds, scale);
        }

        DrawGuidePhoto(drawList, full, GuideCountdown[step],
            step > 0 ? PrismMotion.At(screenAge, start, Motion.Appear) : 1f, start, scale);
        var place = GuideCountdown.Length - step;
        if (place == 1)
        {
            PrismArt.Refraction(drawList, full, PrismMotion.At(screenAge, start, Motion.Sheet));
        }

        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var pixels = GuideNumeralUnits * scale;
        var numeralHeight = PrismType.Snap(PrismFace.Numerals, pixels);
        var numeralTop = stage.Max.Y - numeralHeight;
        GuideShade(drawList, full, numeralTop, scale);
        var left = stage.Min.X;
        DrawWipe(drawList, new Vector2(left, numeralTop), Plain[place], PrismFace.Numerals, pixels,
            PrismMotion.At(screenAge, start, Motion.PageSettle), PrismArt.Fore);
        var numeralWidth = 0f;
        for (var rank = 1; rank <= GuideCountdown.Length; rank++)
        {
            numeralWidth = MathF.Max(numeralWidth, PrismType.Measure(Plain[rank], PrismFace.Numerals, pixels).X);
        }

        var columnLeft = left + numeralWidth + Metrics.Space.Lg * scale;
        var prizeTop = numeralTop + numeralHeight - labelLine - Metrics.Space.Lg * scale;
        var labelMark = PrismMotion.Mark(drawList);
        PrismType.Draw(drawList, new Vector2(columnLeft, prizeTop - labelLine - Metrics.Space.Xxs * scale),
            Loc.T(L.Prism.PlaceLabel), PrismArt.Muted, PrismFace.Label, labelPixels);
        PrismMotion.Rise(drawList, labelMark, PrismMotion.At(screenAge, GuideControlsDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
        var prize = PrizeLine(store.Week?.Prizes?.Final, place - 1);
        if (prize.Length == 0)
        {
            return;
        }

        var glyph = labelLine * CurrencyGlyph.GlyphFraction;
        var mark = PrismMotion.Mark(drawList);
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins,
            new Vector2(columnLeft + glyph * 0.5f, prizeTop + labelLine * 0.5f), glyph);
        PrismType.Draw(drawList, new Vector2(columnLeft + CurrencyGlyph.Reserve(labelLine), prizeTop), prize,
            PrismArt.Fore, PrismFace.Label, labelPixels);
        PrismMotion.Shift(drawList, mark, Vector2.Zero,
            PrismMotion.At(screenAge, start + GuidePhotoDelay, Motion.Appear));
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
