using System.Collections.ObjectModel;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeCommandManager : ICommandManager
{
    private readonly Dictionary<string, IReadOnlyCommandInfo> commands = new(StringComparer.OrdinalIgnoreCase);

    public ReadOnlyDictionary<string, IReadOnlyCommandInfo> Commands => new(commands);

    public bool ProcessCommand(string content)
    {
        var trimmed = content.Trim();
        var separator = trimmed.IndexOf(' ');
        var command = separator < 0 ? trimmed : trimmed[..separator];
        var arguments = separator < 0 ? string.Empty : trimmed[(separator + 1)..].Trim();
        if (!commands.TryGetValue(command, out var info))
        {
            return false;
        }

        info.Handler(command, arguments);
        return true;
    }

    public void DispatchCommand(string command, string argument, IReadOnlyCommandInfo info) => info.Handler(command, argument);

    public bool AddHandler(string command, CommandInfo info)
    {
        commands[command] = info;
        return true;
    }

    public bool RemoveHandler(string command) => commands.Remove(command);
}
