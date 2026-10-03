using Aetherphone.Core;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RadioRoomSessionTests
{
    private const string Station = "st_01";
    private const string Me = "u_me";
    private const long UnixNow = 1_759_500_000_000;

    private sealed class Harness
    {
        public readonly List<CallControl> Sent = new();
        public long Tick = 10_000;
        public long Unix = UnixNow;
        public bool Online = true;
        public readonly RadioRoomSession Session;

        public Harness()
        {
            Session = new RadioRoomSession(control =>
            {
                if (!Online)
                {
                    return false;
                }

                Sent.Add(control);
                return true;
            }, () => Me, () => Tick, () => Unix, unixMs => "12:00");
        }

        public void Receive(string json)
        {
            Session.Receive(RadioRoomWireTests.Parse(json));
        }

        public void Enter(bool isDj = false, bool isModerator = false, long? mutedUntil = null)
        {
            Session.Attach(Station);
            Receive(RadioRoomSamples.Room(Station, isDj, isModerator, mutedUntil));
        }
    }

    [Fact]
    public void AttachSendsOnceAndSnapshotMarksAttached()
    {
        var harness = new Harness();

        harness.Session.Attach(Station);
        harness.Session.Attach(Station);

        Assert.Single(harness.Sent);
        Assert.Equal(SignalType.RadioAttach, harness.Sent[0].Type);
        Assert.Equal(Station, harness.Sent[0].StationId);
        Assert.Equal(RadioRoomStatus.Attaching, harness.Session.Status);
        Assert.False(harness.Session.IsAttached);

        harness.Receive(RadioRoomSamples.Room(Station));

        Assert.True(harness.Session.IsAttached);
        Assert.Equal(2, harness.Session.MessageCount);
        Assert.Equal("Welcome in", harness.Session.Pinned);
        Assert.Equal(12, harness.Session.ListenerCount);
        Assert.True(harness.Session.IsLive);
        Assert.True(harness.Session.RequestsOpen);
        Assert.Equal(1, harness.Session.RequestCount);
        Assert.Equal(RadioRequestState.Accepted, harness.Session.RequestAt(0).State);
    }

    [Fact]
    public void SnapshotEntriesCarryPublicIdentityAndPreformattedLabels()
    {
        var harness = new Harness();
        harness.Enter();

        var dj = harness.Session.MessageAt(0);
        Assert.Equal("Lumi", dj.DisplayName);
        Assert.Equal("@lumi", dj.HandleLabel);
        Assert.Equal("https://cdn/a.png", dj.AvatarUrl);
        Assert.Equal("12:00", dj.TimeLabel);
        Assert.True(dj.IsDj);

        var fallback = harness.Session.MessageAt(1);
        Assert.Equal("bee", fallback.DisplayName);
        Assert.Equal("@bee", fallback.HandleLabel);
    }

    [Fact]
    public void SignalsForAnotherStationOrWhileDetachedAreIgnored()
    {
        var harness = new Harness();
        harness.Receive(RadioRoomSamples.Room(Station));
        Assert.Equal(0, harness.Session.MessageCount);

        harness.Enter();
        var version = harness.Session.Version;
        harness.Receive(RadioRoomSamples.Message("other", 50, "u_b", "elsewhere"));

        Assert.Equal(2, harness.Session.MessageCount);
        Assert.Equal(version, harness.Session.Version);
    }

    [Fact]
    public void MessageRingKeepsTheNewestHundredInOrder()
    {
        var harness = new Harness();
        harness.Enter();

        for (var messageId = 10; messageId < 260; messageId++)
        {
            harness.Receive(RadioRoomSamples.Message(Station, messageId, "u_b", "m"));
        }

        Assert.Equal(RadioRoomSession.MessageCapacity, harness.Session.MessageCount);
        Assert.Equal(160, harness.Session.MessageAt(0).MessageId);
        Assert.Equal(259, harness.Session.MessageAt(RadioRoomSession.MessageCapacity - 1).MessageId);
    }

    [Fact]
    public void DeleteRemovesFromTheMiddleOfAWrappedRing()
    {
        var harness = new Harness();
        harness.Enter();
        for (var messageId = 10; messageId < 130; messageId++)
        {
            harness.Receive(RadioRoomSamples.Message(Station, messageId, "u_b", "m"));
        }

        harness.Receive($"{{\"type\":\"radio.deleted\",\"stationId\":\"{Station}\",\"messageId\":100}}");

        Assert.Equal(RadioRoomSession.MessageCapacity - 1, harness.Session.MessageCount);
        Assert.Equal(30, harness.Session.MessageAt(0).MessageId);
        Assert.Equal(99, harness.Session.MessageAt(69).MessageId);
        Assert.Equal(101, harness.Session.MessageAt(70).MessageId);
        Assert.Equal(129, harness.Session.MessageAt(harness.Session.MessageCount - 1).MessageId);

        harness.Receive(RadioRoomSamples.Message(Station, 130, "u_b", "m"));
        Assert.Equal(RadioRoomSession.MessageCapacity, harness.Session.MessageCount);
        Assert.Equal(130, harness.Session.MessageAt(RadioRoomSession.MessageCapacity - 1).MessageId);
    }

    [Fact]
    public void EveryRoomChangeBumpsTheVersion()
    {
        var harness = new Harness();
        var version = harness.Session.Version;
        harness.Session.Attach(Station);
        Assert.True(harness.Session.Version > version);

        string[] signals =
        {
            RadioRoomSamples.Room(Station),
            $"{{\"type\":\"radio.presence\",\"stationId\":\"{Station}\",\"listenerCount\":3,\"isLive\":false}}",
            RadioRoomSamples.Message(Station, 9, "u_b", "hello"),
            $"{{\"type\":\"radio.deleted\",\"stationId\":\"{Station}\",\"messageId\":9}}",
            $"{{\"type\":\"radio.muted\",\"stationId\":\"{Station}\",\"userId\":\"{Me}\",\"mutedUntilUnixMs\":{UnixNow + 60_000}}}",
            $"{{\"type\":\"radio.pinned\",\"stationId\":\"{Station}\",\"text\":\"Theme night\"}}",
            $"{{\"type\":\"radio.pinned\",\"stationId\":\"{Station}\"}}",
            RadioRoomSamples.Requests(Station, false, "u_b"),
            $"{{\"type\":\"radio.refused\",\"stationId\":\"{Station}\",\"action\":\"radio.chat\",\"reason\":\"muted\"}}",
        };

        for (var index = 0; index < signals.Length; index++)
        {
            version = harness.Session.Version;
            harness.Receive(signals[index]);
            Assert.True(harness.Session.Version > version, signals[index]);
        }

        Assert.Null(harness.Session.Pinned);
        Assert.Equal(3, harness.Session.ListenerCount);
        Assert.False(harness.Session.IsLive);
        Assert.False(harness.Session.RequestsOpen);
        Assert.True(harness.Session.IsMuted());

        version = harness.Session.Version;
        harness.Session.Detach();
        Assert.True(harness.Session.Version > version);
        Assert.Equal(SignalType.RadioDetach, harness.Sent[^1].Type);
        Assert.Equal(RadioRoomStatus.Idle, harness.Session.Status);
        Assert.Equal(0, harness.Session.MessageCount);
    }

    [Fact]
    public void ChatPacingMirrorsTheServerBucket()
    {
        var harness = new Harness();
        harness.Enter();

        Assert.True(harness.Session.SendChat("one"));
        Assert.True(harness.Session.SendChat("two"));
        Assert.True(harness.Session.SendChat("three"));
        Assert.False(harness.Session.SendChat("four"));
        Assert.False(harness.Session.CanSendChat());

        harness.Tick += RadioRoomPacing.ChatRefillMilliseconds - 1;
        Assert.False(harness.Session.SendChat("early"));

        harness.Tick += 1;
        Assert.True(harness.Session.SendChat("refilled"));
        Assert.False(harness.Session.SendChat("again"));

        harness.Tick += RadioRoomPacing.ChatRefillMilliseconds * 10;
        Assert.True(harness.Session.SendChat("a"));
        Assert.True(harness.Session.SendChat("b"));
        Assert.True(harness.Session.SendChat("c"));
        Assert.False(harness.Session.SendChat("d"));

        var chats = 0;
        for (var index = 0; index < harness.Sent.Count; index++)
        {
            if (harness.Sent[index].Type == SignalType.RadioChat)
            {
                chats++;
            }
        }

        Assert.Equal(7, chats);
    }

    [Fact]
    public void ChatRejectsBlankOverlongMutedAndDetached()
    {
        var harness = new Harness();
        Assert.False(harness.Session.SendChat("not attached"));

        harness.Enter(mutedUntil: UnixNow + 60_000);
        Assert.False(harness.Session.SendChat("muted"));

        harness.Unix += 61_000;
        Assert.False(harness.Session.SendChat("   "));
        Assert.False(harness.Session.SendChat(new string('x', RadioRoomSession.MaxChatLength + 1)));
        Assert.True(harness.Session.SendChat("  trimmed  "));
        Assert.Equal("trimmed", harness.Sent[^1].Text);
    }

    [Fact]
    public void ChatPacingIsNotSpentWhenTheSocketIsDown()
    {
        var harness = new Harness();
        harness.Enter();
        harness.Online = false;

        Assert.False(harness.Session.SendChat("one"));
        Assert.False(harness.Session.SendChat("two"));
        Assert.False(harness.Session.SendChat("three"));

        harness.Online = true;
        Assert.True(harness.Session.SendChat("one"));
    }

    [Fact]
    public void ReactionsArePacedShownLocallyAndPruned()
    {
        var harness = new Harness();
        harness.Enter();

        Assert.True(harness.Session.React(2));
        Assert.False(harness.Session.React(2));
        Assert.False(harness.Session.React(RadioRoomSession.ReactionKinds));
        Assert.Equal(1, harness.Session.ReactionCount);
        Assert.True(harness.Session.ReactionAt(0).IsMine);

        harness.Receive($"{{\"type\":\"radio.reaction\",\"stationId\":\"{Station}\",\"userId\":\"{Me}\",\"reaction\":2}}");
        Assert.Equal(1, harness.Session.ReactionCount);

        harness.Receive($"{{\"type\":\"radio.reaction\",\"stationId\":\"{Station}\",\"userId\":\"u_b\",\"reaction\":4}}");
        Assert.Equal(2, harness.Session.ReactionCount);
        Assert.Equal(4, harness.Session.ReactionAt(1).Reaction);

        harness.Tick += RadioRoomPacing.ReactionMilliseconds;
        Assert.True(harness.Session.React(1));

        harness.Tick += RadioRoomSession.ReactionLifetimeMilliseconds;
        harness.Session.Tick();
        Assert.Equal(0, harness.Session.ReactionCount);
    }

    [Fact]
    public void RefusalsSurfaceOnceAsTransientEnums()
    {
        var harness = new Harness();
        harness.Enter();
        Assert.False(harness.Session.TryTakeRefusal(out _));

        harness.Receive($"{{\"type\":\"radio.refused\",\"stationId\":\"{Station}\",\"action\":\"radio.request\",\"reason\":\"requestsClosed\"}}");

        Assert.True(harness.Session.HasRefusal);
        Assert.True(harness.Session.TryTakeRefusal(out var refusal));
        Assert.Equal(new RadioRefusal(RadioRoomAction.Request, RadioRefusalKind.RequestsClosed), refusal);
        Assert.False(harness.Session.TryTakeRefusal(out _));
    }

    [Fact]
    public void AttachRefusalMarksUnavailableAndCooldownRetries()
    {
        var harness = new Harness();
        harness.Session.Attach(Station);

        harness.Receive($"{{\"type\":\"radio.refused\",\"stationId\":\"{Station}\",\"action\":\"radio.attach\",\"reason\":\"cooldown\"}}");
        Assert.False(harness.Session.HasRefusal);
        Assert.Equal(RadioRoomStatus.Attaching, harness.Session.Status);

        harness.Tick += RadioRoomPacing.AttachMilliseconds;
        harness.Session.Tick();
        Assert.Equal(2, harness.Sent.Count);
        Assert.Equal(SignalType.RadioAttach, harness.Sent[1].Type);

        harness.Receive($"{{\"type\":\"radio.refused\",\"stationId\":\"{Station}\",\"action\":\"radio.attach\",\"reason\":\"unavailable\"}}");
        Assert.Equal(RadioRoomStatus.Unavailable, harness.Session.Status);
        Assert.True(harness.Session.TryTakeRefusal(out var refusal));
        Assert.Equal(RadioRefusalKind.Unavailable, refusal.Kind);
    }

    [Fact]
    public void ALateSnapshotAfterAQuickRevisitHealsWhenTheServerSaysNotInRoom()
    {
        var harness = new Harness();
        harness.Session.Attach(Station);
        harness.Session.Detach();
        harness.Session.Attach(Station);
        Assert.Equal(2, harness.Sent.Count);
        Assert.Equal(SignalType.RadioDetach, harness.Sent[1].Type);

        harness.Receive(RadioRoomSamples.Room(Station));
        Assert.True(harness.Session.IsAttached);
        harness.Tick += RadioRoomPacing.AttachMilliseconds;
        harness.Session.Tick();
        Assert.Equal(2, harness.Sent.Count);

        harness.Receive($"{{\"type\":\"radio.refused\",\"stationId\":\"{Station}\",\"action\":\"radio.chat\",\"reason\":\"notInRoom\"}}");

        Assert.Equal(RadioRoomStatus.Attaching, harness.Session.Status);
        Assert.Equal(3, harness.Sent.Count);
        Assert.Equal(SignalType.RadioAttach, harness.Sent[2].Type);
        Assert.Equal(Station, harness.Sent[2].StationId);
    }

    [Fact]
    public void SwitchingStationsQuicklyDefersTheSecondAttach()
    {
        var harness = new Harness();
        harness.Session.Attach(Station);
        harness.Session.Attach("st_02");

        Assert.Single(harness.Sent);
        harness.Tick += RadioRoomPacing.AttachMilliseconds;
        harness.Session.Tick();

        Assert.Equal(2, harness.Sent.Count);
        Assert.Equal("st_02", harness.Sent[1].StationId);
    }

    [Fact]
    public void ReconnectReattachesWhileStillOnTheStation()
    {
        var harness = new Harness();
        harness.Enter();

        harness.Session.OnConnectionChanged(false);
        Assert.Equal(RadioRoomStatus.Attaching, harness.Session.Status);
        Assert.Equal(2, harness.Session.MessageCount);

        harness.Session.OnConnectionChanged(true);
        Assert.Equal(2, harness.Sent.Count);
        Assert.Equal(SignalType.RadioAttach, harness.Sent[1].Type);

        harness.Receive(RadioRoomSamples.Room(Station));
        Assert.True(harness.Session.IsAttached);

        harness.Session.Detach();
        harness.Session.OnConnectionChanged(true);
        Assert.Equal(SignalType.RadioDetach, harness.Sent[^1].Type);
    }

    [Fact]
    public void RouterDrainsSocketSignalsOnTheFrameworkPump()
    {
        var bus = new RealtimeSignalBus();
        var harness = new Harness();
        using var router = new RadioRoomRouter(bus, harness.Session, null);
        harness.Session.Attach(Station);

        bus.PublishRadio(RadioRoomWireTests.Parse(RadioRoomSamples.Room(Station)));
        Assert.False(harness.Session.IsAttached);

        router.Drain();
        Assert.True(harness.Session.IsAttached);

        bus.SetActive(true);
        router.Drain();
        Assert.Equal(SignalType.RadioAttach, harness.Sent[^1].Type);
    }

    [Fact]
    public void ModerationGatesFollowTheRoomRole()
    {
        var listener = new Harness();
        listener.Enter();
        Assert.False(listener.Session.Pin("notice"));
        Assert.False(listener.Session.Mute("u_b"));
        Assert.False(listener.Session.Delete(1));
        Assert.False(listener.Session.AcceptRequest(4));
        Assert.False(listener.Session.SetRequestsOpen(false));

        var dj = new Harness();
        dj.Enter(isDj: true);
        Assert.True(dj.Session.Pin("notice"));
        Assert.True(dj.Session.Unpin());
        Assert.True(dj.Session.Mute("u_b", 99_999));
        Assert.Equal(RadioRoomSession.MaxMuteMinutes, dj.Sent[^1].Minutes);
        Assert.False(dj.Session.Mute(Me));
        Assert.True(dj.Session.Unmute("u_b"));
        Assert.True(dj.Session.Delete(2));
        Assert.True(dj.Session.AcceptRequest(4));
        Assert.True(dj.Session.MarkPlayed(4));
        Assert.False(dj.Session.MarkPlayed(404));
        Assert.True(dj.Session.SetRequestsOpen(false));
        Assert.Equal(false, dj.Sent[^1].Open);

        dj.Receive($"{{\"type\":\"radio.muted\",\"stationId\":\"{Station}\",\"userId\":\"u_b\",\"mutedUntilUnixMs\":{UnixNow + 60_000}}}");
        Assert.Equal(UnixNow + 60_000, dj.Session.MutedUntilFor("u_b"));
        dj.Receive($"{{\"type\":\"radio.muted\",\"stationId\":\"{Station}\",\"userId\":\"u_b\",\"mutedUntilUnixMs\":0}}");
        Assert.Equal(0, dj.Session.MutedUntilFor("u_b"));
    }

    [Fact]
    public void OwnRequestCanBeWithdrawnAndBlocksASecond()
    {
        var harness = new Harness();
        harness.Enter();
        harness.Receive(RadioRoomSamples.Requests(Station, true, Me));

        Assert.True(harness.Session.HasOwnRequest);
        Assert.True(harness.Session.RequestAt(0).IsMine);
        Assert.False(harness.Session.Request("another"));
        Assert.True(harness.Session.SkipRequest(5));
        Assert.False(harness.Session.AcceptRequest(5));

        harness.Receive($"{{\"type\":\"radio.requests\",\"stationId\":\"{Station}\",\"requests\":[],\"requestsOpen\":true}}");
        Assert.False(harness.Session.HasOwnRequest);
        Assert.True(harness.Session.Request("Song C"));
        harness.Tick += RadioRoomPacing.RequestMilliseconds - 1;
        Assert.False(harness.Session.CanRequest());
    }

    [Fact]
    public void HiddenUsersDisappearAndStayHidden()
    {
        var harness = new Harness();
        harness.Enter();
        var version = harness.Session.Version;

        harness.Session.HideUser("u_b");

        Assert.True(harness.Session.Version > version);
        Assert.Equal(1, harness.Session.MessageCount);
        Assert.Equal("u_dj", harness.Session.MessageAt(0).UserId);
        Assert.Equal(0, harness.Session.RequestCount);

        harness.Receive(RadioRoomSamples.Message(Station, 9, "u_b", "still here?"));
        harness.Receive($"{{\"type\":\"radio.reaction\",\"stationId\":\"{Station}\",\"userId\":\"u_b\",\"reaction\":1}}");
        harness.Receive(RadioRoomSamples.Room(Station));

        Assert.Equal(1, harness.Session.MessageCount);
        Assert.Equal(0, harness.Session.ReactionCount);
        Assert.Equal(0, harness.Session.RequestCount);
    }
}
