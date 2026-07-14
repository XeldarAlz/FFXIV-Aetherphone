using System.Numerics;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private string introName = string.Empty;
    private string introText = string.Empty;

    private void DrawMessages(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var pad = Metrics.Space.Lg * scale;
        var segRect = new Rect(new Vector2(area.Min.X + pad, area.Min.Y + 8f * scale),
            new Vector2(area.Max.X - pad, area.Min.Y + 8f * scale + 32f * scale));
        var requestCount = store.RequestCount;
        var labels = new[] { "Chats", requestCount > 0 ? "Requests (" + requestCount + ")" : "Requests" };
        var picked = VSegmented.Draw("velvetMessages", segRect, labels, (int)messagesTab, scale);
        if (picked >= 0)
        {
            messagesTab = (VelvetMessagesTab)picked;
        }

        var listRect = new Rect(new Vector2(area.Min.X, segRect.Max.Y + 8f * scale), area.Max);
        using (AppSurface.Begin(listRect))
        {
            if (messagesTab == VelvetMessagesTab.Chats)
            {
                DrawChatsList(listRect);
            }
            else
            {
                DrawRequestsList(listRect);
            }
        }
    }

    private void DrawChatsList(Rect listRect)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (!store.ThreadsLoaded && !store.LoadingThreads)
        {
            store.RefreshThreads();
        }

        var threads = store.Threads;
        if (threads.Length == 0)
        {
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale),
                "No conversations yet", VelvetTheme.TitleInk, TextStyles.Headline);
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 106f * scale),
                "Send an intro from Discover.", VelvetTheme.MutedInk, TextStyles.Subheadline);
            return;
        }

        Gap(6f);
        for (var index = 0; index < threads.Length; index++)
        {
            var thread = threads[index];
            var preview = string.IsNullOrEmpty(thread.LastMessagePreview) ? "Say hello" : thread.LastMessagePreview;
            var model = new VRowModel
            {
                Title = DisplayNameOf(thread.OtherDisplayName, thread.OtherHandle),
                Subtitle = preview,
                Height = 64f,
                Leading = VRowLeading.Avatar,
                AvatarRadius = 22f,
                Name = DisplayNameOf(thread.OtherDisplayName, thread.OtherHandle),
                World = string.Empty,
                AvatarUrl = thread.OtherAvatarUrl,
                Presence = thread.Presence,
                Time = RelativeShort(thread.LastMessageAtUnix),
                Badge = thread.UnreadCount,
            };
            if (VRow.Draw(in model, ui, theme, images, lodestone) == VRowHit.Body)
            {
                OpenThread(thread.OtherUserId);
            }
        }

        Gap(40f);
    }

    private void DrawRequestsList(Rect listRect)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (!store.RequestsLoaded && !store.LoadingRequests)
        {
            store.RefreshRequests();
        }

        if (!store.SentRequestsLoaded && !store.LoadingSentRequests)
        {
            store.RefreshSentRequests();
        }

        var requests = store.Requests;
        var sent = store.SentRequests;
        if (requests.Length == 0 && sent.Length == 0)
        {
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale), "No requests",
                VelvetTheme.TitleInk, TextStyles.Headline);
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 106f * scale),
                "Intros you receive land here.", VelvetTheme.MutedInk, TextStyles.Subheadline);
            return;
        }

        Gap(8f);
        if (requests.Length > 0)
        {
            VSectionHeader.Overline("Requests", requests.Length.ToString());
            for (var index = 0; index < requests.Length; index++)
            {
                DrawRequestRow(requests[index]);
            }
        }

        if (sent.Length > 0)
        {
            Gap(14f);
            VSectionHeader.Overline("Sent", sent.Length.ToString());
            for (var index = 0; index < sent.Length; index++)
            {
                var request = sent[index];
                var model = new VRowModel
                {
                    Title = DisplayNameOf(request.DisplayName, request.Handle),
                    Subtitle = "@" + request.Handle,
                    Height = 60f,
                    Leading = VRowLeading.Avatar,
                    AvatarRadius = 20f,
                    Name = DisplayNameOf(request.DisplayName, request.Handle),
                    AvatarUrl = request.AvatarUrl,
                    Pill = "Requested",
                    PillFilled = false,
                    PillEnabled = true,
                };
                var hit = VRow.Draw(in model, ui, theme, images, lodestone);
                if (hit == VRowHit.Pill)
                {
                    store.CancelRequest(request.UserId);
                }
                else if (hit == VRowHit.Body)
                {
                    OpenProfile(request.UserId);
                }
            }
        }

        Gap(40f);
    }

    private void DrawRequestRow(VelvetConnectionDto request)
    {
        var model = new VRowModel
        {
            Title = DisplayNameOf(request.DisplayName, request.Handle),
            Subtitle = IntroLineOf(request),
            Height = 64f,
            Leading = VRowLeading.Avatar,
            AvatarRadius = 22f,
            Name = DisplayNameOf(request.DisplayName, request.Handle),
            AvatarUrl = request.AvatarUrl,
            Pill = "Accept",
            PillFilled = true,
            PillEnabled = true,
            Decline = true,
        };
        var hit = VRow.Draw(in model, ui, theme, images, lodestone);
        switch (hit)
        {
            case VRowHit.Pill:
                store.AcceptRequest(request.UserId);
                OpenThread(request.UserId);
                break;
            case VRowHit.Decline:
                store.DeclineRequest(request.UserId);
                break;
            case VRowHit.Body:
                OpenProfile(request.UserId);
                break;
        }
    }

    private void RequestIntro(string userId, string displayName)
    {
        introName = displayName;
        introText = string.Empty;
        router.Push(VelvetView.Intro(userId));
    }

    private void DrawIntro(Rect area, string userId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (VHeader.Push(area, "Send an intro", theme))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(30f);
            var drawList = ImGui.GetWindowDrawList();
            var width = ImGui.GetContentRegionAvail().X;
            var centerX = ImGui.GetCursorScreenPos().X + width * 0.5f;
            var moonY = ImGui.GetCursorScreenPos().Y + 22f * scale;
            VelvetArt.Moon(drawList, new Vector2(centerX, moonY), 18f * scale, VelvetTheme.Moonlight,
                VelvetTheme.GroundTop);
            Gap(80f);
            Typography.DrawWrappedCentered(new Vector2(centerX, ImGui.GetCursorScreenPos().Y),
                "Introduce yourself to " + introName, VelvetTheme.TitleInk, TextStyles.Title3, width - 48f * scale);
            Gap(50f);

            ui.Field("Your intro", "##introText", ref introText, 140, true);
            ui.HelpText("Your intro lands in their Requests. A reply accepts you.");
            Gap(16f);

            var sendRect = Reserve(46f);
            var canSend = introText.Trim().Length > 0;
            if (canSend)
            {
                if (ui.PillButton(sendRect, "Send intro", true))
                {
                    SendIntro(userId);
                }
            }
            else
            {
                Squircle.Fill(drawList, sendRect.Min, sendRect.Max, sendRect.Height * 0.5f,
                    VelvetTheme.Alpha(VelvetTheme.Rose, 0.35f).Packed());
                Typography.DrawCentered(sendRect.Center, "Send intro", VelvetTheme.Alpha(VelvetTheme.OnAccent, 0.6f),
                    0.9f, FontWeight.SemiBold);
            }

            Gap(40f);
        }
    }

    private void SendIntro(string userId)
    {
        store.Connect(userId, introText.Trim());
        introText = string.Empty;
        router.Pop();
    }

    private static string IntroLineOf(VelvetConnectionDto request) =>
        string.IsNullOrWhiteSpace(request.Intro) ? "wants to connect" : request.Intro;

    private static string RelativeShort(long unix)
    {
        if (unix <= 0)
        {
            return string.Empty;
        }

        var then = DateTimeOffset.FromUnixTimeSeconds(unix);
        var span = DateTimeOffset.UtcNow - then;
        if (span.TotalMinutes < 1)
        {
            return "now";
        }

        if (span.TotalHours < 1)
        {
            return (int)span.TotalMinutes + "m";
        }

        if (span.TotalDays < 1)
        {
            return (int)span.TotalHours + "h";
        }

        if (span.TotalDays < 7)
        {
            return (int)span.TotalDays + "d";
        }

        return then.ToLocalTime().ToString("MMM d");
    }
}
