using System.Runtime.InteropServices;

namespace Aetherphone.Core.SystemMedia;

internal static unsafe class WinRt
{
    public static readonly Guid AsyncInfoId = new("00000036-0000-0000-c000-000000000046");
    public static readonly Guid UnknownId = new("00000000-0000-0000-c000-000000000046");
    public static readonly Guid AgileObjectId = new("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90");

    private const int MultithreadedApartment = 1;
    private const int ChangedModeError = unchecked((int)0x80010106);
    private const int AsyncInfoStatusSlot = 7;
    private const int AsyncInfoCancelSlot = 9;
    private const int AsyncOperationGetResultsSlot = 8;
    private const int AsyncStarted = 0;
    private const int AsyncCompleted = 1;
    private const int PollMilliseconds = 4;

    public static bool EnterMultithreadedApartment()
    {
        var status = RoInitialize(MultithreadedApartment);
        return status != ChangedModeError && ComCall.Succeeded(status);
    }

    public static void LeaveApartment() => RoUninitialize();

    public static int GetActivationFactory(string className, in Guid interfaceId, out nint factory)
    {
        var classId = CreateString(className);
        try
        {
            nint value = 0;
            var localInterfaceId = interfaceId;
            var status = RoGetActivationFactory(classId, &localInterfaceId, &value);
            factory = value;
            return status;
        }
        finally
        {
            DeleteString(classId);
        }
    }

    public static int ActivateInstance(string className, in Guid interfaceId, out nint instance)
    {
        instance = 0;
        var classId = CreateString(className);
        nint inspectable = 0;
        try
        {
            var status = RoActivateInstance(classId, &inspectable);
            if (!ComCall.Succeeded(status))
            {
                return status;
            }

            return ComCall.QueryInterface(inspectable, interfaceId, out instance);
        }
        finally
        {
            ComCall.Release(inspectable);
            DeleteString(classId);
        }
    }

    public static nint CreateString(string value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        nint handle = 0;
        fixed (char* source = value)
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(source, (uint)value.Length, &handle));
        }

        return handle;
    }

    public static void DeleteString(nint handle)
    {
        if (handle == 0)
        {
            return;
        }

        _ = WindowsDeleteString(handle);
    }

    public static string TakeString(nint handle, string previous)
    {
        if (handle == 0)
        {
            return string.Empty;
        }

        try
        {
            uint length = 0;
            var buffer = WindowsGetStringRawBuffer(handle, &length);
            var text = new ReadOnlySpan<char>(buffer, (int)length);
            return text.SequenceEqual(previous) ? previous : text.ToString();
        }
        finally
        {
            DeleteString(handle);
        }
    }

    public static bool WaitForPointer(nint operation, int timeoutMilliseconds, out nint result)
    {
        result = 0;
        if (!WaitForCompletion(operation, timeoutMilliseconds))
        {
            return false;
        }

        return ComCall.Succeeded(ComCall.GetPointer(operation, AsyncOperationGetResultsSlot, out result));
    }

    public static bool WaitForCompletion(nint operation, int timeoutMilliseconds)
    {
        if (!ComCall.Succeeded(ComCall.QueryInterface(operation, AsyncInfoId, out var asyncInfo)))
        {
            return false;
        }

        try
        {
            var deadline = Environment.TickCount64 + timeoutMilliseconds;
            while (true)
            {
                if (!ComCall.Succeeded(ComCall.GetInt32(asyncInfo, AsyncInfoStatusSlot, out var status)))
                {
                    return false;
                }

                if (status != AsyncStarted)
                {
                    return status == AsyncCompleted;
                }

                if (Environment.TickCount64 >= deadline)
                {
                    _ = ComCall.Invoke(asyncInfo, AsyncInfoCancelSlot);
                    return false;
                }

                Thread.Sleep(PollMilliseconds);
            }
        }
        finally
        {
            ComCall.Release(asyncInfo);
        }
    }

    [DllImport("combase.dll")]
    private static extern int RoInitialize(int initType);

    [DllImport("combase.dll")]
    private static extern void RoUninitialize();

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(nint activatableClassId, Guid* interfaceId, nint* factory);

    [DllImport("combase.dll")]
    private static extern int RoActivateInstance(nint activatableClassId, nint* instance);

    [DllImport("combase.dll")]
    private static extern int WindowsCreateString(char* source, uint length, nint* handle);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(nint handle);

    [DllImport("combase.dll")]
    private static extern char* WindowsGetStringRawBuffer(nint handle, uint* length);
}
