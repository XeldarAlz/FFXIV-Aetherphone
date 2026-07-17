using System.Text;

namespace Aetherphone.Core.Crypto;

// Seals attachment and voice-note bytes with the conversation CEK, reusing the AES-GCM primitive the
// text envelope trusts. The AAD binds scope, generation, sender, a "media" domain, and the message
// kind, so a sealed blob can never be reinterpreted as text, as a different media kind, or as media
// from another conversation or key generation.
internal static class MediaEnvelope
{
    public static byte[] Seal(byte[] plaintext, byte[] cek, string scope, int generation, string senderId,
        int mediaKind)
    {
        return CryptoBox.Seal(plaintext, cek, Aad(scope, generation, senderId, mediaKind));
    }

    public static byte[]? Open(byte[] sealedBytes, byte[] cek, string scope, int generation, string senderId,
        int mediaKind)
    {
        return CryptoBox.Open(sealedBytes, cek, Aad(scope, generation, senderId, mediaKind));
    }

    private static byte[] Aad(string scope, int generation, string senderId, int mediaKind)
    {
        return Encoding.UTF8.GetBytes($"{scope}|{generation}|{senderId}|media|{mediaKind}");
    }
}
