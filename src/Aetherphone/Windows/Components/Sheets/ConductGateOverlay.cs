using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class ConductGateOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const string LineProbe = "Ay";
    private const float MaxDim = 0.5f;
    private const float PanelRounding = 28f;
    private const float PanelTopGap = 8f;
    private const float ContentClipInset = 6f;
    private const float AntiAliasFringe = 1f;
    private const float BarHeight = 56f;
    private const float EdgeFade = 20f;
    private const float BottomFadeSpan = 1.5f;
    private const float WashHeight = 300f;
    private const float WashAlpha = 0.22f;
    private const float HeroTop = 40f;
    private const float HeroIcon = 76f;
    private const float HeroIconRest = 0.86f;
    private const float HeroGlyphFraction = 0.44f;
    private const float IntroInset = 12f;
    private const float TileSize = 30f;
    private const float TileGlyph = 15f;
    private const float TileGap = 12f;
    private const float MarkColumn = 20f;
    private const float MarkGlyph = 11f;
    private const float DotRadius = 2.5f;
    private const float RowPad = 8f;
    private const float CloseGlyph = 12f;
    private const float CompactTitleRise = 6f;
    private const float CollapseRange = 18f;
    private const float FooterBottomInset = 44f;
    private const float PendingFillAlpha = 0.24f;
    private const float PendingLabelAlpha = 0.78f;
    private const float GrabberAlpha = 0.35f;
    private const float EndOfRulesFraction = 0.99f;
    private const float MinimumReadSeconds = 4f;
    private const float RevealDelay = 0.12f;
    private const float RevealStagger = 0.06f;
    private const float RevealRise = 14f;
    private const int RevealStaggerCap = 8;
    private const int HeroBeats = 3;

    private const float BandTail = 0.75f;
    private const float BandedCardTint = 0.06f;
    private const float BandOverlayAlpha = 0.22f;
    private const float PillFillAlpha = 0.14f;
    private const float PillPadX = 8f;
    private const float PillPadY = 3f;
    private const float LightInkDeepen = 0.3f;

    private static readonly Vector4 AllowedColor = new(0.204f, 0.780f, 0.349f, 1f);
    private static readonly Vector4 VeiledColor = new(1f, 0.584f, 0f, 1f);
    private static readonly Vector4 ZeroColor = new(0.80f, 0.16f, 0.20f, 1f);
    private static readonly Vector4 GlyphInk = new(1f, 1f, 1f, 1f);

    private readonly ConductGateService service;
    private Spring reveal;
    private Spring readiness;
    private Spring progressShown;
    private ConductGate? shown;
    private bool shownIsReview;
    private bool wasActive;
    private bool scrollTopPending;
    private int openedFrame;
    private float elapsed;
    private bool reachedEnd;
    private float readProgress;
    private int waitSeconds;
    private string waitLabel = string.Empty;

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
            Present(active);
        }
        else if (active is null && wasActive)
        {
            UiFeedback.Play(UiSound.SheetDismiss);
        }

        wasActive = active is not null;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(active is not null ? 1f : 0f, Motion.Sheet, delta);
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

        var presence = Math.Clamp(reveal.Value, 0f, 1f);
        var interactive = active is not null && presence > 0.5f;
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##conductOverlay", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * presence)));
            var panel = PanelRect(screen, theme, presence);
            DrawPanel(panel, theme, shown, delta, interactive);
            if (!interactive || !shownIsReview || ImGui.GetFrameCount() == openedFrame)
            {
                return;
            }

            if (UiInteract.ClickedOutside(panel.Min, panel.Max))
            {
                service.Dismiss();
            }
        }
    }

    private void Present(ConductGate gate)
    {
        shown = gate;
        shownIsReview = service.ActiveIsReview;
        openedFrame = ImGui.GetFrameCount();
        elapsed = 0f;
        reachedEnd = false;
        readProgress = 0f;
        waitSeconds = 0;
        scrollTopPending = true;
        readiness.SnapTo(0f);
        progressShown.SnapTo(0f);
        UiFeedback.Play(UiSound.SheetPresent);
    }

    private static Rect PanelRect(Rect screen, PhoneTheme theme, float presence)
    {
        var scale = UiScale.Current;
        var restTop = screen.Min.Y + (theme.TopZoneHeight + PanelTopGap) * scale;
        var height = screen.Max.Y - restTop;
        var top = restTop + height * (1f - presence);
        return new Rect(new Vector2(screen.Min.X, top), new Vector2(screen.Max.X, top + height));
    }

    private void DrawPanel(Rect panel, PhoneTheme theme, ConductGate gate, float delta, bool interactive)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var accent = AppAccents.For(gate.AppId) with { W = 1f };
        var rounding = PanelRounding * scale;
        var surface = theme.AppBackground with { W = 1f };
        Squircle.Fill(drawList, panel.Min, panel.Max, rounding, ImGui.GetColorU32(surface));
        Squircle.FillVerticalGradient(drawList, panel.Min,
            new Vector2(panel.Max.X, panel.Min.Y + WashHeight * scale), rounding,
            ImGui.GetColorU32(accent with { W = WashAlpha }), ImGui.GetColorU32(accent with { W = 0f }));
        Squircle.Stroke(drawList, panel.Min, panel.Max, rounding, ImGui.GetColorU32(theme.Hairline),
            Metrics.Stroke.Hairline);

        var listBottom = shownIsReview
            ? panel.Max.Y - Metrics.Space.Xl * scale
            : panel.Max.Y - (Metrics.Space.Md + Button.LargeHeight + FooterBottomInset) * scale;
        var listRect = new Rect(new Vector2(panel.Min.X, panel.Min.Y + ContentClipInset * scale),
            new Vector2(panel.Max.X, listBottom));
        DrawRules(listRect, panel, theme, gate, accent, surface, interactive);
        if (shownIsReview)
        {
            return;
        }

        DrawFooter(drawList, panel, listBottom, theme, accent, delta, interactive);
    }

    private void DrawRules(Rect listRect, Rect panel, PhoneTheme theme, ConductGate gate, Vector4 accent,
        Vector4 surface, bool interactive)
    {
        if (listRect.Height <= 0f)
        {
            return;
        }

        var scale = UiScale.Current;
        var rulesKey = ImGui.GetID("##conductRules");
        ImGui.SetCursorScreenPos(listRect.Min);
        using (ImRaii.Child("##conductRules", listRect.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            var scrollSurface = DragScrollHost.Begin(rulesKey);
            if (scrollTopPending)
            {
                scrollSurface.JumpToTop();
                scrollTopPending = false;
            }

            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var fullWidth = ScrollLayout.StableContentWidth();
            var gutter = Metrics.Space.Lg * scale;
            var left = origin.X + gutter;
            var width = fullWidth - gutter * 2f;
            var centerX = origin.X + fullWidth * 0.5f;

            var y = DrawHero(gate, theme, accent, centerX, origin.Y + (HeroTop - ContentClipInset) * scale, width,
                out var titleBottom);
            y += Metrics.Space.Xl * scale;
            for (var index = 0; index < gate.Sections.Length; index++)
            {
                y += DrawSection(drawList, gate.Sections[index], index, left, y, width, theme, accent);
                y += Metrics.Space.Md * scale;
            }

            if (!shownIsReview)
            {
                y += Metrics.Space.Xs * scale;
                y += DrawClosing(centerX, y, width, theme, gate.Sections.Length);
            }

            y += Metrics.Space.Xl * scale;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(fullWidth, y - origin.Y));
            TrackReading();

            var collapseStart = titleBottom - origin.Y + (ContentClipInset - BarHeight) * scale;
            var windowMin = ImGui.GetWindowPos();
            var windowMax = windowMin + ImGui.GetWindowSize();
            var cover = new Rect(new Vector2(left - AntiAliasFringe, windowMin.Y),
                new Vector2(left + width + AntiAliasFringe, windowMax.Y));
            DrawScrollEdges(drawList, cover, panel, gate, theme, accent, surface, collapseStart);
            if (!shownIsReview)
            {
                return;
            }

            DrawGrabber(drawList, panel, theme);
            DrawCloseButton(drawList, panel, theme, interactive);
        }
    }

    private float DrawHero(ConductGate gate, PhoneTheme theme, Vector4 accent, float centerX, float top, float width,
        out float titleBottom)
    {
        var scale = UiScale.Current;
        var trim = ImGui.GetStyle().ItemSpacing.Y;
        var iconSize = HeroIcon * scale;
        var iconReveal = Reveal(0);
        var iconCenter = new Vector2(centerX, top + iconSize * 0.5f + Rise(iconReveal));
        DrawAppIcon(ImGui.GetWindowDrawList(), gate, accent, iconCenter,
            iconSize * (HeroIconRest + (1f - HeroIconRest) * iconReveal), iconReveal);

        var y = top + iconSize + Metrics.Space.Lg * scale;
        var titleReveal = Reveal(1);
        var titleRise = Rise(titleReveal);
        var eyebrowInk = ControlInk.From(theme).WithAccent(accent).AccentInk;
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y + titleRise), Loc.T(L.Conduct.Eyebrow),
            eyebrowInk with { W = titleReveal }, TextStyles.FootnoteEmphasized, width) - trim;
        y += Metrics.Space.Xxs * scale;
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y + titleRise), Loc.T(gate.Title),
            theme.TextStrong with { W = titleReveal }, TextStyles.Title1, width) - trim;
        titleBottom = y;

        y += Metrics.Space.Sm * scale;
        var introReveal = Reveal(2);
        y += Typography.DrawWrappedCentered(new Vector2(centerX, y + Rise(introReveal)), Loc.T(gate.Intro),
            theme.TextMuted with { W = introReveal }, TextStyles.Subheadline, width - IntroInset * 2f * scale) - trim;
        return y;
    }

    private static void DrawAppIcon(ImDrawListPtr drawList, ConductGate gate, Vector4 accent, Vector2 center,
        float size, float alpha)
    {
        var scale = UiScale.Current;
        var half = new Vector2(size * 0.5f, size * 0.5f);
        var min = center - half;
        var max = center + half;
        var radius = size * Metrics.Radius.HomeTileFactor;
        if (AppIconTile.TryDraw(drawList, gate.AppId, accent, min, max, radius, alpha, true, scale))
        {
            return;
        }

        Elevation.IconRest(drawList, min, max, radius, scale, alpha);
        IconTile.FillShaded(drawList, min, max, radius, IconTile.Surface(accent), alpha);
        ProgressRing.CenterIcon(drawList, center, gate.Icon, GlyphInk with { W = alpha }, size * HeroGlyphFraction);
    }

    private float DrawSection(ImDrawListPtr drawList, in ConductSection section, int order, float left, float top,
        float width, PhoneTheme theme, Vector4 accent)
    {
        var scale = UiScale.Current;
        var alpha = Reveal(order + HeroBeats);
        var cardTop = top + Rise(alpha);
        var trim = ImGui.GetStyle().ItemSpacing.Y;
        var pad = Metrics.Space.Lg * scale;
        var tile = TileSize * scale;
        var rowPad = RowPad * scale;
        var markColumn = MarkColumn * scale;
        var textLeft = left + pad + tile + TileGap * scale;
        var textWidth = left + width - pad - textLeft;
        var itemWidth = textWidth - markColumn;
        var items = section.Items;
        var banded = section.Tone == ConductTone.ZeroTolerance;

        var heading = Loc.T(section.Heading);
        var pill = PillLabel(section.Pill);
        var pillSize = pill is null ? Vector2.Zero : PillSize(pill);
        var pillGap = Metrics.Space.Sm * scale;
        var pillBeside = pill is null ||
                         Typography.Measure(heading, TextStyles.Headline).X + pillGap + pillSize.X <= textWidth;
        var headingWidth = pill is not null && pillBeside ? textWidth - pillGap - pillSize.X : textWidth;
        var headingHeight = Typography.MeasureWrappedBlock(heading, TextStyles.Headline, headingWidth).Y - trim;
        var pillStack = pillBeside ? 0f : Metrics.Space.Xs * scale + pillSize.Y;
        var headerHeight = MathF.Max(tile, headingHeight + pillStack);
        var bandTail = banded ? pad * BandTail : 0f;
        var lead = section.Lead is { } leadKey ? Loc.T(leadKey) : null;
        var leadHeight = lead is null
            ? 0f
            : Typography.MeasureWrappedBlock(lead, TextStyles.Subheadline, textWidth).Y - trim;
        var note = section.Note is { } noteKey ? Loc.T(noteKey) : null;
        var noteHeight = note is null
            ? 0f
            : Typography.MeasureWrappedBlock(note, TextStyles.Footnote, itemWidth).Y - trim;

        var itemHeights = items.Length > 0 ? stackalloc float[items.Length] : default;
        var itemsHeight = 0f;
        for (var index = 0; index < items.Length; index++)
        {
            itemHeights[index] =
                Typography.MeasureWrappedBlock(Loc.T(items[index]), TextStyles.Subheadline, itemWidth).Y - trim;
            itemsHeight += itemHeights[index] + rowPad * 2f;
        }

        var leadGap = banded ? Metrics.Space.Md * scale : Metrics.Space.Xs * scale;
        var itemsGap = Metrics.Space.Xxs * scale;
        var noteGap = items.Length > 0 ? 0f : Metrics.Space.Md * scale;
        var cardHeight = pad + headerHeight + bandTail;
        if (lead is not null)
        {
            cardHeight += leadGap + leadHeight;
        }

        if (items.Length > 0)
        {
            cardHeight += itemsGap + itemsHeight;
        }

        if (note is not null)
        {
            cardHeight += noteGap + rowPad + noteHeight;
        }

        cardHeight += items.Length > 0 && note is null ? pad - rowPad : pad;

        var min = new Vector2(left, cardTop);
        var max = new Vector2(left + width, cardTop + cardHeight);
        var rounding = Metrics.Radius.Grouped * scale;
        var tone = ToneColor(section.Tone, theme, accent);
        var ink = Ink(tone, theme);
        var cardFill = banded ? Palette.Mix(theme.GroupedCard, tone, BandedCardTint) : theme.GroupedCard;
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(cardFill with { W = theme.GroupedCard.W * alpha }));

        var cursorY = cardTop + pad;
        if (banded)
        {
            drawList.PushClipRect(min, new Vector2(max.X, cursorY + headerHeight + bandTail), true);
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(tone with { W = alpha }));
            drawList.PopClipRect();
        }

        var tileMin = new Vector2(left + pad, cursorY + (headerHeight - tile) * 0.5f);
        var tileMax = tileMin + new Vector2(tile, tile);
        IconTile.FillShaded(drawList, tileMin, tileMax, tile * Metrics.Radius.TileFactor,
            banded ? GlyphInk with { W = BandOverlayAlpha } : tone, alpha);
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, section.Icon, GlyphInk with { W = alpha },
            TileGlyph * scale);
        var headingTop = cursorY + (headerHeight - headingHeight - pillStack) * 0.5f;
        Typography.DrawWrappedLeft(new Vector2(textLeft, headingTop), heading,
            (banded ? GlyphInk : theme.TextStrong) with { W = alpha }, TextStyles.Headline, headingWidth);
        if (pill is not null)
        {
            var pillMin = pillBeside
                ? new Vector2(max.X - pad - pillSize.X, cursorY + (headerHeight - pillSize.Y) * 0.5f)
                : new Vector2(textLeft, headingTop + headingHeight + Metrics.Space.Xs * scale);
            DrawPill(drawList, pillMin, pillSize, pill,
                banded ? GlyphInk with { W = BandOverlayAlpha } : tone with { W = PillFillAlpha },
                banded ? GlyphInk : ink, alpha);
        }

        cursorY += headerHeight + bandTail;
        if (lead is not null)
        {
            cursorY += leadGap;
            Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), lead, theme.TextMuted with { W = alpha },
                TextStyles.Subheadline, textWidth);
            cursorY += leadHeight;
        }

        if (items.Length > 0)
        {
            cursorY += itemsGap;
            var markY = Typography.Measure(LineProbe, TextStyles.Subheadline).Y * 0.5f;
            for (var index = 0; index < items.Length; index++)
            {
                if (index > 0)
                {
                    DrawSeparator(drawList, textLeft, max.X, cursorY, theme, alpha);
                }

                var textTop = cursorY + rowPad;
                DrawMark(drawList, section.Tone, ink, new Vector2(textLeft + MarkGlyph * scale * 0.5f, textTop + markY),
                    theme, alpha);
                Typography.DrawWrappedLeft(new Vector2(textLeft + markColumn, textTop), Loc.T(items[index]),
                    theme.TextStrong with { W = alpha }, TextStyles.Subheadline, itemWidth);
                cursorY += itemHeights[index] + rowPad * 2f;
            }
        }

        if (note is not null)
        {
            cursorY += noteGap;
            DrawSeparator(drawList, textLeft, max.X, cursorY, theme, alpha);
            var noteTop = cursorY + rowPad;
            var noteMarkY = Typography.Measure(LineProbe, TextStyles.Footnote).Y * 0.5f;
            ProgressRing.CenterIcon(drawList, new Vector2(textLeft + MarkGlyph * scale * 0.5f, noteTop + noteMarkY),
                FontAwesomeIcon.InfoCircle, theme.TextMuted with { W = alpha }, MarkGlyph * scale);
            Typography.DrawWrappedLeft(new Vector2(textLeft + markColumn, noteTop), note,
                theme.TextMuted with { W = alpha }, TextStyles.Footnote, itemWidth);
        }

        return cardHeight;
    }

    private float DrawClosing(float centerX, float top, float width, PhoneTheme theme, int order)
    {
        var inset = Metrics.Space.Lg * UiScale.Current;
        var alpha = Reveal(order + HeroBeats);
        return Typography.DrawWrappedCentered(new Vector2(centerX, top + Rise(alpha)), Loc.T(L.Conduct.Acknowledge),
            theme.TextMuted with { W = alpha }, TextStyles.Footnote, width - inset * 2f) - ImGui.GetStyle().ItemSpacing.Y;
    }

    private static void DrawSeparator(ImDrawListPtr drawList, float left, float right, float y, PhoneTheme theme,
        float alpha)
    {
        var top = MathF.Floor(y);
        drawList.AddRectFilled(new Vector2(left, top), new Vector2(right, top + Metrics.Stroke.Hairline),
            ImGui.GetColorU32(theme.Separator with { W = theme.Separator.W * alpha }));
    }

    private static void DrawMark(ImDrawListPtr drawList, ConductTone tone, Vector4 ink, Vector2 center,
        PhoneTheme theme, float alpha)
    {
        var scale = UiScale.Current;
        if (tone is ConductTone.Rule or ConductTone.Info)
        {
            var dot = tone == ConductTone.Rule ? ink : theme.TextMuted;
            drawList.AddCircleFilled(center, DotRadius * scale, ImGui.GetColorU32(dot with { W = alpha }), 12);
            return;
        }

        var glyph = tone switch
        {
            ConductTone.Allowed => FontAwesomeIcon.Check,
            ConductTone.Veiled => FontAwesomeIcon.EyeSlash,
            _ => FontAwesomeIcon.Times,
        };
        ProgressRing.CenterIcon(drawList, center, glyph, ink with { W = alpha }, MarkGlyph * scale);
    }

    private static Vector4 ToneColor(ConductTone tone, PhoneTheme theme, Vector4 accent) => tone switch
    {
        ConductTone.Allowed => AllowedColor,
        ConductTone.Veiled => VeiledColor,
        ConductTone.Removed => theme.Danger with { W = 1f },
        ConductTone.ZeroTolerance => ZeroColor,
        ConductTone.Info => AccentRing.Slate,
        _ => accent,
    };

    private static Vector4 Ink(Vector4 tone, PhoneTheme theme) =>
        Palette.Luminance(theme.TextStrong) < 0.5f ? Palette.Mix(tone, theme.TextStrong, LightInkDeepen) : tone;

    private static string? PillLabel(ConductPill pill) => pill switch
    {
        ConductPill.Fine => Loc.T(L.Conduct.PillFine),
        ConductPill.Veiled => Loc.T(L.Conduct.PillVeiled),
        ConductPill.Removed => Loc.T(L.Conduct.PillRemoved),
        ConductPill.Suspended => Loc.T(L.Conduct.PillSuspended),
        _ => null,
    };

    private static Vector2 PillSize(string label)
    {
        var scale = UiScale.Current;
        var text = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        return new Vector2(text.X + PillPadX * 2f * scale, text.Y + PillPadY * 2f * scale);
    }

    private static void DrawPill(ImDrawListPtr drawList, Vector2 min, Vector2 size, string label, Vector4 fill,
        Vector4 ink, float alpha)
    {
        var max = min + size;
        Squircle.Fill(drawList, min, max, size.Y * 0.5f, ImGui.GetColorU32(fill with { W = fill.W * alpha }));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, ink with { W = alpha },
            TextStyles.FootnoteEmphasized);
    }

    private void TrackReading()
    {
        if (ImGui.GetFrameCount() == openedFrame)
        {
            return;
        }

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

    private static void DrawScrollEdges(ImDrawListPtr drawList, Rect cover, Rect panel, ConductGate gate,
        PhoneTheme theme, Vector4 accent, Vector4 surface, float collapseStart)
    {
        var scale = UiScale.Current;
        var fade = EdgeFade * scale;
        var scrollY = ImGui.GetScrollY();
        var collapse = Math.Clamp((scrollY - collapseStart) / (CollapseRange * scale), 0f, 1f);
        var topEdge = Math.Clamp(scrollY / fade, 0f, 1f);
        if (topEdge > 0.001f)
        {
            var solidTop = cover.Min.Y;
            var solidBottom = solidTop + (panel.Min.Y + BarHeight * scale - solidTop) * collapse;
            var fadeBottom = solidBottom + fade;
            var top = ImGui.GetColorU32(Backdrop(surface, accent, panel, solidTop) with { W = topEdge });
            var middle = ImGui.GetColorU32(Backdrop(surface, accent, panel, solidBottom) with { W = topEdge });
            var clear = ImGui.GetColorU32(Backdrop(surface, accent, panel, fadeBottom) with { W = 0f });
            if (solidBottom > solidTop + 0.5f)
            {
                drawList.AddRectFilledMultiColor(new Vector2(cover.Min.X, solidTop),
                    new Vector2(cover.Max.X, solidBottom), top, top, middle, middle);
            }

            drawList.AddRectFilledMultiColor(new Vector2(cover.Min.X, solidBottom),
                new Vector2(cover.Max.X, fadeBottom), middle, middle, clear, clear);
        }

        if (collapse > 0.001f)
        {
            DrawCompactTitle(drawList, panel, gate, theme, collapse);
        }

        var bottomEdge = Math.Clamp((ImGui.GetScrollMaxY() - scrollY) / fade, 0f, 1f);
        if (bottomEdge <= 0.001f)
        {
            return;
        }

        var fadeTop = cover.Max.Y - fade * BottomFadeSpan;
        var bottomClear = ImGui.GetColorU32(Backdrop(surface, accent, panel, fadeTop) with { W = 0f });
        var bottomSolid = ImGui.GetColorU32(Backdrop(surface, accent, panel, cover.Max.Y) with { W = bottomEdge });
        drawList.AddRectFilledMultiColor(new Vector2(cover.Min.X, fadeTop), cover.Max, bottomClear, bottomClear,
            bottomSolid, bottomSolid);
    }

    private static Vector4 Backdrop(Vector4 surface, Vector4 accent, Rect panel, float y)
    {
        var depth = Math.Clamp((y - panel.Min.Y) / (WashHeight * UiScale.Current), 0f, 1f);
        return Palette.Mix(surface, accent, WashAlpha * (1f - depth));
    }

    private static void DrawCompactTitle(ImDrawListPtr drawList, Rect panel, ConductGate gate, PhoneTheme theme,
        float collapse)
    {
        var scale = UiScale.Current;
        var reserve = (Metrics.Space.Lg * 2f + RoundButton.SmallRadius * 2f) * scale;
        var title = Typography.FitText(Loc.T(gate.Title), panel.Width - reserve * 2f, TextStyles.Headline);
        var center = new Vector2(panel.Center.X,
            panel.Min.Y + BarHeight * scale * 0.5f + (1f - collapse) * CompactTitleRise * scale);
        Typography.DrawCentered(drawList, center, title, theme.TextStrong with { W = collapse }, TextStyles.Headline);
    }

    private static void DrawGrabber(ImDrawListPtr drawList, Rect panel, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var width = Metrics.Size.GrabberWidth * scale;
        var height = Metrics.Size.GrabberHeight * scale;
        var min = new Vector2(panel.Center.X - width * 0.5f, panel.Min.Y + Metrics.Space.Sm * scale);
        drawList.AddRectFilled(min, min + new Vector2(width, height),
            ImGui.GetColorU32(theme.TextMuted with { W = GrabberAlpha }), height * 0.5f);
    }

    private void DrawCloseButton(ImDrawListPtr drawList, Rect panel, PhoneTheme theme, bool interactive)
    {
        var scale = UiScale.Current;
        var radius = RoundButton.SmallRadius * scale;
        var center = new Vector2(panel.Max.X - Metrics.Space.Lg * scale - radius,
            panel.Min.Y + BarHeight * scale * 0.5f);
        if (RoundButton.FontIcon(drawList, "conduct.close", center, radius, FontAwesomeIcon.Times,
                CloseGlyph * scale, ControlInk.From(theme)) && interactive)
        {
            service.Dismiss();
        }
    }

    private void DrawFooter(ImDrawListPtr drawList, Rect panel, float footerTop, PhoneTheme theme, Vector4 accent,
        float delta, bool interactive)
    {
        var scale = UiScale.Current;
        var gutter = Metrics.Space.Lg * scale;
        var buttonTop = footerTop + Metrics.Space.Md * scale;
        var rect = new Rect(new Vector2(panel.Min.X + gutter, buttonTop),
            new Vector2(panel.Max.X - gutter, buttonTop + Button.LargeHeight * scale));
        var floorRemaining = MathF.Max(0f, MinimumReadSeconds - elapsed);
        var ready = reachedEnd && floorRemaining <= 0.001f;
        var blend = Math.Clamp(readiness.Step(ready ? 1f : 0f, Motion.Sheet, delta), 0f, 1f);
        var progress = Math.Clamp(progressShown.Step(reachedEnd ? 1f : readProgress, Motion.Sheet, delta), 0f, 1f);
        var ink = ControlInk.From(theme).WithAccent(accent);
        if (blend < 0.999f)
        {
            DrawPendingButton(drawList, rect, ink, progress, PendingLabel(floorRemaining), 1f - blend);
        }

        if (blend <= 0.001f)
        {
            return;
        }

        if (Button.Draw(drawList, rect, Loc.T(L.Conduct.AgreeAction), ink, enabled: ready, id: "conduct.agree",
                opacity: blend) && interactive)
        {
            service.Acknowledge();
        }
    }

    private string PendingLabel(float floorRemaining)
    {
        if (!reachedEnd)
        {
            return Loc.T(L.Conduct.ReadToEndAction);
        }

        var seconds = Math.Max(1, (int)MathF.Ceiling(floorRemaining));
        if (seconds != waitSeconds)
        {
            waitSeconds = seconds;
            waitLabel = Loc.T(L.Conduct.WaitAction, seconds);
        }

        return waitLabel;
    }

    private static void DrawPendingButton(ImDrawListPtr drawList, Rect rect, in ControlInk ink, float progress,
        string label, float alpha)
    {
        var radius = rect.Height * 0.5f;
        var track = Surfaces.Fill(ink, FillLevel.Secondary);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(track with { W = track.W * alpha }));
        if (progress > 0.001f)
        {
            drawList.PushClipRect(rect.Min, new Vector2(rect.Min.X + rect.Width * progress, rect.Max.Y), true);
            Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(ink.Accent with { W = PendingFillAlpha * alpha }));
            drawList.PopClipRect();
        }

        var style = Button.LabelStyle(rect.Height);
        Typography.DrawCentered(drawList, rect.Center, Typography.FitText(label, rect.Width - rect.Height, style),
            ink.Ink with { W = PendingLabelAlpha * alpha }, style);
    }

    private float Reveal(int order) =>
        Spring.Settle(elapsed - RevealDelay - Math.Min(order, RevealStaggerCap) * RevealStagger, Motion.Sheet);

    private static float Rise(float revealAmount) => (1f - revealAmount) * RevealRise * UiScale.Current;
}
