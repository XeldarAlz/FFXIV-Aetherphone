using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Message;

namespace Aetherphone.Windows.Components;

internal static class DirectTranscript
{
    public static TranscriptMessage[] Map<TMessage, TThread>(ChatThreadStoreBase<TMessage, TThread> store,
        TMessage[] source, string myUserId, string otherName)
        where TMessage : class, IDirectMessage
        where TThread : class, IIdentified
    {
        var mapped = new TranscriptMessage[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var message = source[index];
            if (message.Deleted)
            {
                mapped[index] = new TranscriptMessage(message.Id, message.SenderId, Loc.T(L.Message.DeletedBody),
                    0, message.CreatedAtUnix, 0, 0, null, string.Empty, default, TranscriptFlags.Deleted);
                continue;
            }

            var replySender = string.Empty;
            var replyBody = string.Empty;
            var replyKind = message.ReplyKind;
            if (message.ReplyToId is not null)
            {
                replySender = message.ReplySenderId == myUserId ? Loc.T(L.Message.You) : otherName;
                replyKind = ChatText.EffectiveKind(message.ReplyBody, replyKind);
                replyBody = ChatText.QuotePreview(message.ReplyBody, replyKind);
            }

            var flags = message.EditedAtUnix is null ? (byte)0 : TranscriptFlags.Edited;
            if (message.EncVersion != 0)
            {
                flags |= SealFlags(store.DecryptionState(message.Id));
            }

            mapped[index] = new TranscriptMessage(message.Id, message.SenderId, message.Body, message.Kind,
                message.CreatedAtUnix, message.MediaWidth, message.MediaHeight, message.ReadAtUnix, string.Empty,
                default, flags, message.ReplyToId, replySender, replyBody, replyKind, message.DurationSecs,
                Reactions(message.Reactions));
        }

        return mapped;
    }

    public static TranscriptReaction[]? Reactions(ReactionSummaryDto[]? summaries)
    {
        if (summaries is not { Length: > 0 })
        {
            return null;
        }

        var reactions = new TranscriptReaction[summaries.Length];
        for (var index = 0; index < summaries.Length; index++)
        {
            reactions[index] = new TranscriptReaction(summaries[index].Token, summaries[index].Count,
                summaries[index].Mine);
        }

        return reactions;
    }

    public static byte SealFlags(DmDecryptedBody state)
    {
        if (state.IsPlaceholder)
        {
            return TranscriptFlags.Encrypted | TranscriptFlags.Placeholder;
        }

        return state.State == DmBodyState.Decrypted && !state.Verified
            ? (byte)(TranscriptFlags.Encrypted | TranscriptFlags.Unverified)
            : TranscriptFlags.Encrypted;
    }
}
