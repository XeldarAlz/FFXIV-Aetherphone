using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GlamWireContractTests
{
    [Fact]
    public void RoutesMatchTheContract()
    {
        Assert.Equal("/prism/glams?scope=new", GlamClient.FeedPath("new", null, null));
        Assert.Equal("/prism/glams?scope=today&vanilla=true&cursor=c%2F1", GlamClient.FeedPath("today", true, "c/1"));
        Assert.Equal("/prism/glams?scope=following&vanilla=false", GlamClient.FeedPath("following", false, null));
        Assert.Equal("/prism/glams?scope=new&race=8&gender=1", GlamClient.FeedPath("new", null, null, 8, 1));
        Assert.Equal("/prism/glams?scope=new&race=8", GlamClient.FeedPath("new", null, null, 8));
        Assert.Equal("/prism/glams?scope=popular&gender=0", GlamClient.FeedPath("popular", null, null, 0, 0));
        Assert.Equal("/prism/glams?scope=new&vanilla=false&gender=1&cursor=x",
            GlamClient.FeedPath("new", false, "x", 0, 1));
        Assert.Equal("/prism/glams/g%2F1", GlamClient.GlamPath("g/1"));
        Assert.Equal("/prism/glams/g_1/like", GlamClient.LikePath("g_1"));
        Assert.Equal("/prism/glams/g_1/tryon", GlamClient.TryOnPath("g_1"));
        Assert.Equal("/users/u_1/glams?cursor=x", GlamClient.UserGlamsPath("u_1", "x"));
        Assert.Equal("/users/u_1/prism", GlamClient.UserProfilePath("u_1"));
        Assert.Equal("/prism/collections/saved/glams/g_1", GlamClient.CollectionGlamPath("saved", "g_1"));
        Assert.Equal("/prism/collections/k_1/glams", GlamClient.CollectionGlamsPath("k_1", null));
        Assert.Equal("/prism/items/45123/glams?cursor=x", GlamClient.ItemGlamsPath(45123, "x"));
        Assert.Equal("/prism/mods/glams?name=Cute%20Coat&author=Bea&cursor=x",
            GlamClient.ModGlamsPath("Cute Coat", "Bea", "x"));
        Assert.Equal("/prism/tags/summer%20goth/glams?cursor=x", GlamClient.TagGlamsPath("summer goth", "x"));
        Assert.Equal("/prism/weeks/2026-W43/glams?track=modded", GlamClient.WeekGlamsPath("2026-W43", "modded", null));
        Assert.Equal("/prism/weeks/2026-W43/glams?track=vanilla&cursor=c%2F1",
            GlamClient.WeekGlamsPath("2026-W43", "vanilla", "c/1"));
    }

    [Fact]
    public void AGlamParsesWithModsPerSlotAndCharacterMods()
    {
        var glam = JsonSerializer.Deserialize("{\"id\":\"g_1\",\"author\":{\"id\":\"u_1\",\"displayName\":\"Ada Moon\","
            + "\"handle\":\"ada.moon\"},\"title\":\"Studious magic\",\"photos\":[{\"url\":\"https://x/1.jpg\",\"width\":810,"
            + "\"height\":1080}],\"gear\":[{\"slot\":3,\"itemId\":45123,\"stain0\":12,\"mods\":[{\"name\":\"Cute Coat\","
            + "\"author\":\"Bea\",\"link\":\"https://heliosphere.app/mod/x\",\"options\":[{\"group\":\"Colour\","
            + "\"picked\":[\"Red\"]}]}]},{\"slot\":-1,\"mods\":[{\"name\":\"Body Plus\"}]}],\"tags\":[\"cute\"],"
            + "\"race\":6,\"gender\":1,\"vanilla\":false,\"tryOns\":34,\"saved\":true,\"createdAtUnix\":1792540000}",
            AethernetJsonContext.Default.GlamDto);
        Assert.NotNull(glam);
        Assert.Equal("ada.moon", glam.Author?.Handle);
        Assert.Equal(2, glam.Gear?.Length);
        Assert.Equal("Red", glam.Gear![0].Mods![0].Options![0].Picked![0]);
        Assert.Equal(-1, glam.Gear[1].Slot);
        Assert.Equal(34, glam.TryOns);
        Assert.True(glam.Saved);
        Assert.Equal(string.Empty, glam.WeekId);
    }

    [Fact]
    public void SparsePayloadsFallBackToDefaults()
    {
        var page = JsonSerializer.Deserialize("{\"items\":[{\"id\":\"g_1\"}]}", AethernetJsonContext.Default.GlamPage);
        Assert.NotNull(page?.Items);
        Assert.Null(page.NextCursor);
        Assert.Null(page.Items![0].Gear);
        var profile = JsonSerializer.Deserialize("{\"glamCount\":3}", AethernetJsonContext.Default.GlamProfileDto);
        Assert.Equal(3, profile?.GlamCount);
        Assert.Null(profile?.Placings);
    }

    [Fact]
    public void CreatingAGlamSendsPhotosGearAndTheStamp()
    {
        var json = JsonSerializer.Serialize(new CreateGlamRequest("Studious magic",
                new[] { new GlamPhotoRequest("k_1", 810, 1080) }, new[] { new GlamGearDto(3, 45123, 12) },
                new[] { "cute" }, 6, 1, false, string.Empty),
            AethernetJsonContext.Default.CreateGlamRequest);
        Assert.Equal("{\"title\":\"Studious magic\",\"photos\":[{\"key\":\"k_1\",\"width\":810,\"height\":1080}],"
            + "\"gear\":[{\"slot\":3,\"itemId\":45123,\"stain0\":12,\"stain1\":0}],\"tags\":[\"cute\"],\"race\":6,"
            + "\"gender\":1,\"vanilla\":false,\"inspiredBy\":\"\"}", json);
    }

    [Fact]
    public void PrismActivityRowsCarryTheirRankAndThumbnail()
    {
        var page = JsonSerializer.Deserialize("{\"items\":[{\"id\":\"n_1\",\"type\":23,\"app\":\"prism\","
            + "\"postId\":\"g_1\",\"actorId\":\"\",\"actorName\":\"\",\"actorDisplayName\":\"\",\"actorHandle\":\"\","
            + "\"preview\":\"Midnight Garden\",\"createdAtUnix\":1792540000,\"rank\":2,\"imageUrl\":\"https://x/1.jpg\"},"
            + "{\"id\":\"n_2\",\"type\":0,\"app\":\"prism\",\"postId\":\"g_1\",\"actorId\":\"u_1\",\"actorName\":\"Ada\","
            + "\"actorDisplayName\":\"Ada Moon\",\"actorHandle\":\"ada.moon\",\"createdAtUnix\":1792530000}],"
            + "\"unreadByApp\":{\"prism\":2}}", AethernetJsonContext.Default.NotificationPage);
        var placing = page!.Items[0];
        Assert.Equal(SocialActivity.TypePlacing, placing.Type);
        Assert.Equal(2, placing.Rank);
        Assert.Equal("https://x/1.jpg", placing.ImageUrl);
        Assert.True(SocialActivity.OpensPost(placing));
        Assert.Equal("Your glam placed #2 in Midnight Garden", SocialActivity.Body(placing));
        Assert.Equal(0, page.Items[1].Rank);
        Assert.Null(page.Items[1].ImageUrl);
        Assert.Equal("liked your glam", SocialActivity.Body(page.Items[1]));
        Assert.Equal(2, page.UnreadByApp?["prism"]);
    }

    [Fact]
    public void ItemsAndSeasonsParse()
    {
        var item = JsonSerializer.Deserialize("{\"itemId\":45123,\"glamCount\":12,\"wornWith\":[{\"itemId\":7,\"count\":4}]}",
            AethernetJsonContext.Default.GlamItemDto);
        Assert.Equal(4, item?.WornWith?[0].Count);
        var season = JsonSerializer.Deserialize("{\"id\":\"s_1\",\"name\":\"Season one\",\"standings\":[{\"user\":"
            + "{\"id\":\"u_1\"},\"points\":30,\"rank\":1}],\"me\":{\"points\":4,\"rank\":40}}",
            AethernetJsonContext.Default.GlamSeasonDto);
        Assert.Equal(1, season?.Standings?[0].Rank);
        Assert.Equal(40, season?.Me?.Rank);
    }
}
