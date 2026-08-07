using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class ConductGateOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float RevealSmoothTime = 0.18f;
    private const float MaxDim = 0.74f;
    private const float MinPanelScale = 0.96f;
    private const float PanelRounding = 28f;
    private const float SideMargin = 14f;
    private const float TopMargin = 52f;
    private const float BottomMargin = 34f;
    private const float Padding = 22f;
    private const float ButtonHeight = 50f;
    private const float BarGap = 12f;
    private const float BarHeight = 4f;
    private const float CloseRadius = 13f;
    private const float CardPad = 14f;
    private const float ChipSize = 26f;
    private const float SectionGap = 12f;
    private const float ItemGap = 7f;
    private const float EndOfRulesFraction = 0.99f;
    private const float MinimumReadSeconds = 4f;

    private static readonly Vector4 EncouragedColor = new(0.34f, 0.74f, 0.48f, 1f);

    private readonly ConductGateService service;
    private Spring reveal;
    private ConductGate? shown;
    private bool wasActive;
    private bool scrollTopPending;
    private float elapsed;
    private bool reachedEnd;
    private float readProgress;

    public ConductGateOverlay(ConductGateService service)
    {
        this.service = service;
    }

    public bool Captures => service.Active is not null;

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var active = service.Active;
        if (active is not null && (!wasActive || !ReferenceEquals(shown, active)))
        {
            shown = active;
            elapsed = 0f;
            reachedEnd = false;
            readProgress = 0f;
            scrollTopPending = true;
        }

        wasActive = active is not null;

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(active is not null ? 1f : 0f, RevealSmoothTime, delta);
        if (shown is null)
        {
            return;
        }

        if (active is null && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            shown = null;
            return;
        }

        if (active is not null)
        {
            elapsed += delta;
        }

        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##conductOverlay", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            DrawPanel(screen, theme, shown, opacity, active is not null);
        }
    }

    private void DrawPanel(Rect screen, PhoneTheme theme, ConductGate gate, float opacity, bool interactive)
    {
        var scale = UiScale.Current;
        var accent = AppAccents.For(gate.AppId);
        var drawList = ImGui.GetWindowDrawList();
        var reviewing = service.ActiveIsReview;

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
        var centerX = panel.Center.X;

        var ack = reviewing ? string.Empty : Loc.T(L.Conduct.Acknowledge);
        var ackHeight = reviewing ? 0f : Typography.MeasureWrappedBlock(ack, TextStyles.Footnote, innerWidth).Y;
        var footerHeight = reviewing
            ? 0f
            : ackHeight + BarGap * scale + BarHeight * scale + BarGap * scale + ButtonHeight * scale;
        var footerTop = panel.Max.Y - pad - footerHeight;
        var listBottom = reviewing ? footerTop : footerTop - 12f * scale;

        var headerBottom = DrawHeader(panel, theme, gate, accent, opacity, centerX, innerWidth, pad);

        var listRect = new Rect(new Vector2(innerLeft, headerBottom + 10f * scale),
            new Vector2(innerLeft + innerWidth, listBottom));
        DrawRules(listRect, theme, gate, accent, opacity);

        if (reviewing)
        {
            DrawCloseButton(new Vector2(panel.Max.X - pad * 0.85f, panel.Min.Y + pad * 0.85f), theme, opacity,
                interactive);
            return;
        }

        drawList.AddLine(new Vector2(innerLeft, listBottom), new Vector2(innerLeft + innerWidth, listBottom),
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f * opacity)), 1f);

        DrawFooter(theme, gate, accent, opacity, interactive, centerX, innerLeft, innerWidth, footerTop, ack,
            ackHeight);
    }

    private void DrawCloseButton(Vector2 center, PhoneTheme theme, float opacity, bool interactive)
    {
        var pressed = AppSkin.IconButton(center, CloseRadius * UiScale.Current,
            FontAwesomeIcon.Times.ToIconString(), Palette.WithAlpha(theme.TextStrong, opacity),
            Palette.WithAlpha(theme.TextStrong, 0.10f * opacity), 0.5f, theme);
        if (pressed && interactive && opacity > 0.5f)
        {
            service.Dismiss();
        }
    }

    private static float DrawHeader(Rect panel, PhoneTheme theme, ConductGate gate, Vector4 accent, float opacity,
        float centerX, float innerWidth, float pad)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();

        var tileSize = 48f * scale;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, panel.Min.Y + pad);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        Squircle.Fill(drawList, tileMin, tileMax, tileSize * 0.30f,
            ImGui.GetColorU32(Palette.WithAlpha(accent, opacity)));
        AppSkin.Icon(drawList, new Vector2(centerX, (tileMin.Y + tileMax.Y) * 0.5f), gate.Icon.ToIconString(),
            new Vector4(1f, 1f, 1f, opacity), 0.95f);

        var y = tileMax.Y + 12f * scale;
        var eyebrow = Loc.T(L.Conduct.Eyebrow);
        var eyebrowHeight = Typography.Measure(eyebrow, TextStyles.FootnoteEmphasized).Y;
        Typography.DrawCentered(drawList, new Vector2(centerX, y + eyebrowHeight * 0.5f), eyebrow,
            Palette.WithAlpha(accent, opacity), TextStyles.FootnoteEmphasized);
        y += eyebrowHeight + 6f * scale;

        y += Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(gate.Title),
            Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.Title2, innerWidth);
        y += 8f * scale;

        y += Typography.DrawWrappedCentered(new Vector2(centerX, y), Loc.T(gate.Intro),
            Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Subheadline, innerWidth);
        return y;
    }

    private void DrawRules(Rect listRect, PhoneTheme theme, ConductGate gate, Vector4 accent, float opacity)
    {
        var scale = UiScale.Current;
        if (listRect.Height <= 0f)
        {
            return;
        }

        var rulesKey = ImGui.GetID("##conductRules");
        ImGui.SetCursorScreenPos(listRect.Min);
        using (ImRaii.Child("##conductRules", listRect.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            var surface = DragScrollHost.Begin(rulesKey);
            if (scrollTopPending)
            {
                surface.JumpToTop();
                scrollTopPending = false;
            }

            var width = ScrollLayout.StableContentWidth();
            for (var index = 0; index < gate.Sections.Length; index++)
            {
                DrawSection(gate.Sections[index], width, theme, accent, opacity);
            }

            ImGui.Dummy(new Vector2(width, 4f * scale));
            TrackReading();
        }
    }

    private void TrackReading()
    {
        var maxScroll = ImGui.GetScrollMaxY();
        if (maxScroll <= 0.001f)
        {
            readProgress = 1f;
            reachedEnd = true;
            return;
        }

        var fraction = Math.Clamp(ImGui.GetScrollY() / maxScroll, 0f, 1f);
        readProgress = MathF.Max(readProgress, fraction);
        if (fraction >= EndOfRulesFraction)
        {
            reachedEnd = true;
        }
    }

    private static void DrawSection(in ConductSection section, float width, PhoneTheme theme, Vector4 accent,
        float opacity)
    {
        if (section.Heading is null && section.Items.Length == 0)
        {
            if (section.Lead is { } note)
            {
                DrawNote(Loc.T(note), width, theme, opacity);
            }

            return;
        }

        var scale = UiScale.Current;
        var toneColor = section.Tone switch
        {
            ConductTone.Encouraged => EncouragedColor,
            ConductTone.Prohibited => theme.Danger,
            ConductTone.Restricted => theme.TextMuted,
            _ => accent,
        };

        var pad = CardPad * scale;
        var innerWidth = width - pad * 2f;
        var chip = ChipSize * scale;
        var chipGap = 10f * scale;
        var blockGap = 9f * scale;
        var itemIndent = 22f * scale;
        var itemGap = ItemGap * scale;
        var itemTextWidth = innerWidth - itemIndent;

        var headingText = section.Heading is { } heading ? Loc.T(heading) : null;
        var headingHeight = headingText is null
            ? 0f
            : Typography.MeasureWrappedBlock(headingText, TextStyles.SubheadlineEmphasized,
                innerWidth - chip - chipGap).Y;
        var headerHeight = headingText is null ? 0f : MathF.Max(chip, headingHeight);

        var leadText = section.Lead is { } lead ? Loc.T(lead) : null;
        var leadHeight = leadText is null
            ? 0f
            : Typography.MeasureWrappedBlock(leadText, TextStyles.Subheadline, innerWidth).Y;

        Span<float> itemHeights = section.Items.Length > 0 ? stackalloc float[section.Items.Length] : default;
        var itemsHeight = 0f;
        for (var index = 0; index < section.Items.Length; index++)
        {
            var text = Loc.T(section.Items[index]);
            var lineHeight = Typography.Measure(text, TextStyles.Subheadline).Y;
            var wrapped = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, itemTextWidth).Y;
            itemHeights[index] = MathF.Max(wrapped, lineHeight);
            itemsHeight += itemHeights[index];
            if (index > 0)
            {
                itemsHeight += itemGap;
            }
        }

        var cardHeight = pad + headerHeight + pad;
        if (leadText is not null)
        {
            cardHeight += blockGap + leadHeight;
        }

        if (section.Items.Length > 0)
        {
            cardHeight += blockGap + itemsHeight;
        }

        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var cardMax = origin + new Vector2(width, cardHeight);
        var rounding = Metrics.Radius.Card * scale;
        var cardColor = section.Tone switch
        {
            ConductTone.Encouraged => Palette.Mix(theme.GroupedCard, EncouragedColor, 0.10f),
            ConductTone.Prohibited => Palette.Mix(theme.GroupedCard, theme.Danger, 0.08f),
            _ => theme.GroupedCard,
        };
        Squircle.Fill(drawList, origin, cardMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(cardColor, theme.GroupedCard.W * opacity)));
        Material.EdgeSquircle(drawList, origin, cardMax, rounding, scale, opacity);

        var left = origin.X + pad;
        var cursorY = origin.Y + pad;
        if (headingText is not null)
        {
            var chipMin = new Vector2(left, cursorY + (headerHeight - chip) * 0.5f);
            var chipMax = chipMin + new Vector2(chip, chip);
            var chipFill = section.Tone == ConductTone.Restricted
                ? Palette.WithAlpha(theme.TextStrong, 0.10f * opacity)
                : Palette.WithAlpha(toneColor, 0.16f * opacity);
            Squircle.Fill(drawList, chipMin, chipMax, chip * Metrics.Radius.TileFactor,
                ImGui.GetColorU32(chipFill));
            var chipIcon = section.Icon ?? section.Tone switch
            {
                ConductTone.Encouraged => FontAwesomeIcon.Check,
                ConductTone.Prohibited => FontAwesomeIcon.Ban,
                ConductTone.Restricted => FontAwesomeIcon.Ban,
                _ => FontAwesomeIcon.InfoCircle,
            };
            AppSkin.Icon(drawList, (chipMin + chipMax) * 0.5f, chipIcon.ToIconString(),
                Palette.WithAlpha(toneColor, opacity), 0.55f);

            Typography.DrawWrappedLeft(new Vector2(left + chip + chipGap, cursorY + (headerHeight - headingHeight) * 0.5f),
                headingText, Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.SubheadlineEmphasized,
                innerWidth - chip - chipGap);
            cursorY += headerHeight;
        }

        if (leadText is not null)
        {
            cursorY += blockGap;
            Typography.DrawWrappedLeft(new Vector2(left, cursorY), leadText,
                Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Subheadline, innerWidth);
            cursorY += leadHeight;
        }

        if (section.Items.Length > 0)
        {
            cursorY += blockGap;
            for (var index = 0; index < section.Items.Length; index++)
            {
                var text = Loc.T(section.Items[index]);
                var lineHeight = Typography.Measure(text, TextStyles.Subheadline).Y;
                var markCenter = new Vector2(left + 8f * scale, cursorY + lineHeight * 0.5f);
                if (section.Tone == ConductTone.Neutral)
                {
                    drawList.AddCircleFilled(markCenter, 2.5f * scale,
                        ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, opacity)));
                }
                else
                {
                    var mark = section.Tone == ConductTone.Encouraged ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
                    AppSkin.Icon(drawList, markCenter, mark.ToIconString(), Palette.WithAlpha(toneColor, opacity),
                        0.58f);
                }

                Typography.DrawWrappedLeft(new Vector2(left + itemIndent, cursorY), text,
                    Palette.WithAlpha(theme.TextStrong, 0.92f * opacity), TextStyles.Subheadline, itemTextWidth);
                cursorY += itemHeights[index];
                if (index < section.Items.Length - 1)
                {
                    cursorY += itemGap;
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + SectionGap * scale));
    }

    private static void DrawNote(string text, float width, PhoneTheme theme, float opacity)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var inset = 4f * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + inset, origin.Y), text,
            Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Footnote, width - inset * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + SectionGap * scale));
    }

    private void DrawFooter(PhoneTheme theme, ConductGate gate, Vector4 accent, float opacity, bool interactive,
        float centerX, float innerLeft, float innerWidth, float footerTop, string ack, float ackHeight)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();

        Typography.DrawWrappedCentered(new Vector2(centerX, footerTop), ack,
            Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Footnote, innerWidth);

        var barY = footerTop + ackHeight + BarGap * scale;
        var barMin = new Vector2(innerLeft, barY);
        var barMax = new Vector2(innerLeft + innerWidth, barY + BarHeight * scale);
        Squircle.Fill(drawList, barMin, barMax, BarHeight * scale * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.10f * opacity)));
        var progress = reachedEnd ? 1f : readProgress;
        if (progress > 0.001f)
        {
            Squircle.Fill(drawList, barMin, new Vector2(innerLeft + innerWidth * progress, barMax.Y),
                BarHeight * scale * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(accent, opacity)));
        }

        var floorRemaining = MathF.Max(0f, MinimumReadSeconds - elapsed);
        var ready = reachedEnd && floorRemaining <= 0.001f;
        var label = ready
            ? Loc.T(L.Conduct.AgreeAction)
            : reachedEnd
                ? Loc.T(L.Conduct.WaitAction, (int)MathF.Ceiling(floorRemaining))
                : Loc.T(L.Conduct.ReadToEndAction);
        var buttonY = barMax.Y + BarGap * scale;
        var buttonRect = new Rect(new Vector2(innerLeft, buttonY),
            new Vector2(innerLeft + innerWidth, buttonY + ButtonHeight * scale));
        var enabled = ready && interactive && opacity > 0.5f;
        if (ConfirmDialog.DrawPillButton(buttonRect, label, enabled, theme, 1f, opacity, ConfirmButtonTone.Primary) &&
            enabled)
        {
            service.Acknowledge();
        }
    }
}
