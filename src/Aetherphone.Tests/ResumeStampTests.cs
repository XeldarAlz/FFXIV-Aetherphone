using Aetherphone.Core.Apps;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ResumeStampTests
{
    private const ulong CharacterA = 1001;
    private const ulong CharacterB = 2002;
    private const ulong AccountA = 1001;
    private const ulong AccountB = 2002;
    private const long ClosedAt = 50_000;

    private static readonly ResumeStamp Closed = new(ClosedAt, CharacterA, AccountA);

    [Fact]
    public void SameCharacterAndAccount_WithinTheWindow_Resumes()
    {
        Assert.True(Closed.Allows(ClosedAt + ResumeStamp.WindowMilliseconds, CharacterA, AccountA));
    }

    [Fact]
    public void AfterTheWindow_OpensFresh()
    {
        Assert.False(Closed.Allows(ClosedAt + ResumeStamp.WindowMilliseconds + 1, CharacterA, AccountA));
    }

    [Fact]
    public void AnotherCharacter_OpensFreshEvenWithinTheWindow()
    {
        Assert.False(Closed.Allows(ClosedAt + 1, CharacterB, AccountA));
        Assert.False(Closed.BelongsTo(CharacterB, AccountA));
    }

    [Fact]
    public void AnotherAccountOnTheSameCharacter_OpensFresh()
    {
        Assert.False(Closed.Allows(ClosedAt + 1, CharacterA, AccountB));
        Assert.False(Closed.BelongsTo(CharacterA, AccountB));
    }

    [Fact]
    public void RelogIntoTheSameCharacter_StillResumes()
    {
        Assert.True(Closed.BelongsTo(CharacterA, AccountA));
    }
}
