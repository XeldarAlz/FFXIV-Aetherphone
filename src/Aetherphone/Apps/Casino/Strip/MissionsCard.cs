using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class MissionsCard
{
    public const int MaxMissions = 3;
    public const float Pad = 16f;
    public const float RowHeight = 76f;
    public const float TileSize = 36f;
    public const float BarHeight = 6f;
    public const float RowPad = 10f;
    public const float MinimumActionWidth = 76f;
    public const int MaxSentenceLines = 3;

    private static readonly FontAwesomeIcon[] SlotIcons =
        { FontAwesomeIcon.Play, FontAwesomeIcon.LayerGroup, FontAwesomeIcon.Bolt };

    private static readonly Vector4[] SlotTints = { AccentRing.Azure, AccentRing.Violet, AccentRing.Gold };

    private readonly CasinoTextCache texts = new();
    private readonly string[] sentences = new string[MaxMissions];
    private readonly string[] sentenceIds = new string[MaxMissions];
    private readonly float[] rowHeights = new float[MaxMissions];
    private int sentenceDay = -1;
    private LanguageInfo? sentenceLanguage;
    private Vector2 claimCenter;

    public Vector2 ClaimCenter => claimCenter;

    public static int Count(CasinoMissionsDto? missions) =>
        Math.Min(MaxMissions, missions?.Missions?.Length ?? 0);

    public static float Height(int count, float headerHeight, float rowsHeight, float scale) =>
        count <= 0 ? 0f : Pad * scale + headerHeight + rowsHeight + Pad * scale * 0.5f;

    public static float RowHeightFor(int lines, float lineHeight, float footnoteHeight, float scale) =>
        MathF.Max(RowHeight * scale,
            Math.Max(1, lines) * lineHeight + Metrics.Space.Xs * scale * 2f + BarHeight * scale + footnoteHeight
            + RowPad * scale * 2f);

    public static float TextWidth(float rowWidth, float buttonWidth, float scale) =>
        MathF.Max(1f, rowWidth - Pad * scale * 2f - TileSize * scale - CoinArt.TextGap * scale - buttonWidth
                      - Metrics.Space.Sm * scale);

    public string Draw(ImDrawListPtr drawList, AppSkin ui, CasinoMissionsDto missions, Vector2 origin, float width,
        string claiming, long nowUnix, float scale, out float bottom)
    {
        var count = Count(missions);
        if (count > 0)
        {
            Refresh(missions);
        }

        var rowsHeight = 0f;
        for (var index = 0; index < count; index++)
        {
            rowHeights[index] = MeasureRow(missions.Missions![index], sentences[index], width, scale);
            rowsHeight += rowHeights[index];
        }

        var height = Height(count, Typography.LineHeight(TextStyles.Headline) + Metrics.Space.Sm * scale, rowsHeight,
            scale);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        bottom = max.Y;
        if (count == 0)
        {
            return string.Empty;
        }

        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var pad = Pad * scale;
        var headerTop = min.Y + pad;
        var resetIn = Math.Max(0, missions.ResetsAtUnix - nowUnix);
        var reset = missions.ResetsAtUnix > 0
            ? texts.Duration(L.Club.MissionsReset, (int)Math.Min(resetIn, int.MaxValue))
            : string.Empty;
        var resetWidth = reset.Length > 0 ? Typography.Measure(reset, TextStyles.Footnote).X : 0f;
        Typography.Draw(drawList, new Vector2(min.X + pad, headerTop),
            Typography.FitText(Loc.T(L.Club.MissionsTitle), width - pad * 2f - resetWidth - Metrics.Space.Sm * scale,
                TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        if (reset.Length > 0)
        {
            Typography.Draw(drawList,
                new Vector2(max.X - pad - resetWidth,
                    headerTop + (Typography.LineHeight(TextStyles.Headline) - Typography.LineHeight(TextStyles.Footnote))),
                reset, ui.BodyInk, TextStyles.Footnote);
        }

        var rowTop = headerTop + Typography.LineHeight(TextStyles.Headline) + Metrics.Space.Sm * scale;
        var tapped = string.Empty;
        for (var index = 0; index < count; index++)
        {
            var mission = missions.Missions![index];
            var row = new Rect(new Vector2(min.X, rowTop), new Vector2(max.X, rowTop + rowHeights[index]));
            rowTop = row.Max.Y;
            using (ImRaii.PushId(index))
            {
                if (DrawRow(drawList, ui, mission, sentences[index], row, index > 0, claiming, scale))
                {
                    tapped = mission.Id;
                }
            }
        }

        return tapped;
    }

    private bool DrawRow(ImDrawListPtr drawList, AppSkin ui, CasinoMissionDto mission, string sentence, Rect row,
        bool hairline, string claiming, float scale)
    {
        var pad = Pad * scale;
        var tile = TileSize * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + pad + tile + CoinArt.TextGap * scale, row.Max.X, row.Min.Y);
        }

        var slot = Math.Clamp(mission.Slot, 0, SlotIcons.Length - 1);
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, mission.Claimed ? AccentRing.Slate : SlotTints[slot],
            mission.Claimed ? FontAwesomeIcon.Check : SlotIcons[slot]);
        var ready = mission.Complete && !mission.Claimed;
        var actionLabel = ActionLabel(mission);
        var buttonHeight = Button.LargeHeight * scale;
        var buttonWidth = ActionWidth(actionLabel, scale);
        var button = new Rect(new Vector2(row.Max.X - pad - buttonWidth, row.Center.Y - buttonHeight * 0.5f),
            new Vector2(row.Max.X - pad, row.Center.Y + buttonHeight * 0.5f));
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textWidth = TextWidth(row.Width, ActionReserve(mission, actionLabel, scale), scale);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var lines = Typography.WrapText(sentence, TextStyles.Subheadline, textWidth);
        var lineCount = Math.Clamp(lines.Length, 1, MaxSentenceLines);
        var blockHeight = lineCount * lineHeight + Metrics.Space.Xs * scale + BarHeight * scale
                          + Metrics.Space.Xs * scale + footnote;
        var top = row.Center.Y - blockHeight * 0.5f;
        var ink = mission.Claimed ? ui.BodyInk : ui.TitleInk;
        for (var line = 0; line < lineCount && line < lines.Length; line++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top), lines[line], ink, TextStyles.Subheadline);
            top += lineHeight;
        }

        top += Metrics.Space.Xs * scale;
        var fraction = mission.Target <= 0 ? 1f : Math.Clamp((float)mission.Progress / mission.Target, 0f, 1f);
        CoinArt.Bar(drawList, new Vector2(textLeft, top), new Vector2(textLeft + textWidth, top + BarHeight * scale),
            fraction, Palette.WithAlpha(ui.MutedInk, 0.25f), mission.Complete ? CasinoColors.Money : SlotTints[slot]);
        top += BarHeight * scale + Metrics.Space.Xs * scale;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(texts.Numbers(L.Club.MissionProgress, mission.Progress, mission.Target), textWidth,
                TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);

        if (!ready)
        {
            var size = Typography.Measure(actionLabel, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(button.Max.X - MathF.Min(size.X, buttonWidth), row.Center.Y - size.Y * 0.5f),
                Typography.FitText(actionLabel, buttonWidth, TextStyles.FootnoteEmphasized),
                mission.Claimed ? ui.BodyInk : CasinoColors.Money, TextStyles.FootnoteEmphasized);
            return false;
        }

        var pressed = Button.Draw(drawList, button, actionLabel, ui.Ink, ButtonStyle.Prominent,
            enabled: claiming.Length == 0, overlay: true, id: "casino.mission.claim");
        if (pressed)
        {
            claimCenter = button.Center;
        }

        return pressed;
    }

    private float MeasureRow(CasinoMissionDto mission, string sentence, float width, float scale)
    {
        var textWidth = TextWidth(width, ActionReserve(mission, ActionLabel(mission), scale), scale);
        var lines = Math.Min(Typography.WrapText(sentence, TextStyles.Subheadline, textWidth).Length, MaxSentenceLines);
        return RowHeightFor(lines, Typography.LineHeight(TextStyles.Subheadline),
            Typography.LineHeight(TextStyles.Footnote), scale);
    }

    private string ActionLabel(CasinoMissionDto mission)
    {
        if (mission.Complete && !mission.Claimed)
        {
            return texts.Compact(L.Strip.BonusClaim, mission.Reward);
        }

        return mission.Claimed ? Loc.T(L.Club.MissionDone) : texts.Compact(L.Club.MissionReward, mission.Reward);
    }

    private static float ActionWidth(string label, float scale) =>
        MathF.Max(Button.WidthFor(label, ButtonSize.Large), MinimumActionWidth * scale);

    private static float ActionReserve(CasinoMissionDto mission, string label, float scale) =>
        ActionColumn(mission.Complete && !mission.Claimed, ActionWidth(label, scale),
            Typography.Measure(label, TextStyles.FootnoteEmphasized).X);

    public static float ActionColumn(bool claimable, float buttonWidth, float labelWidth) =>
        claimable ? buttonWidth : MathF.Min(buttonWidth, labelWidth);

    private void Refresh(CasinoMissionsDto missions)
    {
        var list = missions.Missions!;
        var stale = sentenceDay != missions.DayIndex || !ReferenceEquals(sentenceLanguage, Loc.Current);
        for (var index = 0; index < Count(missions); index++)
        {
            if (!stale && string.Equals(sentenceIds[index], list[index].Id, StringComparison.Ordinal))
            {
                continue;
            }

            sentenceIds[index] = list[index].Id;
            sentences[index] = MissionText.Compose(list[index]);
        }

        sentenceDay = missions.DayIndex;
        sentenceLanguage = Loc.Current;
    }
}
