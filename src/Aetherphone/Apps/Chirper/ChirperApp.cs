using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

internal enum ChirperRoute
{
    Feed,
    Discover,
}

internal sealed class ChirperApp : IPhoneApp
{
    private const float FeedRefreshSeconds = 20f;
    private const int MaxPostLength = 500;

    public string Id => "chirper";

    public string DisplayName => "Chirper";

    public string Glyph => "Ch";

    public Vector4 Accent => new(0.33f, 0.67f, 0.93f, 1f);

    public int BadgeCount => 0;

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly LodestoneService lodestone;

    private readonly ViewRouter<ChirperRoute> router;
    private readonly RouterDraw<ChirperRoute> drawView;
    private readonly Action backToFeed;
    private readonly CancellationTokenSource cancellation = new();

    private volatile PostDto[] feed = Array.Empty<PostDto>();
    private volatile UserDto[] discoverResults = Array.Empty<UserDto>();
    private volatile bool feedBusy;
    private volatile bool searching;
    private volatile bool posting;

    private string draft = string.Empty;
    private string searchDraft = string.Empty;
    private float sinceFeed;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public ChirperApp(AethernetSession session, AethernetClient client, LodestoneService lodestone)
    {
        this.session = session;
        this.client = client;
        this.lodestone = lodestone;

        router = new ViewRouter<ChirperRoute>(ChirperRoute.Feed);
        drawView = DrawView;
        backToFeed = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        if (session.IsSignedIn)
        {
            RefreshFeed();
        }
    }

    public void OnClosed()
    {
        router.Reset();
        draft = string.Empty;
        searchDraft = string.Empty;
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;

        var delta = ImGui.GetIO().DeltaTime;
        sinceFeed += delta;
        if (session.IsSignedIn && router.Current == ChirperRoute.Feed && !feedBusy && sinceFeed >= FeedRefreshSeconds)
        {
            RefreshFeed();
        }

        router.Draw(context.Content, context.Theme.AppBackground, delta, drawView);
    }

    private void DrawView(ChirperRoute route, Rect area, int depth)
    {
        if (route == ChirperRoute.Discover)
        {
            DrawDiscover(area);
        }
        else
        {
            DrawFeed(area);
        }
    }

    private void DrawFeed(Rect area)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;

        if (!session.IsSignedIn)
        {
            var prompt = new Rect(new Vector2(area.Min.X, top), area.Max);
            Typography.DrawCentered(prompt.Center, "Set up your account in Settings", frameTheme.TextMuted);
            return;
        }

        if (DrawDiscoverButton(context))
        {
            router.Push(ChirperRoute.Discover);
        }

        var composerHeight = 52f * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));
        var snapshot = feed;

        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                Typography.DrawCentered(listRect.Center, "No chirps yet — post the first one", frameTheme.TextMuted);
            }
            else
            {
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawPost(snapshot[index], frameTheme);
                }
            }
        }

        DrawComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), frameTheme);
    }

    private void DrawDiscover(Rect area)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, "Find People", backToFeed);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchHeight = 52f * scale;

        DrawSearchBar(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)), frameTheme);

        var listRect = new Rect(new Vector2(area.Min.X, top + searchHeight), area.Max);
        var snapshot = discoverResults;
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                Typography.DrawCentered(listRect.Center, searching ? "Searching…" : "Search by name or name@world", frameTheme.TextMuted);
            }
            else
            {
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawUserRow(snapshot[index], frameTheme);
                }
            }
        }
    }

    private void DrawPost(PostDto post, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 18f * scale;
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + radius);
        AvatarView.Draw(dl, avatarCenter, radius, theme.Accent, Initials.Of(post.AuthorName), 0.95f, lodestone.Avatar(post.AuthorName, post.AuthorWorld), 32);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(radius * 2f, radius * 2f));
        ImGui.SameLine(0f, 10f * scale);

        ImGui.BeginGroup();
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextUnformatted(post.AuthorName);
        }

        ImGui.SameLine(0f, 6f * scale);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextUnformatted($"@{post.AuthorWorld} · {RelativeTime(post.CreatedAtUnix)}");
        }

        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextWrapped(post.Text);
        }

        ImGui.PopTextWrapPos();

        if (post.Likes > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextUnformatted(post.Likes == 1 ? "1 like" : $"{post.Likes} likes");
            }
        }

        ImGui.EndGroup();

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        var sepY = ImGui.GetCursorScreenPos().Y;
        dl.AddLine(new Vector2(origin.X, sepY), new Vector2(origin.X + ImGui.GetContentRegionAvail().X, sepY), ImGui.GetColorU32(theme.SurfaceMuted), 1f);
        ImGui.Dummy(new Vector2(0f, 6f * scale));
    }

    private void DrawUserRow(UserDto user, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 54f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var dl = ImGui.GetWindowDrawList();
        var radius = 18f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(dl, avatarCenter, radius, theme.Accent, Initials.Of(user.Name), 0.95f, lodestone.Avatar(user.Name, user.World), 32);

        var textLeft = origin.X + radius * 2f + 10f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + 9f * scale), user.DisplayName, theme.TextStrong);
        Typography.Draw(new Vector2(textLeft, origin.Y + 30f * scale), $"{user.Name}@{user.World}", theme.TextMuted, 0.85f);

        var buttonWidth = 92f * scale;
        var buttonHeight = 28f * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + width - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f));
        var label = user.IsFollowing ? "Following" : "Follow";
        var fill = user.IsFollowing ? theme.SurfaceMuted : theme.Accent;
        using (ImRaii.PushColor(ImGuiCol.Button, fill)
            .Push(ImGuiCol.ButtonHovered, Palette.Mix(fill, theme.TextStrong, 0.15f))
            .Push(ImGuiCol.ButtonActive, fill)
            .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            if (ImGui.Button($"{label}##{user.Id}", new Vector2(buttonWidth, buttonHeight)))
            {
                ToggleFollow(user);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawSearchBar(Rect bar, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - 12f * scale, bar.Max.Y - 9f * scale);
        dl.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(theme.GroupedCard), (pillMax.Y - pillMin.Y) * 0.5f);

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 28f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##chirperSearch", "Name or Name@World", ref searchDraft, 64, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                StartSearch(searchDraft);
            }
        }
    }

    private void DrawComposer(Rect bar, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X, bar.Min.Y + 7f * scale);
        var pillMax = new Vector2(bar.Max.X, bar.Max.Y - 7f * scale);
        dl.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(theme.GroupedCard), (pillMax.Y - pillMin.Y) * 0.5f);

        var sendDiameter = pillMax.Y - pillMin.Y - 6f * scale;
        var inputWidth = pillMax.X - pillMin.X - sendDiameter - 30f * scale;

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 16f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(inputWidth);

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##chirperComposer", "Chirp something", ref draft, MaxPostLength, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var hasText = !string.IsNullOrWhiteSpace(draft);
        var sendCenter = new Vector2(pillMax.X - sendDiameter * 0.5f - 6f * scale, (pillMin.Y + pillMax.Y) * 0.5f);
        dl.AddCircleFilled(sendCenter, sendDiameter * 0.5f, ImGui.GetColorU32(hasText ? theme.Accent : theme.SurfaceMuted), 24);
        Typography.DrawCentered(sendCenter, ">", new Vector4(1f, 1f, 1f, 1f), 1.1f);

        var sendMin = sendCenter - new Vector2(sendDiameter * 0.5f, sendDiameter * 0.5f);
        var sendMax = sendCenter + new Vector2(sendDiameter * 0.5f, sendDiameter * 0.5f);
        if (hasText && ImGui.IsMouseHoveringRect(sendMin, sendMax))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                submitted = true;
            }
        }

        if (submitted && hasText && !posting)
        {
            SubmitPost(draft.Trim());
            draft = string.Empty;
        }
    }

    private bool DrawDiscoverButton(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var center = new Vector2(content.Max.X - 14f * scale, content.Min.Y + AppHeader.Height * scale * 0.5f);
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(16f * scale, 16f * scale), center + new Vector2(16f * scale, 16f * scale));

        var glyph = FontAwesomeIcon.UserPlus.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, hovered ? context.Theme.TextStrong : context.Theme.Accent))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void RefreshFeed()
    {
        feedBusy = true;
        sinceFeed = 0f;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var page = await client.FeedAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                feed = page.Items;
            }

            feedBusy = false;
        });
    }

    private void SubmitPost(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        posting = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var created = await client.CreatePostAsync(text, token).ConfigureAwait(false);
            posting = false;
            if (created is not null)
            {
                RefreshFeed();
            }
        });
    }

    private void StartSearch(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            discoverResults = Array.Empty<UserDto>();
            return;
        }

        searching = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var result = await client.SearchAsync(trimmed, token).ConfigureAwait(false);
            if (result is not null)
            {
                discoverResults = result.Users;
            }

            searching = false;
        });
    }

    private void ToggleFollow(UserDto user)
    {
        var follow = !user.IsFollowing;
        var snapshot = discoverResults;
        var updated = new UserDto[snapshot.Length];
        for (var index = 0; index < snapshot.Length; index++)
        {
            updated[index] = snapshot[index].Id == user.Id
                ? snapshot[index] with { IsFollowing = follow, Followers = Math.Max(0, snapshot[index].Followers + (follow ? 1 : -1)) }
                : snapshot[index];
        }

        discoverResults = updated;

        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            if (follow)
            {
                await client.FollowAsync(user.Id, token).ConfigureAwait(false);
            }
            else
            {
                await client.UnfollowAsync(user.Id, token).ConfigureAwait(false);
            }
        });
    }

    private static string RelativeTime(long unixSeconds)
    {
        var moment = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        var span = DateTime.UtcNow - moment;
        if (span.TotalSeconds < 60)
        {
            return "now";
        }

        if (span.TotalMinutes < 60)
        {
            return $"{(int)span.TotalMinutes}m";
        }

        if (span.TotalHours < 24)
        {
            return $"{(int)span.TotalHours}h";
        }

        if (span.TotalDays < 7)
        {
            return $"{(int)span.TotalDays}d";
        }

        return moment.ToString("MMM d");
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
