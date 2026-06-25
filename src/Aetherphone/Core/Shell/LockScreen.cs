using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell;

internal sealed class LockScreen
{
    private const ImGuiWindowFlags OverlayFlags =
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private const float SmoothTime = 0.22f;
    private const float UnlockFraction = 0.40f;
    private const float CommitFraction = 0.30f;
    private const float FlingVelocity = 900f;
    private const int MaxCards = 4;

    private readonly NotificationService notifications;
    private readonly DragTracker drag = new();

    private Spring cover;
    private float target;
    private bool locked;

    public LockScreen(NotificationService notifications)
    {
        this.notifications = notifications;
    }

    public bool IsActive => locked || cover.Value > 0.001f || drag.Active;

    public bool CapturesPointer => IsActive;

    public void Lock()
    {
        if (locked)
        {
            return;
        }

        locked = true;
        cover.SnapTo(0f);
        target = 1f;
    }

    public void Draw(Rect screen, PhoneTheme theme, float delta, INavigator navigation)
    {
        HandleGesture(screen, delta, navigation);

        var amount = cover.Value;
        if (amount <= 0.001f && !drag.Active)
        {
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##lockScreen", screen.Size, false, OverlayFlags))
        {
            var scale = ImGuiHelpers.GlobalScale;
            var dl = ImGui.GetWindowDrawList();
            var height = screen.Height;
            var rounding = theme.ScreenRounding * scale;
            var lockTop = screen.Min.Y - (1f - amount) * height;

            dl.PushClipRect(screen.Min, screen.Max, true);

            Material.Veil(dl, screen.Min, screen.Max, 0.40f * amount, rounding);
            Material.Frosted(dl, new Vector2(screen.Min.X, lockTop), new Vector2(screen.Max.X, lockTop + height), rounding, scale, 1f);

            var opacity = Math.Clamp(amount * 1.6f, 0f, 1f);
            var interactive = locked && !drag.Active && cover.Value > 0.96f;
            DrawContents(screen, theme, lockTop, scale, opacity, interactive, navigation);

            dl.PopClipRect();
        }
    }

    private void DrawContents(Rect screen, PhoneTheme theme, float lockTop, float scale, float opacity, bool interactive, INavigator navigation)
    {
        var now = DateTime.Now;
        DrawClock(screen, theme, lockTop, scale, opacity, now);

        var cards = CollectCards();
        var listTop = lockTop + screen.Height * 0.42f;
        var pad = 18f * scale;
        var left = screen.Min.X + pad;
        var right = screen.Max.X - pad;

        if (cards.Count == 0)
        {
            Typography.DrawCentered(new Vector2(screen.Center.X, listTop + 30f * scale), Loc.T(L.Notifications.Empty), Palette.WithAlpha(theme.TextMuted, opacity), 0.9f);
        }
        else
        {
            var cardHeight = 64f * scale;
            var gap = 10f * scale;
            for (var index = 0; index < cards.Count; index++)
            {
                var top = listTop + index * (cardHeight + gap);
                var rect = new Rect(new Vector2(left, top), new Vector2(right, top + cardHeight));
                if (DrawCard(rect, cards[index], theme, scale, opacity, interactive))
                {
                    navigation.Open(cards[index].AppId);
                    target = 0f;
                }
            }
        }

        DrawHint(screen, theme, scale, opacity);
    }

    private static void DrawClock(Rect screen, PhoneTheme theme, float lockTop, float scale, float opacity, DateTime now)
    {
        var time = now.ToString("HH:mm");
        var center = new Vector2(screen.Center.X, lockTop + screen.Height * 0.20f);

        using (Plugin.Fonts.Push(1.9f, FontWeight.Bold))
        {
            ImGui.SetWindowFontScale(1.8f);
            var size = ImGui.CalcTextSize(time);
            ImGui.SetCursorScreenPos(new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f));
            using (Dalamud.Interface.Utility.Raii.ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(theme.TextStrong, opacity)))
            {
                ImGui.TextUnformatted(time);
            }

            ImGui.SetWindowFontScale(1f);
        }

        var date = now.ToString("dddd, MMMM d", Loc.Culture);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 44f * scale), date, Palette.WithAlpha(theme.TextStrong, 0.82f * opacity), 0.95f, FontWeight.Medium);
    }

    private bool DrawCard(Rect rect, PhoneNotification notification, PhoneTheme theme, float scale, float opacity, bool interactive)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 18f * scale;
        var hovered = interactive && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);

        Squircle.Fill(dl, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (hovered ? 0.16f : 0.10f) * opacity)));
        Material.EdgeSquircle(dl, rect.Min, rect.Max, radius, scale, opacity);

        var iconExtent = 19f * scale;
        var iconCenter = new Vector2(rect.Min.X + 14f * scale + iconExtent, rect.Center.Y);
        var iconMin = iconCenter - new Vector2(iconExtent, iconExtent);
        var iconMax = iconCenter + new Vector2(iconExtent, iconExtent);
        Squircle.Fill(dl, iconMin, iconMax, iconExtent * 0.52f, ImGui.GetColorU32(Palette.WithAlpha(notification.Accent, opacity)));

        var ink = Palette.WithAlpha(theme.TextStrong, opacity);
        if (!AppIconArt.TryDraw(notification.AppId, iconCenter, iconExtent * 2f, ink, Palette.WithAlpha(notification.Accent, opacity)))
        {
            var initial = notification.Title.Length > 0 ? notification.Title.Substring(0, 1) : "?";
            Typography.DrawCentered(iconCenter, initial, ink, 1.0f);
        }

        var textLeft = iconMax.X + 12f * scale;
        var textRight = rect.Max.X - 14f * scale;
        var time = NotificationCard.RelativeTime(notification.ReceivedAt);
        var timeSize = Typography.Measure(time, 0.74f);
        Typography.Draw(new Vector2(textRight - timeSize.X, rect.Min.Y + 12f * scale), time, Palette.WithAlpha(theme.TextMuted, opacity), 0.74f);

        dl.PushClipRect(new Vector2(textLeft, rect.Min.Y), new Vector2(textRight - timeSize.X - 6f * scale, rect.Max.Y), true);
        Typography.Draw(new Vector2(textLeft, rect.Min.Y + 11f * scale), notification.Title, ink, 0.9f, FontWeight.SemiBold);
        dl.PopClipRect();

        dl.PushClipRect(new Vector2(textLeft, rect.Min.Y), new Vector2(textRight, rect.Max.Y), true);
        Typography.Draw(new Vector2(textLeft, rect.Min.Y + 32f * scale), notification.Body, Palette.WithAlpha(theme.TextMuted, opacity), 0.84f);
        dl.PopClipRect();

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawHint(Rect screen, PhoneTheme theme, float scale, float opacity)
    {
        var center = new Vector2(screen.Center.X, screen.Max.Y - 40f * scale);
        var dl = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, 0.8f * opacity));
        var half = 7f * scale;
        var tipY = center.Y - 8f * scale;
        dl.AddLine(new Vector2(center.X - half, tipY + half), new Vector2(center.X, tipY), color, 2f * scale);
        dl.AddLine(new Vector2(center.X, tipY), new Vector2(center.X + half, tipY + half), color, 2f * scale);

        Typography.DrawCentered(new Vector2(center.X, center.Y + 6f * scale), Loc.T(L.LockScreen.SwipeToOpen), Palette.WithAlpha(theme.TextMuted, 0.8f * opacity), 0.78f);
    }

    private List<PhoneNotification> CollectCards()
    {
        var source = notifications.Recent;
        var cards = new List<PhoneNotification>(MaxCards);
        for (var index = source.Count - 1; index >= 0 && cards.Count < MaxCards; index--)
        {
            cards.Add(source[index]);
        }

        return cards;
    }

    private void HandleGesture(Rect screen, float delta, INavigator navigation)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = screen.Height;
        var unlockDistance = height * UnlockFraction;
        var fling = FlingVelocity * scale;

        drag.Track(delta);

        if (locked && cover.Value > 0.9f)
        {
            var swipeZone = new Rect(new Vector2(screen.Min.X, screen.Max.Y - height * 0.18f), screen.Max);
            drag.Begin(swipeZone);
        }

        if (drag.Active)
        {
            var fraction = Math.Clamp(1f + drag.Delta.Y / unlockDistance, 0f, 1f);
            cover.SnapTo(fraction);
            target = fraction;
        }

        if (drag.Released(out var totalDelta, out var velocity))
        {
            var unlock = -totalDelta.Y / unlockDistance > CommitFraction || velocity < -fling;
            target = unlock ? 0f : 1f;
        }

        if (!drag.Active)
        {
            cover.Step(target, SmoothTime, delta);
            if (cover.IsResting(target, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
            {
                cover.SnapTo(target);
            }
        }

        if (locked && target <= 0f && cover.Value <= 0.01f && !drag.Active)
        {
            locked = false;
            notifications.MarkAllRead();
        }
    }
}
