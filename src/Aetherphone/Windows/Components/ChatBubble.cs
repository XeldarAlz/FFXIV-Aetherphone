using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal readonly struct GroupBubble
{
    public readonly bool Active;
    public readonly AvatarHandle Avatar;
    public readonly string SenderName;
    public readonly Vector4 SenderColor;
    public readonly bool ShowHeader;

    public GroupBubble(AvatarHandle avatar, string senderName, Vector4 senderColor, bool showHeader)
    {
        Active = true;
        Avatar = avatar;
        SenderName = senderName;
        SenderColor = senderColor;
        ShowHeader = showHeader;
    }
}

internal static class ChatBubble
{
    public static void Draw(ChatLine line, PhoneTheme theme, float entrance = 1f, GroupBubble group = default)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var available = ImGui.GetContentRegionAvail().X;
        var padding = 10f * scale;
        var outgoing = line.Direction == MessageDirection.Outgoing;
        var grouped = group.Active && !outgoing;
        var avatarRadius = 13f * scale;
        var gutter = grouped ? avatarRadius * 2f + 8f * scale : 0f;
        var headerHeight = grouped && group.ShowHeader ? 16f * scale : 0f;
        var wrap = (available - gutter) * 0.80f - padding * 2f;
        var textSize = ImGui.CalcTextSize(line.Text, false, wrap);
        var bubbleWidth = textSize.X + padding * 2f;
        var bubbleHeight = textSize.Y + padding * 2f;
        var start = ImGui.GetCursorPos();
        var screenOrigin = ImGui.GetCursorScreenPos();
        var offsetX = outgoing ? available - bubbleWidth : gutter;
        var fillColor = outgoing ? theme.Accent : theme.GroupedCard;
        var textColor = outgoing ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
        var bubbleTop = start.Y + headerHeight;
        if (grouped && group.ShowHeader)
        {
            var senderName = group.SenderName ?? string.Empty;
            var dl = ImGui.GetWindowDrawList();
            Typography.Draw(dl, new Vector2(screenOrigin.X + gutter + padding, screenOrigin.Y), senderName,
                group.SenderColor, TextStyles.Caption1.Scale, TextStyles.Caption1.Weight);
            var avatarCenter = new Vector2(screenOrigin.X + avatarRadius,
                screenOrigin.Y + headerHeight + bubbleHeight - avatarRadius);
            AvatarView.Draw(dl, avatarCenter, avatarRadius, group.SenderColor, Initials.Of(senderName), 0.7f,
                group.Avatar, 24);
        }

        if (entrance < 1f)
        {
            DrawEntering(line.Text, scale, new Vector2(start.X, bubbleTop), offsetX, bubbleWidth, bubbleHeight, padding,
                wrap, outgoing, fillColor, textColor, entrance);
        }
        else
        {
            ImGui.SetCursorPos(new Vector2(start.X + offsetX, bubbleTop));
            var bubbleScreen = ImGui.GetCursorScreenPos();
            Squircle.Fill(ImGui.GetWindowDrawList(), bubbleScreen,
                bubbleScreen + new Vector2(bubbleWidth, bubbleHeight), 13f * scale, ImGui.GetColorU32(fillColor));
            ImGui.SetCursorPos(new Vector2(start.X + offsetX + padding, bubbleTop + padding));
            ImGui.PushTextWrapPos(start.X + offsetX + padding + wrap);
            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            {
                ImGui.TextUnformatted(line.Text);
            }

            ImGui.PopTextWrapPos();
        }

        ImGui.SetCursorPos(new Vector2(start.X, bubbleTop + bubbleHeight + 6f * scale));
    }

    private static void DrawEntering(string text, float scale, Vector2 start, float offsetX, float bubbleWidth,
        float bubbleHeight, float padding, float wrap, bool outgoing, Vector4 fillColor, Vector4 textColor,
        float entrance)
    {
        var pop = 0.78f + 0.22f * Easing.EaseOutBack(entrance);
        var alpha = MathF.Min(entrance * 1.8f, 1f);
        var rise = new Vector2(0f, (1f - Easing.EaseOutCubic(entrance)) * 10f * scale);
        ImGui.SetCursorPos(start);
        var screenStart = ImGui.GetCursorScreenPos();
        var fillMin = screenStart + new Vector2(offsetX, 0f);
        var fillMax = fillMin + new Vector2(bubbleWidth, bubbleHeight);
        var anchor = new Vector2(outgoing ? fillMax.X : fillMin.X, fillMax.Y);
        var scaledMin = anchor + (fillMin - anchor) * pop + rise;
        var scaledMax = anchor + (fillMax - anchor) * pop + rise;
        Squircle.Fill(ImGui.GetWindowDrawList(), scaledMin, scaledMax, 13f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fillColor, fillColor.W * alpha)));
        var textLocal = new Vector2(start.X + offsetX + padding, start.Y + padding);
        var anchorLocal = new Vector2(outgoing ? start.X + offsetX + bubbleWidth : start.X + offsetX,
            start.Y + bubbleHeight);
        var scaledTextLocal = anchorLocal + (textLocal - anchorLocal) * pop + rise;
        ImGui.SetWindowFontScale(pop);
        ImGui.SetCursorPos(scaledTextLocal);
        ImGui.PushTextWrapPos(scaledTextLocal.X + wrap * pop);
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(textColor, textColor.W * alpha)))
        {
            ImGui.TextUnformatted(text);
        }

        ImGui.PopTextWrapPos();
        ImGui.SetWindowFontScale(1f);
    }
}
