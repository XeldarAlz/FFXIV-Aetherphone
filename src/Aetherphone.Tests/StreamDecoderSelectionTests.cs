using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StreamDecoderSelectionTests
{
    [Theory]
    [InlineData("audio/aac")]
    [InlineData("audio/aacp")]
    [InlineData("application/aacp")]
    public void ContentTypeChoosesAac(string contentType)
    {
        using var decoder = StreamDecoders.Create(contentType, null, Stream.Null);
        Assert.IsType<AacStreamDecoder>(decoder);
    }

    [Theory]
    [InlineData("audio/mpeg")]
    [InlineData("audio/mp3")]
    public void ContentTypeChoosesMp3(string contentType)
    {
        using var decoder = StreamDecoders.Create(contentType, null, Stream.Null);
        Assert.IsType<Mp3StreamDecoder>(decoder);
    }

    [Fact]
    public void TheServerBeatsTheDirectoryWhenTheyDisagree()
    {
        // Radio Browser's codec column describes the station, not the bytes arriving today.
        using var decoder = StreamDecoders.Create("audio/mpeg", "AAC", Stream.Null);
        Assert.IsType<Mp3StreamDecoder>(decoder);
    }

    [Fact]
    public void TheDirectoryDecidesWhenTheServerSaysNothingUseful()
    {
        using var aac = StreamDecoders.Create(null, "AAC+", Stream.Null);
        Assert.IsType<AacStreamDecoder>(aac);

        using var mp3 = StreamDecoders.Create("application/octet-stream", "MP3", Stream.Null);
        Assert.IsType<Mp3StreamDecoder>(mp3);
    }

    [Fact]
    public void AnUnknownStreamIsTreatedAsMp3()
    {
        // Every station that played before this change had no content type worth trusting either.
        using var decoder = StreamDecoders.Create(null, null, Stream.Null);
        Assert.IsType<Mp3StreamDecoder>(decoder);
    }
}
