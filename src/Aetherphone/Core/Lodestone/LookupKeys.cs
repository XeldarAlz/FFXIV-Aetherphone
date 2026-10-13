namespace Aetherphone.Core.Lodestone;

internal static class LookupKeys
{
    public static string CharacterDetail(string id, string name, string world)
    {
        var trimmedId = id.Trim();
        if (trimmedId.Length > 0)
        {
            return trimmedId;
        }

        return string.Concat(name.Trim(), "@", world.Trim());
    }
}
