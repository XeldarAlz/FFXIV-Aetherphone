using Aetherphone.Apps.Games.Online;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games;

internal static class GamesHubArt
{
    public const float SectionHeight = CardSectionHeader.HeightUnits;
    public const float SectionGap = 22f;
    private const float SeeAllChevron = 11f;
    private const float SeeAllGap = 4f;
    private const float StateTileSize = 76f;
    private const float StateGlyphSize = 34f;
    private const float StateLift = 44f;
    private const float StateTitleGap = 18f;
    private const float StateHintGap = 6f;
    private const float StateActionGap = 20f;
    private const float StateActionHeight = Button.RegularHeight;
    private const float StateActionMinWidth = 150f;
    private const float StateTextInset = 56f;
    private const float StateMaxText = 290f;
    private const float MedallionRim = 2f;
    private const float MedallionArt = 1.3f;
    private const float NoticeIconSize = 18f;
    private const float NoticePadding = 14f;

    public static float ButtonWidth(string label, float height) =>
        Typography.Measure(label, Button.LabelStyle(height)).X + height;

    public static bool Section(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, string title,
        string action, string id)
    {
        var scale = UiScale.Current;
        var height = CardSectionHeader.HeightUnits * scale;
        var centerY = top + height * 0.5f;
        var actionWidth = 0f;
        var clicked = false;
        if (action.Length > 0)
        {
            var labelSize = Typography.Measure(action, TextStyles.Body);
            var chevron = SeeAllChevron * scale;
            actionWidth = labelSize.X + SeeAllGap * scale + chevron;
            var min = new Vector2(left + width - actionWidth, top);
            var max = new Vector2(left + width, top + height);
            var hovered = UiInteract.Hover(min, max);
            var ink = hovered ? Palette.Mix(ui.Accent, ui.TitleInk, 0.25f) : ui.Accent;
            Typography.Draw(drawList, new Vector2(min.X, centerY - labelSize.Y * 0.5f), action, ink, TextStyles.Body);
            PhoneIcon.Draw(drawList, new Vector2(max.X - chevron * 0.5f, centerY), PhoneIcons.ChevronRight, ink,
                chevron);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            clicked = UiInteract.Click(min, max, hovered);
            ReportAnchor(id, new Rect(min, max));
        }

        var reserve = actionWidth > 0f ? actionWidth + Metrics.Space.Md * scale : 0f;
        CardSectionHeader.Draw(drawList, new Vector2(left, top), width, title, ui.TitleInk, reserve);
        return clicked;
    }

    public static bool StateScreen(ImDrawListPtr drawList, AppSkin ui, Rect body, FontAwesomeIcon icon, string title,
        string hint, string action, string id)
    {
        var scale = UiScale.Current;
        var centerX = body.Center.X;
        var tileSize = StateTileSize * scale;
        var tileTop = MathF.Max(body.Min.Y + Metrics.Space.Xl * scale, body.Center.Y - StateLift * scale - tileSize);
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyphSize * scale);
        var maxWidth = MathF.Min(body.Width - StateTextInset * scale, StateMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTitleGap * scale), maxWidth);
        var hintBottom = hint.Length == 0
            ? titleBottom
            : Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, titleBottom + StateHintGap * scale), maxWidth);
        if (action.Length == 0)
        {
            return false;
        }

        var height = StateActionHeight * scale;
        var width = MathF.Min(maxWidth, MathF.Max(StateActionMinWidth * scale,
            ButtonWidth(action, height)));
        var top = hintBottom + StateActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top), new Vector2(centerX + width * 0.5f, top + height));
        return Button.Draw(drawList, rect, action, ui.Ink, id: id);
    }

    public static float Notice(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale,
        string message, FontAwesomeIcon icon, Vector4 tint)
    {
        var pad = NoticePadding * scale;
        var iconSize = NoticeIconSize * scale;
        var textLeft = left + pad + iconSize + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, left + width - pad - textLeft);
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Subheadline, textWidth);
        var height = MathF.Max(block.Y, iconSize) + pad * 2f;
        var max = new Vector2(left + width, top + height);
        ui.Card(drawList, new Vector2(left, top), max, Metrics.Radius.Widget * scale);
        ProgressRing.CenterIcon(drawList, new Vector2(left + pad + iconSize * 0.5f, top + height * 0.5f), icon, tint,
            iconSize);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + (height - block.Y) * 0.5f), message,
            ui.MutedInk, TextStyles.Subheadline, textWidth);
        return max.Y;
    }

    public static void Medallion(ImDrawListPtr drawList, string kind, Vector2 center, float radius, Vector4 rim,
        float scale)
    {
        var accent = OnlineGameArt.Accent(kind);
        drawList.AddCircleFilled(center, radius + MedallionRim * scale, ImGui.GetColorU32(rim), 40);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.Darken(accent, 0.30f)), 40);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.Lighten(accent, 0.35f) with { W = 0.55f }), 40,
            1f * scale);
        OnlineGameArt.Draw(drawList, kind, center, radius * MedallionArt, scale);
    }

    public static void ReportAnchor(string key, Rect rect)
    {
        if (!Core.Onboarding.UiAnchors.Recording)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var top = MathF.Max(rect.Min.Y, drawList.GetClipRectMin().Y);
        var bottom = MathF.Min(rect.Max.Y, drawList.GetClipRectMax().Y);
        if (bottom <= top)
        {
            return;
        }

        Core.Onboarding.UiAnchors.Report(key, new Rect(new Vector2(rect.Min.X, top), new Vector2(rect.Max.X, bottom)));
    }
}
