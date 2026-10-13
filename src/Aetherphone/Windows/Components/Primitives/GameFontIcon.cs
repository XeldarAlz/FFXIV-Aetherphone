using System.Buffers.Binary;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;

namespace Aetherphone.Windows.Components;

internal static class GameFontIcon
{
    private const string AtlasPath = "common/font/fonticon_ps5.tex";
    private const string LayoutPath = "common/font/gfdata.gfd";
    private const int HeaderSize = 16;
    private const int EntrySize = 16;
    // Dalamud's SeStringRenderer reads the double size icon copies 341 pixels down the atlas.
    private const float DoubleSizeTop = 341f;
    private static readonly Dictionary<BitmapFontIcon, Vector4> Rects = new();
    private static byte[]? layout;
    private static bool layoutLoaded;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, BitmapFontIcon icon, float height)
    {
        if (!TryGetRect(icon, out var rect))
        {
            return;
        }

        var texture = Plugin.TextureProvider.GetFromGame(AtlasPath).GetWrapOrDefault();
        if (texture is null)
        {
            return;
        }

        var size = rect.Z >= rect.W
            ? new Vector2(height, height * rect.W / rect.Z)
            : new Vector2(height * rect.Z / rect.W, height);
        var min = center - size * 0.5f;
        var uv0 = new Vector2(rect.X * 2f, rect.Y * 2f + DoubleSizeTop) / texture.Size;
        var uv1 = new Vector2((rect.X + rect.Z) * 2f, (rect.Y + rect.W) * 2f + DoubleSizeTop) / texture.Size;
        drawList.AddImage(texture.Handle, min, min + size, uv0, uv1);
    }

    private static bool TryGetRect(BitmapFontIcon icon, out Vector4 rect)
    {
        if (Rects.TryGetValue(icon, out rect))
        {
            return true;
        }

        if (!layoutLoaded)
        {
            layout = LoadLayout();
            layoutLoaded = true;
        }

        if (layout is null || layout.Length < HeaderSize)
        {
            return false;
        }

        var count = Math.Min(BinaryPrimitives.ReadInt32LittleEndian(layout.AsSpan(8)),
            (layout.Length - HeaderSize) / EntrySize);
        for (var index = 0; index < count; index++)
        {
            var entry = layout.AsSpan(HeaderSize + index * EntrySize, EntrySize);
            if (BinaryPrimitives.ReadUInt16LittleEndian(entry) != (ushort)icon)
            {
                continue;
            }

            rect = new Vector4(BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]),
                BinaryPrimitives.ReadUInt16LittleEndian(entry[4..]), BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]),
                BinaryPrimitives.ReadUInt16LittleEndian(entry[8..]));
            if (rect.Z <= 0f || rect.W <= 0f)
            {
                return false;
            }

            Rects[icon] = rect;
            return true;
        }

        return false;
    }

    private static byte[]? LoadLayout()
    {
        try
        {
            return Plugin.DataManager.GetFile(LayoutPath)?.Data;
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "Game font icon layout failed to load");
            return null;
        }
    }
}
