using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Aetherphone.Core.Game;

internal sealed class GameUiVisibility : IDisposable
{
    private readonly IFramework framework;
    private readonly IDalamudPluginInterface pluginInterface;
    private bool hidden;
    private bool previousDisableUserUiHide;

    public GameUiVisibility(IFramework framework, IDalamudPluginInterface pluginInterface)
    {
        this.framework = framework;
        this.pluginInterface = pluginInterface;
    }

    public void Hide()
    {
        if (framework.IsInFrameworkUpdateThread)
        {
            HideNow();
            return;
        }

        _ = framework.RunOnFrameworkThread(HideNow);
    }

    public void Restore()
    {
        if (framework.IsInFrameworkUpdateThread)
        {
            RestoreNow();
            return;
        }

        _ = framework.RunOnFrameworkThread(RestoreNow);
    }

    public void Dispose()
    {
        RestoreNow();
    }

    private unsafe void HideNow()
    {
        if (hidden)
        {
            return;
        }

        var atkModule = RaptureAtkModule.Instance();
        if (atkModule == null || !atkModule->IsUiVisible)
        {
            return;
        }

        previousDisableUserUiHide = pluginInterface.UiBuilder.DisableUserUiHide;
        pluginInterface.UiBuilder.DisableUserUiHide = true;
        hidden = true;
        atkModule->SetUiVisibility(false);
    }

    private unsafe void RestoreNow()
    {
        if (!hidden)
        {
            return;
        }

        hidden = false;

        var atkModule = RaptureAtkModule.Instance();
        if (atkModule != null)
        {
            atkModule->SetUiVisibility(true);
        }

        pluginInterface.UiBuilder.DisableUserUiHide = previousDisableUserUiHide;
    }
}
