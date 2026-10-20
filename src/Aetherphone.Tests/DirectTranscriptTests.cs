using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DirectTranscriptTests
{
    [Fact]
    public void NoSummariesMapToNoReactions()
    {
        Assert.Null(DirectTranscript.Reactions(null));
        Assert.Null(DirectTranscript.Reactions(Array.Empty<ReactionSummaryDto>()));
    }

    [Fact]
    public void ReactionsKeepTokenCountAndMine()
    {
        var reactions = DirectTranscript.Reactions(new[]
        {
            new ReactionSummaryDto("heart", 3, true),
            new ReactionSummaryDto("laugh", 1, false),
        });

        Assert.NotNull(reactions);
        Assert.Equal(2, reactions.Length);
        Assert.Equal("heart", reactions[0].Token);
        Assert.Equal(3, reactions[0].Count);
        Assert.True(reactions[0].Mine);
        Assert.Equal("laugh", reactions[1].Token);
        Assert.False(reactions[1].Mine);
    }

    [Fact]
    public void AnUnreadableBodyIsAnEncryptedPlaceholder()
    {
        var flags = DirectTranscript.SealFlags(new DmDecryptedBody(DmBodyState.NoKey, string.Empty, null, false));

        Assert.Equal(TranscriptFlags.Encrypted | TranscriptFlags.Placeholder, flags);
    }

    [Fact]
    public void ADecryptedBodyThatFailedFrankingIsUnverified()
    {
        var flags = DirectTranscript.SealFlags(new DmDecryptedBody(DmBodyState.Decrypted, "hi", null, false));

        Assert.Equal(TranscriptFlags.Encrypted | TranscriptFlags.Unverified, flags);
    }

    [Fact]
    public void AVerifiedOrRememberedBodyIsOnlyEncrypted()
    {
        Assert.Equal(TranscriptFlags.Encrypted,
            DirectTranscript.SealFlags(new DmDecryptedBody(DmBodyState.Decrypted, "hi", "key", true)));
        Assert.Equal(TranscriptFlags.Encrypted,
            DirectTranscript.SealFlags(new DmDecryptedBody(DmBodyState.Remembered, "hi", null, false)));
    }

    [Fact]
    public void AnInquiryReadAtZeroReadsAsUnread()
    {
        IDirectMessage unread = new AdInquiryMessageDto("message", "inquiry", "sender", "hi", 100, 0);
        IDirectMessage read = new AdInquiryMessageDto("message", "inquiry", "sender", "hi", 100, 200);

        Assert.Null(unread.ReadAtUnix);
        Assert.Equal(200, read.ReadAtUnix);
    }
}
