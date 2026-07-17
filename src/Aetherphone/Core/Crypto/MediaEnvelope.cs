using System.Text;

namespace Aetherphone.Core.Crypto;

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

    private const string Domain = "aep-media-v1";

    private static byte[] Aad(string scope, int generation, string senderId, int mediaKind)
    {
        return Encoding.UTF8.GetBytes($"{Domain}|{mediaKind}|{generation}|{scope}|{senderId}");
    }
}
