using Aetherphone.Apps.Games.Online;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hub;

internal static class RoomCard
{
    public const float Height = 76f;
    public const float IconSize = 48f;
    public const float Pad = 14f;
    public const float TextGap = 12f;
    public const float StatusGap = 6f;
    public const float HoverFloor = 0.001f;
    public const int DiscSegments = 32;
    private const string PoseId = "card";
    private const float OwnerRadius = 12f;
    private const float OwnerRing = 2f;
    private const float OwnerOverhang = 4f;
    private const float SeatDotSize = 8f;
    private const float SeatDotGap = 4f;
    private const float ChevronSize = 13f;
    private const float ChevronGap = 8f;
    private const int MaxSeatDots = 6;

    public static Rect Pose(Rect rect, bool hovered, out float hover)
    {
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        hover = HoverFx.Amount(PoseId, hovered);
        var press = PressFx.Scale(PoseId, pressed, Motion.PressScaleCard);
        var half = rect.Size * 0.5f * press * (1f + Motion.HoverLiftCard * hover);
        return new Rect(rect.Center - half, rect.Center + half);
    }

    public static bool Draw(ImDrawListPtr drawList, AppSkin ui, Rect rect, GameRoomCardDto room, string title,
        string subtitle, string monogram, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var card = Pose(rect, hovered, out var hover);
        var radius = HubMetrics.CardRadius * scale;
        ui.Card(drawList, card.Min, card.Max, radius);
        if (hover > HoverFloor)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius,
                ImGui.GetColorU32(ui.HoverTint with { W = ui.HoverTint.W * hover }));
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ref readonly var info = ref OnlineGameArt.Info(room.GameKind);
        var accent = AppAccents.For(info.AccentId);
        var pad = Pad * scale;
        var iconSize = IconSize * scale;
        var iconMin = new Vector2(card.Min.X + pad, card.Center.Y - iconSize * 0.5f);
        var iconMax = new Vector2(iconMin.X + iconSize, iconMin.Y + iconSize);
        GameIconArt.Draw(drawList, info.AccentId, accent, iconMin, iconMax, null, true);
        DrawOwner(drawList, ui, iconMax, monogram, scale);
        var chevron = ChevronSize * scale;
        var chevronCenter = new Vector2(card.Max.X - pad - chevron * 0.5f, card.Center.Y);
        PhoneIcon.Draw(drawList, chevronCenter, PhoneIcons.ChevronRight, ui.MutedInk, chevron);
        var statusLeft = DrawStatus(drawList, ui, room, chevronCenter.X - chevron * 0.5f - ChevronGap * scale,
            card.Center.Y, accent, scale);
        var textLeft = iconMax.X + TextGap * scale;
        DrawTitlePair(drawList, ui, textLeft, card.Center.Y, MathF.Max(1f, statusLeft - TextGap * scale - textLeft),
            title, subtitle);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static void DrawOwner(ImDrawListPtr drawList, AppSkin ui, Vector2 iconMax, string monogram, float scale)
    {
        var radius = OwnerRadius * scale;
        var overhang = OwnerOverhang * scale;
        var center = new Vector2(iconMax.X + overhang - radius, iconMax.Y + overhang - radius);
        drawList.AddCircleFilled(center, radius + OwnerRing * scale, ImGui.GetColorU32(ui.Palette.BackdropBottom),
            DiscSegments);
        AvatarView.Draw(drawList, center, radius, ui.Accent, monogram, TextStyles.FootnoteEmphasized.Scale,
            AvatarHandle.Disabled, DiscSegments);
    }

    private static float DrawStatus(ImDrawListPtr drawList, AppSkin ui, GameRoomCardDto room, float right,
        float centerY, Vector4 accent, float scale)
    {
        var playing = room.Phase == GameRoomWire.PhasePlaying;
        var label = Loc.T(playing ? L.GamesHub.Playing : L.GamesHub.Waiting);
        var statusWidth = playing ? LivePill.Width(label, scale) : Typography.Measure(label, TextStyles.Footnote).X;
        var statusHeight = playing ? LivePill.Height(scale) : Typography.LineHeight(TextStyles.Footnote);
        var seats = Math.Clamp(room.MaxSeats, 0, MaxSeatDots);
        var dot = SeatDotSize * scale;
        var dotGap = SeatDotGap * scale;
        var dotsWidth = seats > 0 ? seats * dot + (seats - 1) * dotGap : 0f;
        var dotsBand = seats > 0 ? StatusGap * scale + dot : 0f;
        var top = centerY - (statusHeight + dotsBand) * 0.5f;
        var statusMin = new Vector2(right - statusWidth, top);
        if (playing)
        {
            LivePill.Draw(drawList, statusMin, label, accent, (float)ImGui.GetTime(), scale);
        }
        else
        {
            Typography.Draw(drawList, statusMin, label, ui.MutedInk, TextStyles.Footnote);
        }

        var empty = ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary));
        var filled = ImGui.GetColorU32(accent);
        var dotCenterY = top + statusHeight + StatusGap * scale + dot * 0.5f;
        for (var seat = 0; seat < seats; seat++)
        {
            var dotCenter = new Vector2(right - dotsWidth + seat * (dot + dotGap) + dot * 0.5f, dotCenterY);
            drawList.AddCircleFilled(dotCenter, dot * 0.5f, seat < room.SeatedCount ? filled : empty, DiscSegments);
        }

        return right - MathF.Max(statusWidth, dotsWidth);
    }

    private static void DrawTitlePair(ImDrawListPtr drawList, AppSkin ui, float left, float centerY, float width,
        string title, string subtitle)
    {
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, width, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight),
            Typography.FitText(subtitle, width, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
    }
}
