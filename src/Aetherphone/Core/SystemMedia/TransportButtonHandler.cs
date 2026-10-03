using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aetherphone.Core.SystemMedia;

internal static unsafe class TransportButtonHandler
{
    public static readonly Guid InterfaceId = WinRtPinterface.Compute(WinRtPinterface.Generic(
        new Guid("9de1c534-6ae1-11e0-84e1-18a905bcc53f"),
        WinRtPinterface.RuntimeClass("Windows.Media.SystemMediaTransportControls",
            new Guid("99fa3ff4-1742-42a6-902e-087d41f965ec")),
        WinRtPinterface.RuntimeClass("Windows.Media.SystemMediaTransportControlsButtonPressedEventArgs",
            new Guid("b7f47116-a56f-4dc8-9e11-92031f4a87c2"))));

    private const int ButtonSlot = 6;
    private const int NoInterface = unchecked((int)0x80004002);
    private const int VtableLength = 4;

    private static readonly void** Vtable = CreateVtable();

    public static nint Create(Action<int> onButton)
    {
        var instance = (Instance*)NativeMemory.Alloc((nuint)sizeof(Instance));
        instance->Vtable = Vtable;
        instance->ReferenceCount = 1;
        instance->Target = GCHandle.ToIntPtr(GCHandle.Alloc(onButton));
        return (nint)instance;
    }

    private static void** CreateVtable()
    {
        var table = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(TransportButtonHandler),
            sizeof(void*) * VtableLength);
        table[0] = (delegate* unmanaged<Instance*, Guid*, nint*, int>)&QueryInterface;
        table[1] = (delegate* unmanaged<Instance*, uint>)&AddRef;
        table[2] = (delegate* unmanaged<Instance*, uint>)&Release;
        table[3] = (delegate* unmanaged<Instance*, nint, nint, int>)&Invoke;
        return table;
    }

    [UnmanagedCallersOnly]
    private static int QueryInterface(Instance* instance, Guid* interfaceId, nint* result)
    {
        var requested = *interfaceId;
        if (requested == InterfaceId || requested == WinRt.UnknownId || requested == WinRt.AgileObjectId)
        {
            Interlocked.Increment(ref instance->ReferenceCount);
            *result = (nint)instance;
            return 0;
        }

        *result = 0;
        return NoInterface;
    }

    [UnmanagedCallersOnly]
    private static uint AddRef(Instance* instance) => (uint)Interlocked.Increment(ref instance->ReferenceCount);

    [UnmanagedCallersOnly]
    private static uint Release(Instance* instance)
    {
        var remaining = Interlocked.Decrement(ref instance->ReferenceCount);
        if (remaining != 0)
        {
            return (uint)remaining;
        }

        GCHandle.FromIntPtr(instance->Target).Free();
        NativeMemory.Free(instance);
        return 0;
    }

    [UnmanagedCallersOnly]
    private static int Invoke(Instance* instance, nint sender, nint arguments)
    {
        try
        {
            if (arguments == 0 || !ComCall.Succeeded(ComCall.GetInt32(arguments, ButtonSlot, out var button)))
            {
                return 0;
            }

            if (GCHandle.FromIntPtr(instance->Target).Target is Action<int> onButton)
            {
                onButton(button);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[SystemMedia] media key handler failed");
        }

        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Instance
    {
        public void** Vtable;
        public int ReferenceCount;
        public nint Target;
    }
}
