using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal enum KnockAnswer : byte
{
    None,
    Approve,
    Deny,
}

internal static class KnockCard
{
    private const int AvatarSegments = 32;

    public static float Height(float width, float scale)
    {
        return DoorLayout.KnockHeight(scale, Typography.LineHeight(TextStyles.Headline),
            Typography.LineHeight(TextStyles.Subheadline), SideBySide(width, scale));
    }

    public static KnockAnswer Draw(ImDrawListPtr drawList, AppSkin ui, CasinoTableKnockDto knocker, Vector2 origin,
        float width, bool enabled, float scale)
    {
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var sideBySide = SideBySide(width, scale);
        var height = DoorLayout.KnockHeight(scale, nameHeight, lineHeight, sideBySide);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var avatar = DoorLayout.KnockAvatar(card, scale);
        DrawAvatar(drawList, ui, avatar, knocker.DisplayName, scale);
        var textLeft = DoorLayout.TextLeft(card, scale);
        var textWidth = MathF.Max(1f, card.Max.X - DoorLayout.Pad * scale - textLeft);
        var top = avatar.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(knocker.DisplayName, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(Loc.T(L.Tables.KnockLine), textWidth, TextStyles.Subheadline), ui.BodyInk,
            TextStyles.Subheadline);
        if (Button.Draw(drawList, DoorLayout.KnockButton(card, 0, sideBySide, scale), Loc.T(L.Casino.DoorApprove),
                ui.Ink, ButtonStyle.Prominent, enabled: enabled, id: "casino.door.approve"))
        {
            return KnockAnswer.Approve;
        }

        return Button.Draw(drawList, DoorLayout.KnockButton(card, 1, sideBySide, scale), Loc.T(L.Casino.DoorDeny),
            ui.Ink, ButtonStyle.Gray, enabled: enabled, id: "casino.door.deny")
            ? KnockAnswer.Deny
            : KnockAnswer.None;
    }

    public static void DrawAvatar(ImDrawListPtr drawList, AppSkin ui, Vector2 center, string name, float scale)
    {
        AvatarView.Draw(drawList, center, DoorLayout.Avatar * 0.5f * scale, ui.Accent, Initials.Of(name),
            TextStyles.Headline.Scale, AvatarHandle.Disabled, AvatarSegments);
    }

    private static bool SideBySide(float width, float scale)
    {
        var buttonStyle = Button.LabelStyle(DoorLayout.Touch * scale);
        return DoorLayout.KnockSideBySide(width, Typography.Measure(Loc.T(L.Casino.DoorApprove), buttonStyle).X,
            Typography.Measure(Loc.T(L.Casino.DoorDeny), buttonStyle).X, scale);
    }
}
