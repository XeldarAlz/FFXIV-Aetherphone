using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class PhotoWindow : Window
{
    private const ImGuiWindowFlags PhotoFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    private const string WindowId = "###AetherphonePhoto";
    private const float MinimumSide = 220f;
    private const float MaximumSide = 4096f;
    private const float ViewportFraction = 0.62f;
    private const float SpinnerRadius = 13f;

    private readonly ThemeProvider themes;
    private Func<IDalamudTextureWrap?>? source;
    private IPhoneApp? owner;
    private bool fitPending;

    public PhotoWindow(ThemeProvider themes)
        : base($"{AepConstants.Name}{WindowId}", PhotoFlags)
    {
        this.themes = themes;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinimumSide, MinimumSide),
            MaximumSize = new Vector2(MaximumSide, MaximumSide),
        };
    }

    public void Open(Func<IDalamudTextureWrap?> textureSource, IPhoneApp app)
    {
        source = textureSource;
        owner = app;
        fitPending = true;
        IsOpen = true;
        BringToFront();
    }

    public override void OnClose()
    {
        source = null;
        owner = null;
    }

    public override void PreDraw()
    {
        WindowName = $"{owner?.DisplayName ?? AepConstants.Name}{WindowId}";
        Size = null;
        if (!fitPending)
        {
            return;
        }

        var texture = source?.Invoke();
        if (texture is null)
        {
            return;
        }

        Size = InitialSize(texture.Size) / UiScale.Global;
        SizeCondition = ImGuiCond.Always;
        fitPending = false;
    }

    public override void Draw()
    {
        if (source is null)
        {
            IsOpen = false;
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var region = ImGui.GetContentRegionAvail();
        if (region.X <= 1f || region.Y <= 1f)
        {
            return;
        }

        var area = new Rect(origin, origin + region);
        var texture = source.Invoke();
        if (texture is null)
        {
            var theme = themes.Chrome;
            LoadingPulse.Draw(area.Center, SpinnerRadius * UiScale.Current, theme.Accent, theme.TextMuted,
                LoadingPulse.SafeLabel());
            return;
        }

        var frame = ImageFit.CenteredRect(area, texture.Size.X / MathF.Max(texture.Size.Y, 1f));
        ImGui.GetWindowDrawList().AddImage(texture.Handle, frame.Min, frame.Max);
    }

    private static Vector2 InitialSize(Vector2 imageSize)
    {
        var work = ImGui.GetMainViewport().WorkSize;
        var room = new Vector2(work.X * ViewportFraction, work.Y * ViewportFraction);
        var fit = MathF.Min(1f, MathF.Min(room.X / MathF.Max(imageSize.X, 1f), room.Y / MathF.Max(imageSize.Y, 1f)));
        var content = imageSize * fit;
        var padding = ImGui.GetStyle().WindowPadding * 2f;
        var chrome = new Vector2(padding.X, padding.Y + ImGui.GetFrameHeight());
        var minimum = MinimumSide * UiScale.Global;
        return new Vector2(MathF.Max(content.X, minimum), MathF.Max(content.Y, minimum)) + chrome;
    }
}
