using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed partial class SetupOverlay : IDisposable
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float SlideSeconds = 0.5f;
    private const float ExitSeconds = 0.7f;
    private const int DisplayNameMax = 32;
    private const int HandleMax = 15;
    private static readonly Vector4 InkStrong = new(1f, 1f, 1f, 0.98f);
    private static readonly Vector4 InkMuted = new(0.74f, 0.77f, 0.85f, 1f);
    private static readonly Vector4 CardFill = new(1f, 1f, 1f, 0.07f);
    private static readonly Vector4 CardStroke = new(1f, 1f, 1f, 0.10f);

    private enum SetupPage
    {
        Welcome,
        Account,
        Profile,
        Photo,
        Features,
        Feedback,
        Ready,
    }

    private static readonly SetupPage[] Order =
    {
        SetupPage.Welcome, SetupPage.Account, SetupPage.Profile, SetupPage.Photo,
        SetupPage.Features, SetupPage.Feedback, SetupPage.Ready,
    };

    private readonly AethernetSession session;
    private readonly AccountClient account;
    private readonly MediaClient media;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly INavigator navigation;
    private readonly SignInFlow flow;
    private readonly ImagePickCrop picker;
    private readonly CancellationTokenSource cancellation = new();

    private SetupPage page = SetupPage.Welcome;
    private SetupPage fromPage = SetupPage.Welcome;
    private float slideClock = SlideSeconds;
    private int slideDirection = 1;
    private bool exiting;
    private float exitClock;

    private string displayNameDraft = string.Empty;
    private string handleDraft = string.Empty;
    private bool profilePrefilled;
    private volatile bool profileSaving;
    private volatile int profileOutcome;
    private bool handleRejected;

    private bool pickingPhoto;
    private volatile bool avatarBusy;
    private volatile int avatarOutcome;

    public SetupOverlay(AethernetSession session, AethernetApi aethernet, GameData gameData,
        RemoteImageCache images, LodestoneService lodestone, PhotoLibrary photoLibrary,
        WallpaperImageCache wallpaperImages, INavigator navigation, Configuration configuration,
        ConfirmService confirm)
    {
        this.session = session;
        account = aethernet.Account;
        media = aethernet.Media;
        this.gameData = gameData;
        this.images = images;
        this.lodestone = lodestone;
        this.navigation = navigation;
        this.configuration = configuration;
        this.confirm = confirm;
        flow = new SignInFlow(session, aethernet.Auth);
        picker = new ImagePickCrop(photoLibrary, wallpaperImages);
    }

    public bool IsActive => !configuration.SetupCompleted || exiting;

    public void Draw(Rect screen, PhoneTheme theme, float delta, bool interactive)
    {
        if (!IsActive)
        {
            return;
        }

        if (flow.ConsumeFailure() is { } failureReason)
        {
            var (title, message) = SignInFailureText.Resolve(failureReason, gameData);
            confirm.Alert(title, message, Loc.T(L.Account.FailDismiss));
        }

        ConsumeOutcomes();
        slideClock = MathF.Min(slideClock + delta, SlideSeconds);
        var exitProgress = 0f;
        if (exiting)
        {
            exitClock += delta;
            exitProgress = Easing.Clamp01(exitClock / ExitSeconds);
            if (exitProgress >= 1f)
            {
                exiting = false;
                return;
            }
        }

        var scale = ImGuiHelpers.GlobalScale;
        var rounding = theme.ScreenRounding * scale;
        var backdropAlpha = 1f - Easing.EaseOutCubic(exitProgress);
        var contentAlpha = 1f - Easing.Clamp01(exitProgress * 1.8f);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##setupOverlay", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            BootScreen.DrawBackdrop(drawList, screen, theme, backdropAlpha, rounding);
            var slide = Easing.EaseOutQuint(Easing.Clamp01(slideClock / SlideSeconds));
            var live = interactive && !exiting && slide >= 0.99f;
            if (slide < 1f && fromPage != page)
            {
                var exitOffset = new Vector2(-slideDirection * screen.Width * 0.3f * slide, 0f);
                DrawPage(fromPage, screen, theme, exitOffset, (1f - slide) * contentAlpha, false);
            }

            var enterOffset = new Vector2(slideDirection * screen.Width * (1f - slide), 0f);
            DrawPage(page, screen, theme, enterOffset, MathF.Min(slide + 0.35f, 1f) * contentAlpha, live);
            DrawBackButton(drawList, screen, theme, contentAlpha, live);
        }
    }

    private void DrawPage(SetupPage target, Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        switch (target)
        {
            case SetupPage.Welcome:
                DrawWelcome(screen, theme, offset, alpha, live);
                break;
            case SetupPage.Account:
                DrawAccount(screen, theme, offset, alpha, live);
                break;
            case SetupPage.Profile:
                DrawProfile(screen, theme, offset, alpha, live);
                break;
            case SetupPage.Photo:
                DrawPhoto(screen, theme, offset, alpha, live);
                break;
            case SetupPage.Features:
                DrawFeatures(screen, theme, offset, alpha, live);
                break;
            case SetupPage.Feedback:
                DrawFeedback(screen, theme, offset, alpha, live);
                break;
            case SetupPage.Ready:
                DrawReady(screen, theme, offset, alpha, live);
                break;
        }
    }

    private void ConsumeOutcomes()
    {
        var profile = profileOutcome;
        if (profile != 0)
        {
            profileOutcome = 0;
            if (profile == 1)
            {
                AdvancePage();
            }
            else
            {
                handleRejected = true;
            }
        }

        var avatar = avatarOutcome;
        if (avatar != 0)
        {
            avatarOutcome = 0;
            if (avatar == 1)
            {
                pickingPhoto = false;
            }
            else
            {
                confirm.Alert(null, Loc.T(L.Account.CannotReach), Loc.T(L.Account.FailDismiss));
            }
        }
    }

    private bool IsSkipped(SetupPage candidate) =>
        candidate is SetupPage.Profile or SetupPage.Photo && !session.IsSignedIn;

    private void AdvancePage()
    {
        var index = Array.IndexOf(Order, page);
        var nextIndex = index + 1;
        while (nextIndex < Order.Length && IsSkipped(Order[nextIndex]))
        {
            nextIndex++;
        }

        if (nextIndex >= Order.Length)
        {
            return;
        }

        BeginSlide(Order[nextIndex], 1);
    }

    private void BackPage()
    {
        var index = Array.IndexOf(Order, page);
        var previousIndex = index - 1;
        while (previousIndex > 0 && IsSkipped(Order[previousIndex]))
        {
            previousIndex--;
        }

        if (previousIndex < 0)
        {
            return;
        }

        BeginSlide(Order[previousIndex], -1);
    }

    private void BeginSlide(SetupPage target, int direction)
    {
        fromPage = page;
        page = target;
        slideDirection = direction;
        slideClock = 0f;
    }

    private void Complete()
    {
        configuration.SetupCompleted = true;
        configuration.Save();
        exiting = true;
        exitClock = 0f;
    }

    private void DrawBackButton(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float alpha, bool live)
    {
        if (page == SetupPage.Welcome || pickingPhoto || exiting)
        {
            return;
        }

        if (page == SetupPage.Account && (flow.XivAuthActive || flow.LodestoneActive))
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var center = new Vector2(screen.Min.X + 26f * scale, screen.Min.Y + 30f * scale);
        var half = 16f * scale;
        var hovered = live && ImGui.IsMouseHoveringRect(center - new Vector2(half, half),
            center + new Vector2(half, half));
        var ink = hovered ? InkStrong : InkMuted;
        AppSkin.Icon(drawList, center, FontAwesomeIcon.ChevronLeft.ToIconString(), ink with { W = ink.W * alpha },
            0.95f);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            BackPage();
        }
    }

    private static bool IsHandleValid(string handle)
    {
        if (handle.Length < 3 || handle.Length > HandleMax)
        {
            return false;
        }

        for (var index = 0; index < handle.Length; index++)
        {
            var character = handle[index];
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static string SanitizeHandle(string raw)
    {
        Span<char> buffer = stackalloc char[HandleMax];
        var written = 0;
        var changed = raw.Length > HandleMax;
        for (var index = 0; index < raw.Length && written < HandleMax; index++)
        {
            var character = char.ToLowerInvariant(raw[index]);
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')
            {
                changed |= character != raw[index];
                buffer[written++] = character;
            }
            else
            {
                changed = true;
            }
        }

        return changed ? new string(buffer[..written]) : raw;
    }

    private void SaveProfile()
    {
        var display = displayNameDraft.Trim();
        var handle = handleDraft;
        var user = session.CurrentUser;
        if (user is not null && string.Equals(display, user.DisplayName, StringComparison.Ordinal) &&
            string.Equals(handle, user.Handle, StringComparison.Ordinal))
        {
            AdvancePage();
            return;
        }

        profileSaving = true;
        handleRejected = false;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var updated = await account
                    .UpdateProfileAsync(new UpdateProfileRequest(display, handle, null, null), token)
                    .ConfigureAwait(false);
                if (updated is null)
                {
                    profileOutcome = 2;
                    return;
                }

                session.SetUser(updated);
                profileOutcome = 1;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Setup profile update failed: {exception.Message}");
                profileOutcome = 2;
            }
            finally
            {
                profileSaving = false;
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        flow.Dispose();
        cancellation.Dispose();
    }
}
