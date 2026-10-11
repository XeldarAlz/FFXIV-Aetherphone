using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.DragDrop;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class ImageIntake
{
    private const string PayloadLabel = "AetherphoneImages";
    private const int NoFailure = 0;
    private const int Unreadable = 1;
    private const int NotDownloaded = 2;

    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
    private static readonly Func<IDragDropManager, bool> CarriesImage = static manager => AnyImage(manager.Files);

    private readonly ConcurrentQueue<string> ready = new();
    private int busy;
    private int failure;
    private uint clipboardSequence = ClipboardWatch.UnseenSequence;
    private bool clipboardHasImage;

    public static bool PasteRequested()
    {
        return ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.V, false) && ClipboardImage.HasImage()
            && !ClipboardImage.HasText();
    }

    public bool ClipboardHasImage()
    {
        if (ClipboardWatch.HasChanged(ref clipboardSequence))
        {
            clipboardHasImage = ClipboardImage.HasImage();
        }

        return clipboardHasImage;
    }

    public void Paste(PhotoLibrary library)
    {
        Start(null, library);
    }

    public void AcceptDrop(PhotoLibrary library, Vector4 highlight)
    {
        var dragDrop = Plugin.DragDrop;
        if (!dragDrop.ServiceAvailable)
        {
            return;
        }

        dragDrop.CreateImGuiSource(PayloadLabel, CarriesImage);
        IReadOnlyList<string> dropped;
        using (ImRaii.PushColor(ImGuiCol.DragDropTarget, highlight))
        {
            if (!dragDrop.CreateImGuiTarget(PayloadLabel, out dropped, out _))
            {
                return;
            }
        }

        var files = new string[dropped.Count];
        for (var index = 0; index < files.Length; index++)
        {
            files[index] = dropped[index];
        }

        Start(files, library);
    }

    public bool TryTake(out string path)
    {
        var failed = Interlocked.Exchange(ref failure, NoFailure);
        if (failed != NoFailure)
        {
            ShellToast.Show(Loc.T(failed == NotDownloaded ? L.Common.FileNotDownloaded : L.Common.PasteFailed));
        }

        if (ready.TryDequeue(out var next))
        {
            path = next;
            return true;
        }

        path = string.Empty;
        return false;
    }

    private void Start(string[]? files, PhotoLibrary library)
    {
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                if (files is null)
                {
                    ReadClipboard(library);
                }
                else
                {
                    Resolve(files);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Images] could not take a pasted or dropped image");
                Volatile.Write(ref failure, Unreadable);
            }
            finally
            {
                Volatile.Write(ref busy, 0);
            }
        });
    }

    private void ReadClipboard(PhotoLibrary library)
    {
        if (!ClipboardImage.TryRead(out var encoded, out var files))
        {
            Volatile.Write(ref failure, Unreadable);
            return;
        }

        if (files is not null)
        {
            Resolve(files);
            return;
        }

        var (pixels, width, height) = ImageProcessor.DecodeRgba32(encoded!);
        var path = library.SavePasted(pixels, width, height);
        if (path is null)
        {
            Volatile.Write(ref failure, Unreadable);
            return;
        }

        ready.Enqueue(path);
    }

    private void Resolve(string[] files)
    {
        var accepted = false;
        var online = false;
        for (var index = 0; index < files.Length; index++)
        {
            if (!IsImage(files[index]))
            {
                continue;
            }

            if (FilePicker.NotYetDownloaded(files[index]))
            {
                online = true;
                continue;
            }

            ready.Enqueue(files[index]);
            accepted = true;
        }

        if (online || !accepted)
        {
            Volatile.Write(ref failure, online ? NotDownloaded : Unreadable);
        }
    }

    private static bool AnyImage(IReadOnlyList<string> files)
    {
        for (var index = 0; index < files.Count; index++)
        {
            if (IsImage(files[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsImage(string path)
    {
        var extension = Path.GetExtension(path);
        for (var index = 0; index < Extensions.Length; index++)
        {
            if (string.Equals(extension, Extensions[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
