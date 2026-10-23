using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace Aetherphone.Harness.Fonts;

internal sealed class FakeFontHandle : IFontHandle
{
    private static readonly PopFontToken PopToken = new();
    private readonly FakeFontAtlas atlas;
    private readonly FontAtlasBuildStepDelegate buildStep;

    public FakeFontHandle(FakeFontAtlas atlas, FontAtlasBuildStepDelegate buildStep)
    {
        this.atlas = atlas;
        this.buildStep = buildStep;
    }

    public event IFontHandle.ImFontChangedDelegate? ImFontChanged { add { } remove { } }

    public ImFontPtr Font { get; private set; }

    public Exception? LoadException { get; private set; }

    public bool Available => !Font.IsNull;

    public ILockedImFont Lock() => new FakeLockedFont(Available ? Font : ImGui.GetFont());

    public ILockedImFont TryLock(out string? errorMessage)
    {
        errorMessage = Available ? null : "Font is not built yet.";
        return Lock();
    }

    public IDisposable Push()
    {
        ImGui.PushFont(Available ? Font : ImGui.GetFont());
        return PopToken;
    }

    public void Pop() => ImGui.PopFont();

    public Task<IFontHandle> WaitAsync() => Task.FromResult<IFontHandle>(this);

    public Task<IFontHandle> WaitAsync(CancellationToken cancellationToken) => WaitAsync();

    public void Dispose() => atlas.Remove(this);

    internal void RunPreBuild(FakeBuildToolkit toolkit)
    {
        toolkit.Font = default;
        try
        {
            buildStep(toolkit);
            Font = toolkit.Font;
            LoadException = null;
        }
        catch (Exception exception)
        {
            Font = default;
            LoadException = exception;
            Fakes.HarnessLog.Note($"font handle pre-build failed: {exception.Message}");
        }
    }

    internal void RunPostBuild(FakeBuildToolkit toolkit)
    {
        if (!Available)
        {
            return;
        }

        toolkit.Font = Font;
        try
        {
            buildStep(toolkit);
        }
        catch (Exception exception)
        {
            Fakes.HarnessLog.Note($"font handle post-build failed: {exception.Message}");
        }
    }

    private sealed class PopFontToken : IDisposable
    {
        public void Dispose() => ImGui.PopFont();
    }
}
