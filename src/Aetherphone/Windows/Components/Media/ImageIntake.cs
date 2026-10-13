using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.DragDrop;

namespace Aetherphone.Windows.Components;

internal sealed class ImageIntake
{
    private const string PayloadLabel = "AetherphoneImages";
    private const int NoFailure = 0;
    private const int Unreadable = 1;
    private const int NotDownloaded = 2;
    private const int PastedMaxDimension = 4096;

    private static readonly Func<IDragDropManager, bool> CarriesImage = static manager => AnyImage(manager.Files);

    private readonly ConcurrentQueue<string> ready = new();
    private int busy;
    private int failure;
    private int generation;
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

    public bool OfferPaste(ref Rect importRect, out Rect pasteRect)
    {
        pasteRect = importRect;
        if (!ClipboardHasImage())
        {
            return false;
        }

        var half = (importRect.Width - Metrics.Space.Sm * UiScale.Current) * 0.5f;
        pasteRect = new Rect(new Vector2(importRect.Max.X - half, importRect.Min.Y), importRect.Max);
        importRect = new Rect(importRect.Min, new Vector2(importRect.Min.X + half, importRect.Max.Y));
        return true;
    }

    public void Paste(PhotoLibrary library)
    {
        Start(null, library);
    }

    public void Discard()
    {
        Interlocked.Increment(ref generation);
        ready.Clear();
        Volatile.Write(ref failure, NoFailure);
    }

    public void AcceptDrop(Rect area, PhotoLibrary library, Vector4 highlight)
    {
        var dragDrop = Plugin.DragDrop;
        if (!dragDrop.ServiceAvailable)
        {
            return;
        }

        dragDrop.CreateImGuiSource(PayloadLabel, CarriesImage);
        ImGuiP.ItemAdd(new ImRect(area.Min, area.Max), ImGui.GetID(PayloadLabel));
        ImGui.PushStyleColor(ImGuiCol.DragDropTarget, highlight);
        var dropped = dragDrop.CreateImGuiTarget(PayloadLabel, out var droppedFiles, out _);
        ImGui.PopStyleColor();
        if (!dropped)
        {
            return;
        }

        var files = new string[droppedFiles.Count];
        for (var index = 0; index < files.Length; index++)
        {
            files[index] = droppedFiles[index];
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

        var startedGeneration = Volatile.Read(ref generation);
        _ = Task.Run(() =>
        {
            var taken = new List<string>();
            var outcome = Unreadable;
            try
            {
                outcome = files is null ? ReadClipboard(library, taken) : Resolve(files, taken);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Images] could not take a pasted or dropped image");
            }
            finally
            {
                Deliver(taken, outcome, startedGeneration);
                Volatile.Write(ref busy, 0);
            }
        });
    }

    private void Deliver(List<string> taken, int outcome, int startedGeneration)
    {
        if (Volatile.Read(ref generation) != startedGeneration)
        {
            return;
        }

        for (var index = 0; index < taken.Count; index++)
        {
            ready.Enqueue(taken[index]);
        }

        if (outcome != NoFailure)
        {
            Volatile.Write(ref failure, outcome);
        }
    }

    private static int ReadClipboard(PhotoLibrary library, List<string> taken)
    {
        if (!ClipboardImage.TryRead(out var encoded, out var files))
        {
            return Unreadable;
        }

        if (files is not null)
        {
            return Resolve(files, taken);
        }

        var (pixels, width, height) =
            ImageProcessor.DecodeRgba32(encoded!, PastedMaxDimension, ImageProcessor.MaxLocalDecodePixels);
        var path = library.SavePasted(pixels, width, height);
        if (path is null)
        {
            return Unreadable;
        }

        taken.Add(path);
        return NoFailure;
    }

    private static int Resolve(string[] files, List<string> taken)
    {
        var online = false;
        for (var index = 0; index < files.Length; index++)
        {
            if (!ImageFiles.IsImage(files[index]))
            {
                continue;
            }

            if (FilePicker.NotYetDownloaded(files[index]))
            {
                online = true;
                continue;
            }

            taken.Add(files[index]);
        }

        return online ? NotDownloaded : taken.Count > 0 ? NoFailure : Unreadable;
    }

    private static bool AnyImage(IReadOnlyList<string> files)
    {
        for (var index = 0; index < files.Count; index++)
        {
            if (ImageFiles.IsImage(files[index]))
            {
                return true;
            }
        }

        return false;
    }
}
