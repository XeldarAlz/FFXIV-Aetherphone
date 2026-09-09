using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CaughtUpDivider
{
    private const float Height = 118f;
    private const float IconSize = 26f;
    private const float IconRing = 22f;
    private const float LinkPadY = 6f;

    private static readonly TextStyle TitleStyle = new(1f, FontWeight.SemiBold);
    private static readonly TextStyle HintStyle = TextStyles.Footnote;
    private static readonly TextStyle LinkStyle = new(0.86f, FontWeight.SemiBold);

    public static bool Draw(SocialInk ink, string title, string hint, string linkLabel)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = Height * scale;
        var centerX = origin.X + width * 0.5f;
        var padX = FeedCell.PadX * scale;

        drawList.AddLine(new Vector2(origin.X + padX, origin.Y), new Vector2(origin.X + width - padX, origin.Y),
            ImGui.GetColorU32(ink.Hairline), 1f);

        var iconCenter = new Vector2(centerX, origin.Y + 14f * scale + IconRing * scale);
        drawList.AddCircleFilled(iconCenter, IconRing * scale, ImGui.GetColorU32(ink.AccentWash), 32);
        PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Check, ink.AccentLink, IconSize * scale);

        var titleTop = iconCenter.Y + IconRing * scale + 10f * scale;
        var titleHeight = Typography.LineHeight(TitleStyle);
        Typography.DrawCentered(drawList, new Vector2(centerX, titleTop + titleHeight * 0.5f),
            Typography.FitText(title, width - padX * 2f, TitleStyle), ink.TitleInk, TitleStyle);

        var hintTop = titleTop + titleHeight + 3f * scale;
        var hintHeight = Typography.LineHeight(HintStyle);
        Typography.DrawCentered(drawList, new Vector2(centerX, hintTop + hintHeight * 0.5f),
            Typography.FitText(hint, width - padX * 2f, HintStyle), ink.MutedInk, HintStyle);

        var linkTop = hintTop + hintHeight + 8f * scale;
        var linkSize = Typography.Measure(linkLabel, LinkStyle);
        var linkMin = new Vector2(centerX - linkSize.X * 0.5f - 10f * scale, linkTop - LinkPadY * scale);
        var linkMax = new Vector2(centerX + linkSize.X * 0.5f + 10f * scale, linkTop + linkSize.Y + LinkPadY * scale);
        var hovered = UiInteract.Hover(linkMin, linkMax);
        Typography.DrawCentered(drawList, new Vector2(centerX, linkTop + linkSize.Y * 0.5f), linkLabel,
            hovered ? ink.Accent : ink.AccentLink, LinkStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.Dummy(new Vector2(width, MathF.Max(height, linkMax.Y + 10f * scale - origin.Y)));
        return UiInteract.Click(linkMin, linkMax, hovered);
    }
}
