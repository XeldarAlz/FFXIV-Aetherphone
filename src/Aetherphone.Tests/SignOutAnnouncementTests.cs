using Aetherphone.Core.Aethernet;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SignOutAnnouncementTests
{
    private const ulong ContentId = 0x1100000000000001;

    [Fact]
    public void RevokedSessionOnALoggedInCharacterAnnouncesOnce()
    {
        Assert.True(CharacterSessionManager.ShouldAnnounceSignOut(false, true, ContentId, false));
        Assert.False(CharacterSessionManager.ShouldAnnounceSignOut(false, true, ContentId, true));
    }

    [Fact]
    public void BansStaySilentBecauseTheBanOverlayExplainsThem()
    {
        Assert.False(CharacterSessionManager.ShouldAnnounceSignOut(true, true, ContentId, false));
    }

    [Fact]
    public void TitleScreenStaysSilent()
    {
        Assert.False(CharacterSessionManager.ShouldAnnounceSignOut(false, true, 0, false));
    }

    [Fact]
    public void SomeoneWhoNeverSignedInStaysSilent()
    {
        Assert.False(CharacterSessionManager.ShouldAnnounceSignOut(false, false, ContentId, false));
    }
}
