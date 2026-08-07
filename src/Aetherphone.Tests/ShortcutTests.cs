using System.Numerics;
using System.Text;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shortcuts;
using Dalamud.Interface;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ShortcutCommandTextTests
{
    [Fact]
    public void APlainCommand_IsSentWholeWithNoWait()
    {
        var line = ShortcutCommandText.Split("/mount \"Company Chocobo\"", out var wait);

        Assert.Equal("/mount \"Company Chocobo\"", line);
        Assert.Equal(0f, wait);
    }

    [Fact]
    public void AMacroInlineWait_IsStrippedAndBecomesTheDelay()
    {
        var line = ShortcutCommandText.Split("/ac \"Fire\" <wait.2.5>", out var wait);

        Assert.Equal("/ac \"Fire\"", line);
        Assert.Equal(2.5f, wait);
    }

    [Fact]
    public void AWaitCommand_SendsNothingAndOnlyDelays()
    {
        var line = ShortcutCommandText.Split("/wait 3", out var wait);

        Assert.Equal(string.Empty, line);
        Assert.Equal(3f, wait);
    }

    [Fact]
    public void ABareWaitCommand_DefaultsToOneSecond()
    {
        var line = ShortcutCommandText.Split("/wait", out var wait);

        Assert.Equal(string.Empty, line);
        Assert.Equal(1f, wait);
    }

    [Fact]
    public void AnAngleBracketThatIsNotAWait_IsLeftAlone()
    {
        var line = ShortcutCommandText.Split("/say <se.1>", out var wait);

        Assert.Equal("/say <se.1>", line);
        Assert.Equal(0f, wait);
    }

    [Fact]
    public void PlainChatText_IsSentAsTyped()
    {
        var line = ShortcutCommandText.Split("  hello there  ", out var wait);

        Assert.Equal("hello there", line);
        Assert.Equal(0f, wait);
    }
}

public sealed class ShortcutMacroTests
{
    [Fact]
    public void EachLineOfAMacro_BecomesAStepInOrder()
    {
        var steps = new List<ShortcutStep>();

        var added = ShortcutMacro.Append("/mount \"Company Chocobo\"\n/sit", steps, 30, out var truncated);

        Assert.Equal(2, added);
        Assert.False(truncated);
        Assert.Equal("/mount \"Company Chocobo\"", steps[0].Text);
        Assert.Equal("/sit", steps[1].Text);
        Assert.All(steps, step => Assert.Equal(ShortcutStepKind.Command, step.Kind));
    }

    [Fact]
    public void ABareWaitLine_BecomesAWaitStep()
    {
        var steps = new List<ShortcutStep>();

        ShortcutMacro.Append("/wait 3", steps, 30, out _);

        Assert.Single(steps);
        Assert.Equal(ShortcutStepKind.Wait, steps[0].Kind);
        Assert.Equal(3f, steps[0].Seconds);
    }

    [Fact]
    public void AnInlineWait_StaysWithItsCommand()
    {
        var steps = new List<ShortcutStep>();

        ShortcutMacro.Append("/ac \"Fire\" <wait.2>", steps, 30, out _);

        Assert.Single(steps);
        Assert.Equal(ShortcutStepKind.Command, steps[0].Kind);
        Assert.Equal("/ac \"Fire\" <wait.2>", steps[0].Text);
    }

    [Fact]
    public void BlankLinesAndWindowsEndings_AreIgnored()
    {
        var steps = new List<ShortcutStep>();

        var added = ShortcutMacro.Append("/one\r\n\r\n  \r\n/two\r\n", steps, 30, out _);

        Assert.Equal(2, added);
        Assert.Equal("/one", steps[0].Text);
        Assert.Equal("/two", steps[1].Text);
    }

    [Fact]
    public void APasteThatOverflowsTheStepCap_StopsAndReportsIt()
    {
        var steps = new List<ShortcutStep>();

        var added = ShortcutMacro.Append("/one\n/two\n/three", steps, 2, out var truncated);

        Assert.Equal(2, added);
        Assert.True(truncated);
        Assert.Equal(2, steps.Count);
    }

    [Fact]
    public void PastingOntoExistingSteps_AppendsRatherThanReplaces()
    {
        var steps = new List<ShortcutStep> { new() { Kind = ShortcutStepKind.Command, Text = "/first" } };

        ShortcutMacro.Append("/second", steps, 30, out _);

        Assert.Equal(2, steps.Count);
        Assert.Equal("/first", steps[0].Text);
        Assert.Equal("/second", steps[1].Text);
    }

    [Fact]
    public void AnEmptyClipboard_AddsNothingAndIsNotATruncation()
    {
        var steps = new List<ShortcutStep>();

        var added = ShortcutMacro.Append("   \n\n", steps, 30, out var truncated);

        Assert.Equal(0, added);
        Assert.False(truncated);
        Assert.Empty(steps);
    }
}

public sealed class ShortcutCodeTests
{
    [Fact]
    public void ACode_RoundTripsEveryStepKind()
    {
        var source = new ShortcutEntry { Name = "Ride", Glyph = (int)FontAwesomeIcon.Horse, Tint = "3355ff" };
        source.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = "/mount \"Company Chocobo\"" });
        source.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Wait, Seconds = 2.5f });
        source.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.OpenUrl, Text = "https://example.com/a?b=1" });

        Assert.True(ShortcutCode.TryDecode(ShortcutCode.Encode(source), out var decoded, out var error));

        Assert.Equal(ShortcutCodeError.None, error);
        Assert.Equal("Ride", decoded.Name);
        Assert.Equal((int)FontAwesomeIcon.Horse, decoded.Glyph);
        Assert.NotEmpty(decoded.Tint);
        Assert.Equal(3, decoded.Steps.Count);
        Assert.Equal("/mount \"Company Chocobo\"", decoded.Steps[0].Text);
        Assert.Equal(ShortcutStepKind.Wait, decoded.Steps[1].Kind);
        Assert.Equal(2.5f, decoded.Steps[1].Seconds);
        Assert.Equal("https://example.com/a?b=1", decoded.Steps[2].Text);
    }

    [Fact]
    public void ADecodedCode_IsANewShortcutRatherThanTheOriginal()
    {
        var source = new ShortcutEntry { Name = "Ride" };
        source.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = "/mount" });

        Assert.True(ShortcutCode.TryDecode(ShortcutCode.Encode(source), out var decoded, out _));
        Assert.NotEqual(source.Id, decoded.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello there")]
    [InlineData("AEPS1.")]
    [InlineData("AEPS9.abcd")]
    public void TextThatIsNotACode_IsRejectedAsSuch(string text)
    {
        Assert.False(ShortcutCode.TryDecode(text, out _, out var error));
        Assert.Equal(ShortcutCodeError.NotACode, error);
    }

    [Fact]
    public void ACodeWithAGarbagePayload_IsMalformed()
    {
        Assert.False(ShortcutCode.TryDecode(ShortcutCode.Prefix + "not base64 !!", out _, out var error));
        Assert.Equal(ShortcutCodeError.Malformed, error);
    }

    [Fact]
    public void ACodeWhoseLinkIsNotWeb_IsRefused()
    {
        var code = CodeFor("{\"name\":\"Trap\",\"steps\":[{\"kind\":3,\"text\":\"file:///C:/Windows/System32/cmd.exe\"}]}");

        Assert.False(ShortcutCode.TryDecode(code, out _, out var error));
        Assert.Equal(ShortcutCodeError.UnsafeLink, error);
    }

    [Fact]
    public void ACodeWithAnUnknownStepKind_IsMalformed()
    {
        var code = CodeFor("{\"name\":\"Odd\",\"steps\":[{\"kind\":9,\"text\":\"/say hi\"}]}");

        Assert.False(ShortcutCode.TryDecode(code, out _, out var error));
        Assert.Equal(ShortcutCodeError.Malformed, error);
    }

    [Fact]
    public void ACodeWithNoName_IsMalformed()
    {
        var code = CodeFor("{\"name\":\"  \",\"steps\":[{\"kind\":0,\"text\":\"/say hi\"}]}");

        Assert.False(ShortcutCode.TryDecode(code, out _, out var error));
        Assert.Equal(ShortcutCodeError.Malformed, error);
    }

    [Fact]
    public void ACodeWithNoUsableSteps_IsMalformed()
    {
        var code = CodeFor("{\"name\":\"Empty\",\"steps\":[{\"kind\":0,\"text\":\"   \"}]}");

        Assert.False(ShortcutCode.TryDecode(code, out _, out var error));
        Assert.Equal(ShortcutCodeError.Malformed, error);
    }

    [Fact]
    public void ACodeOverTheStepCap_IsMalformed()
    {
        var source = new ShortcutEntry { Name = "Long" };
        for (var index = 0; index < ShortcutStore.MaxSteps + 1; index++)
        {
            source.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = "/say " + index });
        }

        Assert.False(ShortcutCode.TryDecode(ShortcutCode.Encode(source), out _, out var error));
        Assert.Equal(ShortcutCodeError.Malformed, error);
    }

    [Fact]
    public void ACodeWithAnOverlongCommand_IsMalformed()
    {
        var code = CodeFor("{\"name\":\"Long\",\"steps\":[{\"kind\":0,\"text\":\"" +
                           new string('a', ShortcutStore.CommandMaxLength + 1) + "\"}]}");

        Assert.False(ShortcutCode.TryDecode(code, out _, out var error));
        Assert.Equal(ShortcutCodeError.Malformed, error);
    }

    [Fact]
    public void AnUnknownGlyph_FallsBackToTheMonogram()
    {
        var code = CodeFor("{\"name\":\"Odd\",\"glyph\":999999,\"steps\":[{\"kind\":0,\"text\":\"/say hi\"}]}");

        Assert.True(ShortcutCode.TryDecode(code, out var decoded, out _));
        Assert.Equal(0, decoded.Glyph);
    }

    [Fact]
    public void AWaitBeyondTheCap_IsClamped()
    {
        var code = CodeFor("{\"name\":\"Slow\",\"steps\":[{\"kind\":1,\"seconds\":9999}]}");

        Assert.True(ShortcutCode.TryDecode(code, out var decoded, out _));
        Assert.Equal(ShortcutRunner.MaxWaitSeconds, decoded.Steps[0].Seconds);
    }

    [Fact]
    public void ACodeWrappedAcrossLines_StillDecodes()
    {
        var source = new ShortcutEntry { Name = "Ride" };
        source.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = "/mount" });
        var wrapped = ShortcutCode.Encode(source).Insert(ShortcutCode.Prefix.Length + 4, "\n");

        Assert.True(ShortcutCode.TryDecode(wrapped, out var decoded, out _));
        Assert.Equal("/mount", decoded.Steps[0].Text);
    }

    private static string CodeFor(string json) =>
        ShortcutCode.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
}

public sealed class ShortcutHomeTileTests
{
    [Fact]
    public void APinnedShortcut_SurvivesAReload()
    {
        var shortcuts = new FakeShortcutSource();
        var entry = new ShortcutEntry { Name = "Ride" };
        shortcuts.Entries.Add(entry);
        var configuration = EmptyConfiguration();
        var first = BuildLayout(configuration, shortcuts);

        Assert.True(first.AddShortcut(entry.Id));

        var reloaded = BuildLayout(configuration, shortcuts);
        Assert.True(reloaded.HasShortcut(entry.Id));
    }

    [Fact]
    public void AMovedShortcutTile_KeepsItsCellAcrossReloads()
    {
        var shortcuts = new FakeShortcutSource();
        var entry = new ShortcutEntry { Name = "Ride" };
        shortcuts.Entries.Add(entry);
        var configuration = EmptyConfiguration();
        var first = BuildLayout(configuration, shortcuts);
        first.AddShortcut(entry.Id);

        first.MoveTile(ShortcutTile(first, entry.Id), 0, new GridCell(2, 3));

        var reloaded = BuildLayout(configuration, shortcuts);
        Assert.Equal(new GridCell(2, 3), ShortcutTile(reloaded, entry.Id).Cell);
    }

    [Fact]
    public void AShortcutTileWhoseEntryIsGone_IsDroppedOnLoad()
    {
        var shortcuts = new FakeShortcutSource();
        var entry = new ShortcutEntry { Name = "Ride" };
        shortcuts.Entries.Add(entry);
        var configuration = EmptyConfiguration();
        BuildLayout(configuration, shortcuts).AddShortcut(entry.Id);

        shortcuts.Entries.Clear();

        var reloaded = BuildLayout(configuration, shortcuts);
        Assert.False(reloaded.HasShortcut(entry.Id));
    }

    [Fact]
    public void AShortcutTile_IsNotMistakenForAFolder()
    {
        var shortcuts = new FakeShortcutSource();
        var entry = new ShortcutEntry { Name = "Ride" };
        shortcuts.Entries.Add(entry);
        var layout = BuildLayout(EmptyConfiguration(), shortcuts);
        layout.AddShortcut(entry.Id);

        var tile = ShortcutTile(layout, entry.Id);
        Assert.True(tile.IsShortcut);
        Assert.False(tile.IsFolder);
        Assert.False(tile.IsWidget);
    }

    [Fact]
    public void RemovingAShortcut_TakesItsTileWithIt()
    {
        var shortcuts = new FakeShortcutSource();
        var entry = new ShortcutEntry { Name = "Ride" };
        shortcuts.Entries.Add(entry);
        var layout = BuildLayout(EmptyConfiguration(), shortcuts);
        layout.AddShortcut(entry.Id);

        Assert.True(layout.RemoveShortcut(entry.Id));
        Assert.False(layout.HasShortcut(entry.Id));
    }

    [Fact]
    public void AShortcutCannotBeAddedTwice()
    {
        var shortcuts = new FakeShortcutSource();
        var entry = new ShortcutEntry { Name = "Ride" };
        shortcuts.Entries.Add(entry);
        var layout = BuildLayout(EmptyConfiguration(), shortcuts);

        Assert.True(layout.AddShortcut(entry.Id));
        Assert.False(layout.AddShortcut(entry.Id));
    }

    private static HomeTile ShortcutTile(HomeLayoutService layout, Guid id)
    {
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            layout.Placements(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].Shortcut?.Id == id)
                {
                    return tiles[index];
                }
            }
        }

        throw new InvalidOperationException("The shortcut tile is not on any page.");
    }

    private static HomeLayoutService BuildLayout(FakeHomeConfiguration configuration, IShortcutSource shortcuts)
    {
        var apps = new List<IPhoneApp>();
        return new HomeLayoutService(apps, new WidgetRegistry(Array.Empty<IHomeWidget>(), apps), shortcuts,
            configuration);
    }

    private static FakeHomeConfiguration EmptyConfiguration() =>
        new()
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { new() },
            },
        };
}

public sealed class ShortcutWebUrlTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?a=1#frag")]
    [InlineData("  https://xivapi.com/  ")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    public void WebUrls_AreAccepted(string url)
    {
        Assert.True(ShortcutCommandText.IsWebUrl(url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("example.com")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("https://")]
    [InlineData("mailto:someone@example.com")]
    public void NonWebOrLocalTargets_AreRejected(string url)
    {
        Assert.False(ShortcutCommandText.IsWebUrl(url));
    }

    [Fact]
    public void HostOf_ReducesAUrlToItsHost()
    {
        Assert.Equal("example.com", ShortcutCommandText.HostOf("https://example.com/some/long/path"));
    }

    [Fact]
    public void HostOf_LeavesUnparseableTextAlone()
    {
        Assert.Equal("not a url", ShortcutCommandText.HostOf("  not a url  "));
    }
}

public sealed class ShortcutEntryCopyTests
{
    [Fact]
    public void Copy_DeepCopiesSteps_SoLaterEditsDoNotLeak()
    {
        var source = new ShortcutEntry { Name = "Ride" };
        source.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = "/mount" });

        var copy = source.Copy();
        source.Steps[0].Text = "/dismount";
        source.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Wait, Seconds = 3f });

        Assert.Single(copy.Steps);
        Assert.Equal("/mount", copy.Steps[0].Text);
    }

    [Fact]
    public void Copy_MintsANewIdentity()
    {
        var source = new ShortcutEntry { Name = "Ride" };

        Assert.NotEqual(source.Id, source.Copy().Id);
    }

    [Fact]
    public void CopyFrom_ReplacesStepsRatherThanAppending()
    {
        var target = new ShortcutEntry { Name = "Old" };
        target.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = "/one" });
        target.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = "/two" });
        var id = target.Id;

        var source = new ShortcutEntry { Name = "New" };
        source.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command, Text = "/three" });
        target.CopyFrom(source);

        Assert.Equal(id, target.Id);
        Assert.Equal("New", target.Name);
        Assert.Single(target.Steps);
        Assert.Equal("/three", target.Steps[0].Text);
    }
}

public sealed class ShortcutHoldTests
{
    [Fact]
    public void AnUnblockedStep_RunsStraightAway()
    {
        var hold = new ShortcutHold();

        Assert.Equal(ShortcutHoldOutcome.Ready, hold.Advance(false, 0.016f));
        Assert.False(hold.IsWaiting);
    }

    [Fact]
    public void ABlockedStep_WaitsInsteadOfFailing()
    {
        var hold = new ShortcutHold();

        Assert.Equal(ShortcutHoldOutcome.Waiting, hold.Advance(true, 1f));
        Assert.True(hold.IsWaiting);
    }

    [Fact]
    public void ABlockThatClears_ReleasesTheStepAndForgetsTheWait()
    {
        var hold = new ShortcutHold();
        hold.Advance(true, 5f);

        Assert.Equal(ShortcutHoldOutcome.Ready, hold.Advance(false, 0.016f));
        Assert.False(hold.IsWaiting);
    }

    [Fact]
    public void ABlockThatNeverClears_ExpiresInsteadOfHangingForever()
    {
        var hold = new ShortcutHold();
        var outcome = ShortcutHoldOutcome.Ready;
        for (var second = 0; second < 60 && outcome != ShortcutHoldOutcome.Expired; second++)
        {
            outcome = hold.Advance(true, 1f);
        }

        Assert.Equal(ShortcutHoldOutcome.Expired, outcome);
    }

    [Fact]
    public void AnExpiredHold_StartsCleanForTheNextRun()
    {
        var hold = new ShortcutHold();

        hold.Advance(true, ShortcutHold.MaxSeconds);

        Assert.False(hold.IsWaiting);
    }
}

public sealed class ShortcutGameGateTests
{
    [Fact]
    public void OnlyCommandSteps_WaitOnTheGame()
    {
        Assert.True(ShortcutRunner.NeedsGame(ShortcutStepKind.Command));
        Assert.False(ShortcutRunner.NeedsGame(ShortcutStepKind.Wait));
        Assert.False(ShortcutRunner.NeedsGame(ShortcutStepKind.OpenPlugin));
        Assert.False(ShortcutRunner.NeedsGame(ShortcutStepKind.OpenUrl));
    }
}

public sealed class ShortcutRunViewTests
{
    [Fact]
    public void AnIdleView_IsNotRunning()
    {
        var view = new ShortcutRunView(Guid.Empty, string.Empty, Vector4.Zero, 1, 0, false);

        Assert.False(view.IsRunning);
        Assert.Equal(0f, view.Progress);
    }

    [Fact]
    public void ProgressTracksTheStepThroughTheQueue()
    {
        var view = new ShortcutRunView(Guid.NewGuid(), "Ride", Vector4.One, 2, 4, false);

        Assert.True(view.IsRunning);
        Assert.Equal(0.5f, view.Progress);
    }
}

public sealed class ShortcutMonogramTests
{
    [Fact]
    public void TwoWords_GiveTwoInitials()
    {
        Assert.Equal("MU", ShortcutStore.Monogram("Mount Up"));
    }

    [Fact]
    public void OneWord_GivesOneInitial()
    {
        Assert.Equal("R", ShortcutStore.Monogram("Ride"));
    }

    [Fact]
    public void AnEmptyName_GivesAPlaceholder()
    {
        Assert.Equal("?", ShortcutStore.Monogram("   "));
    }
}
