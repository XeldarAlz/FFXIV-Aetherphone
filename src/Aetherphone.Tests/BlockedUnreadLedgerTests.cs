using Aetherphone.Core.Message;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlockedUnreadLedgerTests
{
    private const string Group = "group-1";

    [Fact]
    public void PushedMessagesFromABlockedSenderDoNotCount()
    {
        var ledger = new BlockedUnreadLedger();
        ledger.NoteArrival(Group, "troll-1");
        ledger.NoteArrival(Group, "troll-2");

        Assert.Equal(1, ledger.Effective(Group, 3, null));
    }

    [Fact]
    public void ABlockedLastMessageSeenInAPollDoesNotCount()
    {
        var ledger = new BlockedUnreadLedger();

        Assert.Equal(1, ledger.Effective(Group, 2, "troll-1"));
    }

    [Fact]
    public void TheSameMessageIsOnlyDiscountedOnce()
    {
        var ledger = new BlockedUnreadLedger();
        ledger.NoteArrival(Group, "troll-1");

        Assert.Equal(1, ledger.Effective(Group, 2, "troll-1"));
        Assert.Equal(1, ledger.Effective(Group, 2, "troll-1"));
    }

    [Fact]
    public void ReadingTheChatStartsOver()
    {
        var ledger = new BlockedUnreadLedger();
        ledger.NoteArrival(Group, "troll-1");
        ledger.Reset(Group);

        Assert.Equal(1, ledger.Effective(Group, 1, null));
    }

    [Fact]
    public void AZeroCountFromTheServerClearsTheThread()
    {
        var ledger = new BlockedUnreadLedger();
        ledger.NoteArrival(Group, "troll-1");

        Assert.Equal(0, ledger.Effective(Group, 0, null));
        Assert.Equal(1, ledger.Effective(Group, 1, null));
    }

    [Fact]
    public void NeverGoesBelowZero()
    {
        var ledger = new BlockedUnreadLedger();
        ledger.NoteArrival(Group, "troll-1");
        ledger.NoteArrival(Group, "troll-2");

        Assert.Equal(0, ledger.Effective(Group, 1, null));
    }
}
