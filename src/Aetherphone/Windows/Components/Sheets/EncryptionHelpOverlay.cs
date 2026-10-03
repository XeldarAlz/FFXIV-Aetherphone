using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal enum HelpTone : byte
{
    Neutral = 0,
    Good = 1,
    Caution = 2,
    Danger = 3,
}

internal readonly record struct HelpTopic(FontAwesomeIcon Icon, HelpTone Tone, LocString Title, LocString Body);

internal sealed class EncryptionHelpOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                 ImGuiWindowFlags.NoBackground;

    private const float MaxDim = 0.74f;
    private const float PanelRounding = 28f;
    private const float SideMargin = 14f;
    private const float TopMargin = 52f;
    private const float BottomMargin = 34f;
    private const float Padding = 22f;
    private const float CloseRadius = 13f;
    private const float CardPad = 14f;
    private const float ChipSize = 26f;
    private const float SectionGap = 12f;

    private static readonly Vector4 GoodColor = new(0.34f, 0.74f, 0.48f, 1f);
    private static readonly Vector4 CautionColor = new(0.85f, 0.60f, 0.28f, 1f);

    private static readonly HelpTopic[] Topics =
    {
        new(FontAwesomeIcon.Clock, HelpTone.Good, L.Encryption.HelpDecryptingTitle,
            L.Encryption.HelpDecryptingBody),
        new(FontAwesomeIcon.Lock, HelpTone.Caution, L.Encryption.HelpLockedTitle, L.Encryption.HelpLockedBody),
        new(FontAwesomeIcon.History, HelpTone.Caution, L.Encryption.HelpOlderKeyTitle,
            L.Encryption.HelpOlderKeyBody),
        new(FontAwesomeIcon.Desktop, HelpTone.Caution, L.Encryption.HelpUnreadableTitle,
            L.Encryption.HelpUnreadableBody),
        new(FontAwesomeIcon.ExclamationTriangle, HelpTone.Neutral, L.Encryption.HelpDamagedTitle,
            L.Encryption.HelpDamagedBody),
        new(FontAwesomeIcon.Ban, HelpTone.Danger, L.Encryption.HelpNeverTitle, L.Encryption.HelpNeverBody),
        new(FontAwesomeIcon.ShieldAlt, HelpTone.Good, L.Encryption.HelpPreventTitle, L.Encryption.HelpPreventBody),
    };

    private readonly EncryptionHelpService service;
    private Spring reveal;
    private bool wasActive;
    private bool scrollTopPending;
    private int openedFrame;

    public EncryptionHelpOverlay(EncryptionHelpService service)
    {
        this.service = service;
    }

    public bool Captures => service.Active;

    public void Dismiss()
    {
        service.Dismiss();
    }

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var active = service.Active;
        if (active && !wasActive)
        {
            scrollTopPending = true;
            openedFrame = ImGui.GetFrameCount();
        }

        wasActive = active;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(active ? 1f : 0f, Motion.Appear, delta);
        if (!active && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return;
        }

        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##encryptionHelpOverlay", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            var panel = DrawPanel(screen, theme, opacity, active);
            if (!active || opacity <= 0.5f)
            {
                return;
            }

            if (ImGui.GetFrameCount() != openedFrame && UiInteract.ClickedOutside(panel.Min, panel.Max))
            {
                service.Dismiss();
            }
        }
    }

    private Rect DrawPanel(Rect screen, PhoneTheme theme, float opacity, bool interactive)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var panel = new Rect(
            new Vector2(screen.Min.X + SideMargin * scale, screen.Min.Y + TopMargin * scale),
            new Vector2(screen.Max.X - SideMargin * scale, screen.Max.Y - BottomMargin * scale));
        Squircle.Fill(drawList, panel.Min, panel.Max, PanelRounding * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Surface, opacity)));
        Squircle.Stroke(drawList, panel.Min, panel.Max, PanelRounding * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f * opacity)), 1f);

        var pad = Padding * scale;
        var innerLeft = panel.Min.X + pad;
        var innerWidth = panel.Width - pad * 2f;
        var headerBottom = DrawHeader(panel, theme, opacity, panel.Center.X, innerWidth, pad);
        var listRect = new Rect(new Vector2(innerLeft, headerBottom + 10f * scale),
            new Vector2(innerLeft + innerWidth, panel.Max.Y - pad));
        DrawTopics(listRect, theme, opacity);

        var pressed = AppSkin.IconButton(new Vector2(panel.Max.X - pad * 0.85f, panel.Min.Y + pad * 0.85f),
            CloseRadius * scale, IconGlyph.Of(FontAwesomeIcon.Times),
            Palette.WithAlpha(theme.TextStrong, opacity),
            Palette.WithAlpha(theme.TextStrong, 0.10f * opacity), 0.5f, theme);
        if (pressed && interactive && opacity > 0.5f)
        {
            service.Dismiss();
        }

        return panel;
    }

    private static float DrawHeader(Rect panel, PhoneTheme theme, float opacity, float centerX, float innerWidth,
        float pad)
    {
        var scale = UiScale.Current;
        var y = panel.Min.Y + pad;
        Typography.DrawCentered(new Vector2(centerX, y), Loc.T(L.Encryption.HelpEyebrow),
            Palette.WithAlpha(theme.TextMuted, opacity), 0.78f, FontWeight.SemiBold);
        y += 22f * scale;
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Encryption.HelpTitle),
            Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.Title2, innerWidth);
        y += 8f * scale;
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(L.Encryption.HelpIntro),
            Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Footnote, innerWidth);
        return y + 6f * scale;
    }

    private void DrawTopics(Rect listRect, PhoneTheme theme, float opacity)
    {
        var scale = UiScale.Current;
        if (listRect.Height <= 0f)
        {
            return;
        }

        var key = ImGui.GetID("##encryptionHelp");
        ImGui.SetCursorScreenPos(listRect.Min);
        using (ImRaii.Child("##encryptionHelp", listRect.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            var surface = DragScrollHost.Begin(key);
            if (scrollTopPending)
            {
                surface.JumpToTop();
                scrollTopPending = false;
            }

            var width = ScrollLayout.StableContentWidth();
            for (var index = 0; index < Topics.Length; index++)
            {
                DrawTopic(Topics[index], width, theme, opacity);
            }

            ImGui.Dummy(new Vector2(width, 4f * scale));
        }
    }

    private static void DrawTopic(in HelpTopic topic, float width, PhoneTheme theme, float opacity)
    {
        var scale = UiScale.Current;
        var pad = CardPad * scale;
        var innerWidth = width - pad * 2f;
        var chip = ChipSize * scale;
        var chipGap = 10f * scale;
        var toneColor = topic.Tone switch
        {
            HelpTone.Good => GoodColor,
            HelpTone.Caution => CautionColor,
            HelpTone.Danger => theme.Danger,
            _ => theme.TextMuted,
        };

        var titleText = Loc.T(topic.Title);
        var bodyText = Loc.T(topic.Body);
        var titleWidth = innerWidth - chip - chipGap;
        var titleHeight = Typography.MeasureWrappedBlock(titleText, TextStyles.SubheadlineEmphasized, titleWidth).Y;
        var headerHeight = MathF.Max(chip, titleHeight);
        var bodyHeight = Typography.MeasureWrappedBlock(bodyText, TextStyles.Footnote, innerWidth).Y;
        var cardHeight = pad * 2f + headerHeight + 9f * scale + bodyHeight;

        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var cardMax = origin + new Vector2(width, cardHeight);
        var rounding = Metrics.Radius.Card * scale;
        var cardColor = topic.Tone switch
        {
            HelpTone.Good => Palette.Mix(theme.GroupedCard, GoodColor, 0.10f),
            HelpTone.Danger => Palette.Mix(theme.GroupedCard, theme.Danger, 0.08f),
            _ => theme.GroupedCard,
        };
        Squircle.Fill(drawList, origin, cardMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(cardColor, theme.GroupedCard.W * opacity)));
        Material.EdgeSquircle(drawList, origin, cardMax, rounding, scale, opacity);

        var left = origin.X + pad;
        var cursorY = origin.Y + pad;
        var chipMin = new Vector2(left, cursorY + (headerHeight - chip) * 0.5f);
        var chipMax = chipMin + new Vector2(chip, chip);
        Squircle.Fill(drawList, chipMin, chipMax, chip * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(toneColor, 0.16f * opacity)));
        AppSkin.Icon(drawList, (chipMin + chipMax) * 0.5f, IconGlyph.Of(topic.Icon),
            Palette.WithAlpha(toneColor, opacity), 0.55f);
        Typography.DrawWrappedLeft(new Vector2(left + chip + chipGap, cursorY + (headerHeight - titleHeight) * 0.5f),
            titleText, Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.SubheadlineEmphasized, titleWidth);
        cursorY += headerHeight + 9f * scale;
        Typography.DrawWrappedLeft(new Vector2(left, cursorY), bodyText,
            Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Footnote, innerWidth);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + SectionGap * scale));
    }
}
