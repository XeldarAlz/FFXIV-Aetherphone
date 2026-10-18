using Aetherphone.Core.Notifications;
using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SoundRemovalTests : IDisposable
{
    private readonly DirectoryInfo root;
    private readonly DirectoryInfo bundled;
    private readonly DirectoryInfo user;

    public SoundRemovalTests()
    {
        root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aep-sound-" + Guid.NewGuid().ToString("N")));
        bundled = root.CreateSubdirectory("Bundled");
        user = new DirectoryInfo(Path.Combine(root.FullName, "User"));
        File.WriteAllBytes(Path.Combine(bundled.FullName, "Chime.mp3"), new byte[] { 1 });
    }

    public void Dispose()
    {
        root.Delete(true);
    }

    [Fact]
    public void ForgettingANotificationSoundResetsGlobalAndPerAppChoices()
    {
        const string json = """
            {"RingtoneSound":"file:Bubble_Pop.mp3","NotificationSound":"file:Bubble_Pop.mp3",
             "NotificationSettings":{"message":{"Sound":"file:bubble_pop.mp3"},"clock":{"Sound":"file:Spark.mp3"}}}
            """;
        var configuration = JsonConvert.DeserializeObject<Configuration>(json)!;

        Assert.True(configuration.ForgetSound(SoundKind.Notification, "file:Bubble_Pop.mp3"));
        Assert.Equal(SoundLibrary.BundledNotificationToken, configuration.NotificationSound);
        Assert.Null(configuration.NotificationSettings["message"].Sound);
        Assert.Equal("file:Spark.mp3", configuration.NotificationSettings["clock"].Sound);
        Assert.Equal("file:Bubble_Pop.mp3", configuration.RingtoneSound);
        Assert.False(configuration.ForgetSound(SoundKind.Notification, "file:Bubble_Pop.mp3"));
    }

    [Fact]
    public void ForgettingARingtoneLeavesNotificationsAlone()
    {
        const string json = """
            {"RingtoneSound":"file:Bubble_Pop.mp3","NotificationSound":"file:Bubble_Pop.mp3",
             "NotificationSettings":{"message":{"Sound":"file:Bubble_Pop.mp3"}}}
            """;
        var configuration = JsonConvert.DeserializeObject<Configuration>(json)!;

        Assert.True(configuration.ForgetSound(SoundKind.Ringtone, "file:Bubble_Pop.mp3"));
        Assert.Equal(SoundLibrary.BundledRingtoneToken, configuration.RingtoneSound);
        Assert.Equal("file:Bubble_Pop.mp3", configuration.NotificationSound);
        Assert.Equal("file:Bubble_Pop.mp3", configuration.NotificationSettings["message"].Sound);
        Assert.False(configuration.ForgetSound(SoundKind.Ringtone, "file:Other.mp3"));
    }

    [Fact]
    public void OnlyImportedFilesCountAsRemovable()
    {
        var source = Path.Combine(root.FullName, "Bubble_Pop.mp3");
        File.WriteAllBytes(source, new byte[] { 2 });
        var library = new SoundLibrary(bundled, user);

        var token = library.AddUserFile(source);

        Assert.True(library.IsUserFile(token));
        Assert.False(library.IsUserFile(SoundLibrary.BundledNotificationToken));
        Assert.False(library.IsUserFile(SoundTokens.Silent));
    }

    [Fact]
    public void AnImportNamedLikeABundledSoundIsNotRemovable()
    {
        var source = Path.Combine(root.FullName, "chime.mp3");
        File.WriteAllBytes(source, new byte[] { 3 });
        var library = new SoundLibrary(bundled, user);

        var token = library.AddUserFile(source);

        Assert.False(library.IsUserFile(token));
        Assert.False(library.IsUserFile(SoundLibrary.BundledNotificationToken));
    }

    [Fact]
    public void DeletingAnImportedFileDropsItFromTheList()
    {
        var source = Path.Combine(root.FullName, "Bubble_Pop.mp3");
        File.WriteAllBytes(source, new byte[] { 2 });
        var library = new SoundLibrary(bundled, user);
        var token = library.AddUserFile(source);

        Assert.True(library.TryUserPath(token, out var path));
        Assert.True(SoundLibrary.TryDelete(path));
        library.Refresh();

        Assert.False(File.Exists(path));
        Assert.DoesNotContain(token, library.Options);
        Assert.False(library.IsUserFile(token));
        Assert.Equal(SoundLibrary.BundledNotificationToken, library.Resolve(token));
    }

    [Fact]
    public void DeletingAMissingFileCountsAsDone()
    {
        Assert.True(SoundLibrary.TryDelete(Path.Combine(root.FullName, "Gone.mp3")));
    }

    [Fact]
    public void UserPathNeverLeavesTheUserFolder()
    {
        var library = new SoundLibrary(bundled, user);

        Assert.True(library.TryUserPath("file:../Chime.mp3", out var path));
        Assert.Equal(Path.Combine(user.FullName, "Chime.mp3"), path);
        Assert.False(library.TryUserPath(SoundTokens.Silent, out _));
    }
}
