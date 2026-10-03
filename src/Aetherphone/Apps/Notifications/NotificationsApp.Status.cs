using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Notifications;

internal sealed partial class NotificationsApp
{
    private const float SectionHeaderHeight = 34f;
    private const float CardLift = 0.085f;
    private const float LayerLift = 0.045f;
    private const float StatusGlassOpacity = 0.92f;
    private const float StatusTileSize = 40f;
    private const float StatusGlyphSize = 18f;
    private const float StatusTextGap = 12f;
    private const float StatusLineGap = 2f;
    private const float StatusPillHeight = 28f;
    private const float StatusPillPadX = 14f;
    private const float StatusPillAlpha = 0.22f;
    private const float StatusPillHoverAlpha = 0.32f;
    private const float StatusHintAlpha = 0.84f;
    private const float EmptyTileSize = 72f;
    private const float EmptyGlyphSize = 30f;
    private const float EmptyLift = 64f;
    private const float EmptyTitleGap = 16f;
    private const float EmptyHintGap = 6f;
    private const float EmptyTextInset = 56f;
    private const float EmptyMaxTextWidth = 280f;
    private const float LinkHeight = 44f;
    private const float LinkChevronReach = 3.5f;
    private const float LinkChevronGap = 8f;
    private const float LinkChevronThickness = 1.6f;
    private const float LinkHoverLift = 0.25f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private void DrawStatus(float width, float scale)
    {
        var doNotDisturb = configuration.DoNotDisturb;
        if (!doNotDisturb && !(configuration.QuietWhileBusy && PlayerBusy.Now))
        {
            return;
        }

        var title = doNotDisturb ? Loc.T(L.Settings.DoNotDisturb) : Loc.T(L.Settings.QuietWhileBusy);
        var hint = doNotDisturb ? Loc.T(L.Notifications.DoNotDisturbHint) : Loc.T(L.Notifications.BusyHint);
        var accent = doNotDisturb ? AccentRing.Indigo : AccentRing.Slate;
        var icon = doNotDisturb ? FontAwesomeIcon.Moon : FontAwesomeIcon.HourglassHalf;
        var pad = Metrics.Space.Lg * scale;
        var tileSize = StatusTileSize * scale;
        var origin = ImGui.GetCursorScreenPos();
        var textLeft = origin.X + pad + tileSize + StatusTextGap * scale;
        var right = origin.X + width - pad;
        var pillLabel = Loc.T(L.Notifications.TurnOff);
        var pillWidth = doNotDisturb
            ? Typography.Measure(pillLabel, TextStyles.FootnoteEmphasized).X + StatusPillPadX * 2f * scale
            : 0f;
        var titleRow = MathF.Max(Typography.LineHeight(TextStyles.Headline), doNotDisturb ? StatusPillHeight * scale : 0f);
        var hintWidth = MathF.Max(1f, right - textLeft);
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, hintWidth).Y;
        var textHeight = titleRow + StatusLineGap * scale + hintHeight;
        var height = pad * 2f + MathF.Max(tileSize, textHeight);
        var max = new Vector2(origin.X + width, origin.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        Material.AccentGlass(drawList, origin, max, Metrics.Radius.Widget * scale, scale, accent, StatusGlassOpacity);
        var tileMin = new Vector2(origin.X + pad, origin.Y + pad);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor, IconTile.Surface(accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StatusGlyphSize * scale);

        var top = origin.Y + pad;
        var titleRight = doNotDisturb ? right - pillWidth - StatusTextGap * scale : right;
        var fittedTitle = Typography.FitText(title, MathF.Max(1f, titleRight - textLeft), TextStyles.Headline);
        var titleHeight = Typography.Measure(fittedTitle, TextStyles.Headline).Y;
        Typography.Draw(drawList, new Vector2(textLeft, top + (titleRow - titleHeight) * 0.5f), fittedTitle, White,
            TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + titleRow + StatusLineGap * scale), hint,
            Palette.WithAlpha(White, StatusHintAlpha), TextStyles.Subheadline, hintWidth);
        if (doNotDisturb)
        {
            var pillTop = top + (titleRow - StatusPillHeight * scale) * 0.5f;
            var pill = new Rect(new Vector2(right - pillWidth, pillTop),
                new Vector2(right, pillTop + StatusPillHeight * scale));
            if (StatusPill(drawList, pill, pillLabel))
            {
                ToggleDoNotDisturb();
            }
        }

        Advance(origin, width, height, BlockGap, scale);
    }

    private static bool StatusPill(ImDrawListPtr drawList, Rect rect, string label)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        drawList.AddRectFilled(rect.Min, rect.Max,
            ImGui.GetColorU32(Palette.WithAlpha(White, hovered ? StatusPillHoverAlpha : StatusPillAlpha)),
            rect.Height * 0.5f);
        Typography.DrawCentered(drawList, rect.Center, label, White, TextStyles.FootnoteEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void DrawEmpty(Rect body, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerX = body.Center.X;
        var tileSize = EmptyTileSize * scale;
        var tileTop = body.Center.Y - EmptyLift * scale - tileSize * 0.5f;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, FontAwesomeIcon.Bell, AccentRing.Ink,
            EmptyGlyphSize * scale);
        var maxWidth = MathF.Min(body.Width - EmptyTextInset * scale, EmptyMaxTextWidth * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, Loc.T(L.Notifications.CaughtUpTitle),
            TextStyles.Title3, ui.TitleInk, new Vector2(centerX, tileMax.Y + EmptyTitleGap * scale), maxWidth);
        Typography.DrawWrappedCentered(drawList, Loc.T(L.Notifications.CaughtUpHint), TextStyles.Subheadline,
            ui.MutedInk, new Vector2(centerX, titleBottom + EmptyHintGap * scale), maxWidth);
        UiAnchors.Report("notifications.list", new Rect(tileMin, new Vector2(tileMax.X, titleBottom)));
    }

    private void DrawSettingsLink(float width, float scale)
    {
        if (appFilter is not null)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var height = LinkHeight * scale;
        var label = Loc.T(L.Notifications.Settings);
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        var reach = LinkChevronReach * scale;
        var total = labelSize.X + LinkChevronGap * scale + reach;
        var left = origin.X + (width - total) * 0.5f;
        var centerY = origin.Y + height * 0.5f;
        var hitMin = new Vector2(left - Metrics.Space.Md * scale, origin.Y);
        var hitMax = new Vector2(left + total + Metrics.Space.Md * scale, origin.Y + height);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var ink = hovered ? Palette.Mix(ui.HeaderInk, White, LinkHoverLift) : ui.HeaderInk;
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(drawList, new Vector2(left, centerY - labelSize.Y * 0.5f), label, ink, TextStyles.Subheadline);
        var tipX = left + labelSize.X + LinkChevronGap * scale + reach;
        var color = ImGui.GetColorU32(ink);
        drawList.AddLine(new Vector2(tipX - reach, centerY - reach), new Vector2(tipX, centerY), color,
            LinkChevronThickness * scale);
        drawList.AddLine(new Vector2(tipX, centerY), new Vector2(tipX - reach, centerY + reach), color,
            LinkChevronThickness * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(hitMin, hitMax, hovered))
        {
            OpenSettings(null);
        }

        Advance(origin, width, height, 0f, scale);
    }
}
