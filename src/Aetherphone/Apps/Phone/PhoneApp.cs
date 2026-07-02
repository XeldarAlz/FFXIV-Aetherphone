using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Phone;

internal sealed class PhoneApp : IPhoneApp
{
    private static readonly Vector4 CallGreen = new(0.20f, 0.78f, 0.35f, 1f);

    public string Id => "phone";

    public string DisplayName => "Phone";

    public string Glyph => "Ph";

    public Vector4 Accent => CallGreen;

    public int BadgeCount => 0;

    private readonly CallHub calls;
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly LodestoneService lodestone;
    private readonly CancellationTokenSource cancellation = new();

    private volatile UserDto[] searchResults = Array.Empty<UserDto>();
    private volatile bool searching;
    private string searchDraft = string.Empty;
    private bool addingToCall;
    private float clock;

    public PhoneApp(CallHub calls, AethernetSession session, AethernetClient client, LodestoneService lodestone)
    {
        this.calls = calls;
        this.session = session;
        this.client = client;
        this.lodestone = lodestone;
    }

    public void OnOpened()
    {
        addingToCall = false;
    }

    public void OnClosed()
    {
        searchDraft = string.Empty;
        searchResults = Array.Empty<UserDto>();
        addingToCall = false;
    }

    public void Draw(in PhoneContext context)
    {
        clock += MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var view = calls.Snapshot();
        var inCall = view.State is CallState.Dialing or CallState.Connecting or CallState.Active;

        if (inCall && addingToCall && view.State == CallState.Active)
        {
            DrawDialer(context, view, true);
            return;
        }

        if (inCall)
        {
            addingToCall = false;
            DrawCallScreen(context, view);
            return;
        }

        addingToCall = false;
        DrawDialer(context, view, false);
    }

    private void DrawDialer(in PhoneContext context, CallView view, bool addMode)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;

        AppHeader.Draw(context, addMode ? "Add to Call" : "Phone", addMode ? StopAdding : null);

        var top = content.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);

        if (!session.IsSignedIn)
        {
            Typography.DrawCentered(body.Center, "Sign in to Aethernet in Settings to make calls", theme.TextMuted);
            return;
        }

        if (!calls.Enabled)
        {
            DrawEnablePrompt(body, theme, scale);
            return;
        }

        var searchHeight = 52f * scale;
        DrawSearchBar(new Rect(new Vector2(body.Min.X, top), new Vector2(body.Max.X, top + searchHeight)), theme, scale);

        var listRect = new Rect(new Vector2(body.Min.X, top + searchHeight), body.Max);
        var results = searchResults;
        var query = searchDraft.Trim();

        using (AppSurface.Begin(listRect))
        {
            if (query.Length > 0)
            {
                if (results.Length == 0)
                {
                    Typography.DrawCentered(listRect.Center, searching ? "Searching…" : "No one found", theme.TextMuted);
                }
                else
                {
                    for (var index = 0; index < results.Length; index++)
                    {
                        var user = results[index];
                        DrawCallableRow(user.DisplayName, $"{user.Name}@{user.World}", user.Name, user.World, theme, scale, () => Place(new CallContact(user.Id, user.Name, user.World, user.DisplayName), addMode));
                    }
                }
            }
            else
            {
                DrawRecents(theme, scale, addMode);
            }
        }

        if (!view.Connected)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Max.Y - 14f * scale), "Connecting to call service…", theme.TextMuted, 0.8f);
        }
    }

    private void DrawRecents(PhoneTheme theme, float scale, bool addMode)
    {
        var recents = calls.Recents;
        if (recents.Length == 0)
        {
            ImGui.Dummy(new Vector2(0f, 30f * scale));
            Typography.DrawCentered(new Vector2(ImGui.GetContentRegionAvail().X * 0.5f + ImGui.GetCursorScreenPos().X, ImGui.GetCursorScreenPos().Y), "Search for someone to call", theme.TextMuted);
            return;
        }

        SettingsSection.Header("Recents", theme);
        for (var index = 0; index < recents.Length; index++)
        {
            var contact = recents[index];
            DrawCallableRow(contact.DisplayName, $"{contact.Name}@{contact.World}", contact.Name, contact.World, theme, scale, () => Place(contact, addMode));
        }
    }

    private void DrawCallableRow(string title, string subtitle, string name, string world, PhoneTheme theme, float scale, Action onCall)
    {
        var rowHeight = 56f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var dl = ImGui.GetWindowDrawList();

        var radius = 18f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(dl, avatarCenter, radius, theme.Accent, Initial(title), 0.95f, lodestone.Avatar(name, world), 32);

        var textLeft = origin.X + radius * 2f + 10f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + 9f * scale), title, theme.TextStrong);
        Typography.Draw(new Vector2(textLeft, origin.Y + 30f * scale), subtitle, theme.TextMuted, 0.85f);

        var callCenter = new Vector2(origin.X + width - 20f * scale, avatarCenter.Y);
        var clicked = CircleButton(callCenter, 16f * scale, FontAwesomeIcon.Phone, CallGreen, new Vector4(1f, 1f, 1f, 1f));

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));

        if (clicked)
        {
            onCall();
        }
    }

    private void DrawCallScreen(in PhoneContext context, CallView view)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var dl = ImGui.GetWindowDrawList();

        var tintTop = ImGui.GetColorU32(Palette.WithAlpha(CallGreen, 0.22f));
        var tintBottom = ImGui.GetColorU32(Palette.WithAlpha(CallGreen, 0f));
        dl.AddRectFilledMultiColor(content.Min, new Vector2(content.Max.X, content.Min.Y + content.Height * 0.55f), tintTop, tintTop, tintBottom, tintBottom);

        var others = Others(view);
        var centerX = content.Center.X;
        var avatarTop = content.Min.Y + 40f * scale;

        if (others.Count <= 1)
        {
            var radius = 52f * scale;
            var avatarCenter = new Vector2(centerX, avatarTop + radius);
            if (others.Count == 1)
            {
                DrawSpeakingHalo(dl, avatarCenter, radius, calls.LevelOf(others[0]), scale);
                AvatarView.Draw(dl, avatarCenter, radius, theme.Accent, Initial(view.PeerLabel), 2.4f, lodestone.Avatar(others[0].Name, others[0].World), 64);
            }
            else
            {
                dl.AddCircleFilled(avatarCenter, radius, ImGui.GetColorU32(theme.Accent), 64);
                Typography.DrawCentered(avatarCenter, Initial(view.PeerLabel), new Vector4(1f, 1f, 1f, 1f), 2.4f);
            }
        }
        else
        {
            DrawParticipantGrid(content, others, theme, scale, avatarTop);
        }

        var labelY = others.Count <= 1 ? avatarTop + 104f * scale + 22f * scale : avatarTop + 150f * scale;
        Typography.DrawCentered(new Vector2(centerX, labelY), view.PeerLabel, theme.TextStrong, 1.6f);
        Typography.DrawCentered(new Vector2(centerX, labelY + 28f * scale), StatusLine(view), Palette.WithAlpha(theme.TextStrong, 0.75f), 0.95f);

        if (view.State == CallState.Active)
        {
            Typography.DrawCentered(new Vector2(centerX, labelY + 50f * scale), "Use headphones to avoid echo", theme.TextMuted, 0.75f);
        }

        DrawCallControls(context, view, scale, theme);
    }

    private void DrawParticipantGrid(Rect content, List<ParticipantInfo> others, PhoneTheme theme, float scale, float top)
    {
        const int columns = 4;
        var radius = 26f * scale;
        var cellWidth = content.Width / columns;
        var rows = (others.Count + columns - 1) / columns;
        var dl = ImGui.GetWindowDrawList();

        for (var index = 0; index < others.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var cellCenterX = content.Min.X + column * cellWidth + cellWidth * 0.5f;
            var cellCenterY = top + radius + row * (radius * 2f + 22f * scale);
            var center = new Vector2(cellCenterX, cellCenterY);

            DrawSpeakingHalo(dl, center, radius, calls.LevelOf(others[index]), scale);
            AvatarView.Draw(dl, center, radius, theme.Accent, Initial(others[index].DisplayName), 1.2f, lodestone.Avatar(others[index].Name, others[index].World), 48);
            Typography.DrawCentered(new Vector2(cellCenterX, cellCenterY + radius + 12f * scale), Truncate(others[index].DisplayName, 10), theme.TextStrong, 0.78f);
        }

        _ = rows;
    }

    private void DrawCallControls(in PhoneContext context, CallView view, float scale, PhoneTheme theme)
    {
        var content = context.Content;
        var centerX = content.Center.X;
        var controlsY = content.Max.Y - 54f * scale;

        var muteFill = view.Muted ? CallGreen : Palette.WithAlpha(theme.TextStrong, 0.16f);
        if (CircleButton(new Vector2(centerX - 76f * scale, controlsY), 24f * scale, view.Muted ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone, muteFill, theme.TextStrong))
        {
            calls.ToggleMute();
        }

        if (CircleButton(new Vector2(centerX, controlsY), 30f * scale, FontAwesomeIcon.PhoneSlash, theme.Danger, new Vector4(1f, 1f, 1f, 1f)))
        {
            calls.Hangup();
        }

        var canAdd = view.State == CallState.Active;
        if (CircleButton(new Vector2(centerX + 76f * scale, controlsY), 24f * scale, FontAwesomeIcon.UserPlus, Palette.WithAlpha(theme.TextStrong, 0.16f), theme.TextStrong, canAdd) && canAdd)
        {
            addingToCall = true;
        }
    }

    private void DrawEnablePrompt(Rect body, PhoneTheme theme, float scale)
    {
        var centerX = body.Center.X;
        Typography.DrawCentered(new Vector2(centerX, body.Center.Y - 30f * scale), "Phone Calls", theme.TextStrong, 1.4f);
        Typography.DrawCentered(new Vector2(centerX, body.Center.Y - 4f * scale), "Voice calls with other Aetherphone users", theme.TextMuted, 0.85f);

        var toggleWidth = 48f * scale;
        var toggleHeight = 28f * scale;
        var toggleMin = new Vector2(centerX - toggleWidth * 0.5f, body.Center.Y + 24f * scale);
        var bounds = new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight));
        if (Toggle.Draw(bounds, calls.Enabled, theme) != calls.Enabled)
        {
            calls.SetEnabled(!calls.Enabled);
        }

        Typography.DrawCentered(new Vector2(centerX, bounds.Max.Y + 18f * scale), "Enable", theme.TextMuted, 0.85f);
    }

    private void DrawSearchBar(Rect bar, PhoneTheme theme, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X + 4f * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - 4f * scale, bar.Max.Y - 9f * scale);
        dl.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(theme.GroupedCard), (pillMax.Y - pillMin.Y) * 0.5f);

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 28f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##phoneSearch", "Name or Name@World", ref searchDraft, 64, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                StartSearch(searchDraft);
            }
        }
    }

    private static bool CircleButton(Vector2 center, float radius, FontAwesomeIcon icon, Vector4 fill, Vector4 ink, bool enabled = true)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = enabled && ImGui.IsMouseHoveringRect(min, max);
        var color = hovered ? Palette.Mix(fill, new Vector4(1f, 1f, 1f, 1f), 0.14f) : fill;
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, enabled ? color.W : 0.4f)), 32);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(ink, enabled ? 1f : 0.5f)))
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

    private void DrawSpeakingHalo(ImDrawListPtr dl, Vector2 center, float radius, float level, float scale)
    {
        if (level <= 0.03f)
        {
            return;
        }

        var spread = (3f + 10f * Math.Clamp(level * 6f, 0f, 1f)) * scale;
        dl.AddCircle(center, radius + spread, ImGui.GetColorU32(Palette.WithAlpha(CallGreen, 0.5f)), 48, 2.5f * scale);
    }

    private void Place(CallContact contact, bool addMode)
    {
        if (addMode)
        {
            calls.AddParticipant(contact);
            addingToCall = false;
        }
        else
        {
            calls.StartCall(contact);
        }
    }

    private void StopAdding() => addingToCall = false;

    private void StartSearch(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            searchResults = Array.Empty<UserDto>();
            return;
        }

        searching = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var result = await client.SearchAsync(trimmed, token).ConfigureAwait(false);
            if (result is not null)
            {
                searchResults = result.Users;
            }

            searching = false;
        });
    }

    private List<ParticipantInfo> Others(CallView view)
    {
        var list = new List<ParticipantInfo>();
        for (var index = 0; index < view.Participants.Length; index++)
        {
            if (view.Participants[index].UserId != view.LocalUserId)
            {
                list.Add(view.Participants[index]);
            }
        }

        return list;
    }

    private static string StatusLine(CallView view)
    {
        return view.State switch
        {
            CallState.Dialing => "Calling…",
            CallState.Connecting => "Connecting…",
            CallState.Active => CallFormat.Duration(view.Seconds),
            _ => string.Empty,
        };
    }

    private static string Initial(string value) => value.Length > 0 ? value.Substring(0, 1).ToUpperInvariant() : "?";

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, max - 1) + "…";
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
