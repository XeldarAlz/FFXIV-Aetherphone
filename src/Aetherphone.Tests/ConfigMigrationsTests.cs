using Aetherphone.Core;
using Aetherphone.Core.Config;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ConfigMigrationsTests
{
    private const string LegacyJson =
        """{"MessageStarredMessages":[{"$type":"Aetherphone.Apps.DirectMessages.StarredMessage, Aetherphone","MessageId":"m1"}]}""";

    [Fact]
    public void RewritesLegacyStarredMessageTypeName()
    {
        var rewritten = ConfigMigrations.RewriteTypeNames(LegacyJson);
        Assert.Contains("Aetherphone.Core.Message.StarredMessage, Aetherphone", rewritten);
        Assert.DoesNotContain("Aetherphone.Apps.DirectMessages.StarredMessage", rewritten);
    }

    [Fact]
    public void RewritesLegacyVelvetMutePreferencesTypeName()
    {
        const string json =
            """{"VelvetMutes":{"$type":"Aetherphone.Core.Social.VelvetMutePreferences, Aetherphone","Intent":1}}""";
        var rewritten = ConfigMigrations.RewriteTypeNames(json);
        Assert.Contains("Aetherphone.Core.Social.VelvetFilterPreferences, Aetherphone", rewritten);
        Assert.DoesNotContain("VelvetMutePreferences", rewritten);
    }

    [Fact]
    public void RewritesEveryRelocatedAppTypeName()
    {
        const string json =
            """["Aetherphone.Apps.Calendar.CalendarCustomEvent, Aetherphone","Aetherphone.Apps.Notes.PhoneNote, Aetherphone","Aetherphone.Apps.Notes.ReminderItem, Aetherphone","Aetherphone.Apps.Clock.WorldClockEntry, Aetherphone","Aetherphone.Apps.Clock.AlarmEntry, Aetherphone"]""";
        var rewritten = ConfigMigrations.RewriteTypeNames(json);
        Assert.DoesNotContain("Aetherphone.Apps.", rewritten);
    }

    [Fact]
    public void RewriteIsIdempotent()
    {
        var once = ConfigMigrations.RewriteTypeNames(LegacyJson);
        var twice = ConfigMigrations.RewriteTypeNames(once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void LeavesUnrelatedJsonUntouched()
    {
        const string json = """{"AethernetToken":"","MutedLinkshells":["ls1"],"Version":3}""";
        Assert.Equal(json, ConfigMigrations.RewriteTypeNames(json));
    }
}
