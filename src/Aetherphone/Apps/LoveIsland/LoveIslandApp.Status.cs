using Aetherphone.Core;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private const float TileHeight = 66f;
    private const float TileGap = 8f;
    private const float TileRadius = 14f;
    private const float TileRowGap = 12f;
    private const float ProgressGap = 12f;
    private const float ProgressHeight = 5f;
    private const float ProgressHeadRadius = 4f;
    private const string CountdownCardId = "loveisland.tonight.countdown";

    private readonly CountdownTiles countdown = new();

    private float DrawCountdownCard(ImDrawListPtr drawList, float left, float top, float width, long remaining,
        string when, out bool tapped)
    {
        tapped = false;
        var scale = UiScale.Current;
        var firstVertex = CardEntrance.Mark(drawList);
        var pad = LoveIslandKit.CardPad * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var eyebrowHeight = Typography.LineHeight(LoveIslandKit.EyebrowStyle);
        var whenHeight = when.Length > 0
            ? Metrics.Space.Sm * scale + Typography.MeasureWrappedBlock(when, LoveIslandKit.CardBodyStyle, inner).Y
            : 0f;
        var height = pad * 2f + eyebrowHeight + TileRowGap * scale + TileHeight * scale + whenHeight;
        var rest = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (!ImGui.IsRectVisible(rest.Min, rest.Max))
        {
            entrance.Settle(drawList, firstVertex, scale);
            return rest.Max.Y;
        }

        tapped = LoveIslandKit.PressableCard(drawList, ui, rest, CountdownCardId, scale, true, out _, true);
        var cursorY = top + pad;
        LoveIslandKit.Eyebrow(drawList, new Vector2(left + pad, cursorY), Texts.T(CampaignTextKeys.DoorsOpenIn),
            EclipseFx.Rose(ui.Accent, 0.35f), inner);
        cursorY += eyebrowHeight + TileRowGap * scale;
        countdown.Draw(drawList,
            new Rect(new Vector2(left + pad, cursorY), new Vector2(left + pad + inner, cursorY + TileHeight * scale)),
            remaining, nowPrecise, CountdownLook(), scale);
        cursorY += TileHeight * scale;
        if (when.Length > 0)
        {
            cursorY += Metrics.Space.Sm * scale;
            Typography.DrawWrappedLeft(new Vector2(left + pad, cursorY), when, ui.MutedInk,
                LoveIslandKit.CardBodyStyle, inner);
        }

        entrance.Settle(drawList, firstVertex, scale);
        return rest.Max.Y;
    }

    private CountdownStyle CountdownLook() => new(LoveIslandKit.Night with { W = 0.55f },
        Palette.Mix(ui.Accent, LoveIslandKit.Night, 0.82f) with { W = 0.75f },
        EclipseFx.Rose(ui.Accent, 0.4f) with { W = 0.22f }, ui.TitleInk, ui.MutedInk, TextStyles.Title1,
        TextStyles.Caption2, TileRadius, TileGap);

    private void DrawProgressBar(ImDrawListPtr drawList, float left, float top, float width, float progress,
        float scale)
    {
        var height = ProgressHeight * scale;
        var radius = height * 0.5f;
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(LoveIslandKit.White with { W = 0.10f }));
        var clamped = Math.Clamp(progress, 0f, 1f);
        var fillRight = left + MathF.Max(height, width * clamped);
        Squircle.FillHorizontalGradient(drawList, min, new Vector2(fillRight, max.Y), radius,
            ImGui.GetColorU32(Palette.Darken(ui.Accent, 0.1f)), ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.45f)));
        var head = new Vector2(fillRight - radius, top + radius);
        SoftGlow.Draw(drawList, head, ProgressHeadRadius * 4f * scale,
            EclipseFx.Rose(ui.Accent, 0.3f) with { W = 0.8f });
        drawList.AddCircleFilled(head, ProgressHeadRadius * scale, ImGui.GetColorU32(LoveIslandKit.White), 16);
    }

    private float LiveProgress(int experienceIndex)
    {
        var experience = frameCampaign!.Experiences[experienceIndex];
        var span = experience.EndsAtUnix - experience.StartsAtUnix;
        return span <= 0 ? 1f : (float)((nowPrecise - experience.StartsAtUnix) / span);
    }
}
