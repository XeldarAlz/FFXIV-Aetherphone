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
    private const float TileEntranceLift = 14f;
    private const float BadgeHeight = 16f;
    private const float RankChipPadX = 5f;
    private const float RankChipFillAlpha = 0.20f;
    private const float OnlineBadgeRadius = 9f;
    private const float FriendsMedallionRadius = 18f;
    private const float FriendsMedallionPitch = 1.15f;
    private const float FriendsCardPadding = 14f;
    private const float FriendsTextGap = 12f;
    private const float FriendsChevronReserve = 28f;
    private const float FriendsTitleTop = 15f;
    private const float FriendsHintTop = 37f;
    private const float HeroRounding = 22f;
    private const float HeroPadding = 18f;
    private const float HeroIconSize = 76f;
    private const float HeroIconGap = 14f;
    private const float HeroIconBob = 3f;
    private const float HeroPillHeight = 34f;
    private const float HeroPillMinWidth = 92f;
    private const float HeroChipHeight = 24f;
    private const float HeroChipIcon = 11f;
    private const float HeroEntranceLift = 18f;
    private const float HeroChipGlassAlpha = 0.55f;

    private static readonly Vector4 BadgeFill = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 OnlineBadgeFill = new(0f, 0f, 0f, 0.38f);
    private static readonly Vector4 HeroInk = new(0.97f, 0.97f, 0.99f, 1f);
    private static readonly Vector4 HeroPillFill = new(1f, 1f, 1f, 0.96f);
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
        var target = pressed ? Motion.PressScaleControl : hovered ? 1f + Motion.HoverLiftIcon : 1f;
        var smoothTime = pressed ? Motion.PressIn : hovered ? Motion.HoverLift : Motion.Release;
        var grow = lift.Step(target, smoothTime, frameSeconds) * (0.90f + 0.10f * Easing.EaseOutBack(appear));
        var entranceLift = (1f - Easing.EaseOutCubic(appear)) * TileEntranceLift * scale;
        var artHeight = rect.Width * TileArtAspect;
        var artCenter = new Vector2(rect.Center.X, rect.Min.Y + artHeight * 0.5f + entranceLift);
        var half = new Vector2(rect.Width, artHeight) * 0.5f * grow;
        var min = artCenter - half;
        var max = artCenter + half;
        var rounding = rect.Width * TileRoundingFactor * grow;
        var accent = library.Accent(entryIndex);
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
        var subtitleY = titleY + 18f * scale;
        var rankChip = library.RankLabel(entryIndex);
        var chipWidth = rankChip.Length > 0
            ? Typography.Measure(rankChip, TextStyles.Caption2).X + RankChipPadX * 2f * scale
            : 0f;
        var subtitleWidth = chipWidth > 0f ? MathF.Max(1f, textWidth - chipWidth - Metrics.Space.Xs * scale) : textWidth;
        var subtitle = Typography.FitText(library.Subtitle(entryIndex), subtitleWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, subtitleY), subtitle, ui.MutedInk, TextStyles.Footnote);
        if (chipWidth > 0f)
        {
            var chipLeft = textLeft + Typography.Measure(subtitle, TextStyles.Footnote).X + Metrics.Space.Xs * scale;
            DrawRankChip(drawList, new Vector2(chipLeft, subtitleY + Typography.LineHeight(TextStyles.Footnote) * 0.5f),
                rankChip, chipWidth, accent, scale);
        }

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

    private static void DrawEntryIcon(ImDrawListPtr drawList, in GameEntry entry, Vector4 accent, Vector2 center,
        float size, float scale)
    {
        if (!entry.Online)
        {
            DrawGameIcon(drawList, entry.Id, accent, center, size, scale);
            return;
        }

        var half = new Vector2(size, size) * 0.5f;
        Squircle.FillVerticalGradient(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.18f)), ImGui.GetColorU32(GamePalette.Darken(accent, 0.34f)));
        OnlineGameArt.Draw(drawList, entry.OnlineKind, center, size * 0.62f, scale);
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

    private void DrawRankChip(ImDrawListPtr drawList, Vector2 leftCenter, string label, float width, Vector4 accent,
        float scale)
    {
        var height = BadgeHeight * scale;
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(leftCenter.X + width, leftCenter.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(accent with { W = RankChipFillAlpha }));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, ui.TitleInk, TextStyles.Caption2);
    }

    private static void DrawOnlineBadge(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var radius = OnlineBadgeRadius * scale;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(OnlineBadgeFill), 24);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.UserFriends, AccentRing.Ink, radius * 0.95f);
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
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale("games.friends.card", pressed, Motion.PressScaleCard);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var rounding = Metrics.Radius.Widget * scale;
        ui.Card(drawList, min, max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var kinds = OnlineGameArt.Kinds;
        var radius = FriendsMedallionRadius * scale;
        var startX = rect.Min.X + FriendsCardPadding * scale + radius;
        var pitch = radius * FriendsMedallionPitch;
        for (var index = 0; index < kinds.Length; index++)
        {
            GamesHubArt.Medallion(drawList, kinds[index], new Vector2(startX + index * pitch, rect.Center.Y), radius,
                ui.Palette.BackdropBottom, scale);
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
            PhoneIcon.Draw(drawList, new Vector2(rect.Max.X - FriendsChevronReserve * scale * 0.55f, rect.Center.Y),
                PhoneIcons.ChevronRight, ui.MutedInk, 14f * scale);
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
        var pad = HeroPadding * scale;
        var pillHeight = HeroPillHeight * scale;
        var playLabel = Loc.T(L.Games.Play);
        var pillWidth = MathF.Max(HeroPillMinWidth * scale,
            GamesHubArt.ButtonWidth(playLabel, pillHeight));
        var pillRect = new Rect(new Vector2(rect.Min.X + pad, rect.Max.Y - pad - pillHeight),
            new Vector2(rect.Min.X + pad + pillWidth, rect.Max.Y - pad));
        var overPill = UiInteract.Hover(pillRect.Min, pillRect.Max);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && !overPill && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var target = pressed ? Motion.PressScaleCard : hovered ? 1f + Motion.HoverLiftCard : 1f;
        var smoothTime = pressed ? Motion.PressIn : hovered ? Motion.HoverLift : Motion.Release;
        var grow = heroScale.Step(target, smoothTime, frameSeconds) * (0.94f + 0.06f * Easing.EaseOutBack(phase));
        var lift = (1f - Easing.EaseOutCubic(phase)) * HeroEntranceLift * scale;
        var center = rect.Center + new Vector2(0f, lift);
        var half = rect.Size * 0.5f * grow;
        var min = center - half;
        var max = center + half;
        var rounding = HeroRounding * scale;
        var accent = game.Accent;
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.30f)), ImGui.GetColorU32(GamePalette.Darken(accent, 0.50f)));
        drawList.PushClipRect(min, max, true);
        DrawHeroGlow(drawList, min, max, accent);
        DrawSheen(drawList, min, max, Pulse.Phase(5600.0), 0.05f, scale);
        drawList.PopClipRect();
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = 0.42f }), 1f * scale);

        var iconSize = HeroIconSize * scale;
        var iconCenter = new Vector2(min.X + pad + iconSize * 0.5f,
            min.Y + pad + iconSize * 0.5f + MathF.Sin((float)ImGui.GetTime() * 1.6f) * HeroIconBob * scale);
        drawList.AddCircleFilled(iconCenter + new Vector2(0f, iconSize * 0.12f), iconSize * 0.5f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.20f)));
        ProgressRing.Glow(iconCenter, iconSize * 0.5f, GamePalette.Lighten(accent, 0.45f), hovered ? 0.9f : 0.55f);
        if (!DrawGameIcon(drawList, game.Id, accent, iconCenter, iconSize, scale))
        {
            Typography.DrawCentered(drawList, iconCenter, game.Title, HeroInk, TextStyles.Title2);
        }

        var textLeft = min.X + pad + iconSize + HeroIconGap * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var eyebrowY = min.Y + pad + Metrics.Space.Xxs * scale;
        Typography.Draw(drawList, new Vector2(textLeft, eyebrowY),
            Typography.FitText(dailyEyebrow, textWidth, TextStyles.FootnoteEmphasized),
            GamePalette.Lighten(accent, 0.62f), TextStyles.FootnoteEmphasized);
        var titleY = eyebrowY + Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Xxs * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var titleHovering = UiInteract.Hover(new Vector2(textLeft, titleY),
            new Vector2(textLeft + textWidth, titleY + titleHeight));
        Marquee.DrawLeft(drawList, "games.hero.title", game.Title, textLeft, titleY, textWidth, TextStyles.Title2,
            HeroInk, titleHovering);
        Typography.Draw(drawList, new Vector2(textLeft, titleY + titleHeight + Metrics.Space.Xxs * scale),
            Typography.FitText(Loc.T(GameGenres.Label(game.Genre)), textWidth, TextStyles.Subheadline),
            HeroInk with { W = 0.78f }, TextStyles.Subheadline);

        var liftOffset = new Vector2(0f, lift);
        var pillCenter = pillRect.Center + liftOffset;
        var pillFace = Button.Surface(drawList, new Rect(pillRect.Min + liftOffset, pillRect.Max + liftOffset),
            ui.Ink.WithAccent(HeroPillFill), ButtonStyle.Prominent, ButtonRole.Normal, true, overPill,
            ImGui.GetID("games.hero.play"));
        Button.DrawLabel(drawList, pillFace with { LabelInk = GamePalette.Darken(accent, 0.35f) }, playLabel);
        var chipWidth = DrawStreakChip(drawList, new Vector2(max.X - pad, pillCenter.Y), accent, scale);
        var best = library.Best(featuredIndex);
        if (best.Length > 0)
        {
            var bestLeft = pillRect.Max.X + Metrics.Space.Md * scale;
            var chipReserve = chipWidth > 0f ? chipWidth + Metrics.Space.Sm * scale : 0f;
            var bestWidth = MathF.Max(1f, max.X - pad - chipReserve - bestLeft);
            var bestHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(bestLeft, pillCenter.Y - bestHeight * 0.5f),
                Typography.FitText(best, bestWidth, TextStyles.FootnoteEmphasized), HeroInk with { W = 0.86f },
                TextStyles.FootnoteEmphasized);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(pillRect.Min, pillRect.Max, overPill))
        {
            return true;
        }

        return !overPill && UiInteract.Click(rect.Min, rect.Max, hovered);
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

    private float DrawStreakChip(ImDrawListPtr drawList, Vector2 rightCenter, Vector4 accent, float scale)
    {
        var streak = stats.DailyStreak;
        if (streak <= 0)
        {
            return 0f;
        }

        var done = stats.DailyDone;
        var label = GameNumber.Label(streak);
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var iconSize = HeroChipIcon * scale;
        var chipHeight = HeroChipHeight * scale;
        var chipWidth = textSize.X + iconSize + chipHeight * 0.5f + Metrics.Space.Md * scale;
        var min = new Vector2(rightCenter.X - chipWidth, rightCenter.Y - chipHeight * 0.5f);
        var max = new Vector2(rightCenter.X, rightCenter.Y + chipHeight * 0.5f);
        Material.AccentGlass(drawList, min, max, chipHeight * 0.5f, scale, GamePalette.Darken(accent, 0.45f),
            HeroChipGlassAlpha);
        var tint = done ? GamePalette.Lighten(accent, 0.6f) : StreakEmber;
        var iconCenter = new Vector2(min.X + chipHeight * 0.5f, (min.Y + max.Y) * 0.5f);
        ProgressRing.CenterIcon(drawList, iconCenter, done ? FontAwesomeIcon.Check : FontAwesomeIcon.Fire, tint,
            iconSize);
        Typography.Draw(drawList,
            new Vector2(iconCenter.X + iconSize * 0.5f + Metrics.Space.Xxs * scale, (min.Y + max.Y - textSize.Y) * 0.5f),
            label, HeroInk, TextStyles.FootnoteEmphasized);
        return chipWidth;
    }
}
