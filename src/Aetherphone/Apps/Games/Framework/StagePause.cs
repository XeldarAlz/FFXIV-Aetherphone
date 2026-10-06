using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal enum PauseAction : byte
{
    None,
    Resume,
    Restart,
    Leaderboard,
    Quit,
}

internal sealed class StagePause
{
    private const float VeilAlpha = 0.72f;
    private const float FadeSeconds = 0.12f;
    private const float ButtonWidth = 200f;
    private const float ButtonHeight = 44f;
    private const float ButtonGap = 10f;
    private const float TitleGap = 22f;
    private const float ActiveThreshold = 0.9f;
    private const int ButtonCount = 4;

    private Spring veil;

    public float Alpha => Math.Clamp(veil.Value, 0f, 1f);

    public void Reset()
    {
        veil.SnapTo(0f);
    }

    public PauseAction Draw(ImDrawListPtr drawList, Rect full, PhoneTheme theme, Vector4 accent, float deltaSeconds,
        bool shown, float scale)
    {
        veil.Step(shown ? 1f : 0f, FadeSeconds, deltaSeconds);
        var alpha = Alpha;
        if (alpha <= 0.01f)
        {
            return PauseAction.None;
        }

        Material.Veil(drawList, full.Min, full.Max, VeilAlpha * alpha);
        var buttonHeight = ButtonHeight * scale;
        var gap = ButtonGap * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var stackHeight = titleHeight + TitleGap * scale + ButtonCount * buttonHeight + (ButtonCount - 1) * gap;
        var top = full.Center.Y - stackHeight * 0.5f;
        var centerX = full.Center.X;
        Typography.DrawCentered(drawList, new Vector2(centerX, top + titleHeight * 0.5f), Loc.T(L.Games.Paused),
            new Vector4(1f, 1f, 1f, alpha), TextStyles.Title2);
        if (alpha < ActiveThreshold)
        {
            return PauseAction.None;
        }

        var size = new Vector2(ButtonWidth * scale, buttonHeight);
        var secondary = Palette.Mix(theme.SurfaceMuted, accent, 0.18f);
        var y = top + titleHeight + TitleGap * scale + buttonHeight * 0.5f;
        if (GameHud.Button(new Vector2(centerX, y), size, Loc.T(L.Stage.Resume), accent, theme))
        {
            return PauseAction.Resume;
        }

        y += buttonHeight + gap;
        if (GameHud.Button(new Vector2(centerX, y), size, Loc.T(L.Stage.Restart), secondary, theme))
        {
            return PauseAction.Restart;
        }

        y += buttonHeight + gap;
        if (GameHud.Button(new Vector2(centerX, y), size, Loc.T(L.Stage.Leaderboard), secondary, theme))
        {
            return PauseAction.Leaderboard;
        }

        y += buttonHeight + gap;
        if (GameHud.Button(new Vector2(centerX, y), size, Loc.T(L.Stage.Quit), secondary, theme))
        {
            return PauseAction.Quit;
        }

        return PauseAction.None;
    }
}
