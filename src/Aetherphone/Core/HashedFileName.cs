using System.Security.Cryptography;
using System.Text;

namespace Aetherphone.Core;

internal static class HashedFileName
{
    public static string For(DirectoryInfo directory, string key, string extension = ".json")
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var builder = new StringBuilder(hash.Length * 2 + extension.Length);
        for (var index = 0; index < hash.Length; index++)
        {
            builder.Append(hash[index].ToString("x2"));
        }

        builder.Append(extension);
        return Path.Combine(directory.FullName, builder.ToString());
    }
}
