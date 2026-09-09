using Aetherphone.Apps.Velvet;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VelvetFitTests
{
    [Fact]
    public void SharedKinksAndTagsBecomeFitItems()
    {
        var me = Profile("me", kinks: new[] { "rigger", "praise", "brat tamer" }, tags: new[] { "slow burn", "venue" },
            lookingFor: VelvetIntent.Erp | VelvetIntent.Friends);
        var other = Profile("other", kinks: new[] { "praise", "rigger" }, tags: new[] { "venue" },
            lookingFor: VelvetIntent.Erp | VelvetIntent.Wandering);
        var items = new List<VelvetFitItem>();

        VelvetFit.Describe(me, other, items);

        Assert.Equal(VelvetFitKind.SharedKinks, items[0].Kind);
        Assert.Equal(2, items[0].Value);
        Assert.Contains(items, item => item.Kind == VelvetFitKind.SharedIntent && item.Value == VelvetIntent.Erp);
        Assert.Contains(items, item => item.Kind == VelvetFitKind.SharedTag && item.Token == "venue");
        Assert.DoesNotContain(items, item => item.Kind == VelvetFitKind.Conflict);
    }

    [Fact]
    public void LimitsAgainstKinksSurfaceAsConflictsInBothDirections()
    {
        var me = Profile("me", kinks: new[] { "watersports" }, limits: new[] { "pain" });
        var other = Profile("other", kinks: new[] { "pain" }, limits: new[] { "watersports" });
        var items = new List<VelvetFitItem>();

        VelvetFit.Describe(me, other, items);

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(VelvetFitKind.Conflict, item.Kind));
        Assert.Contains(items, item => item.Token == "watersports");
        Assert.Contains(items, item => item.Token == "pain");
        Assert.Equal(2, VelvetFit.ConflictCount(me, other));
    }

    [Fact]
    public void IrlLimitAgainstIrlIntentIsAConflict()
    {
        var me = Profile("me", lookingFor: VelvetIntent.Irl | VelvetIntent.Friends);
        var other = Profile("other", limits: new[] { "irl" });
        var items = new List<VelvetFitItem>();

        VelvetFit.Describe(me, other, items);

        Assert.Single(items);
        Assert.Equal(VelvetFitKind.Conflict, items[0].Kind);
        Assert.Equal("irl", items[0].Token);
    }

    [Fact]
    public void NoConflictsOnlyAppearsWhenThereWasSomethingToCompare()
    {
        var me = Profile("me", kinks: new[] { "praise" });
        var stranger = Profile("stranger");
        var withLimits = Profile("limits", limits: new[] { "gore" });
        var items = new List<VelvetFitItem>();

        VelvetFit.Describe(me, stranger, items);
        Assert.Empty(items);

        VelvetFit.Describe(me, withLimits, items);
        Assert.Single(items);
        Assert.Equal(VelvetFitKind.NoConflicts, items[0].Kind);
    }

    [Fact]
    public void ItemsAreCappedAndConflictsSurviveTheCap()
    {
        var me = Profile("me", kinks: new[] { "praise", "rigger", "watersports" },
            tags: new[] { "slow burn", "venue", "literate" }, limits: new[] { "gore" },
            lookingFor: VelvetIntent.Erp | VelvetIntent.Gpose);
        var other = Profile("other", kinks: new[] { "praise", "rigger", "gore" },
            tags: new[] { "slow burn", "venue", "literate" }, limits: new[] { "watersports" },
            lookingFor: VelvetIntent.Erp | VelvetIntent.Gpose);
        var items = new List<VelvetFitItem>();

        VelvetFit.Describe(me, other, items);

        Assert.Equal(VelvetFit.MaxItems, items.Count);
        Assert.Equal(2, items.FindAll(item => item.Kind == VelvetFitKind.Conflict).Count);
    }

    [Fact]
    public void ScorePrefersCompleteProfilesAndPenalisesConflicts()
    {
        var me = Profile("me", kinks: new[] { "praise" }, limits: new[] { "gore" });
        var empty = Profile("empty", avatar: null, intro: string.Empty);
        var complete = Profile("complete", kinks: new[] { "praise" }, tags: new[] { "venue" });
        var conflicting = Profile("conflicting", kinks: new[] { "praise", "gore" }, tags: new[] { "venue" });

        Assert.True(VelvetFit.Score(me, complete) > VelvetFit.Score(me, empty));
        Assert.True(VelvetFit.Score(me, complete) > VelvetFit.Score(me, conflicting));
        Assert.True(VelvetFit.Score(null, complete) > VelvetFit.Score(null, empty));
    }

    [Fact]
    public void MatchMarksSharedTokensAndClashesAgainstTheViewer()
    {
        var me = Profile("me", kinks: new[] { "praise" }, limits: new[] { "gore" }, tags: new[] { "venue" },
            lookingFor: VelvetIntent.Irl | VelvetIntent.Friends);

        Assert.Equal(VelvetTokenMatch.Shared, VelvetFit.Match(me, VelvetTokenGroup.Kinks, "Praise"));
        Assert.Equal(VelvetTokenMatch.Conflict, VelvetFit.Match(me, VelvetTokenGroup.Kinks, "gore"));
        Assert.Equal(VelvetTokenMatch.None, VelvetFit.Match(me, VelvetTokenGroup.Kinks, "rigger"));
        Assert.Equal(VelvetTokenMatch.Shared, VelvetFit.Match(me, VelvetTokenGroup.Tags, "venue"));
        Assert.Equal(VelvetTokenMatch.None, VelvetFit.Match(me, VelvetTokenGroup.Tags, "praise"));
        Assert.Equal(VelvetTokenMatch.Conflict, VelvetFit.Match(me, VelvetTokenGroup.Limits, "praise"));
        Assert.Equal(VelvetTokenMatch.Conflict, VelvetFit.Match(me, VelvetTokenGroup.Limits, "irl"));
        Assert.Equal(VelvetTokenMatch.Shared, VelvetFit.Match(me, VelvetTokenGroup.Limits, "gore"));
        Assert.Equal(VelvetTokenMatch.None, VelvetFit.Match(me, VelvetTokenGroup.None, "praise"));
        Assert.Equal(VelvetTokenMatch.None, VelvetFit.Match(null, VelvetTokenGroup.Kinks, "praise"));
    }

    [Fact]
    public void SharedLanguageIsAFitRowAndAGapIsAWarning()
    {
        var english = SpokenLanguages.FlagOf("en");
        var german = SpokenLanguages.FlagOf("de");
        var japanese = SpokenLanguages.FlagOf("ja");
        var me = Profile("me", languages: english | german);
        var bilingual = Profile("bilingual", languages: german | japanese);
        var japaneseOnly = Profile("japanese", languages: japanese);
        var unset = Profile("unset");
        var items = new List<VelvetFitItem>();

        VelvetFit.Describe(me, bilingual, items);
        var shared = Assert.Single(items);
        Assert.Equal(VelvetFitKind.SharedLanguage, shared.Kind);
        Assert.Equal(german, shared.Value);

        VelvetFit.Describe(me, japaneseOnly, items);
        Assert.Equal(VelvetFitKind.NoSharedLanguage, Assert.Single(items).Kind);

        VelvetFit.Describe(me, unset, items);
        Assert.Empty(items);

        Assert.True(VelvetFit.Score(me, bilingual) > VelvetFit.Score(me, unset));
        Assert.True(VelvetFit.Score(me, unset) > VelvetFit.Score(me, japaneseOnly));
    }

    [Fact]
    public void HeadlinePrefersAWarningOverAnyMatch()
    {
        var me = Profile("me", kinks: new[] { "praise", "rigger" }, limits: new[] { "gore" },
            lookingFor: VelvetIntent.Erp | VelvetIntent.Friends);
        var other = Profile("other", kinks: new[] { "praise", "rigger", "gore" },
            lookingFor: VelvetIntent.Erp | VelvetIntent.Friends);
        var items = new List<VelvetFitItem>();

        VelvetFit.Describe(me, other, items);

        Assert.Equal(VelvetFitKind.SharedKinks, items[0].Kind);
        Assert.Equal(VelvetFitKind.Conflict, items[VelvetFit.Headline(items)].Kind);
    }

    [Fact]
    public void HeadlineFallsBackToTheFirstMatchAndSkipsNoConflicts()
    {
        var me = Profile("me", kinks: new[] { "praise" }, limits: new[] { "gore" }, tags: new[] { "venue" });
        var matching = Profile("matching", tags: new[] { "venue" }, limits: new[] { "pain" });
        var boundariesOnly = Profile("boundaries", limits: new[] { "pain" });
        var stranger = Profile("stranger");
        var items = new List<VelvetFitItem>();

        VelvetFit.Describe(me, matching, items);
        Assert.Equal(VelvetFitKind.SharedTag, items[VelvetFit.Headline(items)].Kind);

        VelvetFit.Describe(me, boundariesOnly, items);
        Assert.Single(items);
        Assert.Equal(-1, VelvetFit.Headline(items));

        VelvetFit.Describe(me, stranger, items);
        Assert.Equal(-1, VelvetFit.Headline(items));
    }

    private static VelvetProfileDto Profile(string id, string[]? kinks = null, string[]? limits = null,
        string[]? tags = null, int lookingFor = VelvetIntent.Friends, string? avatar = "https://example/avatar.png",
        string intro = "Hello there", int languages = 0) =>
        new(id, id, id, 0, intro, string.Empty, string.Empty, tags ?? Array.Empty<string>(),
            limits ?? Array.Empty<string>(), lookingFor, 0, 0, string.Empty, string.Empty, 0, true, avatar, 0,
            Kinks: kinks, Languages: languages);
}
