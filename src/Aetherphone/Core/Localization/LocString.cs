namespace Aetherphone.Core.Localization;

internal readonly struct LocString
{
    public readonly string Key;
    public readonly string Source;

    public LocString(string key, string source)
    {
        Key = key;
        Source = source;
    }
}

internal readonly struct LocPlural
{
    public readonly string KeyBase;
    public readonly string OneSource;
    public readonly string OtherSource;

    public LocPlural(string keyBase, string oneSource, string otherSource)
    {
        KeyBase = keyBase;
        OneSource = oneSource;
        OtherSource = otherSource;
    }
}
