using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Games;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hub;

internal readonly struct HubGround
{
    private readonly Vector4 bodyTop;
    private readonly Vector4 bodyBottom;
    private readonly Vector4 bloomTop;
    private readonly Vector4 bloomBottom;
    private readonly float top;
    private readonly float height;

    public HubGround(in AppPalette palette, Rect frame)
    {
        bodyTop = palette.BackdropTop;
        bodyBottom = palette.BackdropBottom;
        bloomTop = palette.BloomTop;
        bloomBottom = palette.BloomBottom;
        top = frame.Min.Y;
        height = frame.Height;
    }

    public Vector4 At(float y)
    {
        var fraction = height <= 0f ? 0f : (y - top) / height;
        var body = Vector4.Lerp(bodyTop, bodyBottom, fraction);
        var bloom = Vector4.Lerp(bloomTop, bloomBottom, fraction);
        return Vector4.Lerp(body, bloom, Math.Clamp(bloom.W, 0f, 1f)) with { W = 1f };
    }
}

internal sealed class LivePreview
{
    public const float SafeInset = 12f;
    private const float PosterIcon = 96f;
    private const float IconBreathing = 6f;
    private const float ScrimFrom = 0.4f;
    private const float ScrimAlpha = 0.6f;

    private static readonly string[] Allowlist =
    [
        "tetris", "breakout", "invaders", "capman", "beat", "stack", "bubbles", "swoop", "trailblaze", "pinball",
        "pegfall", "minigolf",
    ];

    private static readonly Vector4 RimInk = new(1f, 1f, 1f, 0.08f);

    private readonly GameStatsStore stats;
    private readonly GameSession session;
    private readonly HudModel hud = new();
    private readonly StageBackdrop backdrop = new();
    private readonly ScreenFx fx;
    private readonly StageChrome chrome = new();
    private IMiniGame? game;
    private int day = -1;
    private bool allowed;

    public LivePreview(GameStatsStore stats)
    {
        this.stats = stats;
        session = new GameSession(stats, NullScoreSink.Instance, NullRankSource.Instance);
        fx = new ScreenFx(backdrop);
    }

    public static bool Allows(string gameId)
    {
        for (var index = 0; index < Allowlist.Length; index++)
        {
            if (string.Equals(Allowlist[index], gameId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static void CoverCorners(ImDrawListPtr drawList, Rect card, float radius, in HubGround ground, float scale)
    {
        var middle = card.Center.Y;
        drawList.PushClipRect(card.Min, new Vector2(card.Max.X, middle), true);
        Squircle.FillOutsideCorners(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ground.At(card.Min.Y)),
            0f);
        drawList.PopClipRect();
        drawList.PushClipRect(new Vector2(card.Min.X, middle), card.Max, true);
        Squircle.FillOutsideCorners(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ground.At(card.Max.Y)),
            0f);
        drawList.PopClipRect();
        Rim(drawList, card, radius, scale);
    }

    public static void Rim(ImDrawListPtr drawList, Rect card, float radius, float scale) =>
        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(RimInk),
            Metrics.Stroke.Hairline * scale);

    public static void Scrim(ImDrawListPtr drawList, Rect card, float fromFraction, float alpha)
    {
        var top = card.Min.Y + card.Height * fromFraction;
        drawList.AddRectFilledMultiColor(new Vector2(card.Min.X, top), card.Max, 0u, 0u,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, alpha)), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, alpha)));
    }

    public void Prepare(IMiniGame daily, int today)
    {
        if (ReferenceEquals(game, daily) && day == today)
        {
            return;
        }

        game = daily;
        day = today;
        allowed = Allows(daily.Id);
        var spec = daily.Spec;
        backdrop.Set(allowed ? spec.Backdrop : PosterCard.PosterBackdrop(spec.Backdrop));
        if (!allowed)
        {
            backdrop.SetSky(PosterCard.NightSky);
        }

        fx.Clear();
        hud.Clear();
        session.Begin(spec, new GameStart(stats.LastMode(spec.Id), GameSeed.Daily(spec.Id, today), true));
    }

    public void Draw(ImDrawListPtr drawList, Rect card, Rect art, PhoneTheme theme, bool live, bool hovered,
        float deltaSeconds, in HubGround ground, float scale)
    {
        if (game is null)
        {
            return;
        }

        var accent = game.Accent;
        drawList.PushClipRect(card.Min, card.Max, true);
        if (live)
        {
            backdrop.Update(deltaSeconds, card, ImGui.GetMousePos(), hovered);
        }

        backdrop.Draw(drawList, card, accent, scale);
        if (live && allowed)
        {
            hud.Clear();
            game.DrawIdle(new GameContext(card, card.Inset(SafeInset * scale), theme, 0f, deltaSeconds, session, hud,
                fx, backdrop, chrome));
        }
        else
        {
            DrawPosterIcon(drawList, art, accent, scale);
        }

        Scrim(drawList, card, ScrimFrom, ScrimAlpha);
        drawList.PopClipRect();
        CoverCorners(drawList, card, HubMetrics.CardRadius * scale, ground, scale);
    }

    private void DrawPosterIcon(ImDrawListPtr drawList, Rect art, Vector4 accent, float scale)
    {
        var side = MathF.Min(PosterIcon * scale, art.Height - IconBreathing * 2f * scale);
        if (side <= 0f || game is null)
        {
            return;
        }

        var half = new Vector2(side * 0.5f);
        var center = art.Center;
        GameIconArt.Draw(drawList, game.Id, accent, center - half, center + half, IconAppearance.Default, true);
    }
}
