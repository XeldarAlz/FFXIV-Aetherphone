using Aetherphone.Core.Emulation.Libretro;

namespace Aetherphone.Core.Emulation;

internal sealed class EmulatorSession : IDisposable
{
    private const double SaveIntervalSeconds = 5;
    private const string GpSpCoreFileName = "gpsp_libretro.dll";
    private readonly IEmulatorCore core;
    private readonly IEmulatorLinkTransport link;
    private readonly EmulatorStateStore states;
    private double accumulator;
    private double sinceSave;
    private bool stateDirty;

    public EmulatorSession(string corePath, EmulatorSystemDefinition systemDefinition, string romPath,
        string emulatorRoot, IReadOnlyDictionary<string, string>? coreOptions = null,
        IEmulatorLinkTransport? link = null, bool preserveSaveMemoryOnStateLoad = false)
    {
        this.link = link ?? NullEmulatorLinkTransport.Instance;
        states = new EmulatorStateStore(emulatorRoot, romPath, Path.GetFileNameWithoutExtension(corePath));
        System = systemDefinition;
        if (!System.Supports(romPath))
        {
            throw new InvalidOperationException($"The selected file is not supported by {System.Name}.");
        }

        var system = Path.Combine(emulatorRoot, "system");
        var legacySaves = Path.Combine(emulatorRoot, "saves");
        var saves = Path.Combine(legacySaves, System.Id);
        Directory.CreateDirectory(system);
        Directory.CreateDirectory(saves);
        core = new LibretroCore(corePath, system, saves, coreOptions: coreOptions,
            analogController: System.InputProfile == EmulatorInputProfile.PlayStation,
            preserveSaveRamOnStateLoad: preserveSaveMemoryOnStateLoad);
        try
        {
            var saveName = Path.GetFileNameWithoutExtension(romPath) + ".srm";
            var savePath = Path.Combine(saves, saveName);
            var oldSaveName = Path.GetFileName(romPath) + ".srm";
            MigrateLegacySave(Path.Combine(saves, oldSaveName), savePath);
            MigrateLegacySave(Path.Combine(legacySaves, oldSaveName), savePath);
            MigrateLegacySave(Path.Combine(legacySaves, saveName), savePath);
            BackupSaveBeforeGpSpMigration(corePath, savePath);
            core.LoadGame(romPath, savePath);
            this.link.Reset();
            RomPath = romPath;
        }
        catch
        {
            core.Dispose();
            if (!ReferenceEquals(this.link, NullEmulatorLinkTransport.Instance))
            {
                this.link.Dispose();
            }

            throw;
        }
    }

    public string RomPath { get; }
    public EmulatorSystemDefinition System { get; }
    public string CoreName => core.Name;
    public int VideoWidth => core.VideoWidth;
    public int VideoHeight => core.VideoHeight;
    public ReadOnlyMemory<byte> VideoFrame => core.VideoFrame;
    public bool HasNewFrame => core.HasNewFrame;
    public EmulatorButtons Buttons { set => core.Buttons = value; }
    public EmulatorInputState Input { set => core.Input = value; }
    public bool HasAutoState => states.HasAuto;
    public int DiskCount => core.DiskCount;
    public int DiskIndex => core.DiskIndex;

    public void SetDiskIndex(int index) => core.SetDiskIndex(index);

    public void Advance(float deltaSeconds, float speedMultiplier = 1f)
    {
        link.Pump();
        var speed = Math.Clamp(speedMultiplier, 1f, 8f);
        core.AudioPlaybackSpeed = Math.Clamp((int)MathF.Round(speed), 1, 8);
        var frameDuration = 1.0 / Math.Clamp(core.FramesPerSecond, 30.0, 240.0);
        accumulator += Math.Clamp(deltaSeconds, 0f, 0.1f) * speed;
        var frames = 0;
        var maximumFrames = Math.Clamp((int)Math.Ceiling(4 * speed), 4, 32);
        while (accumulator >= frameDuration && frames < maximumFrames)
        {
            core.RunFrame();
            accumulator -= frameDuration;
            frames++;
        }

        if (frames == maximumFrames)
        {
            accumulator = Math.Min(accumulator, frameDuration);
        }

        stateDirty |= frames > 0;

        sinceSave += deltaSeconds;
        if (sinceSave >= SaveIntervalSeconds)
        {
            Save();
            sinceSave = 0;
        }
    }

    public void Save()
    {
        try
        {
            core.SavePersistentMemory();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] periodic save failed: {exception.Message}");
        }
    }

    public bool HasState(int slot) => states.HasSlot(slot);

    public DateTime? StateTimestamp(int slot) => states.SlotTimestamp(slot);

    public void SaveState(int slot)
    {
        core.SavePersistentMemory();
        states.WriteSlot(slot, core.SaveState());
    }

    public void LoadState(int slot)
    {
        core.LoadState(states.ReadSlot(slot));
        accumulator = 0;
        stateDirty = true;
    }

    public bool LoadAutoState()
    {
        if (!states.HasAuto)
        {
            return false;
        }

        core.LoadState(states.ReadAuto());
        accumulator = 0;
        stateDirty = false;
        return true;
    }

    public bool SaveAutoState(bool force = false)
    {
        if (!force && !stateDirty)
        {
            return false;
        }

        core.SavePersistentMemory();
        states.WriteAuto(core.SaveState());
        stateDirty = false;
        return true;
    }

    public void Dispose()
    {
        core.Dispose();
        if (!ReferenceEquals(link, NullEmulatorLinkTransport.Instance))
        {
            link.Dispose();
        }
    }

    private static void BackupSaveBeforeGpSpMigration(string corePath, string savePath)
    {
        if (!string.Equals(Path.GetFileName(corePath), GpSpCoreFileName, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(savePath))
        {
            return;
        }

        var backupDirectory = Path.Combine(Path.GetDirectoryName(savePath) ?? string.Empty, "backups");
        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(backupDirectory, Path.GetFileName(savePath) + ".pre-gpsp.bak");
        if (!File.Exists(backupPath))
        {
            File.Copy(savePath, backupPath);
        }
    }

    private static void MigrateLegacySave(string legacyPath, string destination)
    {
        if (!File.Exists(destination) && File.Exists(legacyPath))
        {
            File.Copy(legacyPath, destination);
        }
    }

}
