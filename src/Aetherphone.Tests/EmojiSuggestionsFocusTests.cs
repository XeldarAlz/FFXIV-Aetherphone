using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class EmojiSuggestionsFocusTests
{
    [Fact]
    public void TheFocusedComposerTakesTheKeys()
    {
        Assert.True(EmojiSuggestions.TakesKeys(true, false, false));
    }

    [Fact]
    public void AnUnfocusedComposerNeverTakesTheKeys()
    {
        Assert.False(EmojiSuggestions.TakesKeys(false, false, false));
    }

    [Fact]
    public void AModalOrShieldKeepsTheKeysFromTheFocusedComposer()
    {
        Assert.False(EmojiSuggestions.TakesKeys(true, true, false));
        Assert.False(EmojiSuggestions.TakesKeys(true, false, true));
    }

    [Fact]
    public void AnUnfocusedComposerHidesItsSuggestions()
    {
        Assert.False(EmojiSuggestions.StaysOpen(false, true, false));
        Assert.False(EmojiSuggestions.StaysOpen(false, false, true));
    }

    [Fact]
    public void AClickOnTheOpenListKeepsItUntilRelease()
    {
        Assert.True(EmojiSuggestions.StaysOpen(false, true, true));
        Assert.True(EmojiSuggestions.StaysOpen(true, false, false));
    }
}
