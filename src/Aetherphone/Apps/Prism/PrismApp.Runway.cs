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
    private const float ScoreButtonUnits = 56f;
    private const float ScoreDone = 0.97f;
    private const float ScorePitchLow = 0.84f;
    private const float ScorePitchStep = 0.08f;
    private const float TurnShift = 0.35f;
    private const float TurnIdle = 10f;
    private const float DoneTextDelay = 0.12f;
    private const float InspectZoom = 2.2f;
    private const float InspectHoldSeconds = 0.18f;
    private const int PicksColumns = 3;
    private const float PickPillUnits = 20f;
    private const float PickStarUnits = 11f;
    private const float PodiumLeadShare = 0.38f;
    private const int OutcomePending = 0;
    private const int OutcomeSent = 1;
    private const int OutcomeFailed = 2;

    private static readonly string[] Plain = BuildDigits("0");
    private static readonly uint PickPillFill = Palette.Pack(new Vector4(0f, 0f, 0f, 0.55f));
    private static readonly uint PickHoverWash = Palette.Pack(new Vector4(1f, 1f, 1f, 0.12f));

    private readonly ActionSheet lookMenu = new();
    private readonly ActionSheet.Item[] lookMenuItems = new ActionSheet.Item[1];

    private PrismStarDto[] stars = Array.Empty<PrismStarDto>();
    private int[] pickOrder = Array.Empty<int>();
    private int[] pickFrom = Array.Empty<int>();
    private PrismBallotDto? ratingBallot;
    private int lookIndex;
    private int chosen;
    private float lookSeconds;
    private bool reviewing;
    private bool heatDone;
    private volatile int ballotOutcome;
    private string previousLookUrl = string.Empty;
    private string lookMenuId = string.Empty;
    private float turnAge = TurnIdle;
    private float pickSwapAge = TurnIdle;
    private int turnSign = 1;
    private Spring inspectZoom = new(0f);
    private float inspectHold;
    private Vector2 inspectFocus;
    private Spring scoreFill = new(0f);
    private long heatCoins;
    private CachedText lookCounter;
    private CachedText rateCaption;
    private CachedText picksCaption;
    private CachedText heatTitle;
    private CachedText coinCounter;
    private CachedText rewardText;

    private bool HeatInProgress => store.Ballot is { } ballot && ReferenceEquals(ratingBallot, ballot) &&
                                   stars.Length > 0 && stars[0] is not null && lookIndex <= stars.Length;

    private void StartJudging()
    {
        heatDone = false;
        chosen = 0;
        ballotOutcome = OutcomePending;
        if (!HeatInProgress)
        {
            ratingBallot = null;
            store.ClearBallot();
            store.RequestBallot();
        }

        Go(PrismScreen.Runway);
    }

    private void DrawRunway(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        ConsumeBallotOutcome();
        if (heatDone)
        {
            DrawHeatDone(drawList, content, scale);
            return;
        }

        if (store.Ballot is not { } ballot)
        {
            DrawBallotPending(drawList, content, DrawBackHeader(drawList, content, string.Empty, scale), scale);
            return;
        }

        var looks = ballot.Looks ?? Array.Empty<PrismLookDto>();
        if (looks.Length == 0)
        {
            DrawRunwayNotice(drawList, content, DrawBackHeader(drawList, content, string.Empty, scale),
                Loc.T(L.Prism.NothingLeftTitle), Loc.T(L.Prism.NothingLeftHint), scale);
            return;
        }

        if (!ReferenceEquals(ratingBallot, ballot))
        {
            StartHeat(ballot, looks.Length);
        }

        if (lookIndex >= looks.Length)
        {
            DrawPicks(drawList, content, ballot, looks, scale);
            return;
        }

        DrawLookToRate(drawList, full, content, looks, scale);
    }

    private void StartHeat(PrismBallotDto ballot, int count)
    {
        ratingBallot = ballot;
        stars = new PrismStarDto[count];
        pickOrder = new int[count];
        pickFrom = new int[count];
        lookIndex = 0;
        chosen = 0;
        lookSeconds = 0f;
        reviewing = false;
        previousLookUrl = string.Empty;
        turnAge = TurnIdle;
    }

    private void DrawLookToRate(ImDrawListPtr drawList, Rect full, Rect content, PrismLookDto[] looks, float scale)
    {
        lookMenu.Gate();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        turnAge += delta;
        var look = looks[lookIndex];
        if (chosen == 0 && !lookMenu.CapturesPointer && loadedAt.ContainsKey(look.ImageUrl))
        {
            lookSeconds += delta;
        }

        var left = content.Min.X;
        var width = content.Width;
        var captionLine = Typography.LineHeight(PrismType.Caption);
        var size = MathF.Max(Metrics.Size.TapTarget * scale,
            MathF.Min(ScoreButtonUnits * scale,
                (width - Metrics.Space.Sm * scale * (PrismArt.Stars - 1)) / PrismArt.Stars));
        var question = Loc.T(L.Prism.RateHint);
        var questionHeight = Typography.MeasureWrappedBlock(question, PrismType.Title, width).Y;
        var below = Metrics.Space.Lg * scale * 3f + captionLine * 2f + Metrics.Space.Xxs * scale + questionHeight +
                    size + Metrics.Space.Xs * scale;
        var photoTop = PhotoTop(full, content);
        var blockTop = MathF.Max(photoTop, content.Max.Y - below);
        var photo = new Rect(new Vector2(full.Min.X, photoTop),
            new Vector2(full.Max.X, MathF.Min(photoTop + full.Width / PrismStore.EntryRatio, blockTop)));
        var captionTop = blockTop + Metrics.Space.Lg * scale;
        var questionTop = captionTop + captionLine + Metrics.Space.Xxs * scale;
        var rowTop = questionTop + questionHeight + Metrics.Space.Lg * scale;
        var row = new Rect(new Vector2(left, rowTop), new Vector2(content.Max.X, rowTop + size));
        var anchorsTop = row.Max.Y + Metrics.Space.Xs * scale;
        DrawTurningLook(drawList, photo, look, PrismMotion.At(turnAge, 0f, Motion.PageSettle), delta);
        DrawRunwayChrome(drawList, photo, PhotoInset(full, content), looks.Length, look.Id, scale);
        var previous = Loc.T(L.Prism.PreviousGlam);
        var back = PlainRect(new Vector2(content.Max.X, captionTop + captionLine * 0.5f), previous, width * 0.5f, 1f,
            scale);
        var caption = Typography.FitText(RateCaption(lookIndex + 1, looks.Length),
            MathF.Max(1f, back.Min.X - left), PrismType.Caption);
        Typography.Draw(drawList, new Vector2(left, captionTop), caption, PrismArt.Muted, PrismType.Caption);
        if (!reviewing && lookIndex > 0 && chosen == 0 && !store.Busy &&
            DrawButton(drawList, back, previous, ButtonStyle.Plain, scale))
        {
            StepTo(lookIndex - 1, -1);
        }

        Typography.DrawWrappedLeft(new Vector2(left, questionTop), question, PrismArt.Fore, PrismType.Title, width);
        var fill = chosen > 0 ? scoreFill.Step(1f, Motion.Release, delta) : 0f;
        var picked = DrawScoreButtons(drawList, row, chosen, fill, !store.Busy, scale);
        if (picked > 0 && picked != chosen)
        {
            chosen = picked;
            scoreFill.SnapTo(0f);
        }

        Typography.Draw(drawList, new Vector2(left, anchorsTop), Loc.T(L.Prism.ScoreLow), PrismArt.Muted,
            PrismType.Caption);
        var high = Loc.T(L.Prism.ScoreHigh);
        Typography.Draw(drawList,
            new Vector2(content.Max.X - Typography.Measure(high, PrismType.Caption).X, anchorsTop), high,
            PrismArt.Muted, PrismType.Caption);
        DrawLookMenu(full);
        if (chosen > 0 && fill >= ScoreDone)
        {
            Rate(look, chosen);
        }
    }

    private void DrawRunwayChrome(ImDrawListPtr drawList, Rect photo, float topInset, int total, string lookId,
        float scale)
    {
        PrismArt.TopFog(drawList, photo, scale);
        var reach = Metrics.Size.TapTarget * 0.5f * scale;
        var side = (Metrics.Space.Md + Metrics.Size.GlassButton * 0.5f + Metrics.Space.Xs) * scale + reach;
        var barsTop = photo.Min.Y + topInset + Metrics.Space.Xs * scale + Metrics.Size.GlassButton * 0.5f * scale;
        PrismArt.OverPhoto(true);
        PrismArt.StoryBars(drawList, photo.Min.X + side, photo.Max.X - side, barsTop, total, lookIndex, 1f, scale);
        PrismArt.OverPhoto(false);
        if (DrawPhotoButton(drawList, photo, topInset, 0, PhoneIcons.ChevronLeft, Loc.T(L.Prism.Back), scale))
        {
            if (reviewing)
            {
                ShowPicks(-1);
            }
            else
            {
                LeaveScreen();
            }
        }

        if (DrawPhotoButton(drawList, photo, topInset, 1, PhoneIcons.Dots, Loc.T(L.Prism.ReportGlam), scale))
        {
            lookMenuId = lookId;
            lookMenuItems[0] = new ActionSheet.Item(Loc.T(L.Prism.ReportGlam), PhoneIcons.Flag, true);
            lookMenu.Open();
        }
    }

    private void DrawLookMenu(Rect full)
    {
        if (!lookMenu.CapturesPointer)
        {
            return;
        }

        if (lookMenu.Draw(full, ActionSheetStyle.From(ui), lookMenuItems, Loc.T(L.Common.Cancel),
                false) == 0)
        {
            OpenReport(lookMenuId);
        }
    }

    private void DrawTurningLook(ImDrawListPtr drawList, Rect photo, PrismLookDto look, float turn, float delta)
    {
        var shift = new Vector2(photo.Width * TurnShift * turnSign, 0f);
        drawList.PushClipRect(photo.Min, photo.Max, true);
        if (turn < PrismMotion.Settled && previousLookUrl.Length > 0)
        {
            DrawLook(drawList, new Rect(photo.Min - shift * turn, photo.Max - shift * turn), previousLookUrl, false,
                1f - turn, native: true);
        }

        var holding = turn >= PrismMotion.Settled && ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
                      UiInteract.Hover(photo.Min, photo.Max);
        inspectHold = holding ? inspectHold + delta : 0f;
        if (inspectHold >= InspectHoldSeconds)
        {
            inspectFocus = Vector2.Clamp(ImGui.GetMousePos(), photo.Min, photo.Max);
        }

        var inspect = inspectZoom.Step(inspectHold >= InspectHoldSeconds ? 1f : 0f, Motion.PageSettle, delta);
        var incoming = shift * (1f - turn);
        DrawLook(drawList, new Rect(photo.Min + incoming, photo.Max + incoming), look.ImageUrl, false, turn,
            1f + (InspectZoom - 1f) * inspect, inspectFocus, true);
        drawList.PopClipRect();
    }

    private int DrawScoreButtons(ImDrawListPtr drawList, Rect row, int selected, float fill, bool enabled, float scale)
    {
        var size = row.Height;
        var gap = (row.Width - size * PrismArt.Stars) / (PrismArt.Stars - 1);
        var idle = ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Secondary));
        var hover = ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Primary));
        var chosenFill = ImGui.GetColorU32(PrismArt.Fore);
        var picked = 0;
        for (var index = 0; index < PrismArt.Stars; index++)
        {
            var min = new Vector2(row.Min.X + index * (size + gap), row.Min.Y);
            var max = new Vector2(min.X + size, row.Max.Y);
            var center = (min + max) * 0.5f;
            var hovered = enabled && UiInteract.Hover(min, max);
            var share = selected == index + 1 ? fill : 0f;
            drawList.AddCircleFilled(center, size * 0.5f, hovered ? hover : idle);
            if (share > 0f)
            {
                drawList.AddCircleFilled(center, size * 0.5f * share, chosenFill);
            }

            var digit = Plain[index + 1];
            Typography.Draw(drawList, center - Typography.Measure(digit, PrismType.Headline) * 0.5f, digit,
                Vector4.Lerp(PrismArt.Fore, PrismArt.OnFore, share), PrismType.Headline);
            if (!hovered)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (UiInteract.Click(min, max, true, (UiSound?)null))
            {
                UiFeedback.PlayPitched(UiSound.Tap, ScorePitchLow + ScorePitchStep * index);
                picked = index + 1;
            }
        }

        return picked;
    }

    private void Rate(PrismLookDto look, int value)
    {
        stars[lookIndex] = new PrismStarDto(look.Id, value, (int)(lookSeconds * 1000f));
        chosen = 0;
        lookSeconds = 0f;
        if (reviewing)
        {
            SortPicks(true);
            ShowPicks(-1);
            return;
        }

        previousLookUrl = look.ImageUrl;
        turnAge = 0f;
        turnSign = 1;
        lookIndex++;
        if (lookIndex < stars.Length)
        {
            lookSeconds = SavedSeconds(lookIndex);
            return;
        }

        reviewing = true;
        SortPicks(false);
        ShowPicks(1);
    }

    private void StepTo(int index, int direction)
    {
        previousLookUrl = ratingBallot?.Looks is { } looks && lookIndex < looks.Length
            ? looks[lookIndex].ImageUrl
            : string.Empty;
        lookIndex = index;
        chosen = 0;
        scoreFill.SnapTo(0f);
        lookSeconds = SavedSeconds(index);
        turnAge = previousLookUrl.Length > 0 ? 0f : TurnIdle;
        turnSign = direction;
    }

    private float SavedSeconds(int index) => stars[index] is { } star ? star.ViewedMs / 1000f : 0f;

    private void ShowPicks(int direction)
    {
        lookIndex = stars.Length;
        chosen = 0;
        Go(PrismScreen.Runway, direction);
    }

    private void SortPicks(bool crossfade)
    {
        for (var index = 0; index < pickOrder.Length; index++)
        {
            pickFrom[index] = pickOrder[index];
            pickOrder[index] = index;
        }

        for (var index = 1; index < pickOrder.Length; index++)
        {
            var look = pickOrder[index];
            var score = StarsOf(look);
            var slot = index;
            while (slot > 0 && StarsOf(pickOrder[slot - 1]) < score)
            {
                pickOrder[slot] = pickOrder[slot - 1];
                slot--;
            }

            pickOrder[slot] = look;
        }

        if (crossfade)
        {
            pickSwapAge = 0f;
            return;
        }

        Array.Copy(pickOrder, pickFrom, pickOrder.Length);
    }

    private int StarsOf(int index) => stars[index]?.Stars ?? 0;

    private void DrawPicks(ImDrawListPtr drawList, Rect content, PrismBallotDto ballot, PrismLookDto[] looks,
        float scale)
    {
        pickSwapAge += MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var left = content.Min.X;
        var width = content.Width;
        var top = (store.Busy
            ? content.Min.Y + Metrics.Size.TapTarget * scale
            : DrawBackHeader(drawList, content, string.Empty, scale)) + Metrics.Space.Sm * scale;
        var caption = PicksCaption(ballot);
        if (caption.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(caption, width, PrismType.Caption),
                PrismArt.Muted, PrismType.Caption);
            top += Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xxs * scale;
        }

        top += PrismType.DrawHero(drawList, new Vector2(left, top), Loc.T(L.Prism.YourPicks), width, PrismArt.Fore) +
               Metrics.Space.Xs * scale;
        top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.PicksHint), PrismArt.Muted,
            PrismType.Caption, width) + Metrics.Space.Xl * scale;
        var send = ActionRect(content, scale);
        var gridBottom = send.Min.Y - Metrics.Space.Lg * scale;
        if (store.Failed(PrismAction.Submit))
        {
            var failure = store.ActionFailureText;
            var error = failure.Length > 0 ? failure : Loc.T(L.Common.LoadFailedHint);
            var errorTop = gridBottom - Typography.MeasureWrappedBlock(error, PrismType.Caption, width).Y;
            Typography.DrawWrappedLeft(new Vector2(left, errorTop), error, controls.DangerInk, PrismType.Caption,
                width);
            gridBottom = errorTop - Metrics.Space.Md * scale;
        }

        DrawPickGrid(drawList, new Rect(new Vector2(left, top), new Vector2(content.Max.X, gridBottom)), looks, scale);
        if (DrawButton(drawList, send, Loc.T(store.Busy ? L.Prism.Submitting : L.Prism.SendPicks), ButtonStyle.Solid,
                scale, enabled: !store.Busy, id: "prism.sendPicks"))
        {
            ballotOutcome = OutcomePending;
            heatCoins = 0;
            store.SubmitBallot(stars, ballotSettled);
        }
    }

    private void DrawPickGrid(ImDrawListPtr drawList, Rect area, PrismLookDto[] looks, float scale)
    {
        var count = Math.Min(looks.Length, pickOrder.Length);
        var gap = Metrics.Space.Xs * scale;
        var columns = PicksColumns;
        var tileHeight = PickTileHeight(area, count, columns, gap, scale);
        var wide = PickTileHeight(area, count, columns - 1, gap, scale);
        if (wide > tileHeight)
        {
            columns--;
            tileHeight = wide;
        }

        var tileWidth = tileHeight * PrismStore.EntryRatio;
        var gridLeft = area.Center.X - (tileWidth * columns + gap * (columns - 1)) * 0.5f;
        var rounding = Metrics.Radius.Md * scale;
        var fade = PrismMotion.At(pickSwapAge, 0f, Motion.Appear);
        for (var slot = 0; slot < count; slot++)
        {
            var min = new Vector2(gridLeft + slot % columns * (tileWidth + gap),
                area.Min.Y + slot / columns * (tileHeight + gap));
            var tile = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            var shown = pickOrder[slot];
            var swapping = fade < PrismMotion.Settled && pickFrom[slot] != shown;
            if (swapping)
            {
                DrawRoundedLook(drawList, tile, looks[pickFrom[slot]].ImageUrl, 1f, rounding);
            }

            DrawRoundedLook(drawList, tile, looks[shown].ImageUrl, swapping ? fade : 1f, rounding);
            DrawStarPill(drawList, tile, StarsOf(shown), scale);
            var hovered = !store.Busy && UiInteract.Hover(tile.Min, tile.Max);
            if (!hovered)
            {
                continue;
            }

            drawList.AddRectFilled(tile.Min, tile.Max, PickHoverWash, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (UiInteract.Click(tile.Min, tile.Max, true))
            {
                OpenPick(shown);
            }
        }
    }

    private static float PickTileHeight(Rect area, int count, int columns, float gap, float scale)
    {
        var rows = (count + columns - 1) / columns;
        return MathF.Min((area.Width - gap * (columns - 1)) / columns / PrismStore.EntryRatio,
            MathF.Max(Metrics.Size.TapTarget * scale, (area.Height - gap * (rows - 1)) / MathF.Max(1, rows)));
    }

    private void DrawRoundedLook(ImDrawListPtr drawList, Rect rect, string url, float alpha, float rounding)
    {
        var texture = url.Length > 0 ? images.Sized(url, MathF.Max(rect.Width, rect.Height)) : null;
        if (texture is null)
        {
            var fill = Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary);
            drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(fill with { W = fill.W * alpha }), rounding);
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, rect.Width, rect.Height, CoverFocus);
        drawList.AddImageRounded(texture.Handle, rect.Min, rect.Max, uv0, uv1,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding, ImDrawFlags.RoundCornersAll);
    }

    private static void DrawStarPill(ImDrawListPtr drawList, Rect tile, int value, float scale)
    {
        var digit = Plain[Math.Clamp(value, 0, Plain.Length - 1)];
        var pixels = PrismArt.LabelPixels();
        var glyph = PickStarUnits * scale;
        var pad = Metrics.Space.Xs * scale;
        var height = PickPillUnits * scale;
        var width = pad * 2f + glyph + Metrics.Space.Xxs * scale + PrismType.Measure(digit, PrismFace.Label, pixels).X;
        var max = tile.Max - new Vector2(pad, pad);
        var min = max - new Vector2(width, height);
        var middle = (min.Y + max.Y) * 0.5f;
        drawList.AddRectFilled(min, max, PickPillFill, height * 0.5f);
        PhoneIcon.Draw(drawList, new Vector2(min.X + pad + glyph * 0.5f, middle), PhoneIcons.Star, PrismArt.PhotoFore,
            glyph);
        PrismType.Draw(drawList,
            new Vector2(min.X + pad + glyph + Metrics.Space.Xxs * scale,
                middle - PrismType.Snap(PrismFace.Label, pixels) * 0.5f), digit, PrismArt.PhotoFore, PrismFace.Label,
            pixels);
    }

    private void OpenPick(int index)
    {
        store.ClearFailure();
        StepTo(index, 1);
        Go(PrismScreen.Runway);
    }

    private void SettleBallot(bool succeeded)
    {
        ballotOutcome = succeeded ? OutcomeSent : OutcomeFailed;
    }

    private void ConsumeBallotOutcome()
    {
        var outcome = ballotOutcome;
        if (outcome == OutcomePending)
        {
            return;
        }

        ballotOutcome = OutcomePending;
        if (outcome != OutcomeSent)
        {
            return;
        }

        heatCoins = store.LastBallotCoins;
        heatDone = true;
        UiFeedback.Play(heatCoins > 0 ? UiSound.Payout : UiSound.Success);
        Go(PrismScreen.Runway, 0);
    }

    private void DrawBallotPending(ImDrawListPtr drawList, Rect content, float top, float scale)
    {
        if (store.Busy || !store.Failed(PrismAction.Ballot))
        {
            DrawCenteredLabel(drawList, content, Loc.T(L.Common.Loading));
            return;
        }

        var reason = store.ActionFailureText;
        DrawRunwayNotice(drawList, content, top, Loc.T(L.Common.LoadFailed),
            reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), scale);
        if (DrawButton(drawList, ActionRect(content, scale), Loc.T(L.Common.Retry), ButtonStyle.Solid, scale))
        {
            store.RequestBallot();
        }
    }

    private void DrawRunwayNotice(ImDrawListPtr drawList, Rect content, float top, string title, string body,
        float scale)
    {
        top += Metrics.Space.Xl * scale;
        top += PrismType.DrawHero(drawList, new Vector2(content.Min.X, top), title, content.Width, PrismArt.Fore) +
               Metrics.Space.Sm * scale;
        Typography.DrawWrappedLeft(new Vector2(content.Min.X, top), body, PrismArt.Muted, PrismType.Body,
            content.Width);
    }

    private void DrawHeatDone(ImDrawListPtr drawList, Rect content, float scale)
    {
        var left = content.Min.X;
        var width = content.Width;
        var top = DrawBackHeader(drawList, content, ratingBallot is { } ballot ? HeatTitle(ballot) : string.Empty,
            scale) + Metrics.Space.Lg * scale;
        var text = PrismMotion.Mark(drawList);
        top = DrawTopPicks(drawList, left, top, width, scale) + Metrics.Space.Xl * scale;
        top += PrismType.DrawHero(drawList, new Vector2(left, top), Loc.T(L.Prism.HeatDoneTitle), width,
            PrismArt.Fore) + Metrics.Space.Sm * scale;
        var hint = PrismType.Sentences(Loc.T(L.Prism.HeatDoneHint), PrismType.Body, width);
        top += Typography.DrawWrappedLeft(new Vector2(left, top), hint, PrismArt.Muted, PrismType.Body,
            PrismType.BalancedWidth(hint, PrismType.Body, width)) + Metrics.Space.Lg * scale;
        if (heatCoins > 0)
        {
            var pixels = PrismType.TitleUnits * scale;
            DrawWipe(drawList, new Vector2(left, top), CoinCounter(heatCoins), PrismFace.Numerals, pixels,
                PrismMotion.At(screenAge, 0f, Motion.Sheet), PrismArt.Fore);
            top += PrismType.Snap(PrismFace.Numerals, pixels) + Metrics.Space.Xxs * scale;
            Typography.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.JudgingCoinLabel), PrismArt.Muted,
                PrismType.Caption);
            top += Typography.LineHeight(PrismType.Caption) + Metrics.Space.Sm * scale;
        }

        var reward = RewardLine(store.Week?.JudgeReward);
        if (reward.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(left, top), reward, PrismArt.Muted, PrismType.Caption, width);
        }

        PrismMotion.Rise(drawList, text, PrismMotion.At(screenAge, DoneTextDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
        var buttons = PrismMotion.Mark(drawList);
        var canJudge = store.Week is { } week && PrismPhases.Judgeable(week.Phase);
        if (canJudge && DrawButton(drawList, ActionRect(content, scale, 1), Loc.T(L.Prism.JudgeAnother),
                ButtonStyle.Solid, scale))
        {
            ratingBallot = null;
            StartJudging();
        }

        var done = Loc.T(L.Prism.Done);
        var slot = ActionRect(content, scale);
        if (DrawButton(drawList, canJudge ? PlainRect(slot.Center, done, width, 0.5f, scale) : slot,
                done, canJudge ? ButtonStyle.Plain : ButtonStyle.Solid, scale))
        {
            Go(PrismScreen.Cover, -1);
        }

        PrismMotion.Rise(drawList, buttons, PrismMotion.At(screenAge, DoneTextDelay + PrismMotion.Stagger,
            Motion.PageSettle), PrismMotion.RiseUnits * scale);
    }

    private float DrawTopPicks(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var count = Math.Min(PrismArt.PodiumRanks, pickOrder.Length);
        if (count == 0 || ratingBallot?.Looks is not { } looks || looks.Length < pickOrder.Length)
        {
            return top;
        }

        var gap = Metrics.Space.Sm * scale;
        var lead = width * PodiumLeadShare;
        var side = (width - lead - gap * 2f) / 2f;
        var bottom = top + lead / PrismStore.EntryRatio;
        var rounding = Metrics.Radius.Md * scale;
        for (var place = 1; place <= count; place++)
        {
            var columnLeft = place switch
            {
                1 => left + side + gap,
                2 => left,
                _ => left + side + gap + lead + gap,
            };
            var columnWidth = place == 1 ? lead : side;
            var photo = new Rect(new Vector2(columnLeft, bottom - columnWidth / PrismStore.EntryRatio),
                new Vector2(columnLeft + columnWidth, bottom));
            DrawRoundedLook(drawList, photo, looks[pickOrder[place - 1]].ImageUrl, 1f, rounding);
            DrawStarPill(drawList, photo, StarsOf(pickOrder[place - 1]), scale);
        }

        return bottom;
    }

    private Rect ActionRect(Rect content, float scale, int fromBottom = 0)
    {
        var height = Button.LargeHeight * scale;
        var bottom = content.Max.Y - bottomChrome - Metrics.Space.Lg * scale -
                     fromBottom * (height + Metrics.Space.Sm * scale);
        return new Rect(new Vector2(content.Min.X, bottom - height), new Vector2(content.Max.X, bottom));
    }

    private Rect PlainRect(Vector2 anchor, string label, float cap, float align, float scale)
    {
        var width = ButtonWidth([label], cap, scale);
        var height = Button.LargeHeight * scale;
        var left = anchor.X - width * align - Metrics.Space.Xl * scale * 0.5f * (1f - align * 2f);
        return new Rect(new Vector2(left, anchor.Y - height * 0.5f),
            new Vector2(left + width, anchor.Y + height * 0.5f));
    }

    private void DrawCenteredLabel(ImDrawListPtr drawList, Rect content, string text)
    {
        var labelPixels = PrismArt.LabelPixels();
        var size = PrismType.Measure(text, PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, content.Center - size * 0.5f, text, PrismArt.Muted, PrismFace.Label, labelPixels);
    }

    private string LookCounter(int current, int total)
    {
        current = Math.Min(current, total);
        var key = HashCode.Combine(current, total, Loc.Current.Code);
        return lookCounter.IsCurrent(key)
            ? lookCounter.Value
            : lookCounter.Store(key, Loc.T(L.Prism.LookCount, Plain[Math.Min(current, Plain.Length - 1)],
                Plain[Math.Min(total, Plain.Length - 1)]));
    }

    private string RateCaption(int current, int total) =>
        FactLine(ref rateCaption, LookCounter(current, total), Loc.T(L.Prism.InspectHint));

    private string PicksCaption(PrismBallotDto ballot)
    {
        var heat = HeatTitle(ballot);
        var week = store.Week?.Title ?? string.Empty;
        return heat.Length > 0 && week.Length > 0 ? FactLine(ref picksCaption, heat, week)
            : heat.Length > 0 ? heat : week;
    }

    private string CoinCounter(long coins)
    {
        var key = HashCode.Combine(coins, Loc.Current.Code);
        return coinCounter.IsCurrent(key)
            ? coinCounter.Value
            : coinCounter.Store(key, string.Concat("+", coins.ToString("N0", Loc.Culture)));
    }

    private string RewardLine(PrismJudgeRewardDto? reward)
    {
        var line = PrismRewards.LineFor(reward);
        var amount = reward?.Amount ?? 0;
        var left = reward?.LeftThisWeek ?? 0;
        var key = HashCode.Combine(line, amount, left, Loc.Current.Code);
        if (rewardText.IsCurrent(key))
        {
            return rewardText.Value;
        }

        var coins = amount.ToString("N0", Loc.Culture);
        var text = line switch
        {
            PrismRewardLine.Each => Loc.T(L.Prism.JudgeRewardEach, coins),
            PrismRewardLine.Left => Loc.T(L.Prism.JudgeRewardLeft, coins, left.ToString("N0", Loc.Culture)),
            PrismRewardLine.Spent => Loc.T(L.Prism.JudgeRewardSpent),
            _ => string.Empty,
        };
        return rewardText.Store(key, text);
    }

    private string HeatTitle(PrismBallotDto ballot)
    {
        var key = HashCode.Combine(ballot.Stage, ballot.HeatLabel, Loc.Current.Code);
        if (heatTitle.IsCurrent(key))
        {
            return heatTitle.Value;
        }

        var text = ballot.Stage == PrismPhases.StageFinal ? Loc.T(L.Prism.FinalTitle)
            : ballot.HeatLabel.Length > 0 ? Loc.T(L.Prism.HeatName, ballot.HeatLabel) : string.Empty;
        return heatTitle.Store(key, text);
    }

    private static string[] BuildDigits(string format)
    {
        var digits = new string[100];
        for (var index = 0; index < digits.Length; index++)
        {
            digits[index] = index.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        return digits;
    }
}
