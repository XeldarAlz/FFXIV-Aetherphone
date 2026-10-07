using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class StageHandoff
{
    private const string SurfaceId = "stage.handoff";
    private const float FadeSeconds = 0.18f;
    private const float EntranceSpeed = 2.2f;
    private const float ReadyEntrance = 0.35f;
    private const float TopDarken = 0.52f;
    private const float BottomDarken = 0.78f;
    private const float BadgeRadius = 44f;
    private const float BadgeIconFraction = 0.9f;
    private const float BadgeGlow = 0.55f;
    private const float RingWidth = 2f;
    private const float TextMaxWidth = 300f;
    private const float HintRest = 0.55f;
    private const float HintSwing = 0.3f;
    private const float LiftDistance = 12f;

    private Spring cover;
    private float entrance;
    private int seat = GameOutcome.NoSeat;

    public void Reset()
    {
        cover.SnapTo(0f);
        entrance = 0f;
        seat = GameOutcome.NoSeat;
    }

    public bool Draw(ImDrawListPtr drawList, Rect full, GameSession session, float deltaSeconds, float scale)
    {
        if (session.State is StageFlow.Intro or StageFlow.Result)
        {
            Reset();
            return false;
        }

        if (session.HandoffPending)
        {
            if (seat != session.HandoffSeat || cover.Value < 1f)
            {
                entrance = 0f;
            }

            seat = session.HandoffSeat;
            cover.SnapTo(1f);
        }
        else
        {
            cover.Step(0f, FadeSeconds, deltaSeconds);
        }

        var alpha = Math.Clamp(cover.Value, 0f, 1f);
        if (alpha <= 0.01f || seat < 0)
        {
            return false;
        }

        entrance = GameJuice.Advance(entrance, deltaSeconds, EntranceSpeed);
        var color = GameSeats.Color(seat);
        var topColor = ImGui.GetColorU32(GamePalette.Darken(color, TopDarken) with { W = alpha });
        var bottomColor = ImGui.GetColorU32(GamePalette.Darken(color, BottomDarken) with { W = alpha });
        drawList.AddRectFilledMultiColor(full.Min, full.Max, topColor, topColor, bottomColor, bottomColor);
        var badgeRadius = BadgeRadius * scale;
        var passLine = GameSeats.PassLine(seat);
        var hint = Loc.T(L.Stage.TapWhenReady);
        var textWidth = MathF.Min(full.Width - Metrics.Space.Xl * scale * 2f, TextMaxWidth * scale);
        var passHeight = Typography.MeasureWrappedBlock(passLine, TextStyles.Title1, textWidth).Y;
        var hintHeight = Typography.LineHeight(TextStyles.Subheadline);
        var gapLg = Metrics.Space.Lg * scale;
        var gapSm = Metrics.Space.Sm * scale;
        var stack = badgeRadius * 2f + gapLg + passHeight + gapSm + hintHeight;
        var top = full.Center.Y - stack * 0.5f;
        var centerX = full.Center.X;
        var pop = GameJuice.PopIn(Easing.Clamp01(entrance * 1.6f));
        var badgeCenter = new Vector2(centerX, top + badgeRadius);
        ProgressRing.Glow(badgeCenter, badgeRadius * 1.5f, color, BadgeGlow * alpha);
        drawList.AddCircleFilled(badgeCenter, badgeRadius * pop, ImGui.GetColorU32(color with { W = alpha }), 48);
        drawList.AddCircle(badgeCenter, badgeRadius * pop, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f * alpha)), 48,
            RingWidth * scale);
        ProgressRing.CenterIcon(drawList, badgeCenter, FontAwesomeIcon.User, GamePalette.InkOn(color) with { W = alpha },
            badgeRadius * BadgeIconFraction * pop);
        top += badgeRadius * 2f + gapLg;
        var textPhase = Easing.EaseOutCubic(GameJuice.Stagger(entrance, 1, 3));
        Typography.DrawWrappedCentered(drawList,
            new Vector2(centerX, top + passHeight * 0.5f + (1f - textPhase) * LiftDistance * scale), passLine,
            GamePalette.InkLight with { W = alpha * textPhase }, TextStyles.Title1, textWidth);
        top += passHeight + gapSm;
        var hintPhase = Easing.EaseOutCubic(GameJuice.Stagger(entrance, 2, 3));
        var breathe = HintRest + HintSwing * Pulse.Wave(Pulse.Calm);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + hintHeight * 0.5f), hint,
            GamePalette.InkLight with { W = alpha * hintPhase * breathe }, TextStyles.Subheadline);
        if (!session.HandoffPending || session.State != StageFlow.Playing || entrance < ReadyEntrance)
        {
            return false;
        }

        var hovered = PressSurface.Claim(SurfaceId, full, out _);
        if (!UiInteract.Click(full.Min, full.Max, hovered) && !GameInput.Pressed(ImGuiKey.Space, ImGuiKey.Enter))
        {
            return false;
        }

        session.CompleteHandoff();
        return true;
    }
}
