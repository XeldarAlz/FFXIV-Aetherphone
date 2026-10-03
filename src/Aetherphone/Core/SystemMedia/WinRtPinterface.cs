using System.Security.Cryptography;
using System.Text;

namespace Aetherphone.Core.SystemMedia;

internal static class WinRtPinterface
{
    private static readonly Guid Namespace = new("11f47ad5-7b73-42c0-abae-878b1e16adee");

    public static Guid Compute(string signature)
    {
        var signatureBytes = Encoding.UTF8.GetBytes(signature);
        var buffer = new byte[16 + signatureBytes.Length];
        Namespace.TryWriteBytes(buffer, bigEndian: true, out _);
        signatureBytes.CopyTo(buffer, 16);
        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(buffer, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }

    public static string RuntimeClass(string className, Guid defaultInterface) =>
        $"rc({className};{Braced(defaultInterface)})";

    public static string Generic(Guid genericInterface, string firstArgument, string secondArgument) =>
        $"pinterface({Braced(genericInterface)};{firstArgument};{secondArgument})";

    public static string Generic(Guid genericInterface, string argument) =>
        $"pinterface({Braced(genericInterface)};{argument})";

    private static string Braced(Guid value) => "{" + value.ToString("D") + "}";
}
