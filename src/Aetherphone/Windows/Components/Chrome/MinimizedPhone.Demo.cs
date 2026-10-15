using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum MinimizedDemoStage : byte
{
    Pages,
    Music,
    Call,
    Banner,
}

internal sealed partial class MinimizedPhone
{
    private const int DemoStageCount = 4;
    private const float DemoPagesSeconds = 4.5f;
    private const float DemoStageSeconds = 4f;
    private const int DemoCallSeconds = 42;
    private readonly bool demo;
    private MinimizedDemoStage demoStage;
    private float demoElapsed;
    private bool demoHeld;
    private MinimizedPart? focusPart;

    private string MusicArtworkUrl => demo ? string.Empty : playback.ArtworkUrl;

    private string MusicTitle => demo ? Loc.T(L.Minimized.NowPlaying) : playback.Title;

    private bool MusicPlaying => demo || playback.IsPlaying;

    private bool MusicHasQueue => demo || playback.HasQueue;

    public void DrawPreview(ImDrawListPtr drawList, Rect body, float delta)
    {
        var restore = UiScale.Minimized;
        UiScale.SetMinimized(body.Width / (MinimizedShapes.BodyWidth * UiScale.Global));
        var theme = themes.Chrome;
        var geometry = ChassisGeometry.Puck(body, theme.CaseKind);
        var hovered = UiInteract.Hover(body.Min, body.Max);
        demoHeld = hovered;
        DeviceChrome.DrawShell(drawList, geometry, Scale, theme, 1f);
        DrawFace(drawList, geometry, theme, delta, false, 1f);
        frameBodyHovered = hovered;
        HandleWheel(ShowsMinimap);
        UiScale.SetMinimized(restore);
    }

    public void ShowPart(MinimizedPart part)
    {
        if (MinimizedParts.IsPage(part))
        {
            focusPart = part;
        }

        EnterDemoStage(part switch
        {
            MinimizedPart.NowPlaying => MinimizedDemoStage.Music,
            MinimizedPart.Calls => MinimizedDemoStage.Call,
            MinimizedPart.Alerts => MinimizedDemoStage.Banner,
            _ => MinimizedDemoStage.Pages,
        });
    }

    private bool DemoShows(MinimizedDemoStage stage) => stage switch
    {
        MinimizedDemoStage.Music => layout.IsEnabled(MinimizedPart.NowPlaying),
        MinimizedDemoStage.Call => layout.IsEnabled(MinimizedPart.Calls),
        MinimizedDemoStage.Banner => layout.IsEnabled(MinimizedPart.Alerts),
        _ => true,
    };

    private void StepDemo(float delta)
    {
        if (!demoHeld)
        {
            demoElapsed += delta;
        }

        var duration = demoStage == MinimizedDemoStage.Pages ? DemoPagesSeconds : DemoStageSeconds;
        if (demoElapsed < duration && DemoShows(demoStage))
        {
            return;
        }

        EnterDemoStage(NextDemoStage());
    }

    private MinimizedDemoStage NextDemoStage()
    {
        for (var offset = 1; offset < DemoStageCount; offset++)
        {
            var stage = (MinimizedDemoStage)(((int)demoStage + offset) % DemoStageCount);
            if (DemoShows(stage))
            {
                return stage;
            }
        }

        return MinimizedDemoStage.Pages;
    }

    private void EnterDemoStage(MinimizedDemoStage stage)
    {
        if (demoStage == MinimizedDemoStage.Banner && cardNotification is not null)
        {
            cardDismissed = true;
        }

        demoStage = stage;
        demoElapsed = 0f;
        if (stage != MinimizedDemoStage.Banner)
        {
            return;
        }

        BeginCard(new PhoneNotification(CallAppId, Loc.T(L.Minimized.DemoCaller), Loc.T(L.Message.PreviewIncoming),
            DateTime.Now, AppAccents.For(CallAppId)));
    }

    private CallView DemoCall()
    {
        var state = demoStage == MinimizedDemoStage.Call ? CallState.Active : CallState.Idle;
        return new CallView(state, false, 1f, 0f, 0f, DemoCallSeconds + (int)demoElapsed,
            Array.Empty<ParticipantInfo>(), null, true, string.Empty, Loc.T(L.Minimized.DemoCaller), 0);
    }

    private void ApplyFocus()
    {
        if (focusPart is not { } part)
        {
            return;
        }

        focusPart = null;
        for (var index = 0; index < pageCount; index++)
        {
            if (pageParts[index] != part)
            {
                continue;
            }

            pageIndex = index;
            return;
        }
    }
}
