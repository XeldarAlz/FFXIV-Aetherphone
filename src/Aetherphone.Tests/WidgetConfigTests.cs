using Aetherphone.Core.Home;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WidgetConfigTests
{
    [Fact]
    public void Get_ReadsEachKey()
    {
        const string config = "city=Limsa;zone=3;show=true";

        Assert.Equal("Limsa", WidgetConfig.Get(config, "city"));
        Assert.Equal(3, WidgetConfig.GetInt(config, "zone", 0));
        Assert.True(WidgetConfig.GetBool(config, "show", false));
    }

    [Fact]
    public void Get_FallsBackWhenTheKeyIsMissing()
    {
        Assert.Equal(string.Empty, WidgetConfig.Get(string.Empty, "city"));
        Assert.Equal("Gridania", WidgetConfig.Get("zone=3", "city", "Gridania"));
        Assert.Equal(7, WidgetConfig.GetInt("zone=abc", "zone", 7));
    }

    [Fact]
    public void Set_ReplacesAddsAndRemovesKeys()
    {
        var config = WidgetConfig.Set(string.Empty, "city", "Limsa");
        config = WidgetConfig.Set(config, "zone", "3");
        config = WidgetConfig.Set(config, "city", "Ul'dah");

        Assert.Equal("city=Ul'dah;zone=3", config);
        Assert.Equal("zone=3", WidgetConfig.Set(config, "city", string.Empty));
    }

    [Fact]
    public void Set_StripsSeparatorsFromValues()
    {
        var config = WidgetConfig.Set(string.Empty, "note", "a;b=c");

        Assert.Equal("abc", WidgetConfig.Get(config, "note"));
    }
}
