using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Wallet;

internal enum CurrencyKind : byte
{
    Generic,
    Gil,
    Tomestone,
    LimitedTomestone,
    GrandCompanySeal,
}

internal enum WalletGroup : byte
{
    Tomestones,
    Scrips,
    Hunt,
    GrandCompany,
    Pvp,
    Other,
}

internal sealed class WalletEntry
{
    public WalletEntry(uint itemId, uint iconId, string name, long cap, CurrencyKind kind, WalletGroup group)
    {
        ItemId = itemId;
        IconId = iconId;
        Name = name;
        Cap = cap;
        Kind = kind;
        Group = group;
    }

    public uint ItemId { get; }
    public uint IconId { get; }
    public string Name { get; }
    public CurrencyKind Kind { get; }
    public WalletGroup Group { get; }
    public long Cap { get; set; }
    public long Amount { get; set; }
    public long WeeklyAmount { get; set; }
    public long WeeklyCap { get; set; }
    public bool HasWeeklyCap => Kind == CurrencyKind.LimitedTomestone && WeeklyCap > 0;
    public CapLevel Level => WalletMath.Level(Amount, Cap);
    public float Fraction => WalletMath.Fraction(Amount, Cap);

    private long cachedWeeklyAmount = -1;
    private long cachedWeeklyCap = -1;
    private LanguageInfo? cachedWeeklyLanguage;
    private string cachedWeeklyText = string.Empty;

    public string WeeklyCapText
    {
        get
        {
            if (!HasWeeklyCap)
            {
                return string.Empty;
            }

            if (cachedWeeklyAmount == WeeklyAmount && cachedWeeklyCap == WeeklyCap &&
                ReferenceEquals(cachedWeeklyLanguage, Loc.Current))
            {
                return cachedWeeklyText;
            }

            cachedWeeklyAmount = WeeklyAmount;
            cachedWeeklyCap = WeeklyCap;
            cachedWeeklyLanguage = Loc.Current;
            cachedWeeklyText = Loc.T(L.Wallet.WeeklyCap, NumberText.Group(WeeklyAmount), NumberText.Group(WeeklyCap));
            return cachedWeeklyText;
        }
    }
}

internal sealed class WalletSection
{
    public WalletSection(WalletGroup group, WalletEntry[] entries)
    {
        Group = group;
        Entries = entries;
    }

    public WalletGroup Group { get; }
    public WalletEntry[] Entries { get; }
}
