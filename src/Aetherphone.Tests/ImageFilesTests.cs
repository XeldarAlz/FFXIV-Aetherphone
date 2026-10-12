using Aetherphone.Core.Media;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ImageFilesTests
{
    [Theory]
    [InlineData(@"C:\Shots\snip.png")]
    [InlineData(@"C:\Camera\IMG_0042.JPG")]
    [InlineData(@"D:\memes\dance.gif")]
    [InlineData(@"C:\Downloads\sticker.webp")]
    public void ImageExtensionsMatchInAnyCase(string path)
    {
        Assert.True(ImageFiles.IsImage(path));
    }

    [Theory]
    [InlineData(@"C:\Docs\notes.docx")]
    [InlineData(@"C:\Docs\png")]
    [InlineData(@"C:\Folder.png\readme")]
    public void OtherFilesAreNotImages(string path)
    {
        Assert.False(ImageFiles.IsImage(path));
    }
}
