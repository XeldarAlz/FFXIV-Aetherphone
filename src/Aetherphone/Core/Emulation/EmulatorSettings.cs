namespace Aetherphone.Core.Emulation;

internal enum EmulatorVideoFilter : byte
{
    Pixel = 0,
    Smooth = 1,
    Sharp = 2,
    Balanced = 3,
}

internal enum EmulatorGameplayOrientation : byte
{
    Portrait = 0,
    Landscape = 1,
}

internal enum EmulatorLayoutElement : byte
{
    Screen,
    Dpad,
    A,
    B,
    X,
    Y,
    L,
    R,
    L2,
    R2,
    L3,
    R3,
    Dpad2,
    CUp,
    CDown,
    CLeft,
    CRight,
    Select,
    Start,
    FastForward,
}

[Serializable]
internal sealed class EmulatorElementLayout
{
    public float X { get; set; } = 0.5f;
    public float Y { get; set; } = 0.5f;
    public float Scale { get; set; } = 1f;

    public float SafeX => Math.Clamp(X, 0f, 1f);
    public float SafeY => Math.Clamp(Y, 0f, 1f);
    public float SafeScale => Math.Clamp(Scale, 0.5f, 1f);
}

[Serializable]
internal sealed class EmulatorLayoutSettings
{
    public EmulatorElementLayout Screen { get; set; } = new();
    public EmulatorElementLayout Dpad { get; set; } = new();
    public EmulatorElementLayout A { get; set; } = new();
    public EmulatorElementLayout B { get; set; } = new();
    public EmulatorElementLayout X { get; set; } = new();
    public EmulatorElementLayout Y { get; set; } = new();
    public EmulatorElementLayout L { get; set; } = new();
    public EmulatorElementLayout R { get; set; } = new();
    public EmulatorElementLayout L2 { get; set; } = new();
    public EmulatorElementLayout R2 { get; set; } = new();
    public EmulatorElementLayout L3 { get; set; } = new();
    public EmulatorElementLayout R3 { get; set; } = new();
    public EmulatorElementLayout Dpad2 { get; set; } = new();
    public EmulatorElementLayout CUp { get; set; } = new();
    public EmulatorElementLayout CDown { get; set; } = new();
    public EmulatorElementLayout CLeft { get; set; } = new();
    public EmulatorElementLayout CRight { get; set; } = new();
    public EmulatorElementLayout Select { get; set; } = new();
    public EmulatorElementLayout Start { get; set; } = new();
    public EmulatorElementLayout FastForward { get; set; } = new();

    public EmulatorLayoutSettings() => Reset();

    public EmulatorElementLayout For(EmulatorLayoutElement element) => element switch
    {
        EmulatorLayoutElement.Screen => Screen,
        EmulatorLayoutElement.Dpad => Dpad,
        EmulatorLayoutElement.A => A,
        EmulatorLayoutElement.B => B,
        EmulatorLayoutElement.X => X,
        EmulatorLayoutElement.Y => Y,
        EmulatorLayoutElement.L => L,
        EmulatorLayoutElement.R => R,
        EmulatorLayoutElement.L2 => L2,
        EmulatorLayoutElement.R2 => R2,
        EmulatorLayoutElement.L3 => L3,
        EmulatorLayoutElement.R3 => R3,
        EmulatorLayoutElement.Dpad2 => Dpad2,
        EmulatorLayoutElement.CUp => CUp,
        EmulatorLayoutElement.CDown => CDown,
        EmulatorLayoutElement.CLeft => CLeft,
        EmulatorLayoutElement.CRight => CRight,
        EmulatorLayoutElement.Select => Select,
        EmulatorLayoutElement.Start => Start,
        EmulatorLayoutElement.FastForward => FastForward,
        _ => Screen,
    };

    public void Reset()
    {
        Screen = At(0.5f, 0.23f, 1f);
        Dpad = At(0.21f, 0.69f, 1f);
        A = At(0.82f, 0.65f, 1f);
        B = At(0.68f, 0.72f, 1f);
        X = At(0.75f, 0.57f, 1f);
        Y = At(0.61f, 0.64f, 1f);
        L = At(0.15f, 0.52f, 1f);
        R = At(0.85f, 0.52f, 1f);
        L2 = At(0.28f, 0.52f, 0.85f);
        R2 = At(0.72f, 0.52f, 0.85f);
        L3 = At(0.32f, 0.84f, 0.75f);
        R3 = At(0.68f, 0.84f, 0.75f);
        Dpad2 = At(0.46f, 0.69f, 0.82f);
        CUp = At(0.82f, 0.57f, 0.72f);
        CDown = At(0.82f, 0.73f, 0.72f);
        CLeft = At(0.74f, 0.65f, 0.72f);
        CRight = At(0.90f, 0.65f, 0.72f);
        Select = At(0.40f, 0.88f, 1f);
        Start = At(0.60f, 0.88f, 1f);
        FastForward = At(0.85f, 0.88f, 1f);
    }

    public void ResetLandscape()
    {
        Screen = At(0.50f, 0.50f, 1f);
        Dpad = At(0.09f, 0.57f, 0.78f);
        A = At(0.95f, 0.52f, 0.78f);
        B = At(0.91f, 0.66f, 0.78f);
        X = At(0.91f, 0.38f, 0.78f);
        Y = At(0.87f, 0.52f, 0.78f);
        L = At(0.12f, 0.12f, 0.74f);
        R = At(0.88f, 0.12f, 0.74f);
        L2 = At(0.10f, 0.22f, 0.66f);
        R2 = At(0.90f, 0.22f, 0.66f);
        L3 = At(0.10f, 0.80f, 0.62f);
        R3 = At(0.90f, 0.80f, 0.62f);
        Dpad2 = At(0.09f, 0.76f, 0.66f);
        CUp = At(0.94f, 0.31f, 0.60f);
        CDown = At(0.94f, 0.78f, 0.60f);
        CLeft = At(0.88f, 0.55f, 0.60f);
        CRight = At(0.98f, 0.55f, 0.60f);
        Select = At(0.10f, 0.91f, 0.70f);
        Start = At(0.90f, 0.91f, 0.70f);
        FastForward = At(0.96f, 0.12f, 0.68f);
    }

    public void MigrateLandscapeControlsOutward()
    {
        MoveIfUnchanged(Dpad, 0.13f, 0.59f, 0.09f, 0.57f);
        MoveIfUnchanged(A, 0.90f, 0.55f, 0.95f, 0.52f);
        MoveIfUnchanged(B, 0.82f, 0.66f, 0.91f, 0.66f);
        MoveIfUnchanged(X, 0.84f, 0.43f, 0.91f, 0.38f);
        MoveIfUnchanged(Y, 0.76f, 0.54f, 0.87f, 0.52f);
        MoveIfUnchanged(L, 0.12f, 0.14f, 0.12f, 0.12f);
        MoveIfUnchanged(R, 0.88f, 0.14f, 0.88f, 0.12f);
        MoveIfUnchanged(L2, 0.23f, 0.14f, 0.10f, 0.22f);
        MoveIfUnchanged(R2, 0.77f, 0.14f, 0.90f, 0.22f);
        MoveIfUnchanged(L3, 0.23f, 0.87f, 0.10f, 0.80f);
        MoveIfUnchanged(R3, 0.77f, 0.87f, 0.90f, 0.80f);
        MoveIfUnchanged(Dpad2, 0.25f, 0.59f, 0.09f, 0.76f);
        MoveIfUnchanged(CUp, 0.88f, 0.39f, 0.94f, 0.31f);
        MoveIfUnchanged(CDown, 0.88f, 0.69f, 0.94f, 0.78f);
        MoveIfUnchanged(CLeft, 0.80f, 0.54f, 0.88f, 0.55f);
        MoveIfUnchanged(CRight, 0.96f, 0.54f, 0.98f, 0.55f);
        MoveIfUnchanged(Select, 0.43f, 0.88f, 0.10f, 0.91f);
        MoveIfUnchanged(Start, 0.57f, 0.88f, 0.90f, 0.91f);
        MoveIfUnchanged(FastForward, 0.95f, 0.14f, 0.96f, 0.12f);
    }

    private static void MoveIfUnchanged(EmulatorElementLayout element, float oldX, float oldY, float newX,
        float newY)
    {
        if (MathF.Abs(element.X - oldX) > 0.001f || MathF.Abs(element.Y - oldY) > 0.001f)
        {
            return;
        }

        element.X = newX;
        element.Y = newY;
    }

    public static EmulatorLayoutSettings CreateLandscape()
    {
        var layout = new EmulatorLayoutSettings();
        layout.ResetLandscape();
        return layout;
    }

    private static EmulatorElementLayout At(float x, float y, float scale) =>
        new() { X = x, Y = y, Scale = scale };
}

[Serializable]
internal sealed class EmulatorShortcutSettings
{
    public List<int> Keys { get; set; } = new();
    public ushort GamepadButtons { get; set; }

    public bool IsEmpty => Keys.Count == 0 && GamepadButtons == 0;

    public void Set(IEnumerable<int> keys, ushort gamepadButtons)
    {
        Keys = keys.Distinct().Order().ToList();
        GamepadButtons = gamepadButtons;
    }

    public void Clear()
    {
        Keys.Clear();
        GamepadButtons = 0;
    }
}

[Serializable]
internal sealed class EmulatorRecentGame
{
    public string SystemId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long PlayedAtUnix { get; set; }
}

[Serializable]
internal sealed class EmulatorSettings
{
    public EmulatorVideoFilter VideoFilter { get; set; } = EmulatorVideoFilter.Smooth;
    public EmulatorGameplayOrientation GameplayOrientation { get; set; } = EmulatorGameplayOrientation.Landscape;
    public EmulatorLayoutSettings Layout { get; set; } = new();
    public EmulatorLayoutSettings LandscapeLayout { get; set; } = EmulatorLayoutSettings.CreateLandscape();
    public bool AutoSaveState { get; set; } = true;
    public bool AutoLoadState { get; set; } = true;
    public bool ProtectSaveMemoryOnStateLoad { get; set; } = true;
    public int FastForwardSpeed { get; set; } = 2;
    public EmulatorShortcutSettings FastForwardShortcut { get; set; } = new();
    public EmulatorShortcutSettings SaveStateShortcut { get; set; } = new();
    public EmulatorShortcutSettings LoadStateShortcut { get; set; } = new();
    public List<string> RomFolders { get; set; } = new();
    public List<string> ImportedFiles { get; set; } = new();
    public Dictionary<string, string> CoreOptions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, EmulatorSettings> Cores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<EmulatorRecentGame> RecentGames { get; set; } = new();
    public bool PerCoreSettingsMigrated { get; set; }

    public int KeyUp { get; set; } = 0x26;
    public int KeyDown { get; set; } = 0x28;
    public int KeyLeft { get; set; } = 0x25;
    public int KeyRight { get; set; } = 0x27;
    public int KeyA { get; set; } = 0x58;
    public int KeyB { get; set; } = 0x5A;
    public int KeyX { get; set; } = 0x43;
    public int KeyY { get; set; } = 0x56;
    public int KeyL { get; set; } = 0x41;
    public int KeyR { get; set; } = 0x53;
    public int KeyL2 { get; set; } = 0x51;
    public int KeyR2 { get; set; } = 0x57;
    public int KeyL3 { get; set; } = 0x44;
    public int KeyR3 { get; set; } = 0x46;
    public int KeyCUp { get; set; } = 0x49;
    public int KeyCDown { get; set; } = 0x4B;
    public int KeyCLeft { get; set; } = 0x4A;
    public int KeyCRight { get; set; } = 0x4C;
    public int KeyStart { get; set; } = 0x0D;
    public int KeySelect { get; set; } = 0x08;

    public int KeyFor(EmulatorButtons button) => button switch
    {
        EmulatorButtons.Up => KeyUp,
        EmulatorButtons.Down => KeyDown,
        EmulatorButtons.Left => KeyLeft,
        EmulatorButtons.Right => KeyRight,
        EmulatorButtons.A => KeyA,
        EmulatorButtons.B => KeyB,
        EmulatorButtons.X => KeyX,
        EmulatorButtons.Y => KeyY,
        EmulatorButtons.L => KeyL,
        EmulatorButtons.R => KeyR,
        EmulatorButtons.L2 => KeyL2,
        EmulatorButtons.R2 => KeyR2,
        EmulatorButtons.L3 => KeyL3,
        EmulatorButtons.R3 => KeyR3,
        EmulatorButtons.Start => KeyStart,
        EmulatorButtons.Select => KeySelect,
        _ => 0,
    };

    public void SetKey(EmulatorButtons button, int virtualKey)
    {
        switch (button)
        {
            case EmulatorButtons.Up: KeyUp = virtualKey; break;
            case EmulatorButtons.Down: KeyDown = virtualKey; break;
            case EmulatorButtons.Left: KeyLeft = virtualKey; break;
            case EmulatorButtons.Right: KeyRight = virtualKey; break;
            case EmulatorButtons.A: KeyA = virtualKey; break;
            case EmulatorButtons.B: KeyB = virtualKey; break;
            case EmulatorButtons.X: KeyX = virtualKey; break;
            case EmulatorButtons.Y: KeyY = virtualKey; break;
            case EmulatorButtons.L: KeyL = virtualKey; break;
            case EmulatorButtons.R: KeyR = virtualKey; break;
            case EmulatorButtons.L2: KeyL2 = virtualKey; break;
            case EmulatorButtons.R2: KeyR2 = virtualKey; break;
            case EmulatorButtons.L3: KeyL3 = virtualKey; break;
            case EmulatorButtons.R3: KeyR3 = virtualKey; break;
            case EmulatorButtons.Start: KeyStart = virtualKey; break;
            case EmulatorButtons.Select: KeySelect = virtualKey; break;
        }
    }

    public void ResetKeys()
    {
        KeyUp = 0x26;
        KeyDown = 0x28;
        KeyLeft = 0x25;
        KeyRight = 0x27;
        KeyA = 0x58;
        KeyB = 0x5A;
        KeyX = 0x43;
        KeyY = 0x56;
        KeyL = 0x41;
        KeyR = 0x53;
        KeyL2 = 0x51;
        KeyR2 = 0x57;
        KeyL3 = 0x44;
        KeyR3 = 0x46;
        KeyCUp = 0x49;
        KeyCDown = 0x4B;
        KeyCLeft = 0x4A;
        KeyCRight = 0x4C;
        KeyStart = 0x0D;
        KeySelect = 0x08;
    }

    public void Normalize()
    {
        Layout ??= new EmulatorLayoutSettings();
        LandscapeLayout ??= EmulatorLayoutSettings.CreateLandscape();
        LandscapeLayout.MigrateLandscapeControlsOutward();
        RomFolders ??= new List<string>();
        ImportedFiles ??= new List<string>();
        FastForwardShortcut ??= new EmulatorShortcutSettings();
        SaveStateShortcut ??= new EmulatorShortcutSettings();
        LoadStateShortcut ??= new EmulatorShortcutSettings();
        FastForwardShortcut.Keys ??= new List<int>();
        SaveStateShortcut.Keys ??= new List<int>();
        LoadStateShortcut.Keys ??= new List<int>();
        CoreOptions ??= new Dictionary<string, string>(StringComparer.Ordinal);
        Cores ??= new Dictionary<string, EmulatorSettings>(StringComparer.OrdinalIgnoreCase);
        RecentGames ??= new List<EmulatorRecentGame>();
    }

    public void MigrateToPerCoreSettings(IEnumerable<EmulatorSystemDefinition> systems)
    {
        Normalize();
        if (PerCoreSettingsMigrated)
        {
            return;
        }

        foreach (var system in systems)
        {
            Cores.TryAdd(system.Id, CloneCoreSettings());
        }

        PerCoreSettingsMigrated = true;
    }

    public EmulatorSettings ForCore(EmulatorSystemDefinition system)
    {
        Normalize();
        if (!Cores.TryGetValue(system.Id, out var settings))
        {
            settings = new EmulatorSettings();
            Cores[system.Id] = settings;
        }

        settings.Normalize();
        foreach (var option in system.DefaultCoreOptions)
        {
            settings.CoreOptions.TryAdd(option.Key, option.Value);
        }

        return settings;
    }

    public EmulatorLayoutSettings LayoutFor(EmulatorGameplayOrientation orientation) =>
        orientation == EmulatorGameplayOrientation.Landscape ? LandscapeLayout : Layout;

    public void AddRecent(EmulatorSystemDefinition system, string path, int maximum = 12)
    {
        Normalize();
        RecentGames.RemoveAll(entry => string.Equals(entry.SystemId, system.Id, StringComparison.OrdinalIgnoreCase) &&
                                       string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
        RecentGames.Insert(0, new EmulatorRecentGame
        {
            SystemId = system.Id,
            Path = path,
            PlayedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
        if (RecentGames.Count > maximum)
        {
            RecentGames.RemoveRange(maximum, RecentGames.Count - maximum);
        }
    }

    private EmulatorSettings CloneCoreSettings()
    {
        var clone = new EmulatorSettings
        {
            VideoFilter = VideoFilter,
            GameplayOrientation = GameplayOrientation,
            Layout = CloneLayout(Layout),
            LandscapeLayout = CloneLayout(LandscapeLayout),
            AutoSaveState = AutoSaveState,
            AutoLoadState = AutoLoadState,
            ProtectSaveMemoryOnStateLoad = ProtectSaveMemoryOnStateLoad,
            FastForwardSpeed = FastForwardSpeed,
            FastForwardShortcut = CloneShortcut(FastForwardShortcut),
            SaveStateShortcut = CloneShortcut(SaveStateShortcut),
            LoadStateShortcut = CloneShortcut(LoadStateShortcut),
            RomFolders = RomFolders.ToList(),
            ImportedFiles = ImportedFiles.ToList(),
            KeyUp = KeyUp,
            KeyDown = KeyDown,
            KeyLeft = KeyLeft,
            KeyRight = KeyRight,
            KeyA = KeyA,
            KeyB = KeyB,
            KeyX = KeyX,
            KeyY = KeyY,
            KeyL = KeyL,
            KeyR = KeyR,
            KeyL2 = KeyL2,
            KeyR2 = KeyR2,
            KeyL3 = KeyL3,
            KeyR3 = KeyR3,
            KeyCUp = KeyCUp,
            KeyCDown = KeyCDown,
            KeyCLeft = KeyCLeft,
            KeyCRight = KeyCRight,
            KeyStart = KeyStart,
            KeySelect = KeySelect,
        };
        clone.Normalize();
        return clone;
    }

    private static EmulatorShortcutSettings CloneShortcut(EmulatorShortcutSettings? source)
    {
        var clone = new EmulatorShortcutSettings();
        if (source is not null)
        {
            clone.Set(source.Keys, source.GamepadButtons);
        }

        return clone;
    }

    private static EmulatorLayoutSettings CloneLayout(EmulatorLayoutSettings? source)
    {
        var clone = new EmulatorLayoutSettings();
        if (source is null)
        {
            return clone;
        }

        foreach (var element in Enum.GetValues<EmulatorLayoutElement>())
        {
            var from = source.For(element);
            var to = clone.For(element);
            to.X = from.X;
            to.Y = from.Y;
            to.Scale = from.Scale;
        }

        return clone;
    }
}
