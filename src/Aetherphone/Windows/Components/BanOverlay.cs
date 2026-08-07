using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class BanOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float PresenceSmoothTime = 0.16f;
    private const float DismissBottomMargin = 30f;
    private const float DismissHeight = 50f;
    private readonly AethernetSession session;
    private Spring presence;
    private bool visible;
    private bool wasBanned;

    public BanOverlay(AethernetSession session)
    {
        this.session = session;
    }

    public bool IsActive => session.IsBanned && visible;

    public static bool IsTemporary(SuspensionDto? suspension)
    {
        return suspension is { Permanent: false, UntilUnix: not null };
    }

    public void Present()
    {
        visible = session.IsBanned;
    }

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var banned = session.IsBanned;
        if (banned && !wasBanned)
        {
            visible = true;
        }
        else if (!banned)
        {
            visible = false;
        }

        wasBanned = banned;

        var active = IsActive;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        presence.Step(active ? 1f : 0f, PresenceSmoothTime, delta);
        if (presence.Value <= 0.01f)
        {
            if (!active)
            {
                presence.SnapTo(0f);
            }

            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##banOverlay", screen.Size, false, OverlayFlags))
        {
            DrawContent(screen, theme, Math.Clamp(presence.Value, 0f, 1f), active);
        }
    }

    private void DrawContent(Rect screen, PhoneTheme theme, float reveal, bool interactive)
    {
        var scale = UiScale.Current;
        var dl = ImGui.GetWindowDrawList();
        var alpha = Math.Clamp(reveal * 1.4f, 0f, 1f);
        dl.AddRectFilled(screen.Min, screen.Max,
            ImGui.GetColorU32(new Vector4(0.03f, 0.03f, 0.05f, 0.94f * alpha)));

        var centerX = screen.Center.X;
        var maxWidth = MathF.Min(screen.Size.X - 56f * scale, 320f * scale);

        var iconCenter = new Vector2(centerX, screen.Min.Y + screen.Size.Y * 0.26f);
        var iconRadius = 40f * scale;
        dl.AddCircleFilled(iconCenter, iconRadius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.16f * alpha)), 48);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = FontAwesomeIcon.Ban.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), iconCenter - size * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, alpha)), glyph);
        }

        var suspension = session.Suspension;
        var temporary = IsTemporary(suspension);

        var y = iconCenter.Y + iconRadius + 28f * scale;
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y),
            Loc.T(temporary ? L.Account.BanScreenTimeoutTitle : L.Account.BanScreenTitle),
            Palette.WithAlpha(theme.TextStrong, alpha), new TextStyle(1.5f, FontWeight.SemiBold), maxWidth);

        var bodyStyle = new TextStyle(1f, FontWeight.Regular);
        y += 14f * scale;
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y),
            temporary
                ? Loc.T(L.Account.BanScreenLifts, ModerationNoticeText.LiftMoment(suspension!.UntilUnix!.Value))
                : Loc.T(L.Account.BanScreenBody),
            Palette.WithAlpha(theme.TextMuted, alpha), bodyStyle, maxWidth);

        var rule = suspension is null ? string.Empty : suspension.RuleTitle;
        var reason = rule.Length > 0 ? rule : session.BanReason;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            y += 12f * scale;
            y += Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Account.BanScreenReason, reason),
                Palette.WithAlpha(theme.TextStrong, 0.85f * alpha), bodyStyle, maxWidth);
        }

        if (suspension is not null && suspension.RuleSummary.Length > 0)
        {
            y += 8f * scale;
            y += Typography.DrawWrappedCentered(new Vector2(centerX, y), suspension.RuleSummary,
                Palette.WithAlpha(theme.TextMuted, 0.85f * alpha), new TextStyle(0.9f, FontWeight.Regular), maxWidth);
        }

        if (suspension is not null && suspension.Note.Length > 0)
        {
            y += 10f * scale;
            y += Typography.DrawWrappedCentered(new Vector2(centerX, y),
                Loc.T(L.Moderation.NoticeModeratorNote, suspension.Note),
                Palette.WithAlpha(theme.TextStrong, 0.8f * alpha), new TextStyle(0.9f, FontWeight.Regular), maxWidth);
        }

        y += 14f * scale;
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Account.BanScreenSocialLocked),
            Palette.WithAlpha(theme.TextStrong, 0.9f * alpha), new TextStyle(0.95f, FontWeight.Medium), maxWidth);

        y += 12f * scale;
        Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Account.BanScreenContact),
            Palette.WithAlpha(theme.TextMuted, 0.8f * alpha), new TextStyle(0.9f, FontWeight.Regular), maxWidth);

        DrawDismiss(screen, theme, alpha, interactive);
    }

    private void DrawDismiss(Rect screen, PhoneTheme theme, float alpha, bool interactive)
    {
        var scale = UiScale.Current;
        var centerX = screen.Center.X;
        var width = MathF.Min(screen.Size.X - 56f * scale, 240f * scale);
        var rect = new Rect(
            new Vector2(centerX - width * 0.5f, screen.Max.Y - (DismissBottomMargin + DismissHeight) * scale),
            new Vector2(centerX + width * 0.5f, screen.Max.Y - DismissBottomMargin * scale));
        var enabled = interactive && alpha > 0.5f;
        if (ConfirmDialog.DrawPillButton(rect, Loc.T(L.Account.FailDismiss), enabled, theme, 1f, alpha,
                ConfirmButtonTone.Primary) && enabled)
        {
            visible = false;
        }
    }
}
