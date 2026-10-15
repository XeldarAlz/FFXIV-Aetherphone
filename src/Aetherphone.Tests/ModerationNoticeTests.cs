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
    public void CosmeticNoticesArriveWithoutBlockingThePhone()
    {
        Assert.False(ModerationNoticeText.IsBlocking(Notice(kind: ModerationNoticeKinds.FrameGranted)));
        Assert.False(ModerationNoticeText.IsBlocking(Notice(kind: ModerationNoticeKinds.FrameRevoked)));
        Assert.True(ModerationNoticeText.IsCosmeticGrant(Notice(kind: ModerationNoticeKinds.FrameGranted)));
        Assert.True(ModerationNoticeText.IsCosmeticGrant(Notice(kind: ModerationNoticeKinds.BadgeRevoked)));
        Assert.False(ModerationNoticeText.IsCosmeticGrant(Notice(kind: ModerationNoticeKinds.Warning)));
    }

    [Fact]
    public void AFrameNoticeSaysItIsAFrameAndWhereToWearIt()
    {
        Assert.Equal("New avatar frame", ModerationNoticeText.Title(Notice(kind: ModerationNoticeKinds.FrameGranted)));
        Assert.Equal("Avatar frame removed", ModerationNoticeText.Title(Notice(kind: ModerationNoticeKinds.FrameRevoked)));

        var granted = ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.FrameGranted, detail: "frame-1"));
        Assert.Contains("Aether Coin", granted);
        Assert.Contains("Items", granted);
        Assert.DoesNotContain("badge", granted, StringComparison.OrdinalIgnoreCase);

        var revoked = ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.FrameRevoked, detail: "frame-1"));
        Assert.Contains("Discord", revoked);
        Assert.DoesNotContain("badge", revoked, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ANameResetSaysWhatTheNewHandleIsAndAsksForANewOne()
    {
        var notice = Notice(
            kind: ModerationNoticeKinds.NameReset,
            contentType: "profile_name",
            excerpt: "@a1b2c3d4e5",
            detail: "Account @a1b2c3d4e5, Velvet @f6g7h8i9j0",
            ruleTitle: "Hate speech or slurs",
            ruleSummary: "Discriminatory language.");

        Assert.Equal("Your name was reset", ModerationNoticeText.Title(notice));

        var body = ModerationNoticeText.Body(notice);
        Assert.Contains("@a1b2c3d4e5", body);
        Assert.Contains("Account @a1b2c3d4e5, Velvet @f6g7h8i9j0", body);
        Assert.Contains("Settings", body);
        Assert.Contains("Hate speech or slurs", body);
        // The excerpt is the new handle, not something the player wrote.
        Assert.DoesNotContain("You posted", body);

        Assert.True(ModerationNoticeText.IsBlocking(notice));
        Assert.True(ModerationNoticeText.RefreshesAccount(notice));
        Assert.False(ModerationNoticeText.IsCosmeticGrant(notice));
    }

    [Fact]
    public void TheReporterThankYouCreditsAModeratorWithoutClaimingAction()
    {
        var body = ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.ReportOutcome));
        Assert.Contains("A moderator reviewed your report", body);
        Assert.DoesNotContain("action", body);
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

    [Fact]
    public void EveryRemovalWarnsAboutTheSecondAndThirdOne()
    {
        const string ladder = "A second removal within 30 days brings a 3-day suspension";
        Assert.Contains(ladder, ModerationNoticeText.Body(Notice()));
        Assert.Contains(ladder, ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.Warning)));
        Assert.Contains(ladder, ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.ProfileMediaRemoved, contentType: "avatar", mediaCount: 1)));
        Assert.Contains(ladder, ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.ProfileTextCleared, contentType: "profile_text", detail: "bio")));
        Assert.DoesNotContain(ladder, ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.Suspended)));
        Assert.DoesNotContain(ladder, ModerationNoticeText.Body(Notice(kind: ModerationNoticeKinds.MarkedSensitive)));
    }

    [Fact]
    public void TheRemovalLadderSitsBetweenTheNoteAndTheAppealFooter()
    {
        const string ladder = "A second removal within 30 days";
        var body = ModerationNoticeText.Body(Notice(excerpt: "buy my gil now", note: "be kinder"));
        var noteAt = body.IndexOf("be kinder", StringComparison.Ordinal);
        var ladderAt = body.IndexOf(ladder, StringComparison.Ordinal);
        var footerAt = body.IndexOf("appeal", StringComparison.Ordinal);
        Assert.True(noteAt >= 0 && noteAt < ladderAt && ladderAt < footerAt);

        var sections = ModerationNoticeText.Sections(Notice(excerpt: "buy my gil now", note: "be kinder"));
        Assert.Equal(ConfirmSectionKind.Paragraph, sections[^1].Kind);
        Assert.Contains("appeal", sections[^1].Text);
        Assert.Equal(ConfirmSectionKind.Divider, sections[^2].Kind);
        Assert.Equal(ConfirmSectionKind.Paragraph, sections[^3].Kind);
        Assert.Contains(ladder, sections[^3].Text);
        Assert.Equal(ConfirmSectionKind.Divider, sections[^4].Kind);
        Assert.Equal(ConfirmSectionKind.Labeled, sections[^5].Kind);
        Assert.Equal("be kinder", sections[^5].Text);
    }

    [Fact]
    public void AWalletFreezeBlocksThePhoneAndPointsAtATicket()
    {
        var until = DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds();
        var notice = Notice(kind: ModerationNoticeKinds.WalletFrozen, note: "Exploit under review",
            detail: "Your Aether Coin wallet is frozen for 5 days.", banUntilUnix: until);

        Assert.True(ModerationNoticeText.IsBlocking(notice));
        Assert.True(ModerationNoticeText.RefreshesWallet(notice));
        Assert.Equal("Your wallet is frozen", ModerationNoticeText.Title(notice));

        var body = ModerationNoticeText.Body(notice);
        Assert.Contains("Open a ticket on the Discord", body);
        Assert.Contains(ModerationNoticeText.LiftMoment(until), body);
        Assert.Contains("Exploit under review", body);
        Assert.Contains("appeal", body);
        Assert.DoesNotContain("Spam or self-promotion", body);

        var sections = ModerationNoticeText.Sections(notice);
        Assert.Contains("Open a ticket on the Discord", sections[0].Text);
        Assert.Equal(ConfirmSectionKind.Labeled, sections[4].Kind);
        Assert.Equal("Exploit under review", sections[4].Text);
        Assert.Contains("appeal", sections[^1].Text);
    }

    [Fact]
    public void AWalletFreezeWithoutALiftMomentStillReadsCleanly()
    {
        var notice = Notice(kind: ModerationNoticeKinds.WalletFrozen);
        var body = ModerationNoticeText.Body(notice);
        Assert.Contains("Open a ticket on the Discord", body);
        Assert.DoesNotContain("on hold until", body);
        Assert.Equal(3, ModerationNoticeText.Sections(notice).Length);
    }

    [Fact]
    public void AWalletLiftArrivesWithoutBlockingThePhone()
    {
        var notice = Notice(kind: ModerationNoticeKinds.WalletUnfrozen);
        Assert.False(ModerationNoticeText.IsBlocking(notice));
        Assert.True(ModerationNoticeText.RefreshesWallet(notice));
        Assert.Equal("Your wallet is no longer frozen", ModerationNoticeText.Title(notice));
        Assert.Equal("Earning and spending work again.", ModerationNoticeText.Body(notice));
        Assert.False(ModerationNoticeText.RefreshesWallet(Notice(kind: ModerationNoticeKinds.Warning)));
    }
}
