using System.Collections.Frozen;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Coins;

internal static class CoinQuests
{
    public const string RadioQuestId = "explore.radio";
    public const string PausedReason = "paused";
    public const string FrozenReason = "frozen";
    public const string DailyCapReason = "daily_cap";
    public const string RuleCapReason = "rule_cap";
    public const string AlreadyClaimedReason = "already_claimed";
    public const string IncompleteReason = "incomplete";
    public const string NotTodayReason = "not_today";

    private static readonly FrozenDictionary<string, LocString> Titles = new Dictionary<string, LocString>
    {
        ["play.featured"] = L.Coin.QuestPlayFeatured,
        ["play.three"] = L.Coin.QuestPlayThree,
        ["play.long"] = L.Coin.QuestPlayLong,
        ["play.match"] = L.Coin.QuestPlayMatch,
        ["social.chat"] = L.Coin.QuestSocialChat,
        ["social.comment"] = L.Coin.QuestSocialComment,
        ["social.post"] = L.Coin.QuestSocialPost,
        ["social.story"] = L.Coin.QuestSocialStory,
        ["social.call"] = L.Coin.QuestSocialCall,
        [RadioQuestId] = L.Coin.QuestExploreRadio,
        ["explore.poll"] = L.Coin.QuestExplorePoll,
        ["explore.muster"] = L.Coin.QuestExploreMuster,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, LocString> Hints = new Dictionary<string, LocString>
    {
        ["play.featured"] = L.Coin.QuestPlayFeaturedHint,
        ["play.three"] = L.Coin.QuestPlayThreeHint,
        ["play.long"] = L.Coin.QuestPlayLongHint,
        ["play.match"] = L.Coin.QuestPlayMatchHint,
        ["social.chat"] = L.Coin.QuestSocialChatHint,
        ["social.comment"] = L.Coin.QuestSocialCommentHint,
        ["social.post"] = L.Coin.QuestSocialPostHint,
        ["social.story"] = L.Coin.QuestSocialStoryHint,
        ["social.call"] = L.Coin.QuestSocialCallHint,
        [RadioQuestId] = L.Coin.QuestExploreRadioHint,
        ["explore.poll"] = L.Coin.QuestExplorePollHint,
        ["explore.muster"] = L.Coin.QuestExploreMusterHint,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool IsClaimable(CoinQuestDto quest) =>
        !quest.Claimed && quest.Target > 0 && quest.Progress >= quest.Target;

    public static bool IsComplete(CoinQuestDto quest) =>
        quest.Claimed || (quest.Target > 0 && quest.Progress >= quest.Target);

    public static float Fraction(CoinQuestDto quest)
    {
        if (IsComplete(quest))
        {
            return 1f;
        }

        if (quest.Target <= 0)
        {
            return 0f;
        }

        return Math.Clamp(quest.Progress / (float)quest.Target, 0f, 1f);
    }

    public static bool CountsMinutes(string questId) => string.Equals(questId, RadioQuestId, StringComparison.Ordinal);

    public static LocString TitleFor(string questId) =>
        Titles.TryGetValue(questId, out var title) ? title : L.Coin.RuleQuest;

    public static bool TryHintFor(string questId, out LocString hint) => Hints.TryGetValue(questId, out hint);

    public static string Title(CoinQuestDto quest) => Loc.T(TitleFor(quest.Id), Math.Max(0, quest.Target));

    public static string Hint(CoinQuestDto quest) => TryHintFor(quest.Id, out var hint) ? Loc.T(hint) : string.Empty;

    public static string ProgressText(CoinQuestDto quest)
    {
        var target = Math.Max(0, quest.Target);
        var progress = Math.Clamp(quest.Progress, 0, target);
        return Loc.T(CountsMinutes(quest.Id) ? L.Coin.QuestMinutes : L.Coin.QuestProgress, progress, target);
    }

    public static bool IsStale(string reason) =>
        string.Equals(reason, IncompleteReason, StringComparison.Ordinal)
        || string.Equals(reason, NotTodayReason, StringComparison.Ordinal);

    public static LocString ReasonFor(string reason)
    {
        if (string.Equals(reason, PausedReason, StringComparison.Ordinal))
        {
            return L.Coin.PausedTitle;
        }

        if (string.Equals(reason, FrozenReason, StringComparison.Ordinal))
        {
            return L.Coin.FrozenTitle;
        }

        if (string.Equals(reason, DailyCapReason, StringComparison.Ordinal))
        {
            return L.Coin.CapReached;
        }

        if (string.Equals(reason, RuleCapReason, StringComparison.Ordinal))
        {
            return L.Coin.QuestLimitReached;
        }

        return L.Coin.QuestUnavailable;
    }

    public static long TextKey(CoinQuestDto quest) => ((long)quest.Progress << 32) | (uint)quest.Target;

    public static CoinQuestBoardDto WithClaimed(CoinQuestBoardDto board, string questId)
    {
        var quests = board.Quests;
        var updated = new CoinQuestDto[quests.Length];
        for (var index = 0; index < quests.Length; index++)
        {
            var quest = quests[index];
            updated[index] = string.Equals(quest.Id, questId, StringComparison.Ordinal)
                ? quest with { Claimed = true }
                : quest;
        }

        return board with { Quests = updated };
    }
}
