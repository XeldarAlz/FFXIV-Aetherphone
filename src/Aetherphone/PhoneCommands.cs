using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shell.Spotlight;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace Aetherphone;

internal sealed class PhoneCommands
{
    private const string CameraAppId = "camera";
    private const string LinkpearlAppId = "messages";
    private const string CallsAppId = "message";
    private const string MarketAppId = "market";

    private static readonly string[] SampleSenders = { "Alisaie", "Y'shtola", "Thancred" };

    private static readonly LocString[] HelpLines =
    {
        L.Plugin.HelpToggle, L.Plugin.HelpMini, L.Plugin.HelpFull, L.Plugin.HelpHide, L.Plugin.HelpOpen,
        L.Plugin.HelpSettings, L.Plugin.HelpPhoto, L.Plugin.HelpDoNotDisturb, L.Plugin.HelpMute, L.Plugin.HelpMusic,
        L.Plugin.HelpTell, L.Plugin.HelpCall, L.Plugin.HelpRun, L.Plugin.HelpMarket, L.Plugin.HelpReset,
        L.Plugin.HelpTest,
    };

    private readonly PhoneServices services;
    private readonly PhoneWindow phoneWindow;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly VideoDebugWindow videoDebugWindow;
    private readonly IChatGui chat;
    private int sampleCounter;

    public PhoneCommands(PhoneServices services, PhoneWindow phoneWindow, IReadOnlyList<IPhoneApp> apps,
        VideoDebugWindow videoDebugWindow, IChatGui chat)
    {
        this.services = services;
        this.phoneWindow = phoneWindow;
        this.apps = apps;
        this.videoDebugWindow = videoDebugWindow;
        this.chat = chat;
    }

    private enum MusicAction : byte
    {
        Unknown,
        Toggle,
        Play,
        Pause,
        Next,
        Previous,
    }

    public void Run(string arguments)
    {
        var trimmed = arguments.Trim();
        var split = trimmed.IndexOf(' ');
        var verb = split < 0 ? trimmed : trimmed[..split];
        var rest = split < 0 ? string.Empty : trimmed[(split + 1)..].Trim();
        switch (verb.ToLowerInvariant())
        {
            case "":
                phoneWindow.ToggleShell();
                break;
            case "mini":
                phoneWindow.ToggleMinimized();
                break;
            case "full":
                phoneWindow.ShowFull();
                break;
            case "hide":
                phoneWindow.IsOpen = false;
                break;
            case "open":
                OpenApp(rest);
                break;
            case "settings":
                phoneWindow.OpenSettings();
                break;
            case "photo":
                TakePhoto();
                break;
            case "dnd":
                ToggleDoNotDisturb();
                break;
            case "mute":
                ToggleSilentMode();
                break;
            case "music":
                ControlMusic(rest);
                break;
            case "tell":
                OpenTell(rest);
                break;
            case "call":
                Call(rest);
                break;
            case "run":
                RunShortcut(rest);
                break;
            case "market":
                OpenMarket(rest);
                break;
            case "reset":
                phoneWindow.Recenter();
                break;
            case "test":
                SendSampleNotification();
                break;
            case "videodebug":
                videoDebugWindow.IsOpen = true;
                break;
            case "perfhud":
                services.Configuration.ShowPerfHud = !services.Configuration.ShowPerfHud;
                services.Configuration.Save();
                break;
            case "help":
                PrintHelp();
                break;
            default:
                chat.Print(Loc.T(L.Plugin.UnknownCommand, verb, AepConstants.PrimaryCommand));
                break;
        }
    }

    private void PrintHelp()
    {
        chat.Print(Loc.T(L.Plugin.HelpTitle));
        for (var index = 0; index < HelpLines.Length; index++)
        {
            chat.Print(Loc.T(HelpLines[index], AepConstants.PrimaryCommand));
        }
    }

    private void PrintUsage(LocString usage) => chat.Print(Loc.T(usage, AepConstants.PrimaryCommand));

    private void PrintState(LocString label, bool on) =>
        chat.Print(Loc.T(L.Plugin.StateLine, Loc.T(label), Loc.T(on ? L.Common.On : L.Common.Off)));

    private void OpenApp(string query)
    {
        if (query.Length == 0)
        {
            PrintUsage(L.Plugin.OpenUsage);
            return;
        }

        var app = FindApp(query);
        if (app is null)
        {
            chat.Print(Loc.T(L.Plugin.AppNotFound, query));
            return;
        }

        if (RequireInstalled(app))
        {
            phoneWindow.OpenApp(app.Id);
        }
    }

    private IPhoneApp? FindApp(string query)
    {
        IPhoneApp? best = null;
        var bestScore = 0;
        for (var index = 0; index < apps.Count; index++)
        {
            var app = apps[index];
            if (!app.IsAvailable)
            {
                continue;
            }

            var score = Math.Max(SpotlightMatch.Score(app.DisplayName, query),
                SpotlightMatch.Score(app.Id, query) / 2);
            if (score > bestScore)
            {
                best = app;
                bestScore = score;
            }
        }

        return best;
    }

    private bool RequireInstalled(string appId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId)
            {
                return RequireInstalled(apps[index]);
            }
        }

        return false;
    }

    private bool RequireInstalled(IPhoneApp app)
    {
        if (services.Installer.IsInstalled(app.Id))
        {
            return true;
        }

        chat.Print(Loc.T(L.Plugin.AppNotInstalled, app.DisplayName));
        return false;
    }

    private void TakePhoto()
    {
        if (!RequireInstalled(CameraAppId))
        {
            return;
        }

        services.CameraShutter.Request();
        phoneWindow.OpenApp(CameraAppId);
    }

    private void ToggleDoNotDisturb()
    {
        var configuration = services.Configuration;
        configuration.DoNotDisturb = !configuration.DoNotDisturb;
        configuration.Save();
        PrintState(L.Settings.DoNotDisturb, configuration.DoNotDisturb);
    }

    private void ToggleSilentMode()
    {
        var configuration = services.Configuration;
        configuration.SilentMode = !configuration.SilentMode;
        configuration.Save();
        PrintState(L.Settings.SilentMode, configuration.SilentMode);
    }

    private void ControlMusic(string argument)
    {
        var action = ParseMusicAction(argument);
        if (action == MusicAction.Unknown)
        {
            PrintUsage(L.Plugin.MusicUsage);
            return;
        }

        var playback = services.Playback;
        if (!playback.IsActive)
        {
            chat.Print(Loc.T(L.Plugin.NothingPlaying));
            return;
        }

        switch (action)
        {
            case MusicAction.Toggle:
                playback.TogglePlayPause();
                break;
            case MusicAction.Play:
                if (playback.IsPaused)
                {
                    playback.TogglePlayPause();
                }

                break;
            case MusicAction.Pause:
                if (!playback.IsPaused)
                {
                    playback.TogglePlayPause();
                }

                break;
            case MusicAction.Next:
                playback.Next();
                break;
            case MusicAction.Previous:
                playback.Previous();
                break;
        }
    }

    private static MusicAction ParseMusicAction(string argument) => argument.ToLowerInvariant() switch
    {
        "" => MusicAction.Toggle,
        "play" => MusicAction.Play,
        "pause" => MusicAction.Pause,
        "next" => MusicAction.Next,
        "prev" or "previous" => MusicAction.Previous,
        _ => MusicAction.Unknown,
    };

    private void OpenTell(string target)
    {
        if (!RequireInstalled(LinkpearlAppId))
        {
            return;
        }

        if (!TryResolveTellTarget(target, out var name, out var typedWorld))
        {
            PrintUsage(L.Plugin.TellUsage);
            return;
        }

        var world = services.GameData.FindWorldName(typedWorld);
        if (world.Length == 0)
        {
            chat.Print(Loc.T(L.Plugin.WorldNotFound, typedWorld));
            return;
        }

        if (services.GameData.IsLocalPlayer(name, world))
        {
            PrintUsage(L.Plugin.TellUsage);
            return;
        }

        var row = services.ChatInbox.EnsureTell(name, world);
        services.LinkpearlLauncher.Request(row.Key);
        phoneWindow.OpenApp(LinkpearlAppId);
    }

    private bool TryResolveTellTarget(string text, out string name, out string world)
    {
        name = string.Empty;
        world = string.Empty;
        if (text.Length == 0)
        {
            if (services.GameData.LocalPlayer?.TargetObject is not IPlayerCharacter player)
            {
                return false;
            }

            name = player.Name.TextValue;
            world = services.GameData.WorldName(player.HomeWorld.RowId);
            return name.Length > 0 && world.Length > 0;
        }

        var at = text.IndexOf('@');
        if (at > 0)
        {
            name = CapitalizeWords(text[..at].Trim());
            world = text[(at + 1)..].Trim();
            return name.Length > 0 && world.Length > 0;
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 3)
        {
            name = CapitalizeWords(string.Concat(parts[0], " ", parts[1]));
            world = parts[2];
            return true;
        }

        if (parts.Length == 2)
        {
            name = CapitalizeWords(string.Concat(parts[0], " ", parts[1]));
            world = services.GameData.WorldName(services.GameData.LocalHomeWorldId);
            return world.Length > 0;
        }

        return false;
    }

    private static string CapitalizeWords(string text)
    {
        var characters = text.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (index == 0 || characters[index - 1] == ' ')
            {
                characters[index] = char.ToUpperInvariant(characters[index]);
            }
        }

        return new string(characters);
    }

    private void Call(string query)
    {
        if (query.Length == 0)
        {
            PrintUsage(L.Plugin.CallUsage);
            return;
        }

        if (!RequireInstalled(CallsAppId))
        {
            return;
        }

        var calls = services.Calls;
        if (!calls.SignedIn)
        {
            chat.Print(Loc.T(L.Phone.SignInPrompt));
            return;
        }

        if (!calls.Enabled)
        {
            PrintState(L.Phone.Calls, false);
            return;
        }

        services.Contacts.Refresh();
        var contact = FindContact(query);
        if (contact is null)
        {
            chat.Print(Loc.T(L.Plugin.ContactNotFound, query));
            return;
        }

        calls.StartCall(new CallContact(contact.UserId, string.Empty, string.Empty,
            ContactBook.DisplayLabel(contact)));
        services.DmLauncher.RequestCalls();
        phoneWindow.OpenApp(CallsAppId);
    }

    private ContactDto? FindContact(string query)
    {
        ContactDto? best = null;
        var bestScore = 0;
        var contacts = services.Contacts.Contacts;
        for (var index = 0; index < contacts.Length; index++)
        {
            var contact = contacts[index];
            var score = Math.Max(SpotlightMatch.Score(contact.Alias, query),
                SpotlightMatch.Score(contact.DisplayName, query));
            score = Math.Max(score, SpotlightMatch.Score(contact.Handle, query));
            if (score > bestScore)
            {
                best = contact;
                bestScore = score;
            }
        }

        return best;
    }

    private void RunShortcut(string name)
    {
        if (name.Length == 0)
        {
            chat.Print(Loc.T(L.Plugin.RunUsage));
            return;
        }

        var shortcut = services.Shortcuts.FindByName(name);
        if (shortcut is null)
        {
            chat.Print(Loc.T(L.Plugin.ShortcutNotFound, name));
            return;
        }

        services.ShortcutRunner.Run(shortcut);
    }

    private void OpenMarket(string query)
    {
        if (query.Length > 0)
        {
            services.MarketLauncher.RequestSearch(query);
        }

        phoneWindow.OpenApp(MarketAppId);
    }

    private void SendSampleNotification()
    {
        sampleCounter++;
        var accent = new Vector4(0.30f, 0.78f, 0.42f, 1f);
        var sender = SampleSenders[sampleCounter % SampleSenders.Length];
        services.Notifications.Notify(new PhoneNotification(LinkpearlAppId, sender,
            $"Sample message #{sampleCounter}", DateTime.Now, accent, $"{sender}@Sample"));
    }
}
