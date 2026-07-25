namespace Aetherphone.Core.Emulation;

/// <summary>
/// Bridges shell visibility changes to emulator input capture without coupling the
/// phone shell to a particular mini-game implementation.
/// </summary>
internal static class EmulatorInputCaptureBridge
{
    private static readonly object Gate = new();
    private static Action<bool>? listener;
    private static bool phoneInteractive = true;

    public static IDisposable Register(Action<bool> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        bool interactive;
        lock (Gate)
        {
            listener = callback;
            interactive = phoneInteractive;
        }

        callback(interactive);
        return new Registration(callback);
    }

    public static void SetPhoneInteractive(bool interactive)
    {
        Action<bool>? callback;
        lock (Gate)
        {
            phoneInteractive = interactive;
            callback = listener;
        }

        callback?.Invoke(interactive);
    }

    private sealed class Registration : IDisposable
    {
        private Action<bool>? registered;

        public Registration(Action<bool> callback)
        {
            registered = callback;
        }

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref registered, null);
            if (current is null)
            {
                return;
            }

            lock (Gate)
            {
                if (ReferenceEquals(listener, current))
                {
                    listener = null;
                }
            }
        }
    }
}
