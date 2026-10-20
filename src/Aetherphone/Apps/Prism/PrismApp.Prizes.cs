using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float PrizeNumeralUnits = 40f;
    private const float PrizeAvatarUnits = 22f;
    private const float PrizeMonogramScale = 0.8f;
    private const int PrizeAvatarSegments = 48;

    private readonly List<PrismPrizeRow> finalPrizes = new();
    private readonly List<PrismPrizeRow> heatPrizes = new();
    private PrismPrizesDto? prizesFor;
    private PrismScreen prizesReturn = PrismScreen.Cover;
    private CachedText minHeatText;
    private CachedText monogramText;

    private PrismPrizesDto? Prizes => store.Week?.Prizes ?? store.Results?.Prizes;

    private void OpenPrizes()
    {
        prizesReturn = screen;
        Go(PrismScreen.Prizes);
    }

    private void DrawPrizes(ImDrawListPtr drawList, Rect content, float scale)
    {
        var prizes = Prizes;
        if (!ReferenceEquals(prizes, prizesFor))
        {
            prizesFor = prizes;
            PrismRewards.Fill(finalPrizes, prizes, PrismPhases.StageFinal);
            PrismRewards.Fill(heatPrizes, prizes, PrismPhases.StageHeat);
        }

        var top = DrawHeader(drawList, content, string.Empty, IssueLine(store.Week?.Issue ?? 0), true, scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gutter = PrismArt.Gutter(scale);
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var cursor = DrawSectionTitle(list, left, origin.Y, width, Loc.T(L.Prism.Prizes), scale);
        var section = 0;
        var gil = false;
        if (finalPrizes.Count > 0)
        {
            cursor = DrawSectionLabel(list, left, cursor, width, section++, Loc.T(L.Prism.FinalTitle), scale);
            cursor = DrawPrizeRows(list, left, cursor, width, finalPrizes, ref gil, scale) + Metrics.Space.Xl * scale;
        }

        if (heatPrizes.Count > 0)
        {
            cursor = DrawSectionLabel(list, left, cursor, width, section++, Loc.T(L.Prism.HeatWinners), scale);
            cursor = DrawPrizeRows(list, left, cursor, width, heatPrizes, ref gil, scale);
            if (prizes is { MinHeatSize: > 0 })
            {
                PrismType.Draw(list, new Vector2(left, cursor), MinHeatLine(prizes.MinHeatSize), PrismArt.Muted,
                    PrismFace.Label, labelPixels);
                cursor += labelLine;
            }

            cursor += Metrics.Space.Xl * scale;
        }

        var reward = RewardLine(store.Week?.JudgeReward);
        if (reward.Length > 0)
        {
            cursor = DrawSectionLabel(list, left, cursor, width, section, Loc.T(L.Prism.Judging), scale);
            PrismType.Draw(list, new Vector2(left, cursor), reward, PrismArt.Fore, PrismFace.Label, labelPixels);
            cursor += labelLine + Metrics.Space.Xl * scale;
        }

        if (gil)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Prism.GilSent), PrismArt.Muted,
                TextStyles.Footnote, width);
            if (prizes is { GilNote.Length: > 0 })
            {
                cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), prizes.GilNote, PrismArt.Muted,
                    TextStyles.Footnote, width);
            }
        }

        cursor += PrismArt.BottomMargin(scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
    }

    private float DrawPrizeRows(ImDrawListPtr drawList, float left, float top, float width, List<PrismPrizeRow> rows,
        ref bool gil, float scale)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            gil |= rows[index].Gil > 0;
            top = DrawPrizeRow(drawList, left, top, width, rows[index], scale);
        }

        return top;
    }

    private float DrawPrizeRow(ImDrawListPtr drawList, float left, float top, float width, PrismPrizeRow row,
        float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var valuePixels = StatUnits * scale;
        var valueLine = PrismType.LineHeight(PrismFace.Headline, valuePixels);
        var numeralPixels = PrizeNumeralUnits * scale;
        var radius = PrizeAvatarUnits * scale;
        var reach = row.Frame is null ? 0f : radius + AvatarView.Reserve(row.Frame, radius);
        var textLeft = left + SectionIndexUnits * scale;
        var textRight = left + width - (reach > 0f ? reach * 2f + Metrics.Space.Md * scale : 0f);
        var labels = (row.Badge is null ? 0 : 1) + (row.Frame is null ? 0 : 1) + (row.FrameDays > 0 ? 1 : 0) +
                     (row.Gil > 0 ? 1 : 0);
        var textHeight = (row.Coins > 0 ? valueLine : 0f) + labels * labelLine;
        var height = MathF.Max(MathF.Max(textHeight, reach * 2f),
            PrismType.LineHeight(PrismFace.Numerals, numeralPixels));
        PrismType.Draw(drawList, new Vector2(left, top), Plain[Math.Clamp(row.Rank, 0, Plain.Length - 1)],
            PrismArt.Fore, PrismFace.Numerals, numeralPixels);
        drawList.PushClipRect(new Vector2(textLeft, top), new Vector2(textRight, top + height), true);
        var cursor = top;
        if (row.Coins > 0)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, cursor), CoinLine(row), PrismArt.Fore, PrismFace.Headline,
                valuePixels);
            cursor += valueLine;
        }

        if (row.Badge is { } badge)
        {
            BadgeStrip.DrawOne(drawList, new Vector2(textLeft + labelLine * 0.5f, cursor + labelLine * 0.5f), badge,
                images, Palette.Luminance(PrismArt.Back) >= 0.5f, labelLine);
            PrismType.Draw(drawList, new Vector2(textLeft + labelLine + Metrics.Space.Xs * scale, cursor), badge.Name,
                PrismArt.Muted, PrismFace.Label, labelPixels);
            cursor += labelLine;
        }

        if (row.Frame is { } frame)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, cursor), frame.Name, PrismArt.Muted, PrismFace.Label,
                labelPixels);
            cursor += labelLine;
        }

        if (row.FrameDays > 0)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, cursor), DaysLine(row), PrismArt.Muted, PrismFace.Label,
                labelPixels);
            cursor += labelLine;
        }

        if (row.Gil > 0)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, cursor), GilLine(row), PrismArt.Muted, PrismFace.Label,
                labelPixels);
        }

        drawList.PopClipRect();
        if (row.Frame is not null)
        {
            DrawPrizeAvatar(drawList, new Vector2(left + width - reach, top + reach), radius, row.Frame);
        }

        var rule = top + height + Metrics.Space.Md * scale;
        PrismArt.Hairline(drawList, left, left + width, rule, PrismArt.Faint, scale);
        return rule + Metrics.Space.Md * scale;
    }

    private void DrawPrizeAvatar(ImDrawListPtr drawList, Vector2 center, float radius, FrameStyle frame)
    {
        var user = store.CurrentUser;
        var name = user?.DisplayName is { Length: > 0 } display ? display : user?.Name ?? string.Empty;
        var key = HashCode.Combine(name);
        var monogram = monogramText.IsCurrent(key) ? monogramText.Value : monogramText.Store(key, Initials.Of(name));
        AvatarView.Draw(drawList, center, radius, PrismArt.Muted, monogram, PrizeMonogramScale,
            images.Avatar(user?.AvatarUrl, radius * 2f), PrizeAvatarSegments);
        AvatarView.DrawFrame(drawList, center, radius, images, frame);
    }

    private static string CoinLine(PrismPrizeRow row)
    {
        var key = HashCode.Combine(row.Coins, Loc.Current.Code);
        return row.CoinText.IsCurrent(key)
            ? row.CoinText.Value
            : row.CoinText.Store(key, Loc.T(L.Prism.CoinsWon, row.Coins.ToString("N0", Loc.Culture)));
    }

    private static string GilLine(PrismPrizeRow row)
    {
        var key = HashCode.Combine(row.Gil, Loc.Current.Code);
        return row.GilText.IsCurrent(key)
            ? row.GilText.Value
            : row.GilText.Store(key, Loc.T(L.Character.GilDetail, row.Gil.ToString("N0", Loc.Culture)));
    }

    private static string DaysLine(PrismPrizeRow row)
    {
        var key = HashCode.Combine(row.FrameDays, Loc.Current.Code);
        return row.DaysText.IsCurrent(key)
            ? row.DaysText.Value
            : row.DaysText.Store(key, Loc.Plural(L.Prism.FrameDays, row.FrameDays));
    }

    private string MinHeatLine(int size)
    {
        var key = HashCode.Combine(size, Loc.Current.Code);
        return minHeatText.IsCurrent(key) ? minHeatText.Value : minHeatText.Store(key, Loc.T(L.Prism.MinHeatSize, size));
    }
}
