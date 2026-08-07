using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ModerationNoticeTests
{
    private static ModerationNoticeDto Notice(
        int kind = ModerationNoticeKinds.ContentRemoved,
        string app = "chirper",
        string contentType = "post",
        string excerpt = "",
        int mediaCount = 0,
        string ruleTitle = "Spam or self-promotion",
        string ruleSummary = "Repetitive posting.",
        string note = "",
        string detail = "",
        long? banUntilUnix = null)
    {
        return new ModerationNoticeDto("notice-1", kind, app, "chirper", contentType, "content-1", excerpt,
            mediaCount, "CHP-SPAM", ruleTitle, ruleSummary, "policy", note, detail, null, banUntilUnix,
            1_785_000_000, false);
    }

    [Fact]
    public void RemovalNamesTheThingThatWasRemoved()
    {
        Assert.Equal("Your chirp was removed", ModerationNoticeText.Title(Notice()));
        Assert.Equal("Your gram was removed", ModerationNoticeText.Title(Notice(app: "aethergram")));
        Assert.Equal("Your ad was removed", ModerationNoticeText.Title(Notice(contentType: "ad")));
        Assert.Equal("Your muster was removed", ModerationNoticeText.Title(Notice(contentType: "muster")));
        Assert.Equal("Your comment was removed", ModerationNoticeText.Title(Notice(contentType: "comment")));
        Assert.Equal("Your story was removed", ModerationNoticeText.Title(Notice(contentType: "story")));
    }

    [Fact]
    public void AMessageRemovalDoesNotCallItselfAPost()
    {
        var title = ModerationNoticeText.Title(Notice(contentType: "velvet_message"));
        Assert.Equal("A message you sent was removed", title);
        Assert.DoesNotContain("post", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheBodyQuotesTheContentBack()
    {
        var body = ModerationNoticeText.Body(Notice(excerpt: "buy my gil now"));
        Assert.Contains("buy my gil now", body);
        Assert.Contains("Spam or self-promotion", body);
        Assert.Contains("Repetitive posting.", body);
    }

    [Fact]
    public void MediaOnlyContentReportsTheAttachmentCount()
    {
        var body = ModerationNoticeText.Body(Notice(mediaCount: 2));
        Assert.Contains("2", body);
    }

    [Fact]
    public void AnUnsharedNoteNeverReachesTheBody()
    {
        Assert.DoesNotContain("internal only", ModerationNoticeText.Body(Notice()));
        Assert.Contains("be kinder", ModerationNoticeText.Body(Notice(note: "be kinder")));
    }

    [Fact]
    public void ASuspensionSaysWhenItLifts()
    {
        var until = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds();
        var body = ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.Suspended, banUntilUnix: until));
        Assert.Contains(ModerationNoticeText.LiftMoment(until), body);
        Assert.DoesNotContain("does not expire", body);
    }

    [Fact]
    public void APermanentSuspensionSaysSoInsteadOfPromisingADate()
    {
        var body = ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.Suspended));
        Assert.Contains("does not expire", body);
    }

    [Fact]
    public void ClearedProfileFieldsAreNamed()
    {
        var body = ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.ProfileTextCleared, detail: "bio, velvet_intro"));
        Assert.Contains("bio", body);
        Assert.Contains("velvet_intro", body);
    }

    [Fact]
    public void OnlyTheReporterThankYouIsNonBlocking()
    {
        Assert.False(ModerationNoticeText.IsBlocking(Notice(kind: ModerationNoticeKinds.ReportOutcome)));
        Assert.True(ModerationNoticeText.IsBlocking(Notice(kind: ModerationNoticeKinds.Warning)));
        Assert.True(ModerationNoticeText.IsBlocking(Notice(kind: ModerationNoticeKinds.Suspended)));
        Assert.True(ModerationNoticeText.IsBlocking(Notice(kind: ModerationNoticeKinds.SignedOut)));
    }

    [Fact]
    public void TheReporterThankYouSaysHandledRatherThanPunished()
    {
        var body = ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.ReportOutcome));
        Assert.Contains("has been handled", body);
        Assert.DoesNotContain("action has been taken", body);
    }

    [Fact]
    public void ModerationRowsAreKeptOutOfTheSocialActivityTabs()
    {
        Assert.True(SocialActivity.IsModerationNotice(SocialActivity.TypePostRemoved));
        Assert.True(SocialActivity.IsModerationNotice(SocialActivity.TypeWarning));
        Assert.True(SocialActivity.IsModerationNotice(SocialActivity.TypeReportUpdate));
        Assert.False(SocialActivity.IsModerationNotice(SocialActivity.TypeLike));
        Assert.False(SocialActivity.IsModerationNotice(SocialActivity.TypeComment));
        Assert.False(SocialActivity.IsModerationNotice(SocialActivity.TypeAdHidden));
    }

    [Fact]
    public void TheSafetyLauncherFiresOnceAndThenGoesQuiet()
    {
        var launcher = new SafetyLauncher();
        Assert.False(launcher.TryConsume());

        launcher.Request();
        Assert.True(launcher.TryConsume());
        Assert.False(launcher.TryConsume());
    }

    [Fact]
    public void OnlyASuspensionWithAnExpiryReadsAsTemporary()
    {
        var until = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds();
        Assert.True(BanOverlay.IsTemporary(
            new SuspensionDto("GEN-HARASSMENT", "Harassment", "Targeted bullying.", string.Empty, until, false)));

        Assert.False(BanOverlay.IsTemporary(
            new SuspensionDto("GEN-HARASSMENT", "Harassment", "Targeted bullying.", string.Empty, null, true)));

        Assert.False(BanOverlay.IsTemporary(
            new SuspensionDto(string.Empty, string.Empty, string.Empty, string.Empty, null, false)));

        Assert.False(BanOverlay.IsTemporary(null));
    }

    [Fact]
    public void QueuedAlertsAreShownOneAfterAnotherInsteadOfOverwriting()
    {
        var confirm = new ConfirmService();
        confirm.Alert("first", "one", "OK");
        confirm.Alert("second", "two", "OK");
        confirm.Alert("third", "three", "OK");

        Assert.Equal("first", confirm.Active!.Title);
        confirm.CancelActive();
        Assert.Equal("second", confirm.Active!.Title);
        confirm.Proceed();
        Assert.Equal("third", confirm.Active!.Title);
        confirm.CancelActive();
        Assert.Null(confirm.Active);
    }

    [Fact]
    public void EachQueuedAlertRunsItsOwnDismissCallback()
    {
        var confirm = new ConfirmService();
        var dismissed = new List<string>();
        confirm.Alert("first", "one", "OK", () => dismissed.Add("first"));
        confirm.Alert("second", "two", "OK", () => dismissed.Add("second"));

        confirm.CancelActive();
        confirm.CancelActive();

        Assert.Equal(new[] { "first", "second" }, dismissed);
    }
}
