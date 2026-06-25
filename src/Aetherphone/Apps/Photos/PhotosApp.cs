using System.Collections.Concurrent;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Photos;

internal sealed class PhotosApp : IPhoneApp
{
    private const int Columns = 3;

    public string Id => "photos";

    public string DisplayName => "Photos";

    public string Glyph => "P";

    public Vector4 Accent => new(0.95f, 0.62f, 0.25f, 1f);

    public int BadgeCount => 0;

    private readonly PhotoLibrary library;
    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> ready = new();
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();

    private string[] paths = Array.Empty<string>();
    private int? viewerIndex;
    private int countLabelKey = -1;
    private string countLabel = string.Empty;

    public PhotosApp(PhotoLibrary library)
    {
        this.library = library;
    }

    public void OnOpened()
    {
        viewerIndex = null;
        Refresh();
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        if (viewerIndex is { } index && index >= 0 && index < paths.Length)
        {
            DrawViewer(context, index);
            return;
        }

        viewerIndex = null;
        DrawGrid(context);
    }

    private void DrawGrid(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;

        Typography.Draw(new Vector2(content.Min.X + 4f * scale, content.Min.Y + 2f * scale), "Photos", theme.TextStrong, 1.7f, FontWeight.Bold);

        if (countLabelKey != paths.Length)
        {
            countLabelKey = paths.Length;
            countLabel = paths.Length == 1 ? "1 Photo" : $"{paths.Length} Photos";
        }

        Typography.Draw(new Vector2(content.Min.X + 4f * scale, content.Min.Y + 34f * scale), countLabel, theme.TextMuted, 0.85f);

        if (paths.Length == 0)
        {
            DrawEmpty(content, theme, scale);
            return;
        }

        var bodyTop = content.Min.Y + 58f * scale;
        var body = new Rect(new Vector2(content.Min.X, bodyTop), content.Max);
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(6f * scale, 6f * scale)))
        using (var child = ImRaii.Child("##grid", body.Size, false, ImGuiWindowFlags.NoBackground))
        {
            if (!child)
            {
                return;
            }

            var gap = 6f * scale;
            var avail = ImGui.GetContentRegionAvail().X;
            var cell = (avail - gap * (Columns - 1)) / Columns;

            for (var index = 0; index < paths.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    var clicked = ImGui.InvisibleButton("cell", new Vector2(cell, cell));
                    DrawThumbnail(paths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), scale);
                    if (clicked)
                    {
                        viewerIndex = index;
                    }
                }

                if (index % Columns != Columns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }
    }

    private void DrawThumbnail(string path, Vector2 min, Vector2 max, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = Get(path);

        if (texture is null)
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.14f, 0.15f, 0.18f, 1f)), rounding);
            dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), rounding, ImDrawFlags.RoundCornersAll, 1f);
            return;
        }

        var (uv0, uv1) = CenterCrop(texture.Size, 1f);
        dl.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);

        if (ImGui.IsItemHovered())
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawViewer(in PhoneContext context, int index)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var dl = ImGui.GetWindowDrawList();
        var path = paths[index];
        var texture = Get(path);

        var stage = new Rect(new Vector2(content.Min.X, content.Min.Y + 44f * scale), new Vector2(content.Max.X, content.Max.Y - 36f * scale));

        if (texture is not null)
        {
            var fit = MathF.Min(stage.Width / texture.Size.X, stage.Height / texture.Size.Y);
            var drawn = new Vector2(texture.Size.X * fit, texture.Size.Y * fit);
            var min = stage.Center - drawn * 0.5f;
            var max = stage.Center + drawn * 0.5f;
            dl.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, 8f * scale, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            Typography.DrawCentered(stage.Center, "Loading", theme.TextMuted, 1f);
        }

        if (DrawChevron(new Vector2(content.Min.X + 16f * scale, content.Min.Y + 20f * scale), theme.TextStrong, true, scale))
        {
            viewerIndex = null;
            return;
        }

        if (DrawTrash(new Vector2(content.Max.X - 18f * scale, content.Min.Y + 20f * scale), theme.Danger, scale))
        {
            DeletePhoto(index);
            return;
        }

        Typography.DrawCentered(new Vector2(content.Center.X, content.Max.Y - 16f * scale), $"{index + 1} / {paths.Length}", theme.TextMuted, 0.85f);

        if (paths.Length <= 1)
        {
            return;
        }

        if (DrawArrow(new Vector2(content.Min.X + 16f * scale, content.Center.Y), theme.TextStrong, true, scale))
        {
            viewerIndex = (index - 1 + paths.Length) % paths.Length;
        }

        if (DrawArrow(new Vector2(content.Max.X - 16f * scale, content.Center.Y), theme.TextStrong, false, scale))
        {
            viewerIndex = (index + 1) % paths.Length;
        }
    }

    private static void DrawEmpty(Rect content, PhoneTheme theme, float scale)
    {
        var center = content.Center;
        var glyphCenter = center - new Vector2(0f, 18f * scale);
        AppIconArt.TryDraw("photos", glyphCenter, 64f * scale, theme.TextMuted, theme.AppBackground);
        Typography.DrawCentered(center + new Vector2(0f, 34f * scale), "No Photos", theme.TextMuted, 1.1f);
        Typography.DrawCentered(center + new Vector2(0f, 58f * scale), "Use the Camera to take a shot", theme.TextMuted with { W = 0.7f }, 0.8f);
    }

    private static bool DrawChevron(Vector2 center, Vector4 color, bool pointsLeft, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 16f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var ink = ImGui.GetColorU32(hovered ? color : color with { W = 0.85f });
        var size = 6f * scale;
        var direction = pointsLeft ? -1f : 1f;
        var tip = new Vector2(center.X - direction * size * 0.4f, center.Y);
        dl.AddLine(new Vector2(tip.X + direction * size, tip.Y - size), tip, ink, 2.4f * scale);
        dl.AddLine(tip, new Vector2(tip.X + direction * size, tip.Y + size), ink, 2.4f * scale);
        return Tapped(hovered);
    }

    private static bool DrawArrow(Vector2 center, Vector4 color, bool pointsLeft, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 18f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.5f : 0.32f)), 28);
        return DrawChevron(center, color, pointsLeft, scale) || Tapped(hovered);
    }

    private static bool DrawTrash(Vector2 center, Vector4 color, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 16f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var ink = ImGui.GetColorU32(hovered ? color : color with { W = 0.85f });
        var extent = 7f * scale;
        var bodyMin = new Vector2(center.X - extent * 0.7f, center.Y - extent * 0.4f);
        var bodyMax = new Vector2(center.X + extent * 0.7f, center.Y + extent);
        dl.AddRect(bodyMin, bodyMax, ink, 2f * scale, ImDrawFlags.RoundCornersBottom, 1.6f * scale);
        dl.AddLine(new Vector2(center.X - extent, center.Y - extent * 0.4f), new Vector2(center.X + extent, center.Y - extent * 0.4f), ink, 1.6f * scale);
        dl.AddLine(new Vector2(center.X - extent * 0.4f, center.Y - extent), new Vector2(center.X + extent * 0.4f, center.Y - extent), ink, 1.6f * scale);
        return Tapped(hovered);
    }

    private static bool Tapped(bool hovered)
    {
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static (Vector2 Uv0, Vector2 Uv1) CenterCrop(Vector2 size, float targetAspect)
    {
        if (size.X <= 0f || size.Y <= 0f)
        {
            return (Vector2.Zero, Vector2.One);
        }

        var aspect = size.X / size.Y / targetAspect;
        if (aspect > 1f)
        {
            var inset = (1f - 1f / aspect) * 0.5f;
            return (new Vector2(inset, 0f), new Vector2(1f - inset, 1f));
        }

        if (aspect < 1f)
        {
            var inset = (1f - aspect) * 0.5f;
            return (new Vector2(0f, inset), new Vector2(1f, 1f - inset));
        }

        return (Vector2.Zero, Vector2.One);
    }

    private void DeletePhoto(int index)
    {
        var path = paths[index];
        library.Delete(path);
        if (ready.TryRemove(path, out var wrap))
        {
            wrap.Dispose();
        }

        Refresh();
        if (paths.Length == 0)
        {
            viewerIndex = null;
            return;
        }

        viewerIndex = Math.Clamp(index, 0, paths.Length - 1);
    }

    private void Refresh()
    {
        paths = library.List();
    }

    private IDalamudTextureWrap? Get(string path)
    {
        if (ready.TryGetValue(path, out var wrap))
        {
            return wrap;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd(path, 0))
        {
            return null;
        }

        _ = LoadAsync(path);
        return null;
    }

    private async Task LoadAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var wrap = await Plugin.TextureProvider.CreateFromImageAsync(bytes, path, token).ConfigureAwait(false);
            if (!ready.TryAdd(path, wrap))
            {
                wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed.TryAdd(path, 0);
            AepLog.Warning($"[Photos] failed to load {path}: {exception.Message}");
        }
        finally
        {
            loading.TryRemove(path, out _);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        foreach (var wrap in ready.Values)
        {
            wrap.Dispose();
        }

        ready.Clear();
        cancellation.Dispose();
    }
}
