using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AccountPage : ISettingsPage, IDisposable
{
    private const string LodestoneProfileUrl = "https://na.finalfantasyxiv.com/lodestone/my/setting/profile/";

    public string Title => "Aethernet Account";

    public string Summary => session.IsSignedIn ? session.CurrentUser?.DisplayName ?? "Signed in" : "Not signed in";

    public string Glyph => "@";

    public Vector4 Tint => new(0.36f, 0.72f, 0.62f, 1f);

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly GameData gameData;
    private readonly CancellationTokenSource cancellation = new();

    private volatile string status = string.Empty;
    private volatile string code = string.Empty;
    private volatile string? challengeId;
    private volatile bool busy;
    private bool meRequested;

    public AccountPage(AethernetSession session, AethernetClient client, GameData gameData)
    {
        this.session = session;
        this.client = client;
        this.gameData = gameData;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            if (session.IsSignedIn)
            {
                DrawSignedIn(theme);
            }
            else
            {
                DrawSignedOut(theme);
            }
        }
    }

    private void DrawSignedIn(PhoneTheme theme)
    {
        if (session.CurrentUser is null && !meRequested && !busy)
        {
            meRequested = true;
            StartMe();
        }

        var user = session.CurrentUser;
        ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
        using (Plugin.Fonts.Push(1.4f))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextUnformatted(user?.DisplayName ?? "Signed in");
            }
        }

        if (user is not null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextUnformatted($"{user.Name}@{user.World}");
                ImGui.TextUnformatted($"{user.Followers} followers · {user.Following} following");
            }
        }

        ImGui.Dummy(new Vector2(0f, 12f * ImGuiHelpers.GlobalScale));
        if (Button("Sign out", theme))
        {
            session.SignOut();
            ResetFlow();
        }
    }

    private void DrawSignedOut(PhoneTheme theme)
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            Typography.DrawCentered(new Vector2(ImGui.GetContentRegionAvail().X * 0.5f + ImGui.GetCursorScreenPos().X, ImGui.GetCursorScreenPos().Y + 80f * ImGuiHelpers.GlobalScale), "Log in to your character first", theme.TextMuted);
            return;
        }

        var name = player.Name.TextValue;
        var world = gameData.WorldName(gameData.LocalHomeWorldId);

        ImGui.Dummy(new Vector2(0f, 6f * ImGuiHelpers.GlobalScale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped("Sign in to Aethernet to use Chirper. Ownership is verified through your Lodestone profile — no password.");
        }

        ImGui.Dummy(new Vector2(0f, 4f * ImGuiHelpers.GlobalScale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextUnformatted($"{name}@{world}");
        }

        ImGui.Dummy(new Vector2(0f, 10f * ImGuiHelpers.GlobalScale));

        if (challengeId is null)
        {
            if (Button("Sign in with Lodestone", theme) && !busy && name.Length > 0 && world.Length > 0)
            {
                StartChallenge(name, world);
            }
        }
        else
        {
            DrawVerifyStep(theme);
        }

        var message = status;
        if (message.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextWrapped(message);
            }
        }
    }

    private void DrawVerifyStep(PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextUnformatted("Add this code to your Lodestone profile:");
        }

        using (Plugin.Fonts.Push(1.6f))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.Accent))
            {
                ImGui.TextUnformatted(code);
            }
        }

        ImGui.Dummy(new Vector2(0f, 6f * ImGuiHelpers.GlobalScale));
        if (Button("Copy code", theme))
        {
            ImGui.SetClipboardText(code);
        }

        if (Button("Open Lodestone profile", theme))
        {
            UrlActions.OpenInBrowser(LodestoneProfileUrl);
        }

        ImGui.Dummy(new Vector2(0f, 6f * ImGuiHelpers.GlobalScale));
        if (Button("I've added it — Verify", theme) && !busy)
        {
            StartVerify();
        }

        if (Button("Cancel", theme))
        {
            ResetFlow();
        }
    }

    private void StartChallenge(string name, string world)
    {
        busy = true;
        status = "Requesting a code…";
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var response = await client.ChallengeAsync(name, world, token).ConfigureAwait(false);
            if (response is null)
            {
                status = "Could not reach Aethernet. Is the server running?";
                busy = false;
                return;
            }

            code = response.Code;
            challengeId = response.ChallengeId;
            status = response.Instructions;
            busy = false;
        });
    }

    private void StartVerify()
    {
        var id = challengeId;
        if (id is null)
        {
            return;
        }

        busy = true;
        status = "Verifying via Lodestone…";
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var auth = await client.VerifyAsync(id, token).ConfigureAwait(false);
            if (auth is null)
            {
                status = "Code not found on your profile yet. Save it on Lodestone, then Verify again.";
                busy = false;
                return;
            }

            session.SignIn(auth.Token, auth.User);
            ResetFlow();
        });
    }

    private void StartMe()
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var me = await client.MeAsync(token).ConfigureAwait(false);
            if (me is not null)
            {
                session.SetUser(me);
            }
        });
    }

    private void ResetFlow()
    {
        challengeId = null;
        code = string.Empty;
        status = string.Empty;
        busy = false;
        meRequested = false;
    }

    private static bool Button(string label, PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, theme.GroupedCard)
            .Push(ImGuiCol.ButtonHovered, Palette.Mix(theme.GroupedCard, theme.Accent, 0.35f))
            .Push(ImGuiCol.ButtonActive, theme.Accent)
            .Push(ImGuiCol.Text, theme.TextStrong))
        {
            return ImGui.Button(label, new Vector2(-1f, 32f * ImGuiHelpers.GlobalScale));
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
