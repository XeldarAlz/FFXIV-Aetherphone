using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Camera;

internal sealed class CameraApp : IPhoneApp
{
    private const float TopBarHeight = 64f;
    private const float TrayHeight = 172f;
    private const float FlashDuration = 0.42f;
    private const float ReticleDuration = 1.1f;
    private const float PressDuration = 0.18f;
    private const int SquareModeIndex = 0;
    private static readonly LocString[] Modes = { L.Camera.ModeSquare, L.Camera.ModePhoto };
    public string Id => "camera";
    public string DisplayName => Loc.T(L.Apps.Camera);
    public string Glyph => "O";
    public int BadgeCount => 0;
    public bool WantsTransparentScreen => true;

    public Rect? TransparentViewport(Rect screen, float scale) =>
        new(new Vector2(screen.Min.X, screen.Min.Y + TopBarHeight * scale),
            new Vector2(screen.Max.X, screen.Max.Y - TrayHeight * scale));

    private readonly PhotoCaptureService capture;
    private readonly PhotoLibrary library;
    private int modeIndex = 1;
    private bool gridEnabled;
    private bool flashEnabled = true;
    private float shutterPress;
    private float flashAge = FlashDuration + 1f;
    private float reticleAge = ReticleDuration + 1f;
    private Vector2 reticlePos;
    private IDalamudTextureWrap? lastShot;

    public CameraApp(PhotoCaptureService capture, PhotoLibrary library)
    {
        this.capture = capture;
        this.library = library;
    }

    public void OnOpened()
    {
        flashAge = FlashDuration + 1f;
        reticleAge = ReticleDuration + 1f;
        shutterPress = 0f;
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var rounding = theme.ScreenRounding * scale;
        AdvanceTimers(ImGui.GetIO().DeltaTime);
        var screen = ScreenFrom(context.Content, theme, scale);
        var viewfinder = new Rect(new Vector2(screen.Min.X, screen.Min.Y + TopBarHeight * scale),
            new Vector2(screen.Max.X, screen.Max.Y - TrayHeight * scale));
        var captureRect = CaptureRect(viewfinder);

        var consumed = CameraChrome.TopBar(screen, TopBarHeight, flashEnabled, scale, rounding);
        if (consumed)
        {
            flashEnabled = !flashEnabled;
        }

        CameraChrome.Viewfinder(viewfinder, captureRect, gridEnabled, reticleAge, ReticleDuration, reticlePos, scale);
        consumed |= DrawTray(screen, captureRect, context.Navigation, scale, rounding);
        HandleFocusTap(viewfinder, consumed);
        CameraChrome.Flash(screen, flashAge, FlashDuration, rounding);
    }

    private void AdvanceTimers(float delta)
    {
        if (shutterPress > 0f)
        {
            shutterPress = MathF.Max(0f, shutterPress - delta / PressDuration);
        }

        if (flashAge <= FlashDuration)
        {
            flashAge += delta;
        }

        if (reticleAge <= ReticleDuration)
        {
            reticleAge += delta;
        }
    }

    private bool DrawTray(Rect screen, Rect captureRect, INavigator navigation, float scale, float rounding)
    {
        var trayTop = screen.Max.Y - TrayHeight * scale;
        CameraChrome.TrayBackground(screen, trayTop, rounding);

        var newMode = CameraChrome.ModeCarousel(screen, trayTop + 22f * scale, Modes, modeIndex, scale);
        var consumed = newMode != modeIndex;
        modeIndex = newMode;

        var shutterCenter = new Vector2(screen.Center.X, trayTop + 92f * scale);
        if (CameraChrome.Shutter(shutterCenter, shutterPress, scale))
        {
            Shoot(captureRect);
            consumed = true;
        }

        if (CameraChrome.ThumbnailWell(new Vector2(screen.Min.X + 44f * scale, shutterCenter.Y), lastShot, scale))
        {
            navigation.Open("photos");
            consumed = true;
        }

        if (CameraChrome.GridToggle(new Vector2(screen.Max.X - 44f * scale, shutterCenter.Y), gridEnabled, scale))
        {
            gridEnabled = !gridEnabled;
            consumed = true;
        }

        return consumed;
    }

    private void Shoot(Rect captureRect)
    {
        shutterPress = 1f;
        if (flashEnabled)
        {
            flashAge = 0f;
        }

        if (!capture.TryCapture(captureRect, out var pixels, out var width, out var height))
        {
            return;
        }

        lastShot?.Dispose();
        lastShot = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(width, height), pixels,
            "Aetherphone.Photo.Last");
        library.Save(pixels, width, height);
    }

    private void HandleFocusTap(Rect viewfinder, bool consumed)
    {
        if (consumed || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var mouse = ImGui.GetMousePos();
        if (!viewfinder.Contains(mouse))
        {
            return;
        }

        reticlePos = mouse;
        reticleAge = 0f;
    }

    private Rect CaptureRect(Rect viewfinder)
    {
        if (modeIndex != SquareModeIndex)
        {
            return viewfinder;
        }

        var side = MathF.Min(viewfinder.Width, viewfinder.Height);
        var center = viewfinder.Center;
        var half = new Vector2(side * 0.5f, side * 0.5f);
        return new Rect(center - half, center + half);
    }

    private static Rect ScreenFrom(Rect content, PhoneTheme theme, float scale)
    {
        var min = new Vector2(content.Min.X - theme.SidePadding * scale, content.Min.Y - theme.TopZoneHeight * scale);
        var max = new Vector2(content.Max.X + theme.SidePadding * scale,
            content.Max.Y + theme.BottomZoneHeight * scale);
        return new Rect(min, max);
    }

    public void Dispose()
    {
        lastShot?.Dispose();
        lastShot = null;
    }
}
