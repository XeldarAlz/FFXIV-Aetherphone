using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
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
    private const float Radius = 18f;
    private const float WashAlpha = 0.18f;
    private const float WashFloorAlpha = 0.03f;
    private const float RimAlpha = 0.55f;
    private const float PulseAlpha = 0.5f;
    private const float PulseSpread = 7f;
    private const double PulsePeriod = 1600.0;

    private static readonly Vector4 KnockInk = new(1f, 0.847f, 0.467f, 1f);
    private static readonly Vector4 OnGold = new(0.141f, 0.082f, 0.012f, 1f);

    public static float Height(float width, float scale)
    {
        return DoorLayout.KnockHeight(scale, Typography.LineHeight(TextStyles.Headline),
            Typography.LineHeight(TextStyles.FootnoteEmphasized), Inline(width, scale));
    }

    public static KnockAnswer Draw(ImDrawListPtr drawList, AppSkin ui, CasinoTableKnockDto knocker, Vector2 origin,
        float width, bool enabled, float scale)
    {
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var declineLabel = Loc.T(L.HostDesk.Decline);
        var approveLabel = Loc.T(L.Casino.DoorApprove);
        var pill = DoorLayout.KnockPill * scale;
        var declineWidth = DoorPill.WidthFor(declineLabel, pill, scale);
        var approveWidth = DoorPill.WidthFor(approveLabel, pill, scale);
        var inline = DoorLayout.KnockInline(width, declineWidth, approveWidth, scale);
        var height = DoorLayout.KnockHeight(scale, nameHeight, lineHeight, inline);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        DrawFrame(drawList, ui, card, scale);
        var avatar = DoorLayout.KnockAvatar(card, nameHeight, lineHeight, inline, scale);
        DrawAvatar(drawList, ui, avatar, knocker.DisplayName, scale);
        var buttons = DoorLayout.KnockButtonRects(card, declineWidth, approveWidth, inline, scale);
        var textLeft = DoorLayout.KnockTextLeft(card, scale);
        var textRight = inline ? buttons.Decline.Min.X - DoorLayout.TextGap * scale : card.Max.X - DoorLayout.Pad * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var top = avatar.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(knocker.DisplayName, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(Loc.T(L.HostDesk.WantsToJoin), textWidth, TextStyles.FootnoteEmphasized), KnockInk,
            TextStyles.FootnoteEmphasized);
        if (DoorPill.Draw(drawList, buttons.Approve, approveLabel, ui.Ink.WithAccent(CasinoColors.Money),
                ButtonStyle.Prominent, OnGold, enabled, "casino.door.approve", scale))
        {
            return KnockAnswer.Approve;
        }

        return DoorPill.Draw(drawList, buttons.Decline, declineLabel, ui.Ink, ButtonStyle.Gray, null, enabled,
            "casino.door.deny", scale)
            ? KnockAnswer.Deny
            : KnockAnswer.None;
    }

    public static void DrawAvatar(ImDrawListPtr drawList, AppSkin ui, Vector2 center, string name, float scale)
    {
        AvatarView.Draw(drawList, center, DoorLayout.Avatar * 0.5f * scale, ui.Accent, Initials.Of(name),
            TextStyles.Headline.Scale, AvatarHandle.Disabled, AvatarSegments);
    }

    private static void DrawFrame(ImDrawListPtr drawList, AppSkin ui, in Rect card, float scale)
    {
        var radius = Radius * scale;
        var phase = Pulse.Phase(PulsePeriod);
        var spread = PulseSpread * scale * phase;
        var reach = new Vector2(spread, spread);
        Squircle.Stroke(drawList, card.Min - reach, card.Max + reach, radius + spread,
            ImGui.GetColorU32(CasinoColors.Money with { W = PulseAlpha * (1f - phase) }), 2f * scale);
        ui.Card(drawList, card.Min, card.Max, radius);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(CasinoColors.Money with { W = WashAlpha }),
            ImGui.GetColorU32(CasinoColors.Money with { W = WashFloorAlpha }));
        Squircle.Stroke(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(CasinoColors.Money with { W = RimAlpha }), 1f * scale);
    }

    private static bool Inline(float width, float scale)
    {
        var pill = DoorLayout.KnockPill * scale;
        return DoorLayout.KnockInline(width, DoorPill.WidthFor(Loc.T(L.HostDesk.Decline), pill, scale),
            DoorPill.WidthFor(Loc.T(L.Casino.DoorApprove), pill, scale), scale);
    }
}
