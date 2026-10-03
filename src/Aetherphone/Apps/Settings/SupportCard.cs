using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings;

internal sealed class SupportCard
{
    private const float Padding = 18f;
    private const float MedallionRadius = 20f;
    private const float TextGap = 14f;
    private const float ButtonsTopGap = 16f;
    private const float ButtonGap = 12f;
    private const float ButtonHeight = 48f;
    private const float ButtonGrow = 3f;
    private const float ButtonPressSink = 2f;
    private const float ContentGap = 10f;
    private const float ArrowSlide = 5f;
    private const int FloatingIcons = 10;
    private const int CometTrail = 26;
    private const double CometPeriodMs = 5200.0;
    private const double HeartbeatMs = 1400.0;
    private const double FloatPeriodMs = 6200.0;
    private const int BurstIcons = 14;
    private const float BurstMs = 950f;
    private const float BurstDistance = 80f;
    private const float GoldenRatio = 0.618034f;

    private static readonly Vector4 PatreonCoral = new(1.000f, 0.259f, 0.302f, 1f);
    private static readonly Vector4 PatreonSoft = new(1.000f, 0.580f, 0.600f, 1f);
    private static readonly Vector4 CoffeeYellow = new(1.000f, 0.867f, 0.000f, 1f);
    private static readonly Vector4 CoffeeAmber = new(1.000f, 0.690f, 0.130f, 1f);
    private static readonly Vector4 CoffeeInk = new(0.130f, 0.090f, 0.040f, 1f);
    private static readonly Vector4 White = Vector4.One;

    private static readonly ButtonStyle Patreon = new("##settings.support.patreon", AepConstants.PatreonUrl,
        FontAwesomeIcon.HandHoldingHeart, FontAwesomeIcon.Heart, PatreonCoral, Accent.Violet, White, PatreonSoft);

    private static readonly ButtonStyle Coffee = new("##settings.support.coffee", AepConstants.BuyMeACoffeeUrl,
        FontAwesomeIcon.MugHot, FontAwesomeIcon.MugHot, CoffeeYellow, CoffeeAmber, CoffeeInk, CoffeeYellow);

    private long patreonBurstTick = long.MinValue / 2;
    private long coffeeBurstTick = long.MinValue / 2;

    public void Draw(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padding = Padding * scale;
        var radius = MedallionRadius * scale;
        var textGap = TextGap * scale;
        var textWidth = MathF.Max(1f, width - padding * 2f - radius * 2f - textGap);
        var title = Typography.FitText(Loc.T(L.Settings.SupportAetherphone), textWidth, TextStyles.Headline);
        var titleSize = Typography.Measure(title, TextStyles.Headline);
        var headerX = origin.X + (width - radius * 2f - textGap - titleSize.X) * 0.5f;
        var topHeight = MathF.Max(radius * 2f, titleSize.Y);
        var buttonHeight = ButtonHeight * scale;
        var buttonGap = ButtonGap * scale;
        var height = padding + topHeight + ButtonsTopGap * scale + buttonHeight * 2f + buttonGap + padding;
        var max = origin + new Vector2(width, height);
        var rounding = Metrics.Radius.Card * scale;

        Squircle.FillVerticalGradient(drawList, origin, max, rounding,
            ImGui.GetColorU32(Palette.Mix(theme.GroupedCard, PatreonCoral, 0.16f)),
            ImGui.GetColorU32(Palette.Mix(theme.GroupedCard, CoffeeYellow, 0.10f)));
        drawList.PushClipRect(origin, max, true);
        DrawFloatingIcons(drawList, origin, max, scale);
        drawList.PopClipRect();
        Material.EdgeSquircle(drawList, origin, max, rounding, scale);
        Squircle.Stroke(drawList, origin, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(PatreonCoral, 0.35f)),
            1.2f * scale);
        DrawComet(drawList, origin, max, rounding, scale);

        var beat = Pulse.Heartbeat(HeartbeatMs);
        var medallionCenter = new Vector2(headerX + radius, origin.Y + padding + topHeight * 0.5f);
        ProgressRing.Glow(medallionCenter, radius, PatreonCoral, 0.45f + 0.7f * beat);
        drawList.AddCircleFilled(medallionCenter, radius,
            ImGui.GetColorU32(Palette.Mix(theme.GroupedCard, PatreonCoral, 0.30f) with { W = 1f }), 48);
        ProgressRing.Track(drawList, medallionCenter, radius, 1.5f * scale, Palette.WithAlpha(PatreonSoft, 0.85f));
        ProgressRing.CenterIcon(drawList, medallionCenter, FontAwesomeIcon.Heart, PatreonSoft,
            radius * (0.82f + 0.22f * beat));

        var titleY = origin.Y + padding + (topHeight - titleSize.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(headerX + radius * 2f + textGap, titleY), title, theme.TextStrong,
            TextStyles.Headline);

        var buttonSize = new Vector2(width - padding * 2f, buttonHeight);
        var firstButton = new Vector2(origin.X + padding, max.Y - padding - buttonHeight * 2f - buttonGap);
        DrawButton(drawList, Patreon, Loc.T(L.Settings.SupportOnPatreon), firstButton, buttonSize, beat,
            ref patreonBurstTick);
        DrawButton(drawList, Coffee, Loc.T(L.Settings.BuyMeACoffee),
            firstButton + new Vector2(0f, buttonHeight + buttonGap), buttonSize, beat, ref coffeeBurstTick);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawButton(ImDrawListPtr drawList, in ButtonStyle style, string label, Vector2 slot,
        Vector2 size, float beat, ref long burstTick)
    {
        var scale = UiScale.Current;
        var slotMax = slot + size;
        var hovered = UiInteract.Hover(slot, slotMax);
        var hover = HoverFx.Amount(style.Id, hovered);
        var press = 1f - PressFx.Toward(style.Id, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0f : 1f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(slot, slotMax), Loc.T(L.Settings.SupportHint));
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ImGui.SetClipboardText(style.Url);
                ShellToast.Show();
            }
        }

        if (UiInteract.Click(slot, slotMax, hovered))
        {
            UrlActions.OpenInBrowser(style.Url);
            burstTick = Environment.TickCount64;
        }

        var grow = (ButtonGrow * hover - ButtonPressSink * press) * scale;
        var min = slot - new Vector2(grow, grow);
        var max = slotMax + new Vector2(grow, grow);
        var rounding = (max.Y - min.Y) * 0.5f;
        var breath = 0.55f + 0.45f * Pulse.Wave(Pulse.Breath);
        for (var layer = 4; layer >= 1; layer--)
        {
            var spread = layer * 3f * scale;
            var alpha = 0.05f * layer * breath * (1f + 1.2f * hover);
            Squircle.Fill(drawList, min - new Vector2(spread, spread), max + new Vector2(spread, spread),
                rounding + spread, ImGui.GetColorU32(Palette.WithAlpha(style.Left, alpha)));
        }

        var left = Palette.Darken(Palette.Lighten(style.Left, 0.14f * hover), 0.12f * press);
        var right = Palette.Darken(Palette.Lighten(style.Right, 0.14f * hover), 0.12f * press);
        Squircle.FillHorizontalGradient(drawList, min, max, rounding, ImGui.GetColorU32(left),
            ImGui.GetColorU32(right));
        Material.Sheen(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.30f)), 1f,
            1.5f * scale);
        Sweep(drawList, min, max - min, hover, scale);
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f + 0.30f * hover)), 1.2f * scale);
        DrawButtonContent(drawList, style, label, min, max, hover, beat, scale);
        DrawBurst(drawList, style, (min + max) * 0.5f, burstTick, scale);
    }

    private static void DrawButtonContent(ImDrawListPtr drawList, in ButtonStyle style, string label, Vector2 min,
        Vector2 max, float hover, float beat, float scale)
    {
        var iconHeight = Typography.LineHeight(TextStyles.Headline) * 0.9f;
        var gap = ContentGap * scale;
        var arrowWidth = iconHeight * 0.8f;
        var available = MathF.Max(1f, max.X - min.X - (max.Y - min.Y) - iconHeight - arrowWidth - gap * 2f);
        var fitted = Typography.FitText(label, available, TextStyles.Headline);
        var labelSize = Typography.Measure(fitted, TextStyles.Headline);
        var contentWidth = iconHeight + gap + labelSize.X + gap + arrowWidth;
        var x = (min.X + max.X - contentWidth) * 0.5f;
        var midY = (min.Y + max.Y) * 0.5f;

        ProgressRing.CenterIcon(drawList, new Vector2(x + iconHeight * 0.5f, midY), style.Icon, style.Ink,
            iconHeight * (1f + 0.12f * beat));
        x += iconHeight + gap;
        Typography.Draw(drawList, new Vector2(x, midY - labelSize.Y * 0.5f), fitted, style.Ink, TextStyles.Headline);
        x += labelSize.X + gap;
        ProgressRing.CenterIcon(drawList, new Vector2(x + arrowWidth * 0.5f + ArrowSlide * scale * hover, midY),
            FontAwesomeIcon.ArrowRight, Palette.WithAlpha(style.Ink, 0.55f + 0.45f * hover), arrowWidth);
    }

    private static void Sweep(ImDrawListPtr drawList, Vector2 origin, Vector2 size, float hover, float scale)
    {
        var period = hover > 0.5f ? 1400.0 : 3200.0;
        var window = hover > 0.5f ? 0.8f : 0.35f;
        var phase = Pulse.Phase(period);
        if (phase > window)
        {
            return;
        }

        var sweep = phase / window;
        var slant = size.Y * 0.6f;
        var bandHalf = 18f * scale;
        var centerX = origin.X - bandHalf - slant + sweep * (size.X + slant + bandHalf * 2f);
        drawList.PushClipRect(origin, origin + size, true);
        const int strokes = 18;
        for (var stroke = -strokes; stroke <= strokes; stroke++)
        {
            var alpha = 0.18f * (1f - MathF.Abs(stroke) / (float)strokes);
            var x = centerX + stroke * bandHalf / strokes;
            drawList.AddLine(new Vector2(x + slant, origin.Y), new Vector2(x, origin.Y + size.Y),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), 1.4f * scale);
        }

        drawList.PopClipRect();
    }

    private static void DrawFloatingIcons(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale)
    {
        var width = max.X - min.X;
        var height = max.Y - min.Y;
        for (var index = 0; index < FloatingIcons; index++)
        {
            var seed = index * GoldenRatio % 1f;
            var progress = (Pulse.Phase(FloatPeriodMs + index * 870.0) + seed) % 1f;
            var drift = MathF.Sin(progress * MathF.PI * 2.6f + index) * 10f * scale;
            var position = new Vector2(min.X + width * (0.06f + 0.88f * seed) + drift,
                max.Y + 12f * scale - progress * (height + 24f * scale));
            var alpha = 0.18f * MathF.Sin(progress * MathF.PI);
            var isHeart = index % 2 == 0;
            var icon = isHeart ? FontAwesomeIcon.Heart : FontAwesomeIcon.MugHot;
            var color = isHeart ? PatreonSoft : CoffeeYellow;
            var size = (9f + 7f * (index * 0.37f % 1f)) * scale;
            ProgressRing.CenterIcon(drawList, position, icon, Palette.WithAlpha(color, alpha), size);
        }
    }

    private static void DrawComet(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale)
    {
        var inset = rounding * 0.29f;
        var innerMin = min + new Vector2(inset, inset);
        var innerMax = max - new Vector2(inset, inset);
        var perimeter = 2f * (innerMax.X - innerMin.X + innerMax.Y - innerMin.Y);
        var head = Pulse.Phase(CometPeriodMs) * perimeter;
        var spacing = 5f * scale;
        for (var trailIndex = CometTrail - 1; trailIndex >= 0; trailIndex--)
        {
            var fade = 1f - trailIndex / (float)CometTrail;
            var point = PerimeterPoint(innerMin, innerMax, head - trailIndex * spacing, perimeter);
            var color = Palette.Mix(CoffeeYellow, PatreonSoft, fade);
            drawList.AddCircleFilled(point, (1f + 1.6f * fade) * scale,
                ImGui.GetColorU32(Palette.WithAlpha(color, 0.85f * fade * fade)));
        }

        var headPoint = PerimeterPoint(innerMin, innerMax, head, perimeter);
        drawList.AddCircleFilled(headPoint, 7f * scale, ImGui.GetColorU32(Palette.WithAlpha(PatreonCoral, 0.18f)));
    }

    private static Vector2 PerimeterPoint(Vector2 min, Vector2 max, float distance, float perimeter)
    {
        var width = max.X - min.X;
        var height = max.Y - min.Y;
        distance %= perimeter;
        if (distance < 0f)
        {
            distance += perimeter;
        }

        if (distance < width)
        {
            return new Vector2(min.X + distance, min.Y);
        }

        distance -= width;
        if (distance < height)
        {
            return new Vector2(max.X, min.Y + distance);
        }

        distance -= height;
        if (distance < width)
        {
            return new Vector2(max.X - distance, max.Y);
        }

        return new Vector2(min.X, max.Y - (distance - width));
    }

    private static void DrawBurst(ImDrawListPtr drawList, in ButtonStyle style, Vector2 center, long burstTick,
        float scale)
    {
        var elapsed = Environment.TickCount64 - burstTick;
        if (elapsed >= BurstMs)
        {
            return;
        }

        var progress = elapsed / BurstMs;
        var eased = 1f - MathF.Pow(1f - progress, 3f);
        for (var index = 0; index < BurstIcons; index++)
        {
            var angle = index * MathF.PI * 2f / BurstIcons + index * 0.35f;
            var distance = BurstDistance * scale * eased * (0.7f + 0.3f * (index * GoldenRatio % 1f));
            var position = center + new Vector2(MathF.Cos(angle) * 1.6f, MathF.Sin(angle)) * distance;
            var color = index % 2 == 0 ? style.Burst : Palette.Lighten(style.Right, 0.3f);
            ProgressRing.CenterIcon(drawList, position, style.BurstIcon, Palette.WithAlpha(color, 1f - progress),
                (10f + 8f * (1f - progress)) * scale);
        }
    }

    private readonly record struct ButtonStyle(
        string Id,
        string Url,
        FontAwesomeIcon Icon,
        FontAwesomeIcon BurstIcon,
        Vector4 Left,
        Vector4 Right,
        Vector4 Ink,
        Vector4 Burst);
}
