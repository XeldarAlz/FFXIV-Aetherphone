using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
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

    private static readonly VirtualKey[] RepeatKeys = { VirtualKey.SPACE };

    private readonly StageBackdrop backdrop = new();
    private readonly ScreenFx fx;
    private readonly WinCelebration celebration;
    private readonly ParticleSystem particles = new(ParticleCapacity);
    private readonly CasinoInfoSheet info = new();
    private readonly BetsRail betsRail = new();
    private readonly CasinoBetsLog bets = new();
    private readonly HashSet<string> instantGames = new(StringComparer.Ordinal);

    private CasinoStageSpec spec;
    private CasinoStageLayout layout;
    private RollingAmount balance;
    private bool practice;
    private bool hasSpec;
    private float phase;
    private float delta;
    private long lastFrameTick;
    private float unfocusedSeconds;

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

    public CasinoStageLayout Layout => layout;

    public float Phase => phase;

    public bool OverlayOpen => info.IsOpen || betsRail.IsOpen;

    public static Vector4 AccentFor(Backdrop preset) =>
        preset == Games.Framework.Backdrop.Felt ? AccentRing.Emerald : AppAccents.For("casino");

    public bool InstantFor(string gameId) => instantGames.Contains(gameId);

    public void Reset()
    {
        celebration.Clear();
        particles.Clear();
        fx.Clear();
        info.Close();
        betsRail.Close();
        hasSpec = false;
        lastFrameTick = 0;
        unfocusedSeconds = 0f;
    }

    public CasinoStageFrame Begin(Rect content, in CasinoStageSpec next, long balanceValue)
    {
        var scale = UiScale.Current;
        var now = Environment.TickCount64;
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
        layout = CasinoStageLayout.Compute(content, next.Room, next.Practice,
            next.Deck ? CasinoStageLayout.DeckHeight : 0f, scale);
        var drawList = ImGui.GetWindowDrawList();
        var full = layout.Full;
        backdrop.Update(delta, full, ImGui.GetMousePos(), UiInteract.Hover(full.Min, full.Max));
        backdrop.Draw(drawList, full, AccentFor(next.Preset), scale);
        CasinoSfx.Arm(focused);
        fx.Update(delta);
        particles.Update(delta);
        celebration.Update(delta, layout, snap);
        phase += delta;
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
            var deck = layout.Deck;
            var radius = DeckRadius * scale;
            drawList.PushClipRect(full.Min, full.Max, true);
            Material.Frosted(drawList, deck.Min, new Vector2(deck.Max.X, deck.Max.Y + radius), radius, scale);
            drawList.PopClipRect();
        }

        return new CasinoStageFrame(layout, delta, snap, InstantFor(next.GameId), focused, phase);
    }

    public CasinoStageAction End()
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var full = layout.Full;
        particles.Draw(drawList, scale);
        celebration.Draw(drawList, layout, phase, scale);
        celebration.HandleSkip(layout);
        fx.Draw(drawList, full, AccentFor(spec.Preset));
        if (layout.HasPractice)
        {
            PracticeRibbon.Draw(drawList, layout.Practice, scale);
        }

        var action = CasinoStageAction.None;
        if (spec.BetsRail && layout.HasDeck && BetsRail.DrawHandle(drawList, layout.Deck, scale))
        {
            betsRail.Open();
        }

        if (DrawCapsule(drawList, scale))
        {
            action = CasinoStageAction.Cashier;
        }

        var chip = layout.ChipRadiusPixels;
        if (GlassCircle.Icon(drawList, ImGui.GetID("casino.stage.back"), layout.BackCenter, chip, GlyphSize * scale,
                PhoneIcons.ChevronLeft, CasinoColors.InkTitle, scale, GlassTone.Dark, Loc.T(L.Strip.Back),
                HoverLabelSide.Below))
        {
            action = CasinoStageAction.Back;
        }

        if (GlassCircle.Icon(drawList, ImGui.GetID("casino.stage.info"), layout.InfoCenter, chip, GlyphSize * scale,
                PhoneIcons.InfoCircle, CasinoColors.InkTitle, scale, GlassTone.Dark, Loc.T(L.Strip.Info),
                HoverLabelSide.Below))
        {
            info.Instant = InstantFor(spec.GameId);
            info.Open();
        }

        return action;
    }

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
    }

    public void DrawOverlays(Rect screen, AppSkin ui)
    {
        if (!hasSpec)
        {
            return;
        }

        info.Draw(screen, ui, spec);
        SetInstant(spec.GameId, info.Instant);
        betsRail.Draw(screen, ui, bets);
    }

    public CasinoInfoRequest TakeInfoRequest() => info.TakeRequest();

    public string TakeRoundRequest() => betsRail.TakeRoundRequest();

    public void CloseOverlays()
    {
        info.Close();
        betsRail.Close();
    }

    private void SetInstant(string gameId, bool instant)
    {
        if (!info.IsOpen || gameId.Length == 0)
        {
            return;
        }

        if (instant)
        {
            instantGames.Add(gameId);
            return;
        }

        instantGames.Remove(gameId);
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
        var width = MathF.Min(layout.CapsuleMaxWidth, padX * 2f + glyph + Metrics.Space.Xs * scale + textSize.X);
        var center = layout.CapsuleCenter;
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var interactive = !practice;
        var hovered = interactive && UiInteract.Hover(min, max);
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
        var available = max.X - padX - textLeft;
        if (textSize.X > available)
        {
            text = Typography.FitText(text, available, textScale, style.Weight);
            textSize = Typography.Measure(text, textScale, style.Weight);
        }

        Typography.Draw(drawList, new Vector2(textLeft, center.Y - textSize.Y * 0.5f), text,
            practice ? CasinoColors.Practice : CasinoColors.Money, textScale, style.Weight);
        if (!interactive)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(min, max), Loc.T(L.Strip.CashierHint), HoverLabelSide.Below);
        }

        return UiInteract.Click(min, max, hovered);
    }
}
