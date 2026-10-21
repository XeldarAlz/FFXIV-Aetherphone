using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float RewardsHeadlineUnits = 56f;
    private const float RewardsHeadlineMinUnits = 34f;
    private const int RewardsHeadlineLines = 2;
    private const float MedalUnits = 30f;
    private const float ChapterArtUnits = 36f;
    private const float RewardTitleUnits = 24f;
    private const float RewardRuleDelay = 0.15f;
    private const float MedalDelay = 0.25f;
    private const float ChapterDelay = 0.5f;
    private const float MedalRestScale = 0.82f;
    private const float RingTurnPerSecond = 0.12f;
    private const float PrizeMonogramScale = 0.8f;
    private const int PrizeAvatarSegments = 48;
    private const uint GilItemId = 1;
    private const int RewardKinds = 4;

    private static readonly LocString[] MedalLabels =
    {
        L.Prism.Badges, L.Prism.Frames, L.Prism.RewardCoinTitle, L.Prism.RewardGilTitle,
    };

    private static readonly LocString[] RewardTitles =
    {
        L.Prism.RewardBadgeTitle, L.Prism.RewardFrameTitle, L.Prism.RewardCoinTitle, L.Prism.RewardGilTitle,
    };

    private static readonly LocString[] RewardBodies =
    {
        L.Prism.RewardBadgeBody, L.Prism.RewardFrameBody, L.Prism.RewardCoinBody, L.Prism.RewardGilBody,
    };

    private readonly List<PrismPrizeRow> rewardRows = new();
    private PrismPrizesDto? rewardsFor;
    private BadgeStyle? rewardBadge;
    private FrameStyle? rewardFrame;
    private int rewardFrameDays;
    private CachedText rewardDaysText;
    private CachedText monogramText;

    private PrismPrizesDto? Prizes => store.Week?.Prizes ?? store.Results?.Prizes;

    private void OpenRewards() => Push(PrismScreen.Rewards);

    private void SyncRewardArt()
    {
        var prizes = Prizes;
        if (ReferenceEquals(prizes, rewardsFor))
        {
            return;
        }

        rewardsFor = prizes;
        rewardBadge = null;
        rewardFrame = null;
        rewardFrameDays = 0;
        PrismRewards.Fill(rewardRows, prizes, PrismPhases.StageFinal);
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
        var top = DrawHeader(drawList, content, string.Empty, IssueLine(store.Week?.Issue ?? 0), true, scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gutter = PrismArt.Gutter(scale);
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var cursor = DrawRewardsHero(list, left, origin.Y, width, scale);
        cursor = DrawRewardMedals(list, left, cursor, width, scale);
        for (var index = 0; index < RewardKinds; index++)
        {
            cursor = DrawRewardChapter(list, left, cursor, width, index, scale);
        }

        if (store.Week is { Phase: PrismPhases.Entering, MyEntry: null })
        {
            var strip = new Rect(new Vector2(left, cursor), new Vector2(left + width, cursor + PrismArt.StripUnits * scale));
            if (PrismArt.Strip(list, strip, Loc.T(L.Prism.Submit), Arrow, true, !store.Busy))
            {
                if (HasTracks)
                {
                    StartTrackChoice(false);
                }
                else
                {
                    StartPick();
                }
            }

            cursor = strip.Max.Y;
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        PrismArt.Refraction(list, body, PrismMotion.At(screenAge, RefractionDelay, Motion.Sheet));
    }

    private float DrawRewardsHero(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var mark = PrismMotion.Mark(drawList);
        var labelPixels = PrismArt.LabelPixels();
        PrismType.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.Rewards), PrismArt.Muted, PrismFace.Label,
            labelPixels);
        top += PrismType.LineHeight(PrismFace.Label, labelPixels) + Metrics.Space.Sm * scale;
        var pixels = PrismType.Fit(Loc.T(L.Prism.RewardsHeadline), PrismFace.Headline, width,
            RewardsHeadlineUnits * scale, RewardsHeadlineMinUnits * scale, RewardsHeadlineLines, out var lines);
        top += PrismType.DrawLines(drawList, new Vector2(left, top), lines, PrismArt.Fore, PrismFace.Headline, pixels) +
               Metrics.Space.Md * scale;
        var draw = PrismMotion.At(screenAge, RewardRuleDelay, Motion.Sheet);
        PrismArt.SpectrumRule(drawList, left, left + width * draw, top, scale);
        if (draw >= PrismMotion.Settled)
        {
            PrismArt.RuleGlow(drawList, left, left + width, top, scale);
        }

        top += Metrics.Space.Lg * scale;
        var intro = PrismType.Sentences(Loc.T(L.Prism.RewardsIntro), TextStyles.Subheadline, width);
        top += Typography.DrawWrappedLeft(new Vector2(left, top), intro, PrismArt.Muted, TextStyles.Subheadline,
            PrismType.BalancedWidth(intro, TextStyles.Subheadline, width));
        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, 0f, Motion.PageSettle), PrismMotion.RiseUnits * scale);
        return top + Metrics.Space.Xxl * scale;
    }

    private float DrawRewardMedals(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var gap = Metrics.Space.Md * scale;
        var column = (width - gap * (RewardKinds - 1)) / RewardKinds;
        var radius = MathF.Min(MedalUnits * scale, column * 0.5f);
        var labelPixels = PrismArt.LabelPixels();
        var turn = (float)(ImGui.GetTime() * RingTurnPerSecond);
        for (var index = 0; index < RewardKinds; index++)
        {
            var center = new Vector2(left + column * (index + 0.5f) + gap * index, top + radius);
            var mark = PrismMotion.Mark(drawList);
            DrawRewardArt(drawList, center, radius, index, turn, scale);
            var label = PrismType.FitLabel(Loc.T(MedalLabels[index]), labelPixels, column);
            var size = PrismType.Measure(label, PrismFace.Label, labelPixels);
            PrismType.Draw(drawList, new Vector2(center.X - size.X * 0.5f, center.Y + radius + Metrics.Space.Sm * scale),
                label, PrismArt.Fore, PrismFace.Label, labelPixels);
            var shown = PrismMotion.At(screenAge, MedalDelay + index * PrismMotion.Stagger, Motion.PageSettle);
            PrismMotion.Scale(drawList, mark, center, MedalRestScale + (1f - MedalRestScale) * shown, shown);
        }

        return top + radius * 2f + Metrics.Space.Sm * scale + PrismType.LineHeight(PrismFace.Label, labelPixels) +
               Metrics.Space.Xxl * scale;
    }

    private void DrawRewardArt(ImDrawListPtr drawList, Vector2 center, float radius, int kind, float turn, float scale)
    {
        PrismArt.Medal(drawList, center, radius, kind * 2, turn, scale);
        switch (kind)
        {
            case 0:
                if (rewardBadge is not null)
                {
                    BadgeStrip.DrawOne(drawList, center, rewardBadge, images, Palette.Luminance(PrismArt.Back) >= 0.5f,
                        radius * 1.1f);
                    break;
                }

                var pixels = radius * 1.1f;
                var size = PrismType.Measure(Plain[1], PrismFace.Numerals, pixels);
                PrismType.Draw(drawList, center - size * 0.5f, Plain[1], PrismArt.Fore, PrismFace.Numerals, pixels);
                break;
            case 1:
                DrawRewardAvatar(drawList, center, radius * 0.62f);
                break;
            case 2:
                CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, center, radius * 1.1f);
                break;
            default:
                if (GearItem(GilItemId, out _, out var iconId))
                {
                    var half = new Vector2(radius * 0.55f, radius * 0.55f);
                    GameIconTile.Draw(drawList, textures, iconId, center - half, center + half, 0f, scale);
                }

                break;
        }
    }

    private void DrawRewardAvatar(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        var user = store.CurrentUser;
        var name = user?.DisplayName is { Length: > 0 } display ? display : user?.Name ?? string.Empty;
        var key = HashCode.Combine(name);
        var monogram = monogramText.IsCurrent(key) ? monogramText.Value : monogramText.Store(key, Initials.Of(name));
        AvatarView.Draw(drawList, center, radius, PrismArt.Muted, monogram, PrizeMonogramScale,
            images.Avatar(user?.AvatarUrl, radius * 2f), PrizeAvatarSegments);
        if (rewardFrame is not null)
        {
            AvatarView.DrawFrame(drawList, center, radius, images, rewardFrame);
        }
    }

    private float DrawRewardChapter(ImDrawListPtr drawList, float left, float top, float width, int kind, float scale)
    {
        var mark = PrismMotion.Mark(drawList);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var art = ChapterArtUnits * scale;
        var gap = Metrics.Space.Lg * scale;
        var textLeft = left + art * 2f + gap;
        var textWidth = left + width - textLeft;
        PrismArt.Hairline(drawList, left, left + width, top, PrismArt.Faint, scale);
        top += Metrics.Space.Lg * scale;
        DrawRewardArt(drawList, new Vector2(left + art, top + art), art, kind, 0f, scale);
        var cursor = top;
        PrismType.Draw(drawList, new Vector2(textLeft, cursor), SectionIndex[kind], PrismArt.Muted, PrismFace.Label,
            labelPixels);
        cursor += labelLine;
        var pixels = PrismType.Fit(Loc.T(RewardTitles[kind]), PrismFace.Headline, textWidth, RewardTitleUnits * scale,
            RewardTitleUnits * scale * 0.75f, 2, out var lines);
        cursor += PrismType.DrawLines(drawList, new Vector2(textLeft, cursor), lines, PrismArt.Fore, PrismFace.Headline,
            pixels) + Metrics.Space.Xs * scale;
        cursor += Typography.DrawWrappedLeft(new Vector2(textLeft, cursor), Loc.T(RewardBodies[kind]), PrismArt.Muted,
            TextStyles.Subheadline, textWidth);
        var extra = RewardExtra(kind);
        if (extra.Length > 0)
        {
            cursor += Metrics.Space.Sm * scale + Typography.DrawWrappedLeft(new Vector2(textLeft, cursor + Metrics.Space.Sm * scale),
                extra, PrismArt.Fore, TextStyles.Footnote, textWidth);
        }

        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, ChapterDelay + kind * PrismMotion.Stagger,
            Motion.PageSettle), PrismMotion.RiseUnits * scale);
        return MathF.Max(cursor, top + art * 2f) + Metrics.Space.Xl * scale;
    }

    private string RewardExtra(int kind)
    {
        switch (kind)
        {
            case 1 when rewardFrameDays > 0:
                var key = HashCode.Combine(rewardFrameDays, Loc.Current.Code);
                return rewardDaysText.IsCurrent(key)
                    ? rewardDaysText.Value
                    : rewardDaysText.Store(key, Loc.Plural(L.Prism.FrameDays, rewardFrameDays));
            case 2:
                return RewardLine(store.Week?.JudgeReward);
            case 3:
                return Prizes is { GilNote.Length: > 0 } prizes ? prizes.GilNote : Loc.T(L.Prism.GilSent);
            default:
                return string.Empty;
        }
    }
}
