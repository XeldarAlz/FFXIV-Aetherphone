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

    private static readonly FontAwesomeIcon[] SlotIcons =
        { FontAwesomeIcon.Play, FontAwesomeIcon.LayerGroup, FontAwesomeIcon.Bolt };

    private static readonly Vector4[] SlotTints = { AccentRing.Azure, AccentRing.Violet, AccentRing.Gold };

    private readonly CasinoTextCache texts = new();
    private readonly string[] sentences = new string[MaxMissions];
    private readonly string[] sentenceIds = new string[MaxMissions];
    private int sentenceDay = -1;
    private LanguageInfo? sentenceLanguage;
    private Vector2 claimCenter;

    public Vector2 ClaimCenter => claimCenter;

    public static int Count(CasinoMissionsDto? missions) =>
        Math.Min(MaxMissions, missions?.Missions?.Length ?? 0);

    public static float Height(int count, float headerHeight, float scale) =>
        count <= 0 ? 0f : Pad * scale + headerHeight + count * RowHeight * scale + Pad * scale * 0.5f;

    public float Height(CasinoMissionsDto? missions, float scale) =>
        Height(Count(missions), Typography.LineHeight(TextStyles.Headline) + Metrics.Space.Sm * scale, scale);

    public string Draw(ImDrawListPtr drawList, AppSkin ui, CasinoMissionsDto missions, Vector2 origin, float width,
        string claiming, long nowUnix, float scale, out float bottom)
    {
        var count = Count(missions);
        var height = Height(missions, scale);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        bottom = max.Y;
        if (count == 0)
        {
            return string.Empty;
        }

        Refresh(missions);
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
            var row = new Rect(new Vector2(min.X, rowTop + index * RowHeight * scale),
                new Vector2(max.X, rowTop + (index + 1) * RowHeight * scale));
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
        var actionLabel = ready ? texts.Compact(L.Strip.BonusClaim, mission.Reward)
            : mission.Claimed ? Loc.T(L.Club.MissionDone) : texts.Compact(L.Club.MissionReward, mission.Reward);
        var buttonHeight = Button.LargeHeight * scale;
        var buttonWidth = MathF.Max(Button.WidthFor(actionLabel, ButtonSize.Large), 76f * scale);
        var button = new Rect(new Vector2(row.Max.X - pad - buttonWidth, row.Center.Y - buttonHeight * 0.5f),
            new Vector2(row.Max.X - pad, row.Center.Y + buttonHeight * 0.5f));
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textWidth = MathF.Max(1f, button.Min.X - Metrics.Space.Sm * scale - textLeft);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var blockHeight = lineHeight + Metrics.Space.Xs * scale + BarHeight * scale + Metrics.Space.Xs * scale + footnote;
        var top = row.Center.Y - blockHeight * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(sentence, textWidth, TextStyles.Subheadline),
            mission.Claimed ? ui.BodyInk : ui.TitleInk, TextStyles.Subheadline);
        top += lineHeight + Metrics.Space.Xs * scale;
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
            enabled: claiming.Length == 0, id: "casino.mission.claim");
        if (pressed)
        {
            claimCenter = button.Center;
        }

        return pressed;
    }

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
