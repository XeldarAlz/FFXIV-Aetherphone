using System.Diagnostics.CodeAnalysis;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeSharedTexture : ISharedImmediateTexture
{
    private readonly Func<FakeTextureWrap> loader;
    private readonly FakeTextureWrap empty;
    private FakeTextureWrap? wrap;
    private Exception? failure;
    private bool attempted;

    public FakeSharedTexture(Func<FakeTextureWrap> loader, FakeTextureWrap empty)
    {
        this.loader = loader;
        this.empty = empty;
    }

    public IDalamudTextureWrap GetWrapOrEmpty() => Resolve() ?? empty;

    public IDalamudTextureWrap? GetWrapOrDefault(IDalamudTextureWrap? defaultWrap = null) => Resolve() ?? defaultWrap;

    public bool TryGetWrap([NotNullWhen(true)] out IDalamudTextureWrap? texture, out Exception? exception)
    {
        texture = Resolve();
        exception = failure;
        return texture is not null;
    }

    public Task<IDalamudTextureWrap> RentAsync(CancellationToken cancellationToken = default)
    {
        var resolved = Resolve();
        return resolved is null
            ? Task.FromException<IDalamudTextureWrap>(failure ?? new InvalidOperationException("Texture unavailable."))
            : Task.FromResult<IDalamudTextureWrap>(resolved);
    }

    private FakeTextureWrap? Resolve()
    {
        if (attempted)
        {
            return wrap;
        }

        attempted = true;
        try
        {
            wrap = loader();
        }
        catch (Exception exception)
        {
            failure = exception;
            HarnessLog.Note($"texture load failed: {exception.Message}");
        }

        return wrap;
    }
}
