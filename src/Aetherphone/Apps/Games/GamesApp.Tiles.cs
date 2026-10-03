using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const float TileArtAspect = 0.82f;
    private const float TileLabelBlock = 42f;
    private const float TileRoundingFactor = 0.22f;
    private const float TileLiftSmoothTime = 0.08f;
    private const float TileHoverGrow = 1.04f;
    private const float TilePressShrink = 0.96f;
    private const float TileEntranceLift = 14f;
    private const float BadgeHeight = 16f;
    private const float OnlineBadgeRadius = 9f;
    private const float FriendsMedallionRadius = 18f;
    private const float FriendsMedallionPitch = 1.15f;
    private const float FriendsCardPadding = 14f;
    private const float FriendsTextGap = 12f;
    private const float FriendsChevronReserve = 28f;
    private const float FriendsTitleTop = 15f;
    private const float FriendsHintTop = 37f;
    private const float HeroRounding = 28f;

    private static readonly Vector4 BadgeFill = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 OnlineBadgeFill = new(0f, 0f, 0f, 0.38f);
    private static readonly Vector4 HeroInk = new(0.97f, 0.97f, 0.99f, 1f);
    private static readonly Vector4 StreakEmber = new(0.98f, 0.72f, 0.34f, 1f);

    private static float TileHeight(float tileWidth, float scale) => tileWidth * TileArtAspect + TileLabelBlock * scale;

    private bool DrawTile(Rect rect, int entryIndex, float appear, bool interactive)
    {
        if (appear <= 0f)
        {
            return false;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        ref var lift = ref library.Lift[entryIndex];
        ref readonly var entry = ref library.Entries[entryIndex];
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var target = pressed ? TilePressShrink : hovered ? TileHoverGrow : 1f;
        var grow = lift.Step(target, TileLiftSmoothTime, frameSeconds) * (0.90f + 0.10f * Easing.EaseOutBack(appear));
        var entranceLift = (1f - Easing.EaseOutCubic(appear)) * TileEntranceLift * scale;
        var artHeight = rect.Width * TileArtAspect;
        var artCenter = new Vector2(rect.Center.X, rect.Min.Y + artHeight * 0.5f + entranceLift);
        var half = new Vector2(rect.Width, artHeight) * 0.5f * grow;
        var min = artCenter - half;
        var max = artCenter + half;
        var rounding = rect.Width * TileRoundingFactor * grow;
        var accent = library.Accent(entryIndex);
        Elevation.Card(drawList, min, max, rounding, scale, hovered ? 1f : 0.55f);
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, hovered ? 0.26f : 0.18f)),
            ImGui.GetColorU32(GamePalette.Darken(accent, 0.34f)));
        drawList.PushClipRect(min, max, true);
        var size = max - min;
        drawList.AddCircleFilled(min + size * new Vector2(0.22f, 0.16f), size.X * 0.55f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.09f)), 40);
        drawList.AddCircleFilled(max - size * new Vector2(0.14f, 0.08f), size.X * 0.50f,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.55f) with { W = 0.14f }), 40);
        drawList.PopClipRect();
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.45f) with { W = hovered ? 0.65f : 0.32f }), 1f * scale);
        Material.Sheen(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)), 1f * scale,
            1.5f * scale);
        if (hovered)
        {
            ProgressRing.Glow(artCenter, half.X * 0.6f, GamePalette.Lighten(accent, 0.5f), 0.45f);
        }

        var iconSize = size.Y * 0.58f;
        if (entry.Online)
        {
            OnlineGameArt.Draw(drawList, entry.OnlineKind, artCenter, iconSize, scale);
        }
        else if (!DrawGameIcon(drawList, entry.Id, accent, artCenter, iconSize, scale))
        {
            Typography.DrawCentered(drawList, artCenter, library.Title(entryIndex), AccentRing.Ink, TextStyles.Caption2);
        }

        if (library.IsNew(entryIndex))
        {
            DrawNewBadge(drawList, new Vector2(min.X + 8f * scale, min.Y + 8f * scale), accent, scale);
        }

        if (entry.Online)
        {
            DrawOnlineBadge(drawList, new Vector2(max.X - 8f * scale - OnlineBadgeRadius * scale,
                min.Y + 8f * scale + OnlineBadgeRadius * scale), scale);
        }

        var textLeft = rect.Min.X + 2f * scale;
        var textWidth = MathF.Max(1f, rect.Width - 4f * scale);
        var titleY = rect.Min.Y + artHeight + 7f * scale + entranceLift;
        Marquee.DrawLeft(drawList, library.MarqueeIds[entryIndex], library.Title(entryIndex), textLeft, titleY,
            textWidth, TextStyles.Headline, ui.TitleInk, hovered);
        Typography.Draw(drawList, new Vector2(textLeft, titleY + 18f * scale),
            Typography.FitText(library.Subtitle(entryIndex), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static bool DrawGameIcon(ImDrawListPtr drawList, string id, Vector4 accent, Vector2 center, float size,
        float scale)
    {
        var half = new Vector2(size, size) * 0.5f;
        if (AppIconTile.TryDraw(drawList, id, accent, center - half, center + half, size * Metrics.Radius.TileFactor,
                1f, false, scale))
        {
            return true;
        }

        return AppIconArt.TryDraw(drawList, id, center, size, AccentRing.Ink, GamePalette.Darken(accent, 0.16f));
    }

    private static void DrawNewBadge(ImDrawListPtr drawList, Vector2 topLeft, Vector4 accent, float scale)
    {
        var label = Loc.T(L.Games.BadgeNew);
        var textSize = Typography.Measure(label, TextStyles.Caption2);
        var height = BadgeHeight * scale;
        var max = new Vector2(topLeft.X + textSize.X + 12f * scale, topLeft.Y + height);
        Squircle.Fill(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(BadgeFill));
        Typography.DrawCentered(drawList, (topLeft + max) * 0.5f, label, GamePalette.Darken(accent, 0.30f),
            TextStyles.Caption2);
    }

    private static void DrawOnlineBadge(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var radius = OnlineBadgeRadius * scale;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(OnlineBadgeFill), 24);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.UserFriends, AccentRing.Ink, radius * 0.95f);
    }

    private void DrawShelfHeading(string label, string trailing, float left, float y, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var trailingWidth = 0f;
        if (trailing.Length > 0)
        {
            var trailingSize = Typography.Measure(trailing, TextStyles.Caption1);
            trailingWidth = trailingSize.X + 8f * scale;
            Typography.Draw(drawList, new Vector2(left + width - trailingSize.X, y + 5f * scale), trailing,
                ui.MutedInk, TextStyles.Caption1);
        }

        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(label, MathF.Max(1f, width - trailingWidth), TextStyles.Title3), ui.TitleInk,
            TextStyles.Title3);
    }

    private readonly struct FriendsLayout
    {
        public readonly float Height;
        public readonly float TextOffset;
        public readonly float TextWidth;
        public readonly float PillWidth;
        public readonly int Rooms;

        public FriendsLayout(float height, float textOffset, float textWidth, float pillWidth, int rooms)
        {
            Height = height;
            TextOffset = textOffset;
            TextWidth = textWidth;
            PillWidth = pillWidth;
            Rooms = rooms;
        }
    }

    private FriendsLayout MeasureFriendsCard(float width, float scale)
    {
        var radius = FriendsMedallionRadius * scale;
        var pitch = radius * FriendsMedallionPitch;
        var textOffset = FriendsCardPadding * scale + radius * 2f + (OnlineGameArt.Kinds.Length - 1) * pitch
                         + FriendsTextGap * scale;
        var rooms = gameRooms.AccountId.Length > 0 ? gameRooms.Rooms.Length : 0;
        var pillWidth = rooms > 0 ? LivePill.Width(RoomsLabel(rooms), scale) : 0f;
        var reserve = rooms > 0
            ? pillWidth + (FriendsCardPadding + FriendsTextGap) * scale
            : FriendsChevronReserve * scale;
        var textWidth = MathF.Max(1f, width - textOffset - reserve);
        var hintHeight = Typography.MeasureWrappedBlock(Loc.T(L.Games.OnlineCardHint), TextStyles.Footnote,
            textWidth).Y;
        var height = MathF.Max(FriendsCardHeight * scale, (FriendsHintTop + FriendsCardPadding) * scale + hintHeight);
        return new FriendsLayout(height, textOffset, textWidth, pillWidth, rooms);
    }

    private bool DrawFriendsCard(Rect rect, in FriendsLayout layout, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, rect.Min, rect.Max, rounding, hovered);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var kinds = OnlineGameArt.Kinds;
        var radius = FriendsMedallionRadius * scale;
        var startX = rect.Min.X + FriendsCardPadding * scale + radius;
        var pitch = radius * FriendsMedallionPitch;
        for (var index = 0; index < kinds.Length; index++)
        {
            var center = new Vector2(startX + index * pitch, rect.Center.Y);
            var accent = OnlineGameArt.Accent(kinds[index]);
            drawList.AddCircleFilled(center, radius + 2f * scale, ImGui.GetColorU32(ui.Palette.BackdropBottom), 36);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(accent, 0.34f)), 36);
            drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f) with { W = 0.6f }),
                36, 1f * scale);
            OnlineGameArt.Draw(drawList, kinds[index], center, radius * 1.25f, scale);
        }

        if (layout.Rooms > 0)
        {
            LivePill.Draw(drawList,
                new Vector2(rect.Max.X - FriendsCardPadding * scale - layout.PillWidth,
                    rect.Center.Y - LivePill.Height(scale) * 0.5f), RoomsLabel(layout.Rooms), ui.Accent,
                (float)ImGui.GetTime(), scale);
        }
        else
        {
            ProgressRing.CenterIcon(drawList,
                new Vector2(rect.Max.X - FriendsChevronReserve * scale * 0.55f, rect.Center.Y),
                FontAwesomeIcon.ChevronRight, ui.MutedInk, 12f * scale);
        }

        var textLeft = rect.Min.X + layout.TextOffset;
        Marquee.DrawLeftAuto(drawList, "games.friends.title", Loc.T(L.Games.OnlineTitle), textLeft,
            rect.Min.Y + FriendsTitleTop * scale, layout.TextWidth, TextStyles.Headline, ui.TitleInk);
        Typography.DrawWrappedLeft(new Vector2(textLeft, rect.Min.Y + FriendsHintTop * scale),
            Loc.T(L.Games.OnlineCardHint), ui.MutedInk, TextStyles.Footnote, layout.TextWidth);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private bool DrawHero(Rect rect, IMiniGame game, float phase, float scale)
    {
        if (phase <= 0f)
        {
            return false;
        }

        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var target = pressed ? 0.975f :
            hovered ? 1.012f : 1f;
        var grow = heroScale.Step(target, 0.085f, frameSeconds) * (0.94f + 0.06f * Easing.EaseOutBack(phase));
        var lift = (1f - Easing.EaseOutCubic(phase)) * 18f * scale;
        var center = rect.Center + new Vector2(0f, lift);
        var half = rect.Size * 0.5f * grow;
        var min = center - half;
        var max = center + half;
        var height = max.Y - min.Y;
        var rounding = HeroRounding * scale;
        var accent = game.Accent;
        Elevation.Floating(drawList, min, max, rounding, scale, phase * (hovered ? 1f : 0.8f));
        var topTone = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.34f));
        var bottomTone = ImGui.GetColorU32(GamePalette.Darken(accent, 0.48f));
        Squircle.FillVerticalGradient(drawList, min, max, rounding, topTone, bottomTone);
        drawList.PushClipRect(min, max, true);
        DrawHeroGlow(drawList, min, max, accent);
        DrawSheen(drawList, min, max, Pulse.Phase(5600.0), 0.05f, scale);
        drawList.PopClipRect();
        var iconCenter = new Vector2(min.X + height * 0.40f,
            center.Y + MathF.Sin((float)ImGui.GetTime() * 1.6f) * 3f * scale);
        var iconSize = height * 0.52f;
        drawList.AddCircleFilled(iconCenter + new Vector2(0f, iconSize * 0.10f), iconSize * 0.52f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)));
        ProgressRing.Glow(iconCenter, iconSize * 0.5f, GamePalette.Lighten(accent, 0.45f), hovered ? 0.9f : 0.55f);
        var ink = AccentRing.Ink;
        if (!DrawGameIcon(drawList, game.Id, accent, iconCenter, iconSize, scale))
        {
            Typography.DrawCentered(drawList, iconCenter, game.Title, ink, TextStyles.Title1);
        }

        var textX = min.X + height * 0.72f;
        var heroTextMaxWidth = MathF.Max(1f, max.X - textX - 12f * scale);
        Typography.Draw(drawList, new Vector2(textX, center.Y - 34f * scale),
            Typography.FitText(dailyEyebrow, heroTextMaxWidth, TextStyles.Caption2),
            GamePalette.Lighten(accent, 0.62f), TextStyles.Caption2);
        var heroTitleY = center.Y - 18f * scale;
        var heroTitleSize = Typography.Measure(game.Title, TextStyles.Title2);
        var heroTitleHovering = UiInteract.Hover(new Vector2(textX, heroTitleY),
            new Vector2(textX + heroTextMaxWidth, heroTitleY + heroTitleSize.Y));
        Marquee.DrawLeft(drawList, "games.hero.title", game.Title, textX, heroTitleY, heroTextMaxWidth,
            TextStyles.Title2, ink, heroTitleHovering);
        var genre = Loc.T(GameGenres.Label(game.Genre));
        Typography.Draw(drawList, new Vector2(textX, center.Y + 8f * scale),
            Typography.FitText(genre, heroTextMaxWidth, TextStyles.Footnote), ink with { W = 0.72f },
            TextStyles.Footnote);
        var playCenter = new Vector2(textX + 34f * scale, center.Y + 38f * scale);
        var playClicked = GameHud.Button(playCenter, new Vector2(68f * scale, 28f * scale), Loc.T(L.Games.Play),
            HeroInk, theme);
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = 0.42f }), 1f * scale);
        Material.Sheen(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f)), 1f * scale,
            1.5f * scale);
        var best = library.Best(featuredIndex);
        if (best.Length > 0)
        {
            DrawFrostedChip(drawList, new Vector2(max.X - 11f * scale, min.Y + 11f * scale), best, scale);
        }

        DrawStreakChip(drawList, new Vector2(min.X + 11f * scale, min.Y + 11f * scale), accent, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (playClicked)
        {
            return true;
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static void DrawHeroGlow(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector4 accent)
    {
        var size = max - min;
        var topLeft = new Vector2(min.X + size.X * 0.16f, min.Y + size.Y * 0.14f);
        var bottomRight = new Vector2(min.X + size.X * 0.86f, min.Y + size.Y * 0.95f);
        for (var layer = 3; layer >= 1; layer--)
        {
            var alphaScale = (4 - layer) * 0.34f;
            drawList.AddCircleFilled(topLeft, size.Y * (0.24f + layer * 0.16f),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.045f * alphaScale)));
            drawList.AddCircleFilled(bottomRight, size.Y * (0.30f + layer * 0.20f),
                ImGui.GetColorU32(GamePalette.Lighten(accent, 0.55f) with { W = 0.06f * alphaScale }));
        }
    }

    private static void DrawSheen(ImDrawListPtr drawList, Vector2 min, Vector2 max, float sweep, float alpha,
        float scale)
    {
        var width = max.X - min.X;
        var band = 26f * scale;
        var sweepX = min.X + (width + band * 4f) * sweep - band * 2f;
        var skew = 18f * scale;
        drawList.AddQuadFilled(new Vector2(sweepX - band * 0.5f, max.Y), new Vector2(sweepX + skew - band * 0.5f, min.Y),
            new Vector2(sweepX + skew + band * 0.5f, min.Y), new Vector2(sweepX + band * 0.5f, max.Y),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
    }

    private void DrawStreakChip(ImDrawListPtr drawList, Vector2 topLeft, Vector4 accent, float scale)
    {
        var streak = stats.DailyStreak;
        if (streak <= 0)
        {
            return;
        }

        var done = stats.DailyDone;
        var label = GameNumber.Label(streak);
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var iconSize = 10f * scale;
        var chipHeight = 18f * scale;
        var chipWidth = textSize.X + iconSize + 18f * scale;
        var min = topLeft;
        var max = new Vector2(topLeft.X + chipWidth, topLeft.Y + chipHeight);
        Material.Frosted(drawList, min, max, chipHeight * 0.5f, scale);
        var tint = done ? GamePalette.Lighten(accent, 0.5f) : StreakEmber;
        if (done)
        {
            Squircle.Stroke(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(tint with { W = 0.6f }),
                1f * scale);
        }

        var iconCenter = new Vector2(min.X + 9f * scale + iconSize * 0.5f, (min.Y + max.Y) * 0.5f);
        ProgressRing.CenterIcon(drawList, iconCenter, done ? FontAwesomeIcon.Check : FontAwesomeIcon.Fire, tint,
            iconSize);
        Typography.DrawCentered(drawList,
            new Vector2(iconCenter.X + iconSize * 0.5f + 3f * scale + textSize.X * 0.5f, (min.Y + max.Y) * 0.5f),
            label, HeroInk, TextStyles.Caption1);
    }

    private static void DrawFrostedChip(ImDrawListPtr drawList, Vector2 topRight, string text, float scale)
    {
        var textSize = Typography.Measure(text, TextStyles.Caption1);
        var chipWidth = textSize.X + 14f * scale;
        var chipHeight = 18f * scale;
        var min = new Vector2(topRight.X - chipWidth, topRight.Y);
        var max = new Vector2(topRight.X, topRight.Y + chipHeight);
        Material.Frosted(drawList, min, max, chipHeight * 0.5f, scale);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, text, HeroInk, TextStyles.Caption1);
    }
}
