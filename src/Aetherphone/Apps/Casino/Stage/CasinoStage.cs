using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

namespace Aetherphone.Apps.Casino.Stage;

internal sealed class CasinoStage
{
    public const float SnapSeconds = 2f;
    public const int ParticleCapacity = 768;

    private const float DeckRadius = 24f;
    private const float GlyphSize = 15f;
    private const float CapsulePadX = 14f;
    private const float RealityVeil = 0.6f;
    private const float RealityCardWidth = 300f;
    private const float CappedSeconds = 4f;
    private const float CappedFadeSeconds = 0.4f;
    private const float CappedPillHeight = 34f;

    private static readonly VirtualKey[] RepeatKeys = { VirtualKey.SPACE };

    private readonly StageBackdrop backdrop = new();
    private readonly ScreenFx fx;
    private readonly WinCelebration celebration;
    private readonly ParticleSystem particles = new(ParticleCapacity);
    private readonly CasinoInfoSheet info = new();
    private readonly BetsRail betsRail = new();
    private readonly CasinoBetsLog bets = new();
    private readonly HashSet<string> instantGames = new(StringComparer.Ordinal);
    private readonly RealityCheck reality = new();
    private readonly ChipValueText capsuleValue = new();

    private string realityText = string.Empty;
    private int realityRounds = -1;
    private LanguageInfo? realityLanguage;

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

    public CasinoFloorStore? Feed { get; set; }

    public CasinoStageLayout Layout => layout;

    public float Phase => phase;

    public Rect ActionRow => actionRow;

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

    public bool RealityDue => reality.Due;

    public void ResetSession()
    {
        Reset();
        bets.Clear();
        reality.Reset();
    }

    public void Settle(in CasinoBetRecord record)
    {
        bets.Record(record);
        reality.Record(record.Stake, record.Payout, Environment.TickCount64);
        cappedLeft = record.Capped ? CappedSeconds : 0f;
    }

    public CasinoStageFrame Begin(Rect full, Rect content, in CasinoStageSpec next, long balanceValue,
        in CasinoCeiling ceiling)
    {
        var scale = UiScale.Current;
        var now = Environment.TickCount64;
        info.Ceiling = ceiling;
        info.Rate = Chips?.Rate ?? CasinoChipLots.ChipPerCoin;
        reality.Tick(now);
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

        if (layout.HasDeck)
        {
            var panel = layout.DeckPanel;
            var radius = DeckRadius * scale;
            drawList.PushClipRect(full.Min, full.Max, true);
            Material.Frosted(drawList, panel.Min, new Vector2(panel.Max.X, panel.Max.Y + radius), radius, scale);
            drawList.PopClipRect();
        }

        return new CasinoStageFrame(layout, delta, snap, InstantFor(next.GameId), focused, reality.Due, phase);
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
        if (reality.Due && DrawRealityCheck(drawList, ui, scale))
        {
            action = CasinoStageAction.Back;
        }

        if (spec.BetsRail && layout.HasDeck && BetsRail.DrawHandle(drawList, layout.Deck, scale))
        {
            betsRail.Open();
        }

        if (DrawCapsule(drawList, scale))
        {
            action = CasinoStageAction.Cashier;
        }

        var chip = layout.ChipRadiusPixels;
        var backPressed = GlassCircle.Icon(drawList, ImGui.GetID("casino.stage.back"), layout.BackCenter, chip,
            GlyphSize * scale, PhoneIcons.ChevronLeft, CasinoColors.InkTitle, scale, GlassTone.Dark,
            Loc.T(L.Strip.Back), HoverLabelSide.Below);
        if (TouchTarget(layout.BackCenter) || backPressed)
        {
            action = CasinoStageAction.Back;
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

    private bool DrawRealityCheck(ImDrawListPtr drawList, AppSkin skin, float scale)
    {
        var now = Environment.TickCount64;
        var full = layout.Full;
        drawList.AddRectFilled(full.Min, full.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, RealityVeil)));
        var safe = layout.Safe;
        var width = MathF.Min(safe.Width, RealityCardWidth * scale);
        var left = safe.Center.X - width * 0.5f;
        var title = Loc.T(L.Strip.RealityTitle);
        var body = RealityBody(now);
        var cardHeight = CasinoNotice.Height(CasinoNoticeKind.Card, title, body, width, scale);
        var buttonHeight = Button.RegularHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var total = cardHeight + gap + buttonHeight * 2f + gap;
        var top = safe.Center.Y - total * 0.5f;
        var bottom = CasinoNotice.Draw(drawList, skin, CasinoNoticeKind.Card, title, body, left, top, width, scale);
        var keep = new Rect(new Vector2(left, bottom + gap), new Vector2(left + width, bottom + gap + buttonHeight));
        if (Button.Draw(drawList, keep, Loc.T(L.Strip.KeepPlaying), skin.Ink))
        {
            reality.Acknowledge(now);
        }

        var leave = new Rect(new Vector2(left, keep.Max.Y + gap), new Vector2(left + width, keep.Max.Y + gap + buttonHeight));
        if (!Button.Draw(drawList, leave, Loc.T(L.Strip.TakeBreak), skin.Ink, ButtonStyle.Gray))
        {
            return false;
        }

        reality.Acknowledge(now);
        return true;
    }

    private string RealityBody(long now)
    {
        if (reality.Rounds == realityRounds && ReferenceEquals(realityLanguage, Loc.Current))
        {
            return realityText;
        }

        realityRounds = reality.Rounds;
        realityLanguage = Loc.Current;
        var minutes = (int)(reality.SessionMilliseconds(now) / 60_000);
        realityText = Loc.T(L.Strip.RealityBody, Games.Framework.GameNumber.Label(reality.Rounds),
            Games.Framework.GameNumber.Label(Math.Max(1, minutes)), NumberText.Signed(reality.Net));
        return realityText;
    }

    private bool DrawCapsule(ImDrawListPtr drawList, float scale)
    {
        var height = CasinoStageLayout.CapsuleHeight * scale;
        var style = TextStyles.Headline;
        var text = practice ? Loc.T(L.Strip.Practice) : NumberText.Compact(balance.Display);
        var textScale = style.Scale * (practice ? 1f : balance.PopScale);
        var textSize = Typography.Measure(text, textScale, style.Weight);
        var glyph = GlyphSize * scale;
        var padX = CapsulePadX * scale;
        var coinGap = Metrics.Space.Sm * scale;
        var core = padX * 2f + glyph + Metrics.Space.Xs * scale + textSize.X;
        var coinWidth = 0f;
        var coins = practice ? string.Empty : CapsuleCoins(layout.CapsuleMaxWidth - core - coinGap, out coinWidth);
        var trailing = coins.Length > 0 ? coinGap + coinWidth : 0f;
        var width = MathF.Min(layout.CapsuleMaxWidth, core + trailing);
        var center = layout.CapsuleCenter;
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
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

        var textLeft = glyphCenter.X + glyph * 0.5f + Metrics.Space.Xs * scale;
        var available = max.X - padX - trailing - textLeft;
        if (textSize.X > available)
        {
            text = Typography.FitText(text, available, textScale, style.Weight);
            textSize = Typography.Measure(text, textScale, style.Weight);
        }

        Typography.Draw(drawList, new Vector2(textLeft, center.Y - textSize.Y * 0.5f), text,
            practice ? CasinoColors.Practice : CasinoColors.Money, textScale, style.Weight);
        if (coins.Length > 0)
        {
            var footnote = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(max.X - padX - coinWidth, center.Y - footnote * 0.5f), coins,
                CasinoColors.InkBody, TextStyles.Footnote);
        }

        if (!interactive)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(min, max), Loc.T(L.Strip.CashierHint), HoverLabelSide.Below);
        }

        return UiInteract.Click(hitMin, hitMax, hovered);
    }

    private void DrawCappedPill(ImDrawListPtr drawList, float scale)
    {
        var text = Loc.T(L.Chips.MaxWinReached);
        var style = TextStyles.SubheadlineEmphasized;
        var safe = layout.Safe;
        var height = CappedPillHeight * scale;
        var padX = height * 0.5f;
        var textScale = Typography.FitScale(text, MathF.Max(1f, safe.Width - padX * 2f), style.Scale,
            TextStyles.Footnote.Scale, style.Weight);
        var size = Typography.Measure(text, textScale, style.Weight);
        var width = MathF.Min(safe.Width, size.X + padX * 2f);
        var center = new Vector2(safe.Center.X, safe.Min.Y + height * 0.5f);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var alpha = Math.Clamp(cappedLeft / CappedFadeSeconds, 0f, 1f);
        Material.FrostedGlass(drawList, min, max, height * 0.5f, scale, alpha);
        Typography.Draw(drawList, new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f), text,
            CasinoColors.Money with { W = alpha }, textScale, style.Weight);
    }

    private string CapsuleCoins(float room, out float width)
    {
        width = 0f;
        if (room <= 0f)
        {
            return string.Empty;
        }

        var rate = Chips?.Rate ?? CasinoChipLots.ChipPerCoin;
        var full = capsuleValue.Full(balance.Target, rate);
        var fullWidth = Typography.Measure(full, TextStyles.Footnote).X;
        if (fullWidth <= room)
        {
            width = fullWidth;
            return full;
        }

        var compact = capsuleValue.Compact(balance.Target, rate);
        var compactWidth = Typography.Measure(compact, TextStyles.Footnote).X;
        if (compactWidth > room)
        {
            return string.Empty;
        }

        width = compactWidth;
        return compact;
    }
}
