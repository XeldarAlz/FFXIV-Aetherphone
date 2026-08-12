using System.Runtime.ExceptionServices;
using Aetherphone.Core.Emulation.Libretro;

namespace Aetherphone.Core.Emulation;

internal sealed class EmulatorSession : IDisposable
{
    private const double SaveIntervalSeconds = 5;
    private const double MaximumQueuedSeconds = 0.5;
    private const string GpSpCoreFileName = "gpsp_libretro.dll";
    private readonly IEmulatorLinkTransport link;
    private readonly bool ownsLink;
    private readonly EmulatorStateStore states;
    private readonly object workerGate = new();
    private readonly object frameGate = new();
    private readonly Queue<WorkItem> commands = new();
    private readonly AutoResetEvent wake = new(false);
    private readonly ManualResetEventSlim initialized = new(false);
    private readonly Thread worker;
    private IEmulatorCore? core;
    private ExceptionDispatchInfo? initializationError;
    private EmulatorInputState pendingInput;
    private double queuedSeconds;
    private double sinceSave;
    private float playbackSpeed = 1f;
    private float audioGain = 1f;
    private bool stopping;
    private bool disposed;
    private bool stateDirty;
    private byte[] publishedFrame = Array.Empty<byte>();
    private int publishedWidth;
    private int publishedHeight;
    private float publishedAspect;
    private long publishedVersion;
    private long uploadedVersion;
    private string coreName = string.Empty;
    private int diskCount;
    private int diskIndex;

    public EmulatorSession(string corePath, EmulatorSystemDefinition systemDefinition, string romPath,
        string emulatorRoot, IReadOnlyDictionary<string, string>? coreOptions = null,
        IEmulatorLinkTransport? link = null, bool preserveSaveMemoryOnStateLoad = false)
    {
        this.link = link ?? NullEmulatorLinkTransport.Instance;
        ownsLink = !ReferenceEquals(this.link, NullEmulatorLinkTransport.Instance);
        states = new EmulatorStateStore(emulatorRoot, romPath, Path.GetFileNameWithoutExtension(corePath));
        System = systemDefinition;
        RomPath = romPath;
        if (!System.Supports(romPath))
        {
            throw new InvalidOperationException($"The selected file is not supported by {System.Name}.");
        }

        worker = new Thread(() => WorkerMain(corePath, romPath, emulatorRoot, coreOptions,
            preserveSaveMemoryOnStateLoad))
        {
            IsBackground = true,
            Name = $"Aetherphone emulator ({System.Id})",
        };
        worker.Start();
        initialized.Wait();
        initializationError?.Throw();
    }

    public string RomPath { get; }
    public EmulatorSystemDefinition System { get; }
    public string CoreName => coreName;
    public int VideoWidth
    {
        get
        {
            lock (frameGate)
            {
                return publishedWidth;
            }
        }
    }
    public int VideoHeight
    {
        get
        {
            lock (frameGate)
            {
                return publishedHeight;
            }
        }
    }
    public float VideoAspectRatio
    {
        get
        {
            lock (frameGate)
            {
                return publishedAspect;
            }
        }
    }
    public bool HasNewFrame
    {
        get
        {
            lock (frameGate)
            {
                return publishedVersion != uploadedVersion;
            }
        }
    }
    public EmulatorButtons Buttons
    {
        set
        {
            lock (workerGate)
            {
                pendingInput = pendingInput with { Buttons = value };
            }
        }
    }
    public EmulatorInputState Input
    {
        set
        {
            lock (workerGate)
            {
                pendingInput = value;
            }
        }
    }
    public bool HasAutoState => states.HasAuto;
    public int DiskCount => Volatile.Read(ref diskCount);
    public int DiskIndex => Volatile.Read(ref diskIndex);

    public void Advance(float deltaSeconds, float speedMultiplier = 1f, float volume = 1f)
    {
        var elapsed = Math.Clamp(deltaSeconds, 0f, 0.1f);
        var speed = Math.Clamp(speedMultiplier, 1f, 8f);
        lock (workerGate)
        {
            if (stopping)
            {
                return;
            }

            playbackSpeed = speed;
            audioGain = Math.Clamp(volume, 0f, 1f);
            queuedSeconds = Math.Min(queuedSeconds + elapsed * speed, MaximumQueuedSeconds);
            sinceSave += elapsed;
            if (sinceSave >= SaveIntervalSeconds)
            {
                sinceSave = 0;
                commands.Enqueue(new WorkItem(static emulator => emulator.SavePersistentMemory()));
            }
        }

        wake.Set();
    }

    public bool UploadVideoFrame(EmulatorVideoTexture video, EmulatorVideoFilter filter,
        int displayWidth, int displayHeight)
    {
        lock (frameGate)
        {
            if (publishedVersion == uploadedVersion || publishedFrame.Length == 0)
            {
                return false;
            }

            video.Upload(publishedFrame, publishedWidth, publishedHeight, filter, displayWidth, displayHeight);
            uploadedVersion = publishedVersion;
            return true;
        }
    }

    public void SetDiskIndex(int index)
    {
        Invoke(emulator =>
        {
            emulator.SetDiskIndex(index);
            UpdateDiskState(emulator);
        });
    }

    public void Save() => Post(static emulator => emulator.SavePersistentMemory());

    public bool HasState(int slot) => states.HasSlot(slot);

    public DateTime? StateTimestamp(int slot) => states.SlotTimestamp(slot);

    public void SaveState(int slot)
    {
        Invoke(emulator =>
        {
            emulator.SavePersistentMemory();
            states.WriteSlot(slot, emulator.SaveState());
        });
    }

    public void LoadState(int slot)
    {
        Invoke(emulator =>
        {
            emulator.LoadState(states.ReadSlot(slot));
            stateDirty = true;
        });
        ClearQueuedTime();
    }

    public bool LoadAutoState()
    {
        if (!states.HasAuto)
        {
            return false;
        }

        Invoke(emulator =>
        {
            emulator.LoadState(states.ReadAuto());
            stateDirty = false;
        });
        ClearQueuedTime();
        return true;
    }

    public bool SaveAutoState(bool force = false)
    {
        return Invoke(emulator =>
        {
            if (!force && !stateDirty)
            {
                return false;
            }

            emulator.SavePersistentMemory();
            states.WriteAuto(emulator.SaveState());
            stateDirty = false;
            return true;
        });
    }

    public void Dispose()
    {
        lock (workerGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            stopping = true;
        }

        wake.Set();
        if (Thread.CurrentThread != worker)
        {
            worker.Join();
        }

        initialized.Dispose();
        wake.Dispose();
    }

    private void WorkerMain(string corePath, string romPath, string emulatorRoot,
        IReadOnlyDictionary<string, string>? coreOptions, bool preserveSaveMemoryOnStateLoad)
    {
        try
        {
            var system = Path.Combine(emulatorRoot, "system");
            var legacySaves = Path.Combine(emulatorRoot, "saves");
            var saves = Path.Combine(legacySaves, System.Id);
            Directory.CreateDirectory(system);
            Directory.CreateDirectory(saves);
            core = new LibretroCore(corePath, system, saves, coreOptions: coreOptions,
                analogController: System.InputProfile == EmulatorInputProfile.PlayStation,
                preserveSaveRamOnStateLoad: preserveSaveMemoryOnStateLoad);

            var saveName = Path.GetFileNameWithoutExtension(romPath) + ".srm";
            var savePath = Path.Combine(saves, saveName);
            var oldSaveName = Path.GetFileName(romPath) + ".srm";
            MigrateLegacySave(Path.Combine(saves, oldSaveName), savePath);
            MigrateLegacySave(Path.Combine(legacySaves, oldSaveName), savePath);
            MigrateLegacySave(Path.Combine(legacySaves, saveName), savePath);
            BackupSaveBeforeGpSpMigration(corePath, savePath);
            core.LoadGame(romPath, savePath);
            link.Reset();
            coreName = core.Name;
            UpdateGeometry(core);
            UpdateDiskState(core);
            initialized.Set();
            RunWorker(core);
        }
        catch (Exception exception)
        {
            initializationError = ExceptionDispatchInfo.Capture(exception);
            initialized.Set();
        }
        finally
        {
            try
            {
                core?.Dispose();
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Emulator] core shutdown failed: {exception.Message}");
            }

            if (ownsLink)
            {
                link.Dispose();
            }
        }
    }

    private void RunWorker(IEmulatorCore emulator)
    {
        while (true)
        {
            WorkItem? command = null;
            EmulatorInputState input = default;
            float speed = 1f;
            float gain = 1f;
            var runFrame = false;
            lock (workerGate)
            {
                if (commands.Count > 0)
                {
                    command = commands.Dequeue();
                }
                else if (stopping)
                {
                    return;
                }
                else
                {
                    var frameDuration = 1.0 / Math.Clamp(emulator.FramesPerSecond, 30.0, 240.0);
                    if (queuedSeconds >= frameDuration)
                    {
                        queuedSeconds -= frameDuration;
                        input = pendingInput;
                        speed = playbackSpeed;
                        gain = audioGain;
                        runFrame = true;
                    }
                }
            }

            if (command is not null)
            {
                Execute(command, emulator);
                continue;
            }

            if (!runFrame)
            {
                wake.WaitOne();
                continue;
            }

            try
            {
                emulator.Input = input;
                emulator.AudioPlaybackSpeed = Math.Clamp((int)MathF.Round(speed), 1, 8);
                emulator.AudioGain = gain;
                link.Pump();
                emulator.RunFrame();
                stateDirty = true;
                PublishFrame(emulator);
                UpdateDiskState(emulator);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Emulator] frame failed: {exception.Message}");
                lock (workerGate)
                {
                    queuedSeconds = 0;
                }
            }
        }
    }

    private void PublishFrame(IEmulatorCore emulator)
    {
        UpdateGeometry(emulator);
        if (!emulator.HasNewFrame || emulator.VideoWidth <= 0 || emulator.VideoHeight <= 0)
        {
            return;
        }

        var source = emulator.VideoFrame;
        lock (frameGate)
        {
            if (publishedFrame.Length != source.Length)
            {
                publishedFrame = new byte[source.Length];
            }

            source.Span.CopyTo(publishedFrame);
            publishedWidth = emulator.VideoWidth;
            publishedHeight = emulator.VideoHeight;
            publishedAspect = emulator.VideoAspectRatio;
            publishedVersion++;
        }
    }

    private void UpdateGeometry(IEmulatorCore emulator)
    {
        lock (frameGate)
        {
            publishedWidth = emulator.VideoWidth;
            publishedHeight = emulator.VideoHeight;
            publishedAspect = emulator.VideoAspectRatio;
        }
    }

    private void UpdateDiskState(IEmulatorCore emulator)
    {
        Volatile.Write(ref diskCount, emulator.DiskCount);
        Volatile.Write(ref diskIndex, emulator.DiskIndex);
    }

    private void Post(Action<IEmulatorCore> action)
    {
        lock (workerGate)
        {
            if (stopping)
            {
                return;
            }

            commands.Enqueue(new WorkItem(action));
        }

        wake.Set();
    }

    private void Invoke(Action<IEmulatorCore> action)
    {
        var work = new WorkItem(action, true);
        EnqueueAndWait(work);
    }

    private T Invoke<T>(Func<IEmulatorCore, T> action)
    {
        T result = default!;
        var work = new WorkItem(emulator => result = action(emulator), true);
        EnqueueAndWait(work);
        return result;
    }

    private void EnqueueAndWait(WorkItem work)
    {
        lock (workerGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            commands.Enqueue(work);
        }

        wake.Set();
        work.Wait();
    }

    private static void Execute(WorkItem work, IEmulatorCore emulator)
    {
        try
        {
            work.Action(emulator);
        }
        catch (Exception exception)
        {
            work.Error = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            work.Complete();
        }
    }

    private void ClearQueuedTime()
    {
        lock (workerGate)
        {
            queuedSeconds = 0;
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

    private sealed class WorkItem
    {
        private readonly ManualResetEventSlim? completed;

        public WorkItem(Action<IEmulatorCore> action, bool wait = false)
        {
            Action = action;
            completed = wait ? new ManualResetEventSlim(false) : null;
        }

        public Action<IEmulatorCore> Action { get; }
        public ExceptionDispatchInfo? Error { get; set; }

        public void Complete() => completed?.Set();

        public void Wait()
        {
            completed?.Wait();
            completed?.Dispose();
            Error?.Throw();
        }
    }
}
