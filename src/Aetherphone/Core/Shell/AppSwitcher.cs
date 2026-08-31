using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
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
    private const string ShadowLayerId = "switchershadow";
    private const string TopLayerId = "switchertop";
    private const float RevealSmoothTime = 0.19f;
    private const float CommitSmoothTime = 0.16f;
    private const float CommitDoneProgress = 0.992f;
    private const float MaxVeil = 0.58f;
    private const float CardHeightFraction = 0.52f;
    private const float CardCenterFraction = 0.46f;
    private const float CardGapUnits = 16f;
    private const float LabelTileUnits = 18f;
    private const float LabelGapUnits = 6f;
    private const float LabelLiftUnits = 14f;
    private const float SlideInFactor = 0.5f;
    private const float TapSlopUnits = 6f;
    private const float CloseCommitFraction = 0.28f;
    private const float CloseFlingUnitsPerSecond = 900f;
    private const float ScrollSmoothTime = 0.18f;
    private const float ReturnSmoothTime = 0.14f;
    private const float ReflowSmoothTime = 0.16f;
    private const float FlyOffSmoothTime = 0.10f;
    private const float FlyOffClearanceFactor = 1.25f;
    private const float FlyOffTargetFactor = 1.75f;
    private const float VelocityBlend = 0.5f;
    private const float FlingProjectSeconds = 0.12f;
    private const float OverscrollResistance = 0.35f;
    private const float OthersCommitFade = 2f;
    private const float InteractiveReveal = 0.92f;
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
        public Spring Slot;
        public Spring Lift;
        public Rect HitRect;
        public Rect DrawRect;
        public float Alpha;
        public bool FlyingOff;

        public Card(IPhoneApp app, int slotIndex)
        {
            App = app;
            Slot.SnapTo(slotIndex);
        }
    }

    private readonly struct SwitcherLayout
    {
        public readonly Rect Screen;
        public readonly float CardWidth;
        public readonly float CardHeight;
        public readonly float Pitch;
        public readonly float CenterY;
        public readonly float MaxScroll;

        public SwitcherLayout(Rect screen, float cardWidth, float cardHeight, float pitch, float centerY,
            float maxScroll)
        {
            Screen = screen;
            CardWidth = cardWidth;
            CardHeight = cardHeight;
            Pitch = pitch;
            CenterY = centerY;
            MaxScroll = maxScroll;
        }
    }

    private readonly NavigationStack navigation;
    private readonly ShellScreenPainter painter;
    private readonly List<IPhoneApp> snapshot = new();
    private readonly List<Card> cards = new();
    private SwitcherLayout layout;
    private Spring reveal;
    private Spring commit;
    private Spring scroll;
    private Card? committing;
    private float scrollTarget;
    private bool open;
    private bool closingAll;
    private int openedFrame;
    private bool pressed;
    private bool panning;
    private bool lifting;
    private Card? pressCard;
    private Vector2 pressOrigin;
    private Vector2 lastMouse;
    private float panStartScroll;
    private float velocityX;
    private float velocityY;

    public AppSwitcher(NavigationStack navigation, ShellScreenPainter painter)
    {
        this.navigation = navigation;
        this.painter = painter;
    }

    public bool IsActive => open || committing is not null || reveal.Value > 0.01f;
    public bool Overtakes => IsActive;
    public bool CapturesPointer => IsActive;

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
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
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

        layout = ComputeLayout(screen, UiScale.Current);
        if (committing is not null)
        {
            commit.Step(1f, CommitSmoothTime, delta);
            if (commit.Value >= CommitDoneProgress)
            {
                FinishCommit();
                return;
            }
        }

        StepCards(delta);
        if (!panning)
        {
            scrollTarget = Math.Clamp(scrollTarget, 0f, layout.MaxScroll);
            scroll.Step(scrollTarget, ScrollSmoothTime, delta);
        }

        ComputeCardRects();
        if (closingAll && cards.Count == 0)
        {
            closingAll = false;
            Dismiss();
        }
    }

    private void FinishCommit()
    {
        var chosen = committing!.App;
        committing = null;
        open = false;
        reveal.SnapTo(0f);
        commit.SnapTo(0f);
        cards.Clear();
        ResetPress();
        navigation.OpenSettled(chosen.Id);
    }

    private SwitcherLayout ComputeLayout(Rect screen, float scale)
    {
        var cardHeight = screen.Height * CardHeightFraction;
        var cardWidth = cardHeight * (screen.Width / MathF.Max(1f, screen.Height));
        var pitch = cardWidth + CardGapUnits * scale;
        var centerY = screen.Min.Y + screen.Height * CardCenterFraction;
        var maxScroll = MathF.Max(0f, (cards.Count - 1) * pitch);
        return new SwitcherLayout(screen, cardWidth, cardHeight, pitch, centerY, maxScroll);
    }

    private void StepCards(float delta)
    {
        for (var index = cards.Count - 1; index >= 0; index--)
        {
            var card = cards[index];
            card.Slot.Step(index, ReflowSmoothTime, delta);
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

            card.Lift.Step(0f, ReturnSmoothTime, delta);
        }
    }

    private void ComputeCardRects()
    {
        var revealValue = Easing.Clamp01(reveal.Value);
        var commitValue = committing is null ? 0f : Easing.Clamp01(commit.Value);
        var half = new Vector2(layout.CardWidth, layout.CardHeight) * 0.5f;
        var slideIn = (1f - revealValue) * layout.Pitch * SlideInFactor;
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            var isCurrent = !card.FlyingOff && ReferenceEquals(card.App, navigation.Current);
            var centerX = layout.Screen.Center.X + card.Slot.Value * layout.Pitch - scroll.Value;
            if (!isCurrent)
            {
                centerX += slideIn;
            }

            var center = new Vector2(centerX, layout.CenterY);
            var rest = new Rect(center - half, center + half);
            card.HitRect = rest;
            var rect = rest;
            var alpha = isCurrent ? 1f : revealValue;
            if (isCurrent && committing is null)
            {
                rect = LerpRect(layout.Screen, rest, revealValue);
            }

            if (ReferenceEquals(card, committing))
            {
                rect = LerpRect(rest, layout.Screen, commitValue);
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

    private static float CardRounding(Rect rect, Rect screen, float screenRadius)
    {
        return screenRadius * (rect.Width / MathF.Max(1f, screen.Width));
    }

    public void DrawStage(Rect screen, float screenRadius, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var revealValue = Easing.Clamp01(reveal.Value);
        var commitValue = committing is null ? 0f : Easing.Clamp01(commit.Value);
        var backdrop = revealValue * (1f - commitValue);
        var zoom = 1f + TransitionTiming.HomeZoomDepth * backdrop;
        var homeTransform = LayerTransform.ScaleAbout(screen.Center, zoom, screen);
        using (var homeLayer = ScreenLayer.Begin(ShellScreenPainter.HomeLayerId, screen, true))
        {
            painter.PaintHome(screen, screenRadius, theme, HomeMotion.Recede(backdrop, null));
            homeLayer.Veil(ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxVeil * backdrop)));
            homeLayer.Transform(in homeTransform);
        }

        using (ScreenLayer.BeginPassive(ShadowLayerId, screen))
        {
            var shadowList = ImGui.GetWindowDrawList();
            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                if (card.Alpha <= 0.004f || !VisibleOn(card.DrawRect, screen))
                {
                    continue;
                }

                Elevation.Squircle(shadowList, card.DrawRect.Min, card.DrawRect.Max,
                    CardRounding(card.DrawRect, screen, screenRadius), scale, card.Alpha);
            }
        }

        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            if (card.Alpha <= 0.004f || !TryClipToScreen(card.DrawRect, screen, out var clip))
            {
                continue;
            }

            var transform = LayerTransform.Fit(screen, card.DrawRect, clip, card.Alpha);
            using var layer = ScreenLayer.Begin(card.App.Id, screen, true);
            painter.PaintApp(screen, screenRadius, theme, card.App);
            layer.Transform(in transform);
        }

        using (ScreenLayer.BeginPassive(TopLayerId, screen))
        {
            var topList = ImGui.GetWindowDrawList();
            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                if (card.Alpha <= 0.004f || !VisibleOn(card.DrawRect, screen))
                {
                    continue;
                }

                Material.EdgeSquircle(topList, card.DrawRect.Min, card.DrawRect.Max,
                    CardRounding(card.DrawRect, screen, screenRadius), scale, card.Alpha);
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
        if (interactive)
        {
            UpdateInput(screen, scale);
        }

        if (cards.Count == 0)
        {
            DrawEmptyState(drawList, screen, opacity);
        }
        else
        {
            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                if (card.Alpha <= 0.004f || ReferenceEquals(card, committing) ||
                    (committing is null && ReferenceEquals(card.App, navigation.Current) && revealValue < InteractiveReveal))
                {
                    continue;
                }

                DrawCardLabel(drawList, card.App, card.DrawRect, scale, card.Alpha * opacity);
            }

            DrawArrows(drawList, screen, scale, delta, opacity, interactive);
            DrawFooter(drawList, screen, scale, opacity, interactive);
        }

        drawList.PopClipRect();
    }

    private void UpdateInput(Rect screen, float scale)
    {
        var mouse = ImGui.GetMousePos();
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.GetFrameCount() != openedFrame &&
            UiInteract.Hover(screen.Min, screen.Max) && !OverChrome(screen, scale, mouse))
        {
            pressed = true;
            panning = false;
            lifting = false;
            pressCard = CardAt(mouse);
            pressOrigin = mouse;
            lastMouse = mouse;
            panStartScroll = scrollTarget;
            velocityX = 0f;
            velocityY = 0f;
        }

        if (!pressed)
        {
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            TrackPress(mouse, scale);
            return;
        }

        ReleasePress(mouse, scale);
    }

    private bool OverChrome(Rect screen, float scale, Vector2 mouse)
    {
        if (cards.Count == 0)
        {
            return false;
        }

        if (CloseAllRect(screen, scale).Contains(mouse))
        {
            return true;
        }

        if (cards.Count < 2)
        {
            return false;
        }

        var arrowReach = (ArrowRadiusUnits + 6f) * scale;
        return (ArrowCenter(screen, scale, true) - mouse).Length() <= arrowReach ||
               (ArrowCenter(screen, scale, false) - mouse).Length() <= arrowReach;
    }

    private void TrackPress(Vector2 mouse, float scale)
    {
        var delta = FrameClock.Delta;
        if (delta > 0f)
        {
            var instantaneousX = (mouse.X - lastMouse.X) / delta;
            var instantaneousY = (mouse.Y - lastMouse.Y) / delta;
            velocityX += (instantaneousX - velocityX) * VelocityBlend;
            velocityY += (instantaneousY - velocityY) * VelocityBlend;
        }

        lastMouse = mouse;
        var travel = mouse - pressOrigin;
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
            scroll.SnapTo(RubberBand(panStartScroll - travel.X));
            return;
        }

        if (lifting && pressCard is { } card)
        {
            card.Lift.SnapTo(MathF.Max(0f, -travel.Y));
        }
    }

    private float RubberBand(float raw)
    {
        if (raw < 0f)
        {
            return raw * OverscrollResistance;
        }

        if (raw > layout.MaxScroll)
        {
            return layout.MaxScroll + (raw - layout.MaxScroll) * OverscrollResistance;
        }

        return raw;
    }

    private void ReleasePress(Vector2 mouse, float scale)
    {
        var travel = mouse - pressOrigin;
        var slop = TapSlopUnits * scale;
        var tapped = MathF.Abs(travel.X) < slop && MathF.Abs(travel.Y) < slop;
        if (lifting && pressCard is { FlyingOff: false } card)
        {
            var commitClose = card.Lift.Value > layout.CardHeight * CloseCommitFraction ||
                              -velocityY > CloseFlingUnitsPerSecond * scale;
            if (commitClose)
            {
                CloseCard(card);
            }
        }
        else if (panning)
        {
            var projected = panStartScroll - travel.X - velocityX * FlingProjectSeconds;
            SnapToNearest(projected);
        }
        else if (tapped)
        {
            if (pressCard is { FlyingOff: false } target)
            {
                OpenCard(target);
            }
            else if (pressCard is null)
            {
                Dismiss();
            }
        }

        ResetPress();
    }

    private void SnapToNearest(float projected)
    {
        if (cards.Count == 0 || layout.Pitch <= 0f)
        {
            scrollTarget = 0f;
            return;
        }

        var snapIndex = (int)Math.Clamp(MathF.Round(projected / layout.Pitch), 0f, cards.Count - 1);
        scrollTarget = snapIndex * layout.Pitch;
    }

    private Card? CardAt(Vector2 mouse)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            if (!cards[index].FlyingOff && cards[index].HitRect.Contains(mouse))
            {
                return cards[index];
            }
        }

        return null;
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
        if (ReferenceEquals(navigation.Current, card.App))
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
        pressed = false;
        panning = false;
        lifting = false;
        pressCard = null;
    }

    private static void DrawCardLabel(ImDrawListPtr drawList, IPhoneApp app, Rect bounds, float scale, float alpha)
    {
        if (alpha <= 0.004f)
        {
            return;
        }

        var tileSize = LabelTileUnits * scale;
        var gap = LabelGapUnits * scale;
        var name = Typography.FitText(app.DisplayName, bounds.Width - tileSize - gap, TextStyles.Caption1);
        var nameSize = Typography.Measure(name, TextStyles.Caption1);
        var groupLeft = bounds.Center.X - (tileSize + gap + nameSize.X) * 0.5f;
        var centerY = bounds.Min.Y - LabelLiftUnits * scale;
        var tileCenter = new Vector2(groupLeft + tileSize * 0.5f, centerY);
        var tileHalf = new Vector2(tileSize, tileSize) * 0.5f;
        var surface = IconTile.Surface(app.Accent);
        Squircle.Fill(drawList, tileCenter - tileHalf, tileCenter + tileHalf, tileSize * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(surface, alpha)));
        var ink = AppAccents.InkFor(app.Id);
        if (!AppIconArt.TryDraw(drawList, app.Id, tileCenter, tileSize * 0.9f, Palette.WithAlpha(ink, alpha),
                Palette.WithAlpha(Palette.Mix(surface, ink, 0.28f), alpha)))
        {
            drawList.AddCircleFilled(tileCenter, tileSize * 0.16f, ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)),
                12);
        }

        Typography.Draw(drawList, new Vector2(groupLeft + tileSize + gap, centerY - nameSize.Y * 0.5f), name,
            new Vector4(1f, 1f, 1f, 0.92f * alpha), TextStyles.Caption1);
    }

    private Vector2 ArrowCenter(Rect screen, float scale, bool left)
    {
        var x = left ? screen.Min.X + ArrowInsetUnits * scale : screen.Max.X - ArrowInsetUnits * scale;
        return new Vector2(x, layout.CenterY);
    }

    private void DrawArrows(ImDrawListPtr drawList, Rect screen, float scale, float delta, float opacity,
        bool interactive)
    {
        if (cards.Count < 2 || layout.Pitch <= 0f)
        {
            return;
        }

        var focusIndex = (int)Math.Clamp(MathF.Round(scrollTarget / layout.Pitch), 0f, cards.Count - 1);
        var radius = ArrowRadiusUnits * scale;
        if (focusIndex > 0 &&
            HoverButton.Circle(drawList, "switcher.left", ArrowCenter(screen, scale, true), radius,
                FontAwesomeIcon.ChevronLeft, ArrowTint, ArrowInk, delta, opacity, interactive))
        {
            scrollTarget = (focusIndex - 1) * layout.Pitch;
        }

        if (focusIndex < cards.Count - 1 &&
            HoverButton.Circle(drawList, "switcher.right", ArrowCenter(screen, scale, false), radius,
                FontAwesomeIcon.ChevronRight, ArrowTint, ArrowInk, delta, opacity, interactive))
        {
            scrollTarget = (focusIndex + 1) * layout.Pitch;
        }
    }

    private void DrawEmptyState(ImDrawListPtr drawList, Rect screen, float opacity)
    {
        if (!open || closingAll)
        {
            return;
        }

        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, layout.CenterY), Loc.T(L.AppSwitcher.Empty),
            new Vector4(1f, 1f, 1f, 0.75f * opacity), TextStyles.Subheadline);
    }

    private void DrawFooter(ImDrawListPtr drawList, Rect screen, float scale, float opacity, bool interactive)
    {
        var rect = CloseAllRect(screen, scale);
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (hovered ? 0.22f : 0.14f) * opacity)));
        Typography.DrawCentered(drawList, rect.Center, Loc.T(L.AppSwitcher.CloseAll),
            new Vector4(1f, 1f, 1f, opacity), TextStyles.SubheadlineEmphasized);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.GetFrameCount() != openedFrame)
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
