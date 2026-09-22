using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SealedTextCacheTests
{
    private const string Scope = "chat:conversation-1";
    private const string Sender = "user-1";
    private const string MessageId = "message-1";

    [Fact]
    public void The_same_sealed_text_hits()
    {
        var cache = new SealedTextCache<string>();
        cache.Set(MessageId, "envelope-a", "hello");

        Assert.True(cache.TryGet(MessageId, "envelope-a", out var text));
        Assert.Equal("hello", text);
    }

    [Fact]
    public void An_edit_re_sealed_under_the_same_message_id_misses_the_old_plaintext()
    {
        var cek = CryptoBox.GenerateCek();
        var original = EnvelopeCodec.Encode("gonna edit this one in just a second", cek, 1, Scope, Sender);
        var edited = EnvelopeCodec.Encode("ok this is now edited", cek, 1, Scope, Sender);
        var cache = new SealedTextCache<string>();
        cache.Set(MessageId, original.Envelope, "gonna edit this one in just a second");

        Assert.False(cache.TryGet(MessageId, edited.Envelope, out _));
        Assert.True(cache.TryGet(MessageId, original.Envelope, out _));
    }

    [Fact]
    public void The_latest_entry_is_readable_without_its_sealed_text()
    {
        var cache = new SealedTextCache<string>();
        cache.Set(MessageId, "envelope-a", "before");
        cache.Set(MessageId, "envelope-b", "after");

        Assert.True(cache.TryGetLatest(MessageId, out var text));
        Assert.Equal("after", text);
        Assert.False(cache.TryGet(MessageId, "envelope-a", out _));
        Assert.True(cache.TryGet(MessageId, "envelope-b", out _));
    }

    [Fact]
    public void Forget_and_clear_drop_entries()
    {
        var cache = new SealedTextCache<string>();
        cache.Set("one", "envelope-1", "first");
        cache.Set("two", "envelope-2", "second");

        cache.Forget("one");
        Assert.False(cache.TryGetLatest("one", out _));
        Assert.True(cache.TryGetLatest("two", out _));

        cache.Clear();
        Assert.False(cache.TryGetLatest("two", out _));
    }
}
