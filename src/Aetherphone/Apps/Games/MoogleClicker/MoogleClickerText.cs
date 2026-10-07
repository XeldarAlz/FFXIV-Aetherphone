using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Localization;
using Aetherphone.Core.MoogleClicker;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.MoogleClicker;

internal struct TextSlot
{
    private string? first;
    private string? second;
    private LanguageInfo? language;
    private string? text;

    public string Get(LocString entry, string value)
    {
        if (text is not null && ReferenceEquals(language, Loc.Current) &&
            string.Equals(first, value, StringComparison.Ordinal))
        {
            return text;
        }

        first = value;
        second = null;
        language = Loc.Current;
        text = Loc.T(entry, value);
        return text;
    }

    public string Get(LocString entry, string value, string other)
    {
        if (text is not null && ReferenceEquals(language, Loc.Current) &&
            string.Equals(first, value, StringComparison.Ordinal) && string.Equals(second, other, StringComparison.Ordinal))
        {
            return text;
        }

        first = value;
        second = other;
        language = Loc.Current;
        text = Loc.T(entry, value, other);
        return text;
    }
}

internal sealed class KupoLabels
{
    private const int PlusLimit = 2048;
    private readonly Dictionary<string, string> plus = new(StringComparer.Ordinal);
    private LanguageInfo? language;

    public string Plus(string value)
    {
        if (!ReferenceEquals(language, Loc.Current))
        {
            language = Loc.Current;
            plus.Clear();
        }

        if (plus.TryGetValue(value, out var label))
        {
            return label;
        }

        if (plus.Count >= PlusLimit)
        {
            plus.Clear();
        }

        label = Loc.T(L.Stage.Plus, value);
        plus[value] = label;
        return label;
    }
}

internal static class MoogleClickerText
{
    public static readonly LocString[] BuildingNames =
    {
        L.MoogleClicker.PomBrush, L.MoogleClicker.Apprentice, L.MoogleClicker.KupoGrove, L.MoogleClicker.MogPost,
        L.MoogleClicker.CrystalMine, L.MoogleClicker.ArtisanHall, L.MoogleClicker.ChocoboCaravan,
        L.MoogleClicker.AirshipDock, L.MoogleClicker.GoldSaucer, L.MoogleClicker.CrystalTower,
        L.MoogleClicker.PrimalForge, L.MoogleClicker.MoonAtelier,
    };

    public static readonly LocString[] TapUpgradeNames =
    {
        L.MoogleClicker.SturdyPom, L.MoogleClicker.GoldenPom, L.MoogleClicker.KupoRhythm,
        L.MoogleClicker.MoogleSpirit,
    };

    public static readonly FontAwesomeIcon[] BuildingIcons =
    {
        FontAwesomeIcon.HandPointUp, FontAwesomeIcon.GraduationCap, FontAwesomeIcon.Tree, FontAwesomeIcon.PaperPlane,
        FontAwesomeIcon.Gem, FontAwesomeIcon.Hammer, FontAwesomeIcon.Feather, FontAwesomeIcon.Anchor,
        FontAwesomeIcon.Dice, FontAwesomeIcon.ChessRook, FontAwesomeIcon.Fire, FontAwesomeIcon.Moon,
    };

    public static readonly FontAwesomeIcon[] TapUpgradeIcons =
    {
        FontAwesomeIcon.HandPaper, FontAwesomeIcon.Star, FontAwesomeIcon.Music, FontAwesomeIcon.Magic,
    };

    public static readonly Vector4[] BuildingTints =
    {
        new(0.93f, 0.46f, 0.58f, 1f), new(0.55f, 0.48f, 0.90f, 1f), new(0.48f, 0.70f, 0.32f, 1f),
        new(0.36f, 0.62f, 0.92f, 1f), new(0.38f, 0.78f, 0.86f, 1f), new(0.80f, 0.56f, 0.32f, 1f),
        new(0.96f, 0.76f, 0.26f, 1f), new(0.50f, 0.58f, 0.70f, 1f), new(0.92f, 0.66f, 0.22f, 1f),
        new(0.62f, 0.82f, 0.94f, 1f), new(0.94f, 0.42f, 0.26f, 1f), new(0.72f, 0.66f, 0.92f, 1f),
    };

    public static readonly string[] BuyIds =
    {
        "moogleclicker.buy.0", "moogleclicker.buy.1", "moogleclicker.buy.2", "moogleclicker.buy.3",
        "moogleclicker.buy.4", "moogleclicker.buy.5", "moogleclicker.buy.6", "moogleclicker.buy.7",
        "moogleclicker.buy.8", "moogleclicker.buy.9", "moogleclicker.buy.10", "moogleclicker.buy.11",
    };

    public static ControlInk Controls(Vector4 accent, PhoneTheme theme) => new(accent, StageInks.Strong, StageInks.Muted, theme.Danger);

    public static FontAwesomeIcon UpgradeIcon(int upgrade) =>
        KupoUpgrades.IsBuilding(upgrade)
            ? BuildingIcons[KupoUpgrades.Building(upgrade)]
            : TapUpgradeIcons[KupoUpgrades.TapIndex(upgrade)];

    public static Vector4 UpgradeTint(int upgrade, Vector4 accent) =>
        KupoUpgrades.IsBuilding(upgrade) ? BuildingTints[KupoUpgrades.Building(upgrade)] : accent;
}
