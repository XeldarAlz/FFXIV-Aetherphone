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
    private const float MedalGlyphShare = 1.1f;
    private const int RewardStockFirst = 6;
    private const float RewardDiscAlpha = 0.32f;
    private const int RewardDiscSegments = 64;
    private const float MedalAvatarShare = 0.62f;
    private const float MedalIconShare = 0.55f;
    private const float MedalDelay = 0.25f;
    private const float MedalRestScale = 0.82f;
    private const float RewardMonogramScale = 0.8f;
    private const int RewardAvatarSegments = 48;
    private const float RewardArtShare = 0.3f;

    private static readonly LocString[] RewardTitles =
    {
        L.Prism.RewardBadgeTitle, L.Prism.RewardFrameTitle, L.Prism.RewardCoinTitle, L.Prism.RewardGilTitle,
    };

    private static readonly LocString[] RewardBodies =
    {
        L.Prism.RewardBadgeBody, L.Prism.RewardFrameBody, L.Prism.RewardCoinBody, L.Prism.RewardGilBody,
    };

    private readonly List<PrismPrizeRow> rewardRows = new();
    private readonly PrismRewardKind[] rewardKinds = new PrismRewardKind[PrismRewards.KindCount];
    private readonly Pager rewardPager = new(Motion.PageSettle);
    private int rewardKindCount;
    private PrismPrizesDto? rewardsFor;
    private PrismJudgeRewardDto? rewardsJudging;
    private BadgeStyle? rewardBadge;
    private FrameStyle? rewardFrame;
    private int rewardFrameDays;
    private CachedText rewardDaysText;
    private CachedText monogramText;
    private CachedText rewardWinnerText;
    private Vector2 rewardPress;
    private bool rewardPressed;

    private PrismPrizesDto? Prizes => store.Week?.Prizes ?? ShownResults()?.Prizes;

    private bool ChineseAccount => gameData.IsChineseGameClient();

    private void OpenRewards()
    {
        rewardPager.SnapTo(0, PrismRewards.KindCount);
        Push(PrismScreen.Rewards);
    }

    private void SyncRewardArt()
    {
        var prizes = Prizes;
        var judging = store.Week?.JudgeReward;
        if (ReferenceEquals(prizes, rewardsFor) && ReferenceEquals(judging, rewardsJudging))
        {
            return;
        }

        rewardsFor = prizes;
        rewardsJudging = judging;
        rewardBadge = null;
        rewardFrame = null;
        rewardFrameDays = 0;
        rewardKindCount = PrismRewards.Offered(prizes, judging, rewardKinds);
        PrismRewards.Fill(rewardRows, prizes, PrismPhases.StageFinal);
        TakeRewardArt();
        if (rewardBadge is null || rewardFrame is null)
        {
            PrismRewards.Fill(rewardRows, prizes, PrismPhases.StageHeat);
            TakeRewardArt();
        }
    }

    private void TakeRewardArt()
    {
        for (var index = 0; index < rewardRows.Count; index++)
        {
            var row = rewardRows[index];
            rewardBadge ??= row.Badge;
            if (rewardFrame is null && row.Frame is not null)
            {
                rewardFrame = row.Frame;
                rewardFrameDays = row.FrameDays;
            }
        }
    }

    private void DrawRewards(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        SyncRewardArt();
        var entering = store.Week is { Phase: PrismPhases.Entering, MyEntry: null };
        if (rewardKindCount == 0)
        {
            DrawNoRewards(drawList, content, entering, scale);
            return;
        }

        var topInset = PhotoInset(full, content);
        var button = ActionRect(content, scale);
        var area = new Rect(new Vector2(full.Min.X, PhotoTop(full, content)),
            new Vector2(full.Max.X, content.Max.Y - bottomChrome));
        var dotsBottom = entering ? button.Min.Y - Metrics.Space.Lg * scale : area.Max.Y;
        var count = Math.Max(1, rewardKindCount);
        DrivePager(rewardPager, area, count, ref rewardPressed, ref rewardPress, scale);
        var scroll = rewardPager.Value;
        var first = Math.Clamp((int)MathF.Floor(scroll), 0, count - 1);
        var last = Math.Clamp((int)MathF.Ceiling(scroll), 0, count - 1);
        var results = ShownResults();
        var winner = CoverPlacing(results) is { ImageUrl.Length: > 0 } placing ? placing : null;
        drawList.PushClipRect(area.Min, area.Max, true);
        for (var index = first; index <= last && index < rewardKindCount; index++)
        {
            var offset = new Vector2((index - scroll) * full.Width, 0f);
            DrawRewardPage(drawList, new Rect(area.Min + offset, area.Max + offset), content.Min.X + offset.X,
                content.Width, topInset, dotsBottom, rewardKinds[index], results, winner, scale);
        }

        drawList.PopClipRect();
        var page = Math.Clamp((int)MathF.Round(scroll), 0, count - 1);
        if (rewardKindCount > 1)
        {
            DrawPagerDots(drawList, rewardPager, new Rect(area.Min, new Vector2(area.Max.X, dotsBottom)), count, page,
                PrismArt.PhotoFore, scale);
        }

        DrawPhotoBack(drawList, area, topInset, scale);
        if (entering && DrawButton(drawList, button, EnterLabel(), ButtonStyle.Solid, scale, enabled: !store.Busy,
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

    private void DrawRewardPage(ImDrawListPtr drawList, Rect page, float left, float width, float topInset,
        float dotsBottom, PrismRewardKind kind, PrismResultsDto? results, PrismPlacingDto? winner, float scale)
    {
        var winnerPhoto = kind == PrismRewardKind.Frame && winner is not null;
        if (winnerPhoto)
        {
            DrawLook(drawList, page, winner!.ImageUrl, false, native: true, focusY: ProfileFocus(page, topInset));
        }
        else if (!PrismStock.Draw(drawList, page, RewardStockFirst + (int)kind, 1f))
        {
            PrismArt.Placeholder(drawList, page, false, scale);
        }

        var line = Typography.LineHeight(PrismType.Caption);
        var gap = Metrics.Space.Xxs * scale;
        var title = Loc.T(RewardTitles[(int)kind]);
        var body = Loc.T(RewardBodies[(int)kind]);
        var extra = RewardExtra(kind);
        var gilNote = kind == PrismRewardKind.Gil && !ChineseAccount && Prizes is { GilNote.Length: > 0 } prizes
            ? prizes.GilNote
            : string.Empty;
        var block = line + gap + PrismType.HeroHeight(title, width) + Metrics.Space.Sm * scale +
                    Typography.MeasureWrappedBlock(body, PrismType.Body, width).Y + NoteHeight(extra, width, scale) +
                    NoteHeight(gilNote, width, scale);
        var y = dotsBottom - Metrics.Size.TapTarget * scale - block;
        var artTop = page.Min.Y + topInset + Metrics.Size.TapTarget * scale;
        var radius = MathF.Min(width * RewardArtShare, (y - artTop) * 0.5f - Metrics.Space.Lg * scale);
        if (radius > 0f && !winnerPhoto)
        {
            var center = new Vector2(page.Center.X, (artTop + y) * 0.5f);
            var mark = PrismMotion.Mark(drawList);
            DrawRewardArt(drawList, center, radius, kind, scale);
            var shown = PrismMotion.At(screenAge, MedalDelay, Motion.PageSettle);
            PrismMotion.Scale(drawList, mark, center, MedalRestScale + (1f - MedalRestScale) * shown, shown);
        }

        PrismArt.Scrim(drawList, page, page.Max.Y - y, scale);
        var ink = PrismArt.PhotoFore;
        var muted = PrismArt.PhotoMuted;
        var kicker = !winnerPhoto
            ? Loc.T(L.Prism.Rewards)
            : results?.WeekId == store.Week?.PreviousWeekId
                ? LastWeekCredit(winner!)
                : CoverByLine(winner!);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(kicker, width, PrismType.Caption), muted,
            PrismType.Caption);
        y += line + gap;
        y += PrismType.DrawHero(drawList, new Vector2(left, y), title, width, ink) + Metrics.Space.Sm * scale;
        y += Typography.DrawWrappedLeft(new Vector2(left, y), body, muted, PrismType.Body, width);
        y = DrawRewardNote(left, y, width, extra, ink, scale);
        DrawRewardNote(left, y, width, gilNote, ink, scale);
    }

    private static float NoteHeight(string note, float width, float scale) =>
        note.Length == 0
            ? 0f
            : Metrics.Space.Sm * scale + Typography.MeasureWrappedBlock(note, PrismType.Caption, width).Y;

    private string LastWeekCredit(PrismPlacingDto winner)
    {
        var key = HashCode.Combine(winner.Handle, winner.DisplayName, Loc.Current.Code);
        return rewardWinnerText.IsCurrent(key)
            ? rewardWinnerText.Value
            : rewardWinnerText.Store(key, Loc.T(L.Prism.LastWeekWinner, AtHandle(winner.Handle, winner.DisplayName)));
    }

    private void DrawRewardArt(ImDrawListPtr drawList, Vector2 center, float radius, PrismRewardKind kind, float scale)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, RewardDiscAlpha)), RewardDiscSegments);
        switch (kind)
        {
            case PrismRewardKind.Badge:
                if (rewardBadge is not null)
                {
                    BadgeStrip.DrawOne(drawList, center, rewardBadge, images, false, radius * MedalGlyphShare);
                    break;
                }

                var pixels = radius * MedalGlyphShare;
                var size = PrismType.Measure(Plain[1], PrismFace.Numerals, pixels);
                PrismType.Draw(drawList, center - size * 0.5f, Plain[1], PrismArt.PhotoFore, PrismFace.Numerals, pixels);
                break;
            case PrismRewardKind.Frame:
                DrawRewardAvatar(drawList, center, radius * MedalAvatarShare, scale);
                break;
            case PrismRewardKind.Coin:
                CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, center, radius * MedalGlyphShare);
                break;
            case PrismRewardKind.Gil:
                if (GearItem(GameData.GilItemId, out _, out var iconId))
                {
                    var half = new Vector2(radius * MedalIconShare, radius * MedalIconShare);
                    GameIconTile.Draw(drawList, textures, iconId, center - half, center + half, 0f, scale);
                }

                break;
        }
    }

    private void DrawRewardAvatar(ImDrawListPtr drawList, Vector2 center, float room, float scale)
    {
        var radius = MathF.Max(room / (1f + AvatarView.Reserve(rewardFrame, room) / room),
            AvatarView.MinFrameRadius * scale);
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
        if (rewardFrame is not null)
        {
            AvatarView.DrawFrame(drawList, center, radius, images, rewardFrame);
        }
    }

    private static float DrawRewardNote(float left, float top, float width, string note, Vector4 ink, float scale)
    {
        if (note.Length == 0)
        {
            return top;
        }

        return top + Metrics.Space.Sm * scale + Typography.DrawWrappedLeft(new Vector2(left, top + Metrics.Space.Sm * scale),
            note, ink, PrismType.Caption, width);
    }

    private string RewardExtra(PrismRewardKind kind)
    {
        switch (kind)
        {
            case PrismRewardKind.Frame when rewardFrameDays > 0:
                var key = HashCode.Combine(rewardFrameDays, Loc.Current.Code);
                return rewardDaysText.IsCurrent(key)
                    ? rewardDaysText.Value
                    : rewardDaysText.Store(key, Loc.Plural(L.Prism.FrameDays, rewardFrameDays));
            case PrismRewardKind.Coin:
                return RewardLine(store.Week?.JudgeReward);
            case PrismRewardKind.Gil:
                return Loc.T(ChineseAccount ? L.Prism.GilChinese : L.Prism.GilSent);
            default:
                return string.Empty;
        }
    }
}
