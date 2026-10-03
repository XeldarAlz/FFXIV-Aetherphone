using System.Text.Json;
using Aetherphone.Core.Rolladeck;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RolladeckJsonTests
{
    [Fact]
    public void LiveResponse_AcceptsNumericVenueIds()
    {
        const string json = """
            {"liveDJs":[{"djName":"Coeli","venueId":42,"server":"Alpha","district":"Mist","ward":"12","plot":"15"}],
             "openVenues":[{"id":1782352262737,"name":"The Undergroove","ward":"12","plot":"15"}]}
            """;

        var response = JsonSerializer.Deserialize(json, RolladeckJsonContext.Default.LiveResponse)!;

        Assert.Equal("1782352262737", response.OpenVenues[0].Id);
        Assert.Equal(12, response.LiveDJs[0].Ward);
    }

    [Fact]
    public void DirectoryResponse_AcceptsStringAndNullIds()
    {
        const string json = """{"venues":[{"id":"abc","name":"A"},{"id":null,"name":"B"}]}""";

        var response = JsonSerializer.Deserialize(json, RolladeckJsonContext.Default.DirectoryResponse)!;

        Assert.Equal("abc", response.Venues[0].Id);
        Assert.Null(response.Venues[1].Id);
    }
}
