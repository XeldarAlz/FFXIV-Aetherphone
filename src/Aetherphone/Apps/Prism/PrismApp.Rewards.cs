using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const string RewardTrackId = "prism.rewards.track";
    private const float RewardWashUnits = 420f;
    private const float FirstVisualShare = 0.36f;
    private const float PlaceVisualShare = 0.29f;
    private const float FirstNumeralUnits = 44f;
    private const float PlaceNumeralUnits = 34f;
    private const float HeatNumeralUnits = 26f;
    private const float PedestalStepUnits = 20f;
    private const float PedestalEdgeUnits = 2f;
    private const float PedestalPearlAlpha = 0.6f;
    private const float RewardBadgeShare = 1.7f;
    private const float RewardEmblemShare = 0.7f;
    private const float RewardEmblemOffset = 0.74f;
    private const float RewardEmblemBacking = 0.62f;
    private const float RewardCoinShare = 1.1f;
    private const float FirstGlowReach = 1.9f;
    private const float FirstRingAlpha = 0.9f;
    private const float PlaceRingAlpha = 0.55f;
    private const int FirstSparkles = 11;
    private const int PlaceSparkles = 6;
    private const int HeatSparkles = 3;
    private const float SparkleAlpha = 0.95f;
    private const float RiseDelay = 0.12f;
    private const float RiseStagger = 0.14f;
    private const float RiseUnits = 28f;
    private const float RewardMonogramScale = 0.8f;
    private const int RewardAvatarSegments = 48;
    private const int RewardDiscSegments = 64;
    private const float HeatIconUnits = 44f;
    private const float GilIconShare = 0.95f;

    private static readonly Vector4 FirstGlow = new(0.80f, 0.72f, 1f, 0.26f);
    private static readonly Vector4 PedestalPearl = new(1f, 0.97f, 0.92f, 1f);

    private readonly List<PrismPrizeRow> finalPrizes = new();
    private readonly List<PrismPrizeRow> heatPrizes = new();
    private readonly string[] rewardTrackLabels = new string[2];
    private readonly CachedText[] frameDayTexts = new CachedText[PodiumSize];
    private PrismPrizesDto? rewardsFor;
    private PrismWeekDto? rewardsWeek;
    private PrismJudgeRewardDto? rewardsJudging;
    private bool rewardsAny;
    private bool rewardTracksDiffer;
    private string rewardTrack = string.Empty;
    private CachedText monogramText;

    private PrismPrizesDto? Prizes => store.Week?.Prizes ?? ShownResults()?.Prizes;

    private bool ChineseAccount => gameData.IsChineseGameClient();

    private void OpenRewards()
    {
        rewardTrack = store.Track;
        Push(PrismScreen.Rewards);
    }

    private PrismPrizesDto? TrackPrizes(string track)
    {
        var tracks = store.Tracks;
        for (var index = 0; index < tracks.Length; index++)
        {
            if (tracks[index].Id == track && tracks[index].Prizes is { } prizes)
            {
                return prizes;
            }
        }

        return Prizes;
    }

    private void SyncRewards()
    {
        var week = store.Week;
        if (!ReferenceEquals(week, rewardsWeek))
        {
            rewardsWeek = week;
            var tracks = store.Tracks;
            rewardTracksDiffer = HasTracks && tracks.Length > 1 &&
                                 !PrismRewards.Same(tracks[0].Prizes ?? Prizes, tracks[1].Prizes ?? Prizes);
        }

        var prizes = rewardTracksDiffer ? TrackPrizes(rewardTrack) : Prizes;
        var judging = week?.JudgeReward;
        if (ReferenceEquals(prizes, rewardsFor) && ReferenceEquals(judging, rewardsJudging))
        {
            return;
        }

        rewardsFor = prizes;
        rewardsJudging = judging;
        rewardsAny = PrismRewards.Any(prizes, judging);
        PrismRewards.Fill(finalPrizes, prizes, PrismPhases.StageFinal);
        PrismRewards.Fill(heatPrizes, prizes, PrismPhases.StageHeat);
    }

    private void DrawRewards(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        SyncRewards();
        var entering = store.Week is { Phase: PrismPhases.Entering, MyEntry: null };
        if (!rewardsAny)
        {
            DrawNoRewards(drawList, content, entering, scale);
            return;
        }

        var washBottom = MathF.Min(full.Max.Y, content.Min.Y + RewardWashUnits * scale);
        PrismLight.Week(drawList, new Rect(full.Min, new Vector2(full.Max.X, washBottom)), store.Week?.Id ?? string.Empty,
            PrismArt.Page, PrismArt.Fore, 0f, 0f);
        var top = DrawBackHeader(drawList, content, Loc.T(L.Prism.Rewards), scale);
        var button = ActionRect(content, scale);
        var bottom = entering ? button.Min.Y - Metrics.Space.Md * scale : content.Max.Y - bottomChrome;
        using (BeginBody(new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, bottom))))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = content.Width;
            var left = origin.X + content.Min.X - full.Min.X;
            var cursor = origin.Y + Metrics.Space.Sm * scale;
            if (rewardTracksDiffer)
            {
                cursor = DrawRewardTracks(left, cursor, width, scale);
            }

            var podium = finalPrizes.Count > 0 ? finalPrizes : heatPrizes;
            if (podium.Count > 0)
            {
                cursor = DrawPrizePodium(list, left, cursor, width, podium, scale) + Metrics.Space.Xl * scale;
            }

            if (finalPrizes.Count > 0 && heatPrizes.Count > 0)
            {
                cursor = DrawHeatPrizes(list, left, cursor, width, scale) + Metrics.Space.Xl * scale;
            }

            cursor = DrawJudgePrize(list, left, cursor, width, scale);
            cursor = DrawGilNote(list, left, cursor, width, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        }

        if (!entering)
        {
            return;
        }

        PrismLight.Primary(drawList, button, !store.Busy, scale);
        if (DrawButton(drawList, button, EnterLabel(), ButtonStyle.Solid, scale, enabled: !store.Busy,
                id: "prism.rewards.enter"))
        {
            StartPick();
        }
    }

    private void DrawNoRewards(ImDrawListPtr drawList, Rect content, bool entering, float scale)
    {
        var top = DrawBackHeader(drawList, content, Loc.T(L.Prism.Rewards), scale);
        var area = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, content.Max.Y - bottomChrome));
        DrawEmpty(drawList, area, RewardsGlyph, string.Empty, Loc.T(L.Prism.NoRewardsHint),
            entering ? EnterLabel() : string.Empty, ButtonStyle.Solid, scale, out var enter);
        if (enter && !store.Busy)
        {
            StartPick();
        }
    }

    private float DrawRewardTracks(float left, float top, float width, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        rewardTrackLabels[0] = PrismTracks.Name(PrismTracks.Modded);
        rewardTrackLabels[1] = PrismTracks.Name(PrismTracks.Vanilla);
        var selected = rewardTrack == PrismTracks.Vanilla ? 1 : 0;
        var picked = DrawTrackSegments(RewardTrackId,
            new Rect(new Vector2(left, top), new Vector2(left + width, top + tap)), rewardTrackLabels, selected, scale);
        if (picked != selected)
        {
            rewardTrack = picked == 1 ? PrismTracks.Vanilla : PrismTracks.Modded;
        }

        return top + tap + Metrics.Space.Lg * scale;
    }

    private float DrawPrizePodium(ImDrawListPtr drawList, float left, float top, float width,
        List<PrismPrizeRow> rows, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var column = (width - gap * (PodiumSize - 1)) / PodiumSize;
        var step = PedestalStepUnits * scale;
        var chinese = ChineseAccount;
        var content = 0f;
        for (var index = 0; index < rows.Count; index++)
        {
            content = MathF.Max(content, PedestalHeight(rows[index], chinese, column, scale));
        }

        var baseline = top;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.Rank is < 1 or > PodiumSize)
            {
                continue;
            }

            var outer = VisualRadius(row.Rank, column) * (row.Frame?.Scale ?? 1f);
            baseline = MathF.Max(baseline,
                top + outer * 2f + Metrics.Space.Md * scale + content + PedestalLift(row.Rank, step));
        }

        for (var slot = 0; slot < PodiumSize; slot++)
        {
            var rank = PodiumOrder[slot];
            if (PrizeAt(rows, rank) is not { } row)
            {
                continue;
            }

            var x = left + slot * (column + gap);
            var pedestal = new Rect(new Vector2(x, baseline - content - PedestalLift(rank, step)),
                new Vector2(x + column, baseline));
            var radius = VisualRadius(rank, column);
            var outer = radius * (row.Frame?.Scale ?? 1f);
            var center = new Vector2(pedestal.Center.X, pedestal.Min.Y - Metrics.Space.Md * scale - outer);
            var rise = PrismMotion.At(screenAge, RiseDelay + RiseStagger * (PodiumSize - rank), Motion.Sheet);
            var mark = PrismMotion.Mark(drawList);
            DrawPrizeVisual(drawList, center, radius, row, rank, scale);
            DrawPedestal(drawList, pedestal, row, rank, chinese, scale);
            if (rise < PrismMotion.Settled)
            {
                PrismMotion.Shift(drawList, mark, new Vector2(0f, (1f - rise) * RiseUnits * scale), rise);
            }
        }

        return baseline;
    }

    private static float VisualRadius(int rank, float column) =>
        column * (rank == 1 ? FirstVisualShare : PlaceVisualShare);

    private static float PedestalLift(int rank, float step) => (PodiumSize - rank) * step;

    private static PrismPrizeRow? PrizeAt(List<PrismPrizeRow> rows, int rank)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Rank == rank)
            {
                return rows[index];
            }
        }

        return null;
    }

    private static float NumeralPixels(int rank, float scale) =>
        (rank == 1 ? FirstNumeralUnits : PlaceNumeralUnits) * scale;

    private float PedestalHeight(PrismPrizeRow row, bool chinese, float column, float scale)
    {
        var pad = Metrics.Space.Sm * scale;
        var height = pad + PrismType.Measure(Plain[1], PrismFace.Numerals, NumeralPixels(row.Rank, scale)).Y + pad;
        var line = Typography.LineHeight(PrismType.Headline) + Metrics.Space.Xxs * scale;
        if (row.CoinsFor(chinese) > 0)
        {
            height += line;
        }

        if (row.GilFor(chinese) > 0)
        {
            height += line;
        }

        if (row.Frame is not null && row.FrameDays > 0)
        {
            height += Typography.MeasureWrappedBlock(FrameDaysText(row.Rank, row.FrameDays), PrismType.Caption,
                column - pad * 2f).Y + Metrics.Space.Xxs * scale;
        }

        return height + pad;
    }

    private void DrawPrizeVisual(ImDrawListPtr drawList, Vector2 center, float radius, PrismPrizeRow row, int rank,
        float scale)
    {
        var glow = rank == 1 ? PrismGlow.Spectrum : PrismGlow.Pearl;
        var outer = radius * (row.Frame?.Scale ?? 1f);
        if (rank == 1)
        {
            SoftGlow.Draw(drawList, center, outer * FirstGlowReach, FirstGlow);
        }

        DrawPrizeArt(drawList, center, radius, row);
        PrismLight.CircleEdge(drawList, center, outer, glow, rank == 1 ? FirstRingAlpha : PlaceRingAlpha, scale);
        PrismSparkles.Around(drawList, center, outer, rank, rank == 1 ? FirstSparkles : PlaceSparkles, glow,
            SparkleAlpha, scale);
    }

    private void DrawPrizeArt(ImDrawListPtr drawList, Vector2 center, float radius, PrismPrizeRow row)
    {
        var light = PrismArt.Light;
        var outer = radius * (row.Frame?.Scale ?? 1f);
        var extent = new Vector2(outer, outer);
        if (row.Frame is { } frame)
        {
            DrawPrizeAvatar(drawList, center, radius, frame);
            HoverTooltip.Show(new Rect(center - extent, center + extent), frame.Name, HoverLabelSide.Above);
            if (row.Badge is not { } emblem)
            {
                return;
            }

            var size = radius * RewardEmblemShare;
            var spot = center + new Vector2(radius * RewardEmblemOffset, radius * RewardEmblemOffset);
            drawList.AddCircleFilled(spot, size * RewardEmblemBacking, ImGui.GetColorU32(PrismArt.Page),
                RewardDiscSegments);
            BadgeStrip.DrawOne(drawList, spot, emblem, images, light, size);
            var half = new Vector2(size * 0.5f, size * 0.5f);
            HoverTooltip.Show(new Rect(spot - half, spot + half), emblem.Name, HoverLabelSide.Below);
            return;
        }

        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary)), RewardDiscSegments);
        if (row.Badge is { } badge)
        {
            BadgeStrip.DrawOne(drawList, center, badge, images, light, radius * RewardBadgeShare);
            HoverTooltip.Show(new Rect(center - extent, center + extent), badge.Name, HoverLabelSide.Above);
            return;
        }

        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, center, radius * RewardCoinShare);
    }

    private void DrawPrizeAvatar(ImDrawListPtr drawList, Vector2 center, float radius, FrameStyle frame)
    {
        var user = glams.Me;
        if (user is null)
        {
            glams.EnsureMe();
        }

        var name = AuthorName(user);
        var key = HashCode.Combine(name);
        var monogram = monogramText.IsCurrent(key) ? monogramText.Value : monogramText.Store(key, Initials.Of(name));
        AvatarView.Draw(drawList, center, radius, PrismArt.Muted, monogram, RewardMonogramScale,
            images.Avatar(user?.AvatarUrl, radius * 2f), RewardAvatarSegments);
        AvatarView.DrawFrame(drawList, center, radius, images, frame);
    }

    private void DrawPedestal(ImDrawListPtr drawList, Rect pedestal, PrismPrizeRow row, int rank, bool chinese,
        float scale)
    {
        var rounding = Metrics.Radius.Card * scale;
        var level = rank == 1 ? FillLevel.Primary : FillLevel.Secondary;
        Squircle.FillVerticalGradient(drawList, pedestal.Min, pedestal.Max, rounding,
            ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, level)),
            ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Quaternary)));
        var edge = MathF.Max(1f, PedestalEdgeUnits * scale);
        if (rank == 1)
        {
            PrismLight.Strip(drawList, pedestal.Min.X + rounding, pedestal.Max.X - rounding, pedestal.Min.Y, edge,
                PrismLight.TabAlpha);
        }
        else
        {
            drawList.AddRectFilled(new Vector2(pedestal.Min.X + rounding, pedestal.Min.Y),
                new Vector2(pedestal.Max.X - rounding, pedestal.Min.Y + edge),
                ImGui.GetColorU32(PedestalPearl with { W = PedestalPearlAlpha }));
        }

        var pad = Metrics.Space.Sm * scale;
        var pixels = NumeralPixels(rank, scale);
        var digit = Plain[rank];
        var size = PrismType.Measure(digit, PrismFace.Numerals, pixels);
        PrismType.Draw(drawList, new Vector2(pedestal.Center.X - size.X * 0.5f, pedestal.Min.Y + pad), digit,
            PrismArt.Fore, PrismFace.Numerals, pixels);
        var y = pedestal.Min.Y + pad + size.Y + pad;
        var room = pedestal.Width - pad * 2f;
        var coins = row.CoinsFor(chinese);
        if (coins > 0)
        {
            y = DrawCoinLine(drawList, pedestal.Center.X, y, NumberText.Group(coins), room, scale);
        }

        var gil = row.GilFor(chinese);
        if (gil > 0)
        {
            y = DrawGilLine(drawList, pedestal.Center.X, y, NumberText.Group(gil), room, scale);
        }

        if (row.Frame is not null && row.FrameDays > 0)
        {
            Typography.DrawWrappedCentered(new Vector2(pedestal.Center.X, y), FrameDaysText(rank, row.FrameDays),
                PrismArt.Muted, PrismType.Caption, room);
        }
    }

    private static float DrawCoinLine(ImDrawListPtr drawList, float centerX, float top, string amount, float room,
        float scale)
    {
        var style = FitStyle(amount, MathF.Max(1f, room - CurrencyGlyph.Reserve(
            Typography.LineHeight(PrismType.Headline))), PrismType.Headline);
        var size = CurrencyGlyph.MeasureAmount(amount, style);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(centerX - size.X * 0.5f, top), amount, CurrencyKind.Coins,
            PrismArt.Fore, style);
        return top + Typography.LineHeight(PrismType.Headline) + Metrics.Space.Xxs * scale;
    }

    private float DrawGilLine(ImDrawListPtr drawList, float centerX, float top, string amount, float room,
        float scale)
    {
        var line = Typography.LineHeight(PrismType.Headline);
        var icon = line * GilIconShare;
        var gap = Metrics.Space.Xxs * scale;
        var style = FitStyle(amount, MathF.Max(1f, room - icon - gap), PrismType.Headline);
        var text = Typography.Measure(amount, style);
        var left = centerX - (icon + gap + text.X) * 0.5f;
        if (GearItem(GameData.GilItemId, out _, out var iconId))
        {
            var iconTop = top + (line - icon) * 0.5f;
            GameIconTile.Draw(drawList, textures, iconId, new Vector2(left, iconTop),
                new Vector2(left + icon, iconTop + icon), 0f, scale);
        }

        Typography.Draw(drawList, new Vector2(left + icon + gap, top + (line - text.Y) * 0.5f), amount,
            PrismArt.Fore, style);
        return top + line + Metrics.Space.Xxs * scale;
    }

    private string FrameDaysText(int rank, int days)
    {
        var slot = Math.Clamp(rank - 1, 0, frameDayTexts.Length - 1);
        var key = HashCode.Combine(days, Loc.Current.Code);
        return frameDayTexts[slot].IsCurrent(key)
            ? frameDayTexts[slot].Value
            : frameDayTexts[slot].Store(key, Loc.Plural(L.Prism.FrameDays, days));
    }

    private float DrawHeatPrizes(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        top = DrawSectionTitle(drawList, left, left + width, top, Loc.T(L.Prism.HeatPrizesTitle), string.Empty,
            scale) + Metrics.Space.Md * scale;
        var gap = Metrics.Space.Sm * scale;
        var cell = (width - gap * (PodiumSize - 1)) / PodiumSize;
        var chinese = ChineseAccount;
        var pad = Metrics.Space.Sm * scale;
        var numeral = HeatNumeralUnits * scale;
        var head = MathF.Max(PrismType.Measure(Plain[1], PrismFace.Numerals, numeral).Y, HeatIconUnits * scale);
        var line = Typography.LineHeight(PrismType.Headline) + Metrics.Space.Xxs * scale;
        var lines = 0;
        for (var index = 0; index < heatPrizes.Count && index < PodiumSize; index++)
        {
            var row = heatPrizes[index];
            lines = Math.Max(lines, (row.CoinsFor(chinese) > 0 ? 1 : 0) + (row.GilFor(chinese) > 0 ? 1 : 0));
        }

        var height = pad + head + pad + lines * line + pad;
        for (var index = 0; index < heatPrizes.Count && index < PodiumSize; index++)
        {
            var row = heatPrizes[index];
            var x = left + index * (cell + gap);
            var rect = new Rect(new Vector2(x, top), new Vector2(x + cell, top + height));
            Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Card * scale,
                ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary)));
            var digit = Plain[Math.Clamp(row.Rank, 0, Plain.Length - 1)];
            var size = PrismType.Measure(digit, PrismFace.Numerals, numeral);
            PrismType.Draw(drawList, new Vector2(rect.Min.X + pad, rect.Min.Y + pad + (head - size.Y) * 0.5f), digit,
                PrismArt.Fore, PrismFace.Numerals, numeral);
            DrawHeatArt(drawList, new Vector2(rect.Max.X - pad, rect.Min.Y + pad + head * 0.5f), row, scale);
            var y = rect.Min.Y + pad + head + pad;
            var room = cell - pad * 2f;
            var coins = row.CoinsFor(chinese);
            if (coins > 0)
            {
                y = DrawCoinLine(drawList, rect.Center.X, y, NumberText.Group(coins), room, scale);
            }

            var gil = row.GilFor(chinese);
            if (gil > 0)
            {
                DrawGilLine(drawList, rect.Center.X, y, NumberText.Group(gil), room, scale);
            }
        }

        return top + height;
    }

    private void DrawHeatArt(ImDrawListPtr drawList, Vector2 rightCenter, PrismPrizeRow row, float scale)
    {
        if (row.Badge is null && row.Frame is null)
        {
            return;
        }

        var half = HeatIconUnits * scale * 0.5f;
        var center = new Vector2(rightCenter.X - half, rightCenter.Y);
        DrawPrizeArt(drawList, center, half / (row.Frame?.Scale ?? 1f), row);
        PrismSparkles.Around(drawList, center, half, row.Rank + PodiumSize, HeatSparkles, PrismGlow.Pearl,
            SparkleAlpha, scale);
    }

    private float DrawJudgePrize(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var text = RewardLine(store.Week?.JudgeReward);
        if (text.Length == 0)
        {
            return top;
        }

        var pad = Metrics.Space.Md * scale;
        var line = Typography.LineHeight(PrismType.Headline);
        var glyph = line * CurrencyGlyph.GlyphFraction;
        var textLeft = left + pad + glyph + Metrics.Space.Sm * scale;
        var room = MathF.Max(1f, left + width - pad - textLeft);
        var block = Typography.MeasureWrappedBlock(text, PrismType.Body, room).Y;
        var height = MathF.Max(block, line) + pad * 2f;
        Squircle.Fill(drawList, new Vector2(left, top), new Vector2(left + width, top + height),
            Metrics.Radius.Card * scale, ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Quaternary)));
        var middle = top + height * 0.5f;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, new Vector2(left + pad + glyph * 0.5f, middle), glyph);
        Typography.DrawWrappedLeft(new Vector2(textLeft, middle - block * 0.5f), text, PrismArt.Fore, PrismType.Body,
            room);
        return top + height + Metrics.Space.Lg * scale;
    }

    private float DrawGilNote(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var chinese = ChineseAccount;
        if (!AwardsGil(chinese))
        {
            return top;
        }

        top += Typography.DrawWrappedLeft(new Vector2(left, top),
            Loc.T(chinese ? L.Prism.GilChinese : L.Prism.GilSent), PrismArt.Muted, PrismType.Caption, width);
        if (chinese || rewardsFor is not { GilNote.Length: > 0 } prizes)
        {
            return top;
        }

        return top + Metrics.Space.Xs * scale + Typography.DrawWrappedLeft(
            new Vector2(left, top + Metrics.Space.Xs * scale), prizes.GilNote, PrismArt.Muted, PrismType.Caption,
            width);
    }

    private bool AwardsGil(bool chinese)
    {
        for (var index = 0; index < finalPrizes.Count; index++)
        {
            if (chinese ? finalPrizes[index].ChineseCoins > 0 : finalPrizes[index].Gil > 0)
            {
                return true;
            }
        }

        for (var index = 0; index < heatPrizes.Count; index++)
        {
            if (chinese ? heatPrizes[index].ChineseCoins > 0 : heatPrizes[index].Gil > 0)
            {
                return true;
            }
        }

        return false;
    }
}
