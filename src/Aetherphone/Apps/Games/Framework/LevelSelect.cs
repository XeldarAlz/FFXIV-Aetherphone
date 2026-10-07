using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class LevelSelect
{
    private const string SurfaceId = "stage.levels";
    private const float VeilAlpha = 0.86f;
    private const float FadeSeconds = 0.12f;
    private const float ActiveThreshold = 0.9f;
    private const float EntranceSpeed = 2.4f;
    private const int MinColumns = 5;
    private const float TileMaxWidth = 76f;
    private const float TileGap = 10f;
    private const float TileAspect = 1.12f;
    private const float TileRadius = 14f;
    private const float NumberRise = 0.12f;
    private const float StarDrop = 0.27f;
    private const float StarFraction = 0.19f;
    private const float LockFraction = 0.2f;
    private const float RingWidth = 2f;
    private const float ShadowOffset = 2f;
    private const float ShadowAlpha = 0.28f;
    private const float SheenAlpha = 0.14f;
    private const float UnlockedTint = 0.30f;
    private const float LockedTint = 0.06f;
    private const float LockedAlpha = 0.45f;
    private const float HoverLighten = 0.08f;
    private const float EmptyStarAlpha = 0.24f;
    private const float WheelStep = 56f;

    private readonly KineticScroller scroller = new();
    private Spring veil;
    private LabelPairSlot totalLabel;
    private float entrance;
    private bool open;
    private bool pressing;
    private bool focusPending;

    public bool IsOpen => open;

    public void Open()
    {
        open = true;
        focusPending = true;
        pressing = false;
        entrance = 0f;
        scroller.Reset();
    }

    public void Close()
    {
        open = false;
        pressing = false;
        scroller.CancelGesture();
    }

    public void Reset()
    {
        Close();
        veil.SnapTo(0f);
    }

    public int Draw(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var deltaSeconds = context.RawDeltaSeconds;
        veil.Step(open ? 1f : 0f, FadeSeconds, deltaSeconds);
        var alpha = Math.Clamp(veil.Value, 0f, 1f);
        if (alpha <= 0.01f)
        {
            return 0;
        }

        var session = context.Session;
        var full = context.Full;
        var count = session.LevelCount;
        entrance = GameJuice.Advance(entrance, deltaSeconds, EntranceSpeed);
        Material.Veil(drawList, full.Min, full.Max, VeilAlpha * alpha);
        DrawHeader(drawList, full, session, count, context.Theme, alpha, scale);
        if (count == 0)
        {
            return 0;
        }

        var area = StageLayout.Safe(full, HudStyle.Standard, scale);
        var gap = TileGap * scale;
        var columns = Math.Max(MinColumns, (int)((area.Width + gap) / (TileMaxWidth * scale + gap)));
        var tileWidth = (area.Width - gap * (columns - 1)) / columns;
        var tileHeight = tileWidth * TileAspect;
        var rows = (count + columns - 1) / columns;
        scroller.Scale = scale;
        scroller.SetBounds(rows * tileHeight + (rows - 1) * gap - area.Height);
        if (focusPending)
        {
            focusPending = false;
            var focusRow = Math.Max(0, session.Level - 1) / columns;
            scroller.SyncOffset(focusRow * (tileHeight + gap) + tileHeight * 0.5f - area.Height * 0.5f);
        }

        var interactive = open && alpha >= ActiveThreshold;
        if (interactive)
        {
            Scroll(area, deltaSeconds);
            if (GameInput.Pressed(ImGuiKey.Escape))
            {
                Close();
                return 0;
            }
        }

        scroller.Tick(deltaSeconds);
        var top = area.Min.Y - scroller.Offset + scroller.PullDistance;
        var chosen = 0;
        drawList.PushClipRect(area.Min, area.Max, true);
        for (var index = 0; index < count; index++)
        {
            var row = index / columns;
            var min = new Vector2(area.Min.X + index % columns * (tileWidth + gap), top + row * (tileHeight + gap));
            var tile = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            if (tile.Max.Y < area.Min.Y || tile.Min.Y > area.Max.Y)
            {
                continue;
            }

            var phase = GameJuice.Stagger(entrance, index % (columns * 2), columns * 2);
            if (DrawTile(drawList, tile, area, session, index + 1, accent, alpha * phase, interactive, scale))
            {
                chosen = index + 1;
            }
        }

        drawList.PopClipRect();
        return chosen;
    }

    private void Scroll(Rect area, float deltaSeconds)
    {
        var hovered = PressSurface.Claim(SurfaceId, area, out var activated);
        var pointerY = ImGui.GetMousePos().Y;
        if (activated)
        {
            scroller.Press(pointerY);
            pressing = true;
        }

        if (pressing)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                scroller.Move(pointerY, deltaSeconds);
            }
            else
            {
                scroller.Release();
                pressing = false;
            }
        }

        if (scroller.IsDragging)
        {
            UiInteract.CancelPendingTap();
        }

        var wheel = ImGui.GetIO().MouseWheel;
        if (!hovered || wheel == 0f)
        {
            return;
        }

        scroller.CancelMomentum();
        scroller.SyncOffset(scroller.Offset - wheel * WheelStep * scroller.Scale);
    }

    private void DrawHeader(ImDrawListPtr drawList, Rect full, GameSession session, int count, PhoneTheme theme,
        float alpha, float scale)
    {
        var titleCenter = new Vector2(full.Center.X, full.Min.Y + StageLayout.ChipCenterY * scale);
        Typography.DrawCentered(drawList, titleCenter, Loc.T(L.Stage.Levels), GamePalette.InkLight with { W = alpha },
            TextStyles.Title2);
        var text = totalLabel.Get(L.Stage.StarsOf, session.TotalStars, count * GameStatsStore.MaxStars);
        var pillCenter = new Vector2(full.Center.X, StageLayout.SecondaryRowY(full, scale));
        var pill = StagePill.Around(pillCenter, StagePill.Width(text, true, scale), scale);
        StagePill.Draw(drawList, pill, FontAwesomeIcon.Star, GamePalette.Star, text, StageInks.Strong, alpha, scale);
    }

    private static bool DrawTile(ImDrawListPtr drawList, Rect tile, Rect area, GameSession session, int level,
        Vector4 accent, float alpha, bool interactive, float scale)
    {
        if (alpha <= 0f)
        {
            return false;
        }

        var gameId = session.Spec.Id;
        var stats = session.Stats;
        var unlocked = stats.IsUnlocked(gameId, level);
        var current = level == session.Level;
        var visibleMin = Vector2.Max(tile.Min, area.Min);
        var visibleMax = Vector2.Min(tile.Max, area.Max);
        var hovered = interactive && unlocked && UiInteract.Hover(visibleMin, visibleMax);
        var radius = TileRadius * scale;
        var fill = Palette.Mix(GamePalette.Cell, accent, unlocked ? UnlockedTint : LockedTint);
        if (hovered)
        {
            fill = GamePalette.Lighten(fill, HoverLighten);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var shown = unlocked ? alpha : alpha * LockedAlpha;
        if (current)
        {
            ProgressRing.Glow(tile.Center, tile.Width * 0.62f, accent, 0.5f * alpha);
        }

        var shadow = new Vector2(0f, ShadowOffset * scale);
        Squircle.Fill(drawList, tile.Min + shadow, tile.Max + shadow, radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ShadowAlpha * shown)));
        Squircle.Fill(drawList, tile.Min, tile.Max, radius, ImGui.GetColorU32(fill with { W = shown }));
        Material.Sheen(drawList, tile.Min, tile.Max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, SheenAlpha * shown)), 1f * scale, 1f * scale);
        if (current)
        {
            Squircle.Stroke(drawList, tile.Min, tile.Max, radius,
                ImGui.GetColorU32(GamePalette.Lighten(accent, 0.25f) with { W = alpha }), RingWidth * scale);
        }

        var numberCenter = new Vector2(tile.Center.X, tile.Center.Y - tile.Height * NumberRise);
        Typography.DrawCentered(drawList, numberCenter, GameNumber.Label(level), GamePalette.InkLight with { W = shown },
            TextStyles.Title3);
        var badgeCenter = new Vector2(tile.Center.X, tile.Center.Y + tile.Height * StarDrop);
        if (unlocked)
        {
            StarRow.Draw(drawList, badgeCenter, tile.Width * StarFraction, stats.Stars(gameId, level),
                GamePalette.InkLight with { W = EmptyStarAlpha }, alpha);
        }
        else
        {
            ProgressRing.CenterIcon(drawList, badgeCenter, FontAwesomeIcon.Lock, GamePalette.InkLight with { W = alpha },
                tile.Width * LockFraction);
        }

        return unlocked && interactive && UiInteract.Click(visibleMin, visibleMax, hovered);
    }
}
