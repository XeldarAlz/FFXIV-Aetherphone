using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell;

internal sealed class AppSwitcher
{
    private const string BackdropLayerId = "switcherbackdrop";
    private const string ShadowLayerId = "switchershadow";
    private const string TopLayerId = "switchertop";
    private const float CommitDoneProgress = 0.992f;
    private const float BackdropVeil = 0.25f;
    private const float LabelHeightUnits = 28f;
    private const float LabelTileUnits = 18f;
    private const float LabelGapUnits = 6f;
    private const float LabelTrailingUnits = 12f;
    private const float LabelLiftUnits = 10f;
    private const float LabelHitHeightUnits = 44f;
    private const float SlideInFactor = 0.5f;
    private const float TapSlopUnits = 6f;
    private const float CloseFlingUnitsPerSecond = 900f;
    private const float PressDepth = 1f - Motion.PressScaleCard;
    private const float FlyOffSmoothTime = 0.10f;
    private const float FlyOffClearanceFactor = 1.25f;
    private const float FlyOffTargetFactor = 1.75f;
    private const float OthersCommitFade = 2f;
    private const float InteractiveReveal = 0.92f;
    private const float InvisibleAlpha = 0.004f;
    private const float ArrowRadiusUnits = 15f;
    private const float ArrowInsetUnits = 26f;
    private const float CloseAllHeightUnits = 38f;
    private const float CloseAllPaddingUnits = 18f;
    private const float CloseAllBottomInsetUnits = 46f;
    private static readonly Vector4 ArrowTint = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 ArrowInk = new(1f, 1f, 1f, 0.95f);

    private sealed class Card
    {
        public readonly IPhoneApp App;
        public readonly string LayerId;
        public Spring Slot;
        public Spring Lift;
        public Spring Hover;
        public Spring Press;
        public Rect HitRect;
        public Rect DrawRect;
        public Rect LabelRect;
        public Rect LabelHitRect;
        public Vector2 LabelTextSize;
        public string LabelText = string.Empty;
        public float Alpha;
        public bool FlyingOff;
        public bool Hovered;

        public Card(IPhoneApp app, int slotIndex)
        {
            App = app;
            LayerId = app.Id;
            Slot.SnapTo(slotIndex);
        }
    }

    private readonly NavigationStack navigation;
    private readonly ShellScreenPainter painter;
    private readonly List<IPhoneApp> snapshot = new();
    private readonly List<Card> cards = new();
    private readonly DragTracker drag = new();
    private SwitcherLayout layout;
    private Spring reveal;
    private Spring commit;
    private Spring scroll;
    private Card? committing;
    private Card? pressCard;
    private float scrollTarget;
    private float panStartScroll;
    private float parallaxReachSlots;
    private bool open;
    private bool closingAll;
    private bool panning;
    private bool lifting;
    private int openedFrame;
    private int tapSuppressedFrame = -1;

    public AppSwitcher(NavigationStack navigation, ShellScreenPainter painter)
    {
        this.navigation = navigation;
        this.painter = painter;
    }

    public bool IsActive => open || committing is not null || reveal.Value > 0.01f;
    public bool Overtakes => IsActive;
    public bool CapturesPointer => IsActive;

    private bool TapAllowed => !panning && !lifting && tapSuppressedFrame != ImGui.GetFrameCount();

    public void Open()
    {
        if (open || committing is not null)
        {
            return;
        }

        open = true;
        closingAll = false;
        openedFrame = ImGui.GetFrameCount();
        navigation.CollectOpen(snapshot);
        cards.Clear();
        for (var index = 0; index < snapshot.Count; index++)
        {
            cards.Add(new Card(snapshot[index], index));
        }

        scrollTarget = 0f;
        scroll.SnapTo(0f);
        commit.SnapTo(0f);
        parallaxReachSlots = SwitcherGeometry.ParallaxReachSlots(cards.Count);
        ResetPress();
    }

    public void Dismiss()
    {
        if (committing is not null)
        {
            return;
        }

        open = false;
        closingAll = false;
        ResetPress();
    }

    public void CloseImmediate()
    {
        open = false;
        closingAll = false;
        committing = null;
        reveal.SnapTo(0f);
        commit.SnapTo(0f);
        cards.Clear();
        ResetPress();
    }

    public void Advance(Rect screen, float delta)
    {
        reveal.Step(open ? 1f : 0f, Motion.SwitcherReveal, delta);
        if (!IsActive)
        {
            return;
        }

        if (!open && committing is null &&
            reveal.IsResting(0f, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
        {
            reveal.SnapTo(0f);
            cards.Clear();
            return;
        }

        layout = SwitcherGeometry.Layout(screen, UiScale.Current, cards.Count);
        if (committing is not null)
        {
            commit.Step(1f, Motion.Release, delta);
            if (commit.Value >= CommitDoneProgress)
            {
                FinishCommit();
                return;
            }
        }

        var hadCards = cards.Count > 0;
        StepCards(delta);
        if (!panning)
        {
            scrollTarget = Math.Clamp(scrollTarget, 0f, layout.MaxScroll);
            scroll.Step(scrollTarget, Motion.PageSettle, delta);
        }

        ComputeCardRects();
        if (hadCards && cards.Count == 0)
        {
            Dismiss();
        }
    }

    private void FinishCommit()
    {
        var chosen = committing!;
        committing = null;
        open = false;
        reveal.SnapTo(0f);
        commit.SnapTo(0f);
        cards.Clear();
        ResetPress();
        navigation.OpenSettled(chosen.App.Id);
    }

    private void StepCards(float delta)
    {
        var pressing = drag.Active && !panning && !lifting;
        for (var index = cards.Count - 1; index >= 0; index--)
        {
            var card = cards[index];
            card.Slot.Step(index, Motion.Release, delta);
            var pressed = pressing && ReferenceEquals(card, pressCard);
            card.Press.Step(pressed ? 1f : 0f, pressed ? Motion.PressIn : Motion.Release, delta);
            card.Hover.Step(card.Hovered && !drag.Active ? 1f : 0f, Motion.HoverLift, delta);
            if (card.FlyingOff)
            {
                card.Lift.Step(layout.CardHeight * FlyOffTargetFactor, FlyOffSmoothTime, delta);
                if (card.Lift.Value >= layout.CardHeight * FlyOffClearanceFactor)
                {
                    cards.RemoveAt(index);
                }

                continue;
            }

            if (lifting && ReferenceEquals(card, pressCard))
            {
                continue;
            }

            card.Lift.Step(0f, Motion.Appear, delta);
        }
    }

    private void ComputeCardRects()
    {
        var revealValue = Easing.Clamp01(reveal.Value);
        var commitValue = committing is null ? 0f : Easing.Clamp01(commit.Value);
        var slideIn = new Vector2((1f - revealValue) * layout.Pitch * SlideInFactor, 0f);
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            var isCurrent = IsCurrent(card);
            var rest = SwitcherGeometry.CardRest(in layout, card.Slot.Value, scroll.Value);
            if (!isCurrent)
            {
                rest = rest.Translate(slideIn);
            }

            card.HitRect = rest;
            var pointerScale = (1f + Motion.HoverLiftCard * card.Hover.Value) * (1f - PressDepth * card.Press.Value);
            var rect = SwitcherGeometry.Scaled(rest, pointerScale);
            var alpha = isCurrent ? 1f : revealValue;
            if (isCurrent && committing is null)
            {
                rect = LerpRect(layout.Screen, rect, revealValue);
            }

            if (ReferenceEquals(card, committing))
            {
                rect = LerpRect(rect, layout.Screen, commitValue);
                alpha = 1f;
            }
            else if (committing is not null)
            {
                alpha *= Easing.Clamp01(1f - commitValue * OthersCommitFade);
            }

            if (card.FlyingOff)
            {
                alpha *= 1f - Easing.Clamp01(card.Lift.Value / (layout.CardHeight * FlyOffClearanceFactor));
            }

            card.DrawRect = rect.Translate(new Vector2(0f, -card.Lift.Value));
            card.Alpha = alpha;
        }
    }

    private bool IsCurrent(Card card)
    {
        return !card.FlyingOff && ReferenceEquals(card.App, navigation.Current);
    }

    private bool HasOpenApps()
    {
        for (var index = 0; index < cards.Count; index++)
        {
            if (!cards[index].FlyingOff)
            {
                return true;
            }
        }

        return false;
    }

    private static Rect LerpRect(Rect from, Rect to, float progress)
    {
        return new Rect(Vector2.Lerp(from.Min, to.Min, progress), Vector2.Lerp(from.Max, to.Max, progress));
    }

    private static bool VisibleOn(Rect rect, Rect screen)
    {
        return rect.Max.X > screen.Min.X && rect.Min.X < screen.Max.X && rect.Max.Y > screen.Min.Y;
    }

    private static bool TryClipToScreen(Rect rect, Rect screen, out Rect clip)
    {
        var min = Vector2.Max(rect.Min, screen.Min);
        var max = Vector2.Min(rect.Max, screen.Max);
        if (max.X <= min.X || max.Y <= min.Y)
        {
            clip = default;
            return false;
        }

        clip = new Rect(min, max);
        return true;
    }

    private float BackdropAmount()
    {
        var revealValue = Easing.Clamp01(reveal.Value);
        var commitValue = committing is null ? 0f : Easing.Clamp01(commit.Value);
        return revealValue * (1f - commitValue);
    }

    private Rect BackdropQuad(Rect screen, float backdrop) =>
        LerpRect(screen, SwitcherGeometry.ParallaxQuad(screen, scroll.Value, layout.Pitch, parallaxReachSlots),
            backdrop);

    public void DrawStage(Rect screen, float screenRadius, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var backdrop = BackdropAmount();
        using (ScreenLayer.BeginPassive(BackdropLayerId, screen))
        {
            DeviceChrome.DrawWallpaper(ImGui.GetWindowDrawList(), screen, screen, BackdropQuad(screen, backdrop),
                screenRadius, theme, HomeMotion.Recede(backdrop, null).Recession);
        }

        if (navigation.AtHome)
        {
            using var homeLayer = ScreenLayer.Begin(ShellScreenPainter.HomeLayerId, screen, true);
            painter.PaintHomeContent(screen, screenRadius, theme, HomeMotion.Recede(Easing.Clamp01(reveal.Value), null));
        }

        using (ScreenLayer.BeginPassive(ShadowLayerId, screen))
        {
            var shadowList = ImGui.GetWindowDrawList();
            Material.Veil(shadowList, screen.Min, screen.Max, BackdropVeil * backdrop, screenRadius);
            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                if (card.Alpha <= InvisibleAlpha || !VisibleOn(card.DrawRect, screen))
                {
                    continue;
                }

                Elevation.Floating(shadowList, card.DrawRect.Min, card.DrawRect.Max,
                    SwitcherGeometry.CardRounding(card.DrawRect.Width, screen.Width, screenRadius), scale, card.Alpha);
            }
        }

        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            if (card.Alpha <= InvisibleAlpha || !TryClipToScreen(card.DrawRect, screen, out var clip))
            {
                continue;
            }

            var transform = LayerTransform.Fit(screen, card.DrawRect, clip, card.Alpha);
            using var layer = ScreenLayer.Begin(card.LayerId, screen, true);
            painter.PaintApp(screen, screenRadius, theme, card.App);
            layer.Transform(in transform);
        }

        using (ScreenLayer.BeginPassive(TopLayerId, screen))
        {
            var topList = ImGui.GetWindowDrawList();
            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                if (card.Alpha <= InvisibleAlpha || !VisibleOn(card.DrawRect, screen))
                {
                    continue;
                }

                Material.EdgeSquircle(topList, card.DrawRect.Min, card.DrawRect.Max,
                    SwitcherGeometry.CardRounding(card.DrawRect.Width, screen.Width, screenRadius), scale, card.Alpha);
            }
        }
    }

    public void DrawOverlay(Rect screen, PhoneTheme theme, float delta, bool inputEnabled)
    {
        if (!IsActive)
        {
            return;
        }

        if (UiInteract.HoverWindowOnly(screen.Min, screen.Max, false))
        {
            UiInteract.ReportGestureSurface();
        }

        var scale = UiScale.Current;
        var revealValue = Easing.Clamp01(reveal.Value);
        var commitValue = committing is null ? 0f : Easing.Clamp01(commit.Value);
        var opacity = Easing.Clamp01(revealValue * 1.6f) * Easing.Clamp01(1f - commitValue * OthersCommitFade);
        var interactive = open && committing is null && inputEnabled && revealValue > InteractiveReveal;
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        LayoutLabels(scale);
        if (interactive)
        {
            UpdateInput(screen, scale, delta);
        }
        else
        {
            ClearPointer();
        }

        DeviceChrome.RecordWallpaperBackdrop(screen, BackdropQuad(screen, BackdropAmount()), theme);
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            if (card.Alpha <= InvisibleAlpha || ReferenceEquals(card, committing) ||
                (committing is null && IsCurrent(card) && revealValue < InteractiveReveal))
            {
                continue;
            }

            HandleCardTap(card, interactive);
            DrawCardLabel(drawList, card, theme, scale, card.Alpha * opacity, interactive);
        }

        DrawEmptyHint(drawList, screen, theme, opacity);
        DrawArrows(drawList, screen, scale, delta, opacity, interactive);
        DrawFooter(drawList, screen, theme, scale, opacity, interactive);
        drawList.PopClipRect();
    }

    private void LayoutLabels(float scale)
    {
        var height = LabelHeightUnits * scale;
        var tile = LabelTileUnits * scale;
        var inset = (height - tile) * 0.5f;
        var gap = LabelGapUnits * scale;
        var trailing = LabelTrailingUnits * scale;
        var hitPad = new Vector2(0f, MathF.Max(0f, (LabelHitHeightUnits * scale - height) * 0.5f));
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            if (card.Alpha <= InvisibleAlpha)
            {
                continue;
            }

            var bounds = card.DrawRect;
            var maxTextWidth = MathF.Max(0f, bounds.Width - (inset + tile + gap + trailing));
            card.LabelText = Typography.FitText(card.App.DisplayName, maxTextWidth, TextStyles.FootnoteEmphasized);
            card.LabelTextSize = Typography.Measure(card.LabelText, TextStyles.FootnoteEmphasized);
            var halfWidth = (inset + tile + gap + card.LabelTextSize.X + trailing) * 0.5f;
            var bottom = bounds.Min.Y - LabelLiftUnits * scale;
            card.LabelRect = new Rect(new Vector2(bounds.Center.X - halfWidth, bottom - height),
                new Vector2(bounds.Center.X + halfWidth, bottom));
            card.LabelHitRect = new Rect(card.LabelRect.Min - hitPad, card.LabelRect.Max + hitPad);
        }
    }

    private void UpdateInput(Rect screen, float scale, float delta)
    {
        var hoveredCard = HoveredCard();
        var overChrome = OverChrome(screen, scale);
        if (hoveredCard is not null && !overChrome)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        drag.Track(delta);
        if (!drag.Active && !overChrome && ImGui.GetFrameCount() != openedFrame && drag.Begin(screen))
        {
            pressCard = hoveredCard;
            panning = false;
            lifting = false;
            panStartScroll = scroll.Value;
        }

        if (drag.Active)
        {
            TrackPress(scale);
        }

        if (drag.Released(out var travel, out var velocityY))
        {
            ReleasePress(travel, velocityY, scale);
        }
    }

    private Card? HoveredCard()
    {
        Card? hovered = null;
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            card.Hovered = false;
            if (card.FlyingOff || card.Alpha <= InvisibleAlpha)
            {
                continue;
            }

            if (UiInteract.Hover(card.HitRect.Min, card.HitRect.Max) ||
                UiInteract.Hover(card.LabelHitRect.Min, card.LabelHitRect.Max))
            {
                hovered = card;
            }
        }

        if (hovered is not null)
        {
            hovered.Hovered = true;
        }

        return hovered;
    }

    private void ClearPointer()
    {
        for (var index = 0; index < cards.Count; index++)
        {
            cards[index].Hovered = false;
        }
    }

    private bool OverChrome(Rect screen, float scale)
    {
        if (HasOpenApps())
        {
            var closeAll = CloseAllRect(screen, scale);
            if (UiInteract.Hover(closeAll.Min, closeAll.Max))
            {
                return true;
            }
        }

        if (cards.Count < 2)
        {
            return false;
        }

        var focusIndex = SwitcherGeometry.SnapSlot(scrollTarget, cards.Count);
        var reach = new Vector2(ArrowRadiusUnits * scale, ArrowRadiusUnits * scale);
        var left = ArrowCenter(screen, scale, true);
        var right = ArrowCenter(screen, scale, false);
        return (focusIndex > 0 && UiInteract.Hover(left - reach, left + reach)) ||
               (focusIndex < cards.Count - 1 && UiInteract.Hover(right - reach, right + reach));
    }

    private void TrackPress(float scale)
    {
        var travel = drag.Delta;
        if (!panning && !lifting)
        {
            var slop = TapSlopUnits * scale;
            if (MathF.Abs(travel.X) > slop && MathF.Abs(travel.X) >= MathF.Abs(travel.Y))
            {
                panning = true;
            }
            else if (MathF.Abs(travel.Y) > slop)
            {
                if (pressCard is { FlyingOff: false } && travel.Y < 0f)
                {
                    lifting = true;
                }
                else
                {
                    panning = true;
                }
            }
        }

        if (panning)
        {
            var raw = panStartScroll - travel.X / MathF.Max(1f, layout.Pitch);
            scroll.SnapTo(SwitcherGeometry.RubberBand(raw, layout.MaxScroll));
            return;
        }

        if (lifting && pressCard is { } card)
        {
            card.Lift.SnapTo(MathF.Max(0f, -travel.Y));
        }
    }

    private void ReleasePress(Vector2 travel, float velocityY, float scale)
    {
        if (lifting && pressCard is { FlyingOff: false } card)
        {
            if (SwitcherGeometry.ClosesOnRelease(card.Lift.Value, layout.CardHeight, velocityY,
                    CloseFlingUnitsPerSecond * scale))
            {
                CloseCard(card);
            }
        }
        else if (panning)
        {
            var projected = SwitcherGeometry.ProjectedScroll(panStartScroll, travel.X, drag.VelocityX, layout.Pitch);
            scrollTarget = SwitcherGeometry.SnapSlot(projected, cards.Count);
        }
        else if (pressCard is null && IsTap(travel, scale))
        {
            Dismiss();
        }

        if (panning || lifting)
        {
            tapSuppressedFrame = ImGui.GetFrameCount();
        }

        ResetPress();
    }

    private static bool IsTap(Vector2 travel, float scale)
    {
        var slop = TapSlopUnits * scale;
        return MathF.Abs(travel.X) < slop && MathF.Abs(travel.Y) < slop;
    }

    private void HandleCardTap(Card card, bool interactive)
    {
        if (card.FlyingOff)
        {
            return;
        }

        var hovered = interactive && UiInteract.Hover(card.HitRect.Min, card.HitRect.Max);
        if (UiInteract.Click(card.HitRect.Min, card.HitRect.Max, hovered && TapAllowed))
        {
            OpenCard(card);
        }
    }

    private void CloseCard(Card card)
    {
        if (card.FlyingOff)
        {
            return;
        }

        card.FlyingOff = true;
        var wasCurrent = ReferenceEquals(navigation.Current, card.App);
        navigation.Forget(card.App.Id);
        if (!wasCurrent)
        {
            UiFeedback.Play(UiSound.AppClose);
        }
    }

    private void OpenCard(Card card)
    {
        if (card.FlyingOff || committing is not null)
        {
            return;
        }

        if (IsCurrent(card))
        {
            Dismiss();
            return;
        }

        committing = card;
        commit.SnapTo(0f);
        ResetPress();
    }

    private void CloseAll()
    {
        if (closingAll)
        {
            return;
        }

        closingAll = true;
        var closesCurrent = navigation.Current is not null;
        for (var index = 0; index < cards.Count; index++)
        {
            cards[index].FlyingOff = true;
        }

        navigation.ForgetAll();
        if (!closesCurrent)
        {
            UiFeedback.Play(UiSound.AppClose);
        }

        ResetPress();
    }

    private void ResetPress()
    {
        drag.Cancel();
        panning = false;
        lifting = false;
        pressCard = null;
    }

    private void DrawCardLabel(ImDrawListPtr drawList, Card card, PhoneTheme theme, float scale, float alpha,
        bool interactive)
    {
        if (alpha <= InvisibleAlpha)
        {
            return;
        }

        var rect = card.LabelRect;
        var tile = LabelTileUnits * scale;
        var inset = (rect.Height - tile) * 0.5f;
        Material.LiquidGlass(drawList, rect.Min, rect.Max, rect.Height * 0.5f, scale, GlassTone.Light, 0f, alpha);
        var tileCenter = new Vector2(rect.Min.X + inset + tile * 0.5f, rect.Center.Y);
        DrawLabelIcon(drawList, card, tileCenter, tile, alpha);
        var textPosition = new Vector2(tileCenter.X + tile * 0.5f + LabelGapUnits * scale,
            rect.Center.Y - card.LabelTextSize.Y * 0.5f);
        Typography.Draw(drawList, textPosition, card.LabelText, Palette.WithAlpha(theme.TextStrong, alpha),
            TextStyles.FootnoteEmphasized);
        var hovered = interactive && !card.FlyingOff &&
                      UiInteract.Hover(card.LabelHitRect.Min, card.LabelHitRect.Max);
        if (UiInteract.Click(card.LabelHitRect.Min, card.LabelHitRect.Max, hovered && TapAllowed))
        {
            OpenCard(card);
        }
    }

    private static void DrawLabelIcon(ImDrawListPtr drawList, Card card, Vector2 center, float size, float alpha)
    {
        var half = new Vector2(size, size) * 0.5f;
        var accent = card.App.Accent;
        if (AppIconTile.TryDraw(drawList, card.App.Id, accent, center - half, center + half,
                size * Metrics.Radius.TileFactor, alpha, false))
        {
            return;
        }

        var surface = IconTile.Surface(accent);
        Squircle.Fill(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(surface, alpha)));
        var ink = AppAccents.InkFor(card.App.Id);
        if (AppIconArt.TryDraw(drawList, card.App.Id, center, size * 0.9f, Palette.WithAlpha(ink, alpha),
                Palette.WithAlpha(Palette.Mix(surface, ink, 0.28f), alpha)))
        {
            return;
        }

        drawList.AddCircleFilled(center, size * 0.16f, ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)), 12);
    }

    private void DrawEmptyHint(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float opacity)
    {
        if (cards.Count > 0)
        {
            return;
        }

        Typography.DrawCentered(drawList, screen.Center, Loc.T(L.AppSwitcher.Empty),
            Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.SubheadlineEmphasized);
    }

    private Vector2 ArrowCenter(Rect screen, float scale, bool left)
    {
        var x = left ? screen.Min.X + ArrowInsetUnits * scale : screen.Max.X - ArrowInsetUnits * scale;
        return new Vector2(x, layout.CenterY);
    }

    private void DrawArrows(ImDrawListPtr drawList, Rect screen, float scale, float delta, float opacity,
        bool interactive)
    {
        if (cards.Count < 2)
        {
            return;
        }

        var focusIndex = SwitcherGeometry.SnapSlot(scrollTarget, cards.Count);
        var radius = ArrowRadiusUnits * scale;
        if (focusIndex > 0 &&
            HoverButton.Circle(drawList, "switcher.left", ArrowCenter(screen, scale, true), radius,
                FontAwesomeIcon.ChevronLeft, ArrowTint, ArrowInk, delta, opacity, interactive))
        {
            scrollTarget = focusIndex - 1;
        }

        if (focusIndex < cards.Count - 1 &&
            HoverButton.Circle(drawList, "switcher.right", ArrowCenter(screen, scale, false), radius,
                FontAwesomeIcon.ChevronRight, ArrowTint, ArrowInk, delta, opacity, interactive))
        {
            scrollTarget = focusIndex + 1;
        }
    }

    private void DrawFooter(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float scale, float opacity,
        bool interactive)
    {
        if (!HasOpenApps())
        {
            return;
        }

        var rect = CloseAllRect(screen, scale);
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        Material.LiquidGlass(drawList, rect.Min, rect.Max, rect.Height * 0.5f, scale, GlassTone.Light, 0f, opacity);
        Typography.DrawCentered(drawList, rect.Center, Loc.T(L.AppSwitcher.CloseAll),
            Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered && ImGui.GetFrameCount() != openedFrame))
        {
            CloseAll();
        }
    }

    private static Rect CloseAllRect(Rect screen, float scale)
    {
        var height = CloseAllHeightUnits * scale;
        var halfWidth = (Typography.Measure(Loc.T(L.AppSwitcher.CloseAll), TextStyles.SubheadlineEmphasized).X +
                         CloseAllPaddingUnits * 2f * scale) * 0.5f;
        var center = new Vector2(screen.Center.X, screen.Max.Y - CloseAllBottomInsetUnits * scale);
        return new Rect(center - new Vector2(halfWidth, height * 0.5f), center + new Vector2(halfWidth, height * 0.5f));
    }
}
