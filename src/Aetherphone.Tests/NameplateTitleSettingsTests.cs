using System.Numerics;
using Aetherphone.Core.Honorific;
using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NameplateTitleSettingsTests
{
    [Fact]
    public void NormalizeListsEveryStatusOnceInCatalogOrder()
    {
        var settings = new NameplateTitleSettings();

        settings.Normalize();

        Assert.Equal(NameplateStatusCatalog.Ranked.Length, settings.Order.Length);
        for (var index = 0; index < settings.Order.Length; index++)
        {
            Assert.Equal(NameplateStatusCatalog.Ranked[index], settings.Order[index]);
        }
    }

    [Fact]
    public void NormalizeKeepsTheUsersOrderAndDropsDuplicates()
    {
        var settings = new NameplateTitleSettings
        {
            Order = new[] { NameplateStatus.NowPlaying, NameplateStatus.Custom, NameplateStatus.NowPlaying },
        };

        settings.Normalize();

        Assert.Equal(NameplateStatus.NowPlaying, settings.Order[0]);
        Assert.Equal(NameplateStatus.Custom, settings.Order[1]);
        Assert.Equal(NameplateStatusCatalog.Ranked.Length, settings.Order.Length);
        Assert.Single(settings.Order, status => status == NameplateStatus.NowPlaying);
    }

    [Fact]
    public void NormalizeSlotsNewStatusesAboveTheIdleFallbacks()
    {
        var settings = new NameplateTitleSettings
        {
            Order = new[]
            {
                NameplateStatus.NowPlaying, NameplateStatus.Handle, NameplateStatus.Custom,
            },
        };

        settings.Normalize();

        var handle = Array.IndexOf(settings.Order, NameplateStatus.Handle);
        Assert.Equal(NameplateStatus.NowPlaying, settings.Order[0]);
        Assert.Equal(settings.Order.Length - 2, handle);
        Assert.Equal(NameplateStatus.Custom, settings.Order[^1]);
        Assert.True(Array.IndexOf(settings.Order, NameplateStatus.Games) <
                    Array.IndexOf(settings.Order, NameplateStatus.Gamba));
    }

    [Fact]
    public void NormalizeKeepsSlotsResultsOffThePriorityList()
    {
        var settings = new NameplateTitleSettings
        {
            Order = new[] { NameplateStatus.SlotsWin, NameplateStatus.Gamba, NameplateStatus.SlotsLoss },
        };

        settings.Normalize();

        Assert.Equal(NameplateStatus.Gamba, settings.Order[0]);
        Assert.DoesNotContain(NameplateStatus.SlotsWin, settings.Order);
        Assert.DoesNotContain(NameplateStatus.SlotsLoss, settings.Order);
    }

    [Fact]
    public void FirstNormalizeCarriesOldCombinedSwitchesOver()
    {
        var settings = new NameplateTitleSettings { Statuses = NameplateStatus.Chirper | NameplateStatus.InCall };

        settings.Normalize();

        Assert.True(settings.Shows(NameplateStatus.Aethergram));
        Assert.True(settings.Shows(NameplateStatus.DoNotDisturb));
    }

    [Fact]
    public void MoveSwapsNeighboursAndIgnoresTheEdges()
    {
        var settings = new NameplateTitleSettings();
        settings.Normalize();
        var first = settings.Order[0];
        var second = settings.Order[1];

        settings.Move(1, -1);
        settings.Move(0, -1);

        Assert.Equal(second, settings.Order[0]);
        Assert.Equal(first, settings.Order[1]);
    }

    [Fact]
    public void NormalizeClampsTheTurnInterval()
    {
        var settings = new NameplateTitleSettings { TurnSeconds = 0 };

        settings.Normalize();

        Assert.Equal(NameplateLongTitles.TakeTurns, settings.LongTitles);
        Assert.Equal(NameplateTitleSettings.MinimumTurnSeconds, settings.TurnSeconds);
    }

    [Fact]
    public void NormalizeCarriesSavedPaletteColorsOver()
    {
        const string saved = "{\"Style\":2,\"CustomColor\":3,\"CustomGlow\":9}";
        var settings = JsonConvert.DeserializeObject<NameplateTitleSettings>(saved)!;

        settings.Normalize();

        Assert.Equal(NameplatePalette.PackedAt(3), settings.TextColor);
        Assert.Equal(NameplatePalette.PackedAt(9), settings.GlowColor);
        var reloaded = JsonConvert.DeserializeObject<NameplateTitleSettings>(JsonConvert.SerializeObject(settings))!;
        reloaded.TextColor = NameplatePalette.White;
        reloaded.Normalize();
        Assert.Equal(NameplatePalette.White, reloaded.TextColor);
    }

    [Fact]
    public void FreshSettingsStartWhiteOnTheDarkGlow()
    {
        var settings = new NameplateTitleSettings();

        settings.Normalize();

        Assert.Equal(NameplatePalette.White, settings.TextColor);
        Assert.Equal(NameplatePalette.DarkGlowPacked, settings.GlowColor);
        Assert.Equal(NameplateGlow.Solid, settings.Glow);
    }

    [Fact]
    public void NormalizeResetsAGradientHonorificDoesNotKnow()
    {
        var settings = new NameplateTitleSettings { Gradient = NameplateGradients.All.Length };

        settings.Normalize();

        Assert.Equal(0, settings.Gradient);
    }

    [Fact]
    public void SolidLookSendsNoGradient()
    {
        var settings = new NameplateTitleSettings { TextColor = 0xFF8000, GlowColor = 0x102030 };

        var look = settings.CustomLook();

        Assert.Equal(NameplatePalette.Unpack(0xFF8000), look.Color);
        Assert.Equal(NameplatePalette.Unpack(0x102030), look.Glow);
        Assert.Null(look.GradientColourSet);
        Assert.Null(look.GradientAnimationStyle);
    }

    [Fact]
    public void GradientLookSendsHonorificSetAndMotion()
    {
        var settings = new NameplateTitleSettings
        {
            Glow = NameplateGlow.Gradient,
            Gradient = 11,
            Motion = NameplateGradientMotion.Static,
        };

        var look = settings.CustomLook();

        Assert.Equal(11, look.GradientColourSet);
        Assert.Equal(2, look.GradientAnimationStyle);
        Assert.Null(look.Color3);
    }

    [Fact]
    public void TwoColorLookSendsBothColors()
    {
        var settings = new NameplateTitleSettings
        {
            Glow = NameplateGlow.Gradient,
            Gradient = NameplateGradients.TwoColors,
            Motion = NameplateGradientMotion.Wave,
            GradientFirst = 0xFF0000,
            GradientSecond = 0x00FF00,
        };

        var look = settings.CustomLook();
        var json = NameplateTitleText.ToJson(new NameplateTitle(NameplateStatus.Custom, "Hi", look, false));

        Assert.Equal(-1, look.GradientColourSet);
        Assert.Equal(1, look.GradientAnimationStyle);
        Assert.Equal(new Vector3(1f, 0f, 0f), look.Glow);
        Assert.Equal(new Vector3(0f, 1f, 0f), look.Color3);
        Assert.Contains("\"GradientColourSet\":-1", json);
        Assert.Contains("\"Color3\"", json);
    }

    [Fact]
    public void ColorSlotsReadAndWriteTheirOwnColor()
    {
        var settings = new NameplateTitleSettings();

        settings.SetColor(NameplateColorSlot.GradientSecond, 0x123456);

        Assert.Equal(0x123456u, settings.ColorOf(NameplateColorSlot.GradientSecond));
        Assert.NotEqual(0x123456u, settings.ColorOf(NameplateColorSlot.GradientFirst));
        Assert.NotEqual(0x123456u, settings.ColorOf(NameplateColorSlot.Text));
        Assert.NotEqual(0x123456u, settings.ColorOf(NameplateColorSlot.Glow));
    }

    [Fact]
    public void BlankTemplateFallsBackToDefault()
    {
        var settings = new NameplateTitleSettings();

        settings.SetTemplate(NameplateStatus.Velvet, "  My Velvet: [handle]  ");
        Assert.Equal("My Velvet: [handle]", settings.Template(NameplateStatus.Velvet));

        settings.SetTemplate(NameplateStatus.Velvet, "   ");
        Assert.Equal(string.Empty, settings.Template(NameplateStatus.Velvet));
    }
}
