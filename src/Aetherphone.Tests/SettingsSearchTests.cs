using System.Globalization;
using Aetherphone.Apps.Settings;
using Aetherphone.Apps.Settings.Pages;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SettingsSearchTests
{
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

    [Fact]
    public void EmptyQueryMatchesEveryRow()
    {
        Assert.True(SettingsSearch.Matches(Compare, "Appearance", string.Empty));
        Assert.True(SettingsSearch.Matches(Compare, string.Empty, string.Empty));
    }

    [Fact]
    public void EmptyTextNeverMatchesAQuery()
    {
        Assert.False(SettingsSearch.Matches(Compare, string.Empty, "a"));
    }

    [Theory]
    [InlineData("Appearance", "appear")]
    [InlineData("Appearance", "ANCE")]
    [InlineData("Notifications and Badges", "badges")]
    public void MatchesIgnoreCaseAnywhereInTheText(string text, string query)
    {
        Assert.True(SettingsSearch.Matches(Compare, text, query));
    }

    [Theory]
    [InlineData("Taille du téléphone", "telephone")]
    [InlineData("Telefongröße", "grosse")]
    [InlineData("Resolución", "RESOLUCION")]
    [InlineData("Sonido", "Sonído")]
    public void MatchesIgnoreAccents(string text, string query)
    {
        Assert.True(SettingsSearch.Matches(Compare, text, query));
    }

    [Theory]
    [InlineData("Appearance", "sound")]
    [InlineData("Privacy", "privacyy")]
    public void UnrelatedQueriesDoNotMatch(string text, string query)
    {
        Assert.False(SettingsSearch.Matches(Compare, text, query));
    }

    [Fact]
    public void PagesMatchOnTitleOrSummary()
    {
        Assert.True(SettingsSearch.MatchesPage(Compare, "Sounds", "Ringtone, Notification Sound", "ringtone"));
        Assert.True(SettingsSearch.MatchesPage(Compare, "Sounds", string.Empty, "sound"));
        Assert.False(SettingsSearch.MatchesPage(Compare, "Sounds", "Ringtone", "wallpaper"));
    }

    [Theory]
    [InlineData("allow")]
    [InlineData("NOTIFICATIONS")]
    [InlineData("w Notif")]
    public void EntriesMatchOnTheirLabel(string query)
    {
        var entry = new SettingsEntry(new LocString("test.allow", "Allow Notifications"),
            new LocString("test.alerts", "Alerts"));
        Assert.True(SettingsSearch.MatchesEntry(Compare, entry, query));
    }

    [Fact]
    public void EntriesIgnoreTheirSectionAndNeedAQuery()
    {
        var entry = new SettingsEntry(new LocString("test.banners", "Banners"), new LocString("test.alerts", "Alerts"));
        Assert.False(SettingsSearch.MatchesEntry(Compare, entry, "alerts"));
        Assert.False(SettingsSearch.MatchesEntry(Compare, entry, string.Empty));
        Assert.False(SettingsSearch.MatchesEntry(Compare, entry, "wallpaper"));
    }

    [Fact]
    public void EntriesMatchAccentInsensitively()
    {
        var entry = new SettingsEntry(new LocString("test.vibration", "Vibración"));
        Assert.True(SettingsSearch.MatchesEntry(Compare, entry, "vibracion"));
    }

    [Fact]
    public void AppSettingsDeclareTheFiveSwitches()
    {
        var entries = AppSettingsPage.Searchable;
        Assert.Equal(5, entries.Length);
        var badgeHits = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (SettingsSearch.MatchesEntry(Compare, entries[index], "badge"))
            {
                badgeHits++;
            }
        }

        Assert.Equal(1, badgeHits);
    }

    [Theory]
    [InlineData("ChocoChat", "choco")]
    [InlineData("Aethergram", "GRAM")]
    [InlineData("Páginas Amarillas", "paginas")]
    [InlineData("Yellow Pages", "yellow p")]
    public void AppsMatchOnTheirDisplayName(string displayName, string query)
    {
        Assert.True(SettingsSearch.MatchesApp(Compare, displayName, query));
    }

    [Fact]
    public void AppsNeedAQueryAndStayUnrelatedOtherwise()
    {
        Assert.False(SettingsSearch.MatchesApp(Compare, "Music", string.Empty));
        Assert.False(SettingsSearch.MatchesApp(Compare, "Music", "photo"));
        Assert.False(SettingsSearch.MatchesApp(Compare, string.Empty, "music"));
    }
}
