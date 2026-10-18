using System.Runtime.CompilerServices;
using Aetherphone.Core.Apps;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ResumableAppsTests
{
    private const ulong CharacterA = 1001;
    private const ulong CharacterB = 2002;
    private const ulong AccountA = 1001;
    private const ulong AccountB = 2002;
    private const long ClosedAt = 50_000;
    private static readonly string[] RestartFreshAppIds = { "camera", "casino", "games" };
    private static readonly ResumeStamp Closed = new(ClosedAt, CharacterA, AccountA);

    [Fact]
    public void EveryApp_ResumesWhereItWasLeft_ExceptTheOnesThatRestartFresh()
    {
        var apps = RegisteredApps();
        Assert.True(apps.Count > RestartFreshAppIds.Length, "No IPhoneApp types were discovered.");
        for (var index = 0; index < apps.Count; index++)
        {
            var (appId, type) = apps[index];
            var restartsFresh = Array.IndexOf(RestartFreshAppIds, appId) >= 0;
            Assert.True(typeof(IResumableApp).IsAssignableFrom(type) != restartsFresh,
                restartsFresh ? $"'{appId}' must restart fresh" : $"'{appId}' must resume where it was left");
        }
    }

    [Theory]
    [InlineData("camera")]
    [InlineData("casino")]
    [InlineData("games")]
    public void RestartFreshApp_IsRegisteredAndNotResumable(string appId)
    {
        var apps = RegisteredApps();
        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId)
            {
                Assert.False(typeof(IResumableApp).IsAssignableFrom(apps[index].Type));
                return;
            }
        }

        Assert.Fail($"'{appId}' is not registered");
    }

    [Theory]
    [InlineData("camera")]
    [InlineData("casino")]
    [InlineData("games")]
    public void RestartFreshApp_KeepsASwitcherCardButNeverResumes(string appId)
    {
        var resumable = typeof(IResumableApp).IsAssignableFrom(TypeOf(appId));
        Assert.True(NavigationStack.ListsCard(Closed, ClosedAt + 1, CharacterA, AccountA));
        Assert.False(NavigationStack.Resumes(resumable, true, Closed, true, ClosedAt + 1, CharacterA, AccountA));
        Assert.False(NavigationStack.Resumes(resumable, true, Closed, false, ClosedAt + 1, CharacterA, AccountA));
    }

    [Fact]
    public void RestartFreshCard_DropsOutAfterTheWindowOrACharacterOrAccountSwitch()
    {
        Assert.False(NavigationStack.ListsCard(Closed, ClosedAt + ResumeStamp.WindowMilliseconds + 1, CharacterA,
            AccountA));
        Assert.False(NavigationStack.ListsCard(Closed, ClosedAt + 1, CharacterB, AccountA));
        Assert.False(NavigationStack.ListsCard(Closed, ClosedAt + 1, CharacterA, AccountB));
    }

    [Fact]
    public void ResumableApp_ResumesFromItsCardAndThroughBack()
    {
        Assert.True(NavigationStack.Resumes(true, true, Closed, true, ClosedAt + 1, CharacterA, AccountA));
        Assert.False(NavigationStack.Resumes(true, true, Closed, true,
            ClosedAt + ResumeStamp.WindowMilliseconds + 1, CharacterA, AccountA));
        Assert.True(NavigationStack.Resumes(true, true, Closed, false,
            ClosedAt + ResumeStamp.WindowMilliseconds + 1, CharacterA, AccountA));
        Assert.False(NavigationStack.Resumes(true, true, Closed, false, ClosedAt + 1, CharacterB, AccountA));
    }

    private static Type TypeOf(string appId)
    {
        var apps = RegisteredApps();
        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId)
            {
                return apps[index].Type;
            }
        }

        throw new InvalidOperationException($"'{appId}' is not registered");
    }

    private static List<(string Id, Type Type)> RegisteredApps()
    {
        var apps = new List<(string Id, Type Type)>();
        var types = typeof(IPhoneApp).Assembly.GetTypes();
        for (var index = 0; index < types.Length; index++)
        {
            var type = types[index];
            if (type.IsAbstract || type.IsInterface || !typeof(IPhoneApp).IsAssignableFrom(type) ||
                type.Namespace is null || !type.Namespace.StartsWith("Aetherphone.Apps", StringComparison.Ordinal))
            {
                continue;
            }

            apps.Add((((IPhoneApp)RuntimeHelpers.GetUninitializedObject(type)).Id, type));
        }

        return apps;
    }
}
