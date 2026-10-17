using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Stage;

internal sealed class CasinoStage
{
    public const float SnapSeconds = 2f;
    public const int ParticleCapacity = 768;

    private const float DeckRadius = 24f;
    private const float GlyphSize = 15f;
    private const float CapsulePadX = 14f;
    private const float CappedSeconds = 4f;
    private const float CappedFadeSeconds = 0.4f;
    private const float CappedPillHeight = 34f;
    private const float HostPadLeft = 10f;
    private const float HostIconGap = 6f;
    private const float HostPadRight = 14f;
    private const float KnockBadgeHeight = 18f;
    private const float KnockBadgeOffsetX = 4f;
    private const float KnockBadgeOffsetY = 5f;
    private const float KnockHaloGrowth = 0.6f;
    private const float KnockHaloAlpha = 0.55f;

    private static readonly VirtualKey[] RepeatKeys = { VirtualKey.SPACE };
    private static readonly Vector4 KnockBadgeInk = new(0.141f, 0.082f, 0.012f, 1f);

    private readonly StageBackdrop backdrop = new();
    private readonly ScreenFx fx;
    private readonly WinCelebration celebration;
    private readonly ParticleSystem particles = new(ParticleCapacity);
    private readonly CasinoInfoSheet info = new();
    private readonly BetsRail betsRail = new();
    private readonly CasinoBetsLog bets = new();
    private readonly HashSet<string> instantGames = new(StringComparer.Ordinal);
    private readonly ChipValueText capsuleValue = new();

    private CasinoStageSpec spec;
    private CasinoStageLayout layout;
    private RollingAmount balance;
    private bool practice;
    private bool hasSpec;
    private float phase;
    private float delta;
    private long lastFrameTick;
    private float unfocusedSeconds;
    private Rect actionRow;
    private float actionCursor;
    private int actionSecondaries;
    private float cappedLeft;
    private ChromeCapsule capsule;
    private string capsuleText = string.Empty;
    private string capsuleCoins = string.Empty;
    private float capsuleTextScale = 1f;

    public CasinoStage()
    {
        fx = new ScreenFx(backdrop);
        celebration = new WinCelebration(particles, fx, backdrop);
    }

    public WinCelebration Celebration => celebration;

    public StageBackdrop Backdrop => backdrop;

    public ScreenFx Fx => fx;

    public ParticleSystem Particles => particles;

    public CasinoBetsLog Bets => bets;

    public int SettledRounds { get; private set; }

    public CasinoFloorStore? Feed { get; set; }

    public CasinoStageLayout Layout => layout;

    public float Phase => phase;

    public Rect ActionRow => actionRow;

    public Rect Capsule => capsule.Rect;

    public bool OverlayOpen => info.IsOpen || betsRail.IsOpen || (Chips?.SheetOpen ?? false);

    public ChipsDesk? Chips { get; set; }

    public static Vector4 AccentFor(Backdrop preset) =>
        preset == Games.Framework.Backdrop.Felt ? AccentRing.Emerald : AppAccents.For("casino");

    public CasinoPreferences? Preferences { get; set; }

    public bool InstantFor(string gameId) => Preferences?.Instant(gameId) ?? instantGames.Contains(gameId);

    public void Reset()
    {
        celebration.Clear();
        particles.Clear();
        fx.Clear();
        info.Close();
        betsRail.Close();
        Chips?.Close();
        hasSpec = false;
        lastFrameTick = 0;
        unfocusedSeconds = 0f;
        cappedLeft = 0f;
    }

    public void ResetSession()
    {
        Reset();
        bets.Clear();
    }

    public void Settle(in CasinoBetRecord record)
    {
        bets.Record(record);
        cappedLeft = record.Capped ? CappedSeconds : 0f;
        SettledRounds++;
    }

    public CasinoStageFrame Begin(Rect full, Rect content, in CasinoStageSpec next, long balanceValue,
        in CasinoCeiling ceiling)
    {
        var scale = UiScale.Current;
        var now = Environment.TickCount64;
        info.Ceiling = ceiling;
        info.Rate = Chips?.Rate ?? CasinoChipLots.ChipPerCoin;
        var gap = lastFrameTick == 0 ? 0 : now - lastFrameTick;
        lastFrameTick = now;
        delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var focused = GameFocus.Active;
        var snap = gap > SnapSeconds * 1000f;
        if (!focused)
        {
            unfocusedSeconds += delta;
        }
        else
        {
            snap |= unfocusedSeconds > SnapSeconds;
            unfocusedSeconds = 0f;
        }

        if (!hasSpec || !string.Equals(spec.GameId, next.GameId, StringComparison.Ordinal)
                     || spec.Preset != next.Preset)
        {
            backdrop.Set(next.Preset);
            celebration.Clear();
            fx.Clear();
            particles.Clear();
            balance.Snap(balanceValue);
            hasSpec = true;
        }

        spec = next;
        practice = next.Practice;
        backdrop.SetWarmth(next.Warmth);
        backdrop.SetLampPool(next.LampPool);
        backdrop.SetFeltStyle(FeltStyle.Night, next.Rail);
        layout = CasinoStageLayout.Compute(full, content, next.Room, next.Practice, next.DeckHeight, scale);
        if (next.Host)
        {
            layout = layout.WithHost(HostPillWidth(scale), scale);
        }

        actionRow = DeckActions.Row(layout.Deck, scale);
        actionCursor = actionRow.Min.X;
        actionSecondaries = 0;
        var drawList = ImGui.GetWindowDrawList();
        backdrop.Update(delta, full, ImGui.GetMousePos(), UiInteract.Hover(full.Min, full.Max));
        backdrop.Draw(drawList, full, AccentFor(next.Preset), scale);
        CasinoSfx.Arm(focused);
        fx.Update(delta);
        particles.Update(delta);
        celebration.Update(delta, layout, snap);
        phase += delta;
        cappedLeft = MathF.Max(0f, cappedLeft - delta);
        if (snap)
        {
            balance.Snap(balanceValue);
        }
        else
        {
            balance.Update(balanceValue, delta);
        }

        FitCapsule(scale);

        if (layout.HasDeck)
        {
            var panel = layout.DeckPanel;
            var radius = DeckRadius * scale;
            drawList.PushClipRect(full.Min, full.Max, true);
            Material.Frosted(drawList, panel.Min, new Vector2(panel.Max.X, panel.Max.Y + radius), radius, scale);
            drawList.PopClipRect();
        }

        return new CasinoStageFrame(layout, delta, snap, InstantFor(next.GameId), focused, phase);
    }

    public CasinoStageAction End(AppSkin ui)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var full = layout.Full;
        particles.Draw(drawList, scale);
        celebration.Draw(drawList, layout, phase, scale);
        celebration.HandleSkip(layout);
        fx.Draw(drawList, full, AccentFor(spec.Preset));
        if (cappedLeft > 0f)
        {
            DrawCappedPill(drawList, scale);
        }

        if (layout.HasPractice)
        {
            PracticeRibbon.Draw(drawList, layout.Practice, scale);
        }

        var action = CasinoStageAction.None;
        if (spec.BetsRail && layout.HasDeck && BetsRail.DrawHandle(drawList, layout.Deck, scale))
        {
            betsRail.Open();
        }

        if (!layout.Landscape && DrawCapsule(drawList, scale))
        {
            action = CasinoStageAction.Cashier;
        }

        if (layout.HasHost && DrawHostPill(drawList, ui, scale))
        {
            action = CasinoStageAction.Host;
        }

        var chip = layout.ChipRadiusPixels;
        var backPressed = GlassCircle.Icon(drawList, ImGui.GetID("casino.stage.back"), layout.BackCenter, chip,
            GlyphSize * scale, PhoneIcons.ChevronLeft, CasinoColors.InkTitle, scale, GlassTone.Dark,
            Loc.T(L.Strip.Back), HoverLabelSide.Below);
        if (TouchTarget(layout.BackCenter) || backPressed)
        {
            action = CasinoStageAction.Back;
        }

        if (layout.Landscape)
        {
            return action;
        }

        var infoPressed = GlassCircle.Icon(drawList, ImGui.GetID("casino.stage.info"), layout.InfoCenter, chip,
            GlyphSize * scale, PhoneIcons.InfoCircle, CasinoColors.InkTitle, scale, GlassTone.Dark,
            Loc.T(L.Strip.Info), HoverLabelSide.Below);
        if (TouchTarget(layout.InfoCenter) || infoPressed)
        {
            info.Instant = InstantFor(spec.GameId);
            info.Open();
        }

        return action;
    }

    private bool TouchTarget(Vector2 center)
    {
        var reach = new Vector2(layout.TouchRadiusPixels, layout.TouchRadiusPixels);
        var min = center - reach;
        var max = center + reach;
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public bool SecondaryAction(string label, bool enabled, in ControlInk ink) =>
        layout.HasDeck && DeckActions.DrawSecondary(actionRow, ref actionCursor, ref actionSecondaries, label, enabled,
            ink, UiScale.Current);

    public bool PrimaryAction(string label, bool enabled, in ControlInk ink) =>
        layout.HasDeck && DeckActions.DrawPrimary(actionRow, actionCursor, label, enabled, ink);

    public bool RepeatPressed()
    {
        if (OverlayOpen || ImGui.IsAnyItemActive() || !GameInput.Claim(RepeatKeys))
        {
            return false;
        }

        return ImGui.IsKeyPressed(ImGuiKey.Space, false);
    }

    public void Gate()
    {
        info.Gate();
        betsRail.Gate();
        Chips?.Gate();
    }

    public void DrawOverlays(Rect screen, AppSkin ui)
    {
        if (!hasSpec)
        {
            return;
        }

        info.Draw(screen, ui, spec);
        SetInstant(spec.GameId, info.Instant);
        betsRail.Draw(screen, ui, bets, Feed);
        Chips?.Draw(screen, ui);
    }

    public CasinoInfoRequest TakeInfoRequest() => info.TakeRequest();

    public string TakeRoundRequest() => betsRail.TakeRoundRequest();

    public void CloseOverlays()
    {
        info.Close();
        betsRail.Close();
        Chips?.Close();
    }

    private void SetInstant(string gameId, bool instant)
    {
        if (!info.IsOpen || gameId.Length == 0)
        {
            return;
        }

        Preferences?.RememberInstant(gameId, instant);
        if (instant)
        {
            instantGames.Add(gameId);
            return;
        }

        instantGames.Remove(gameId);
    }

    private void FitCapsule(float scale)
    {
        var style = TextStyles.Headline;
        capsuleText = practice ? Loc.T(L.Strip.Practice) : NumberText.Compact(balance.Display);
        capsuleTextScale = style.Scale * (practice ? 1f : balance.PopScale);
        var textWidth = Typography.Measure(capsuleText, capsuleTextScale, style.Weight).X;
        var chrome = (CapsulePadX * 2f + GlyphSize) * scale + Metrics.Space.Xs * scale;
        var fullCoins = string.Empty;
        var compactCoins = string.Empty;
        var fullWidth = 0f;
        var compactWidth = 0f;
        if (!practice)
        {
            var rate = Chips?.Rate ?? CasinoChipLots.ChipPerCoin;
            fullCoins = capsuleValue.Full(balance.Target, rate);
            compactCoins = capsuleValue.Compact(balance.Target, rate);
            fullWidth = Typography.Measure(fullCoins, TextStyles.Footnote).X;
            compactWidth = Typography.Measure(compactCoins, TextStyles.Footnote).X;
        }

        capsule = ChromeCapsule.Fit(layout.CapsuleCenter, CasinoStageLayout.CapsuleHeight * scale,
            layout.CapsuleMaxWidth, chrome, textWidth, fullWidth, compactWidth, Metrics.Space.Sm * scale);
        capsuleCoins = capsule.Coins switch
        {
            CapsuleCoins.Full => fullCoins,
            CapsuleCoins.Compact => compactCoins,
            _ => string.Empty,
        };
        if (textWidth > capsule.TextRoom)
        {
            capsuleText = Typography.FitText(capsuleText, capsule.TextRoom, capsuleTextScale, style.Weight);
        }
    }

    private bool DrawCapsule(ImDrawListPtr drawList, float scale)
    {
        var rect = capsule.Rect;
        var min = rect.Min;
        var max = rect.Max;
        var height = rect.Height;
        var center = rect.Center;
        var weight = TextStyles.Headline.Weight;
        var glyph = GlyphSize * scale;
        var padX = CapsulePadX * scale;
        var interactive = !practice;
        var reach = MathF.Max(0f, (CasinoStageLayout.TouchTarget * scale - height) * 0.5f);
        var hitMin = new Vector2(min.X, min.Y - reach);
        var hitMax = new Vector2(max.X, max.Y + reach);
        var hovered = interactive && UiInteract.Hover(hitMin, hitMax);
        Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, backdrop.Ground, hovered ? 1f : 0.92f);
        var glyphCenter = new Vector2(min.X + padX + glyph * 0.5f, center.Y);
        if (practice)
        {
            ChipStack.DrawPractice(drawList, glyphCenter, glyph * 0.5f);
        }
        else
        {
            CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, glyphCenter, glyph);
        }

        var textSize = Typography.Measure(capsuleText, capsuleTextScale, weight);
        var textLeft = glyphCenter.X + glyph * 0.5f + Metrics.Space.Xs * scale;
        Typography.Draw(drawList, new Vector2(textLeft, center.Y - textSize.Y * 0.5f), capsuleText,
            practice ? CasinoColors.Practice : CasinoColors.Money, capsuleTextScale, weight);
        if (capsuleCoins.Length > 0)
        {
            var coinsSize = Typography.Measure(capsuleCoins, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(max.X - padX - coinsSize.X, center.Y - coinsSize.Y * 0.5f),
                capsuleCoins, CasinoColors.InkBody, TextStyles.Footnote);
        }

        if (!interactive)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(rect, Loc.T(L.Strip.CashierHint), HoverLabelSide.Below);
        }

        return UiInteract.Click(hitMin, hitMax, hovered);
    }

    private static float HostPillWidth(float scale)
    {
        var label = Typography.Measure(Loc.T(L.HostDesk.HostPill), TextStyles.SubheadlineEmphasized).X;
        return (HostPadLeft + GlyphSize + HostIconGap + HostPadRight) * scale + label;
    }

    private bool DrawHostPill(ImDrawListPtr drawList, AppSkin ui, float scale)
    {
        var pill = layout.HostPill;
        var key = ImGui.GetID("casino.stage.host");
        bool pressed;
        if (layout.HostCollapsed)
        {
            pressed = GlassCircle.Draw(drawList, key, pill.Center, pill.Height * 0.5f, scale, GlassTone.Dark,
                Loc.T(L.Tables.HostPanelHeading), HoverLabelSide.Below, out var grow);
            ProgressRing.CenterIcon(drawList, pill.Center, FontAwesomeIcon.SlidersH, CasinoColors.InkTitle,
                GlyphSize * scale * grow);
        }
        else
        {
            var hovered = UiInteract.Hover(pill.Min, pill.Max);
            var face = Button.Surface(drawList, pill, ui.Ink, ButtonStyle.Prominent, ButtonRole.Normal, true, hovered,
                key).Face;
            var iconCenter = new Vector2(face.Min.X + (HostPadLeft + GlyphSize * 0.5f) * scale, face.Center.Y);
            ProgressRing.CenterIcon(drawList, iconCenter, FontAwesomeIcon.SlidersH, CasinoArt.White,
                GlyphSize * scale * face.Height / MathF.Max(1f, pill.Height));
            var label = Loc.T(L.HostDesk.HostPill);
            var labelLeft = iconCenter.X + (GlyphSize * 0.5f + HostIconGap) * scale;
            var room = MathF.Max(1f, face.Max.X - HostPadRight * scale - labelLeft);
            var fitted = Typography.FitText(label, room, TextStyles.SubheadlineEmphasized);
            var size = Typography.Measure(fitted, TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList, new Vector2(labelLeft, face.Center.Y - size.Y * 0.5f), fitted, CasinoArt.White,
                TextStyles.SubheadlineEmphasized);
            pressed = UiInteract.Click(pill.Min, pill.Max, hovered);
        }

        DrawKnockBadge(drawList, pill, spec.HostKnocks, scale);
        return pressed;
    }

    private void DrawKnockBadge(ImDrawListPtr drawList, Rect pill, int knocks, float scale)
    {
        if (knocks <= 0)
        {
            return;
        }

        var label = Games.Framework.GameNumber.Label(knocks);
        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(label, style);
        var height = KnockBadgeHeight * scale;
        var width = MathF.Max(height, size.X + height * 0.5f);
        var max = new Vector2(pill.Max.X + KnockBadgeOffsetX * scale, pill.Min.Y - KnockBadgeOffsetY * scale + height);
        var min = new Vector2(max.X - width, max.Y - height);
        var wave = Pulse.Wave(Pulse.Calm);
        var halo = height * 0.5f * (1f + KnockHaloGrowth * wave);
        drawList.AddCircleFilled((min + max) * 0.5f, halo,
            ImGui.GetColorU32(CasinoColors.Money with { W = KnockHaloAlpha * (1f - wave) }), 24);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(CasinoColors.Money));
        Typography.Draw(drawList, (min + max) * 0.5f - size * 0.5f, label, KnockBadgeInk, style);
    }

    private void DrawCappedPill(ImDrawListPtr drawList, float scale)
    {
        var text = Loc.T(L.Chips.MaxWinReached);
        var style = TextStyles.SubheadlineEmphasized;
        var height = CappedPillHeight * scale;
        var padX = height * 0.5f;
        var textScale = Typography.FitScale(text, MathF.Max(1f, layout.Safe.Width - padX * 2f), style.Scale,
            TextStyles.Footnote.Scale, style.Weight);
        var size = Typography.Measure(text, textScale, style.Weight);
        var rect = layout.Notice(size.X + padX * 2f, height, scale);
        var center = rect.Center;
        var alpha = Math.Clamp(cappedLeft / CappedFadeSeconds, 0f, 1f);
        Material.FrostedGlass(drawList, rect.Min, rect.Max, height * 0.5f, scale, alpha);
        Typography.Draw(drawList, new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f), text,
            CasinoColors.Money with { W = alpha }, textScale, style.Weight);
    }
}
