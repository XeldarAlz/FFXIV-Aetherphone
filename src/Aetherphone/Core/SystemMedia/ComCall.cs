namespace Aetherphone.Core.SystemMedia;

internal static unsafe class ComCall
{
    private const int QueryInterfaceSlot = 0;
    private const int ReleaseSlot = 2;

    public static bool Succeeded(int result) => result >= 0;

    public static int QueryInterface(nint instance, in Guid interfaceId, out nint result)
    {
        nint value = 0;
        var localInterfaceId = interfaceId;
        var status = ((delegate* unmanaged<nint, Guid*, nint*, int>)Slot(instance, QueryInterfaceSlot))(instance,
            &localInterfaceId, &value);
        result = value;
        return status;
    }

    public static void Release(nint instance)
    {
        if (instance == 0)
        {
            return;
        }

        _ = ((delegate* unmanaged<nint, uint>)Slot(instance, ReleaseSlot))(instance);
    }

    public static void Release(ref nint instance)
    {
        Release(instance);
        instance = 0;
    }

    public static int Invoke(nint instance, int slot) =>
        ((delegate* unmanaged<nint, int>)Slot(instance, slot))(instance);

    public static int GetPointer(nint instance, int slot, out nint result)
    {
        nint value = 0;
        var status = ((delegate* unmanaged<nint, nint*, int>)Slot(instance, slot))(instance, &value);
        result = value;
        return status;
    }

    public static int GetPointerWithInt64(nint instance, int slot, long argument, out nint result)
    {
        nint value = 0;
        var status = ((delegate* unmanaged<nint, long, nint*, int>)Slot(instance, slot))(instance, argument, &value);
        result = value;
        return status;
    }

    public static int GetPointerWithInt32(nint instance, int slot, int argument, out nint result)
    {
        nint value = 0;
        var status = ((delegate* unmanaged<nint, int, nint*, int>)Slot(instance, slot))(instance, argument, &value);
        result = value;
        return status;
    }

    public static int GetPointerWithUInt32(nint instance, int slot, uint argument, out nint result)
    {
        nint value = 0;
        var status = ((delegate* unmanaged<nint, uint, nint*, int>)Slot(instance, slot))(instance, argument, &value);
        result = value;
        return status;
    }

    public static int GetPointerWithPointer(nint instance, int slot, nint argument, out nint result)
    {
        nint value = 0;
        var status = ((delegate* unmanaged<nint, nint, nint*, int>)Slot(instance, slot))(instance, argument, &value);
        result = value;
        return status;
    }

    public static int GetInterfaceWithPointer(nint instance, int slot, nint argument, in Guid interfaceId,
        out nint result)
    {
        nint value = 0;
        var localInterfaceId = interfaceId;
        var status = ((delegate* unmanaged<nint, nint, Guid*, nint*, int>)Slot(instance, slot))(instance, argument,
            &localInterfaceId, &value);
        result = value;
        return status;
    }

    public static int GetBoolean(nint instance, int slot, out bool result)
    {
        byte value = 0;
        var status = ((delegate* unmanaged<nint, byte*, int>)Slot(instance, slot))(instance, &value);
        result = value != 0;
        return status;
    }

    public static int GetInt32(nint instance, int slot, out int result)
    {
        var value = 0;
        var status = ((delegate* unmanaged<nint, int*, int>)Slot(instance, slot))(instance, &value);
        result = value;
        return status;
    }

    public static int GetInt64(nint instance, int slot, out long result)
    {
        long value = 0;
        var status = ((delegate* unmanaged<nint, long*, int>)Slot(instance, slot))(instance, &value);
        result = value;
        return status;
    }

    public static int GetUInt64(nint instance, int slot, out ulong result)
    {
        ulong value = 0;
        var status = ((delegate* unmanaged<nint, ulong*, int>)Slot(instance, slot))(instance, &value);
        result = value;
        return status;
    }

    public static int SetBoolean(nint instance, int slot, bool value) =>
        ((delegate* unmanaged<nint, byte, int>)Slot(instance, slot))(instance, value ? (byte)1 : (byte)0);

    public static int SetInt32(nint instance, int slot, int value) =>
        ((delegate* unmanaged<nint, int, int>)Slot(instance, slot))(instance, value);

    public static int SetInt64(nint instance, int slot, long value) =>
        ((delegate* unmanaged<nint, long, int>)Slot(instance, slot))(instance, value);

    public static int SetPointer(nint instance, int slot, nint value) =>
        ((delegate* unmanaged<nint, nint, int>)Slot(instance, slot))(instance, value);

    public static int AddHandler(nint instance, int slot, nint handler, out long token)
    {
        long value = 0;
        var status = ((delegate* unmanaged<nint, nint, long*, int>)Slot(instance, slot))(instance, handler, &value);
        token = value;
        return status;
    }

    public static int ReadBuffer(nint instance, int slot, byte* destination, uint count, out uint read)
    {
        uint value = 0;
        var status = ((delegate* unmanaged<nint, byte*, uint, uint*, int>)Slot(instance, slot))(instance, destination,
            count, &value);
        read = value;
        return status;
    }

    private static void* Slot(nint instance, int slot) => (*(void***)instance)[slot];
}
