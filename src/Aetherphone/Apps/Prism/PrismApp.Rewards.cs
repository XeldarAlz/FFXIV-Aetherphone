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
    private const float MedalUnits = 30f;
    private const float MedalGlyphShare = 1.1f;
    private const float MedalAvatarShare = 0.62f;
    private const float MedalIconShare = 0.55f;
    private const float MedalDelay = 0.25f;
    private const float ChapterDelay = 0.5f;
    private const float MedalRestScale = 0.82f;
    private const float RewardMonogramScale = 0.8f;
    private const int RewardAvatarSegments = 48;
    private const float RewardPhotoShare = 0.5f;

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
    private int rewardKindCount;
    private PrismPrizesDto? rewardsFor;
    private PrismJudgeRewardDto? rewardsJudging;
    private BadgeStyle? rewardBadge;
    private FrameStyle? rewardFrame;
    private int rewardFrameDays;
    private CachedText rewardDaysText;
    private CachedText monogramText;
    private CachedText rewardWinnerText;

    private PrismPrizesDto? Prizes => store.Week?.Prizes ?? ShownResults()?.Prizes;

    private bool ChineseAccount => gameData.IsChineseGameClient();

    private void OpenRewards() => Push(PrismScreen.Rewards);

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

    private void DrawRewards(ImDrawListPtr drawList, Rect content, float scale)
    {
        SyncRewardArt();
        var top = DrawBackHeader(drawList, content, store.Week?.Title ?? string.Empty, scale);
        var full = FullFrom(content, scale);
        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y));
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = content.Width;
        var left = origin.X + content.Min.X - full.Min.X;
        var cursor = origin.Y + Metrics.Space.Sm * scale;
        if (ShownResults() is { } results && CoverPlacing(results) is { ImageUrl.Length: > 0 } winner)
        {
            var photo = new Rect(new Vector2(origin.X, cursor), new Vector2(origin.X + full.Width,
                cursor + full.Width * RewardPhotoShare));
            DrawLook(list, photo, winner.ImageUrl, false, native: true, focusY: ProfileFocus(photo, 0f));
            DrawRewardCredit(list, photo, left, width, results, winner, scale);
            cursor = photo.Max.Y + Metrics.Space.Lg * scale;
        }

        cursor = DrawRewardsHero(list, left, cursor, width, scale);
        cursor = DrawRewardMedals(list, left, cursor, width, scale);
        for (var index = 0; index < rewardKindCount; index++)
        {
            cursor = DrawRewardChapter(list, left, cursor, width, rewardKinds[index], index, scale);
        }

        if (store.Week is { Phase: PrismPhases.Entering, MyEntry: null })
        {
            var enter = new Rect(new Vector2(left, cursor),
                new Vector2(left + width, cursor + Button.LargeHeight * scale));
            if (DrawButton(list, enter, EnterLabel(), ButtonStyle.Solid, scale, enabled: !store.Busy,
                    id: "prism.rewards.enter"))
            {
                StartEntry();
            }

            cursor = enter.Max.Y;
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private void DrawRewardCredit(ImDrawListPtr drawList, Rect photo, float left, float width,
        PrismResultsDto results, PrismPlacingDto winner, float scale)
    {
        var line = Typography.LineHeight(PrismType.Caption);
        PrismArt.Scrim(drawList, photo, line, scale);
        var credit = results.WeekId == store.Week?.PreviousWeekId ? LastWeekCredit(winner) : CoverByLine(winner);
        Typography.Draw(drawList, new Vector2(left, photo.Max.Y - Metrics.Space.Md * scale - line),
            Typography.FitText(credit, width, PrismType.Caption), PrismArt.PhotoFore, PrismType.Caption);
    }

    private string LastWeekCredit(PrismPlacingDto winner)
    {
        var key = HashCode.Combine(winner.Handle, winner.DisplayName, Loc.Current.Code);
        return rewardWinnerText.IsCurrent(key)
            ? rewardWinnerText.Value
            : rewardWinnerText.Store(key, Loc.T(L.Prism.LastWeekWinner, AtHandle(winner.Handle, winner.DisplayName)));
    }

    private float DrawRewardsHero(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var mark = PrismMotion.Mark(drawList);
        Typography.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.Rewards), PrismArt.Muted, PrismType.Caption);
        top += Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xxs * scale;
        top += PrismType.DrawHero(drawList, new Vector2(left, top), Loc.T(L.Prism.RewardsHeadline), width,
            PrismArt.Fore) + Metrics.Space.Sm * scale;
        var intro = PrismType.Sentences(Loc.T(L.Prism.RewardsIntro), PrismType.Body, width);
        top += Typography.DrawWrappedLeft(new Vector2(left, top), intro, PrismArt.Muted, PrismType.Body,
            PrismType.BalancedWidth(intro, PrismType.Body, width));
        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, 0f, Motion.PageSettle), PrismMotion.RiseUnits * scale);
        return top + Metrics.Space.Xl * scale;
    }

    private float DrawRewardMedals(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (rewardKindCount == 0)
        {
            return top;
        }

        var gap = Metrics.Space.Md * scale;
        var column = (width - gap * (PrismRewards.KindCount - 1)) / PrismRewards.KindCount;
        var radius = MathF.Min(MedalUnits * scale, column * 0.5f);
        for (var index = 0; index < rewardKindCount; index++)
        {
            var center = new Vector2(left + column * (index + 0.5f) + gap * index, top + radius);
            var mark = PrismMotion.Mark(drawList);
            DrawRewardArt(drawList, center, radius, rewardKinds[index], scale);
            var shown = PrismMotion.At(screenAge, MedalDelay + index * PrismMotion.Stagger, Motion.PageSettle);
            PrismMotion.Scale(drawList, mark, center, MedalRestScale + (1f - MedalRestScale) * shown, shown);
        }

        return top + radius * 2f + Metrics.Space.Xl * scale;
    }

    private void DrawRewardArt(ImDrawListPtr drawList, Vector2 center, float radius, PrismRewardKind kind, float scale)
    {
        PrismArt.Medal(drawList, center, radius, 0, 0f, scale);
        switch (kind)
        {
            case PrismRewardKind.Badge:
                if (rewardBadge is not null)
                {
                    BadgeStrip.DrawOne(drawList, center, rewardBadge, images, Palette.Luminance(PrismArt.Back) >= 0.5f,
                        radius * MedalGlyphShare);
                    break;
                }

                var pixels = radius * MedalGlyphShare;
                var size = PrismType.Measure(Plain[1], PrismFace.Numerals, pixels);
                PrismType.Draw(drawList, center - size * 0.5f, Plain[1], PrismArt.Fore, PrismFace.Numerals, pixels);
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
        var user = store.CurrentUser;
        var name = user?.DisplayName is { Length: > 0 } display ? display : user?.Name ?? string.Empty;
        var key = HashCode.Combine(name);
        var monogram = monogramText.IsCurrent(key) ? monogramText.Value : monogramText.Store(key, Initials.Of(name));
        AvatarView.Draw(drawList, center, radius, PrismArt.Muted, monogram, RewardMonogramScale,
            images.Avatar(user?.AvatarUrl, radius * 2f), RewardAvatarSegments);
        if (rewardFrame is not null)
        {
            AvatarView.DrawFrame(drawList, center, radius, images, rewardFrame);
        }
    }

    private float DrawRewardChapter(ImDrawListPtr drawList, float left, float top, float width, PrismRewardKind kind,
        int number,
        float scale)
    {
        var mark = PrismMotion.Mark(drawList);
        var cursor = DrawSectionTitle(drawList, left, left + width, top, Loc.T(RewardTitles[(int)kind]), string.Empty,
            scale) + Metrics.Space.Xs * scale;
        cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(RewardBodies[(int)kind]), PrismArt.Muted,
            PrismType.Body, width);
        cursor = DrawRewardNote(left, cursor, width, RewardExtra(kind), scale);
        if (kind == PrismRewardKind.Gil && !ChineseAccount && Prizes is { GilNote.Length: > 0 } prizes)
        {
            cursor = DrawRewardNote(left, cursor, width, prizes.GilNote, scale);
        }

        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, ChapterDelay + number * PrismMotion.Stagger,
            Motion.PageSettle), PrismMotion.RiseUnits * scale);
        return cursor + Metrics.Space.Xl * scale;
    }

    private static float DrawRewardNote(float left, float top, float width, string note, float scale)
    {
        if (note.Length == 0)
        {
            return top;
        }

        return top + Metrics.Space.Sm * scale + Typography.DrawWrappedLeft(new Vector2(left, top + Metrics.Space.Sm * scale),
            note, PrismArt.Fore, PrismType.Caption, width);
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
