using Aetherphone.Core.Aethernet;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AccountSwitchTests
{
    private const ulong MainContentId = 0x1100000000000001;
    private const ulong AltContentId = 0x1100000000000002;

    [Fact]
    public void FollowModeUsesThePlayedCharacter()
    {
        var sessions = SessionsWith(MainContentId, "main-token");
        Assert.Equal(AltContentId, AccountSelection.Target(sessions, true, MainContentId, AltContentId));
    }

    [Fact]
    public void PinnedAccountWinsOverThePlayedCharacter()
    {
        var sessions = SessionsWith(MainContentId, "main-token");
        Assert.Equal(MainContentId, AccountSelection.Target(sessions, false, MainContentId, AltContentId));
    }

    [Fact]
    public void SignedOutPinFallsBackToThePlayedCharacter()
    {
        var sessions = SessionsWith(MainContentId, string.Empty);
        Assert.Equal(AltContentId, AccountSelection.Target(sessions, false, MainContentId, AltContentId));
    }

    [Fact]
    public void UnknownPinFallsBackToThePlayedCharacter()
    {
        var sessions = SessionsWith(MainContentId, "main-token");
        Assert.Equal(AltContentId, AccountSelection.Target(sessions, false, 0xDEAD, AltContentId));
    }

    [Fact]
    public void PinSurvivesTheLoggedOutTickBetweenCharacters()
    {
        var sessions = SessionsWith(MainContentId, "main-token");
        Assert.Equal(MainContentId, AccountSelection.Target(sessions, false, MainContentId, 0));
    }

    [Fact]
    public void NoPinLeavesTheLoggedOutTickSignedOut()
    {
        var sessions = SessionsWith(MainContentId, "main-token");
        Assert.Equal(0ul, AccountSelection.Target(sessions, true, 0, 0));
    }

    private static Dictionary<ulong, CharacterSession> SessionsWith(ulong contentId, string token)
    {
        return new Dictionary<ulong, CharacterSession>
        {
            [contentId] = new()
            {
                Token = token,
                AccountId = "account-1",
                Handle = "main",
                DisplayName = "Main",
                CharacterName = "Main Character",
                World = "Phoenix",
            },
        };
    }
}
