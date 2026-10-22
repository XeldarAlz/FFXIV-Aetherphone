using Aetherphone.Core.Game;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Plugin;
using System.Collections.Concurrent;
using System.Text;
using D3D11 = SharpDX.Direct3D11;
using GfxKernel = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace Aetherphone.Core.Video;

internal static class DxHandler
{
	internal static D3D11.Device? Device { get; private set; }

	private const int PrologueDumpBytes = 16;

	private static readonly ConcurrentDictionary<string, Action> pendingRenderWork = new();
	private static readonly Lock drainLock = new();

	internal static event Action? OnPresent;

	private unsafe delegate int PresentDelegate(void* swapChain, uint syncInterval, uint flags);
	private static Hook<PresentDelegate>? presentHook;
	private static IUiBuilder? pumpBuilder;

	internal static void Initialise(IDalamudPluginInterface pluginInterface)
	{
		if (!GameMemory.Attached)
		{
			return;
		}

		Device = new D3D11.Device(pluginInterface.UiBuilder.DeviceHandle);

		if (TryHookPresent())
		{
			return;
		}

		pumpBuilder = pluginInterface.UiBuilder;
		pumpBuilder.Draw += PumpRenderThread;
	}

	internal static void RunOnRenderThread(string key, Action work)
	{
		pendingRenderWork[key] = work;
	}

	internal static void CancelRenderThreadWork(string key)
	{
		lock (drainLock)
		{
			pendingRenderWork.TryRemove(key, out _);
		}
	}

	private static unsafe bool TryHookPresent()
	{
		try
		{
			var device = GfxKernel.Device.Instance();
			var swapChainPtr = device is null || device->SwapChain is null
				? 0
				: (nint)device->SwapChain->DXGISwapChain;
			if (swapChainPtr == 0)
			{
				AepLog.Warning("[DxHandler] No DXGI swap chain yet, using the UI render pump.");
				return false;
			}

			var vtable = *(nint**)swapChainPtr;
			var presentAddress = vtable[8];

			if (TryInstallPresentHook(presentAddress))
			{
				return true;
			}

			AepLog.Warning($"[DxHandler] Present at 0x{presentAddress:X} reads {DescribePrologue(presentAddress)}");
			return false;
		}
		catch (Exception e)
		{
			AepLog.Warning($"[DxHandler] Swap chain unreadable, using the UI render pump: {e.Message}");
			return false;
		}
	}

	private static unsafe string DescribePrologue(nint presentAddress)
	{
		var prologue = (byte*)presentAddress;
		var hex = new StringBuilder(PrologueDumpBytes * 3);
		for (var byteIndex = 0; byteIndex < PrologueDumpBytes; byteIndex++)
		{
			hex.Append(prologue[byteIndex].ToString("X2"));
			hex.Append(' ');
		}

		return hex.ToString();
	}

	private static unsafe bool TryInstallPresentHook(nint presentAddress)
	{
		try
		{
			presentHook = Plugin.InteropProvider.HookFromAddress<PresentDelegate>(presentAddress, PresentDetour);
			presentHook.Enable();
			return true;
		}
		catch (Exception e)
		{
			presentHook?.Dispose();
			presentHook = null;
			AepLog.Warning($"[DxHandler] Present hook unavailable, using the UI render pump: {e.Message}");
			return false;
		}
	}

	private static void PumpRenderThread()
	{
		DrainRenderWork();
		NotifyPresent();
	}

	private static unsafe int PresentDetour(void* swapChain, uint syncInterval, uint flags)
	{
		DrainRenderWork();
		NotifyPresent();

		return presentHook!.Original(swapChain, syncInterval, flags);
	}

	private static void DrainRenderWork()
	{
		if (pendingRenderWork.IsEmpty)
		{
			return;
		}

		lock (drainLock)
		{
			foreach (var key in pendingRenderWork.Keys)
			{
				if (pendingRenderWork.TryRemove(key, out var work))
				{
					try
					{
						work();
					}
					catch (Exception e)
					{
						AepLog.Error($"[DxHandler] Render-thread callback '{key}' failed: {e}");
					}
				}
			}
		}
	}

	private static void NotifyPresent()
	{
		try
		{
			OnPresent?.Invoke();
		}
		catch (Exception e)
		{
			AepLog.Error($"[DxHandler] OnPresent subscriber failed: {e}");
		}
	}

	public static void Dispose()
	{
		if (pumpBuilder is not null)
		{
			pumpBuilder.Draw -= PumpRenderThread;
			pumpBuilder = null;
		}

		presentHook?.Disable();
		presentHook?.Dispose();
		presentHook = null;
		pendingRenderWork.Clear();
		OnPresent = null;

		Device = null;
	}
}
