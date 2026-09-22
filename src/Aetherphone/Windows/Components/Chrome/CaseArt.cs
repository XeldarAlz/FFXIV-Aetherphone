using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CaseArt
{
    private const float CanvasWidth = 1500f;
    private const float CanvasHeight = 2755f;
    private const float CanvasBodyWidth = 1000f;
    private const float CanvasMargin = 250f;
    private const float CanvasCorner = 161.08f;
    private const float CanvasSlice = CanvasMargin + CanvasCorner;
    private const float SilhouetteInset = 1f;
    private const int SliceCount = 3;
    private const int EdgeCount = SliceCount + 1;
    private const uint Opaque = 0xFFFFFFFFu;

    public const float MarginFraction = CanvasMargin / CanvasBodyWidth;

    private static readonly float[] ColumnUv =
    {
        0f, CanvasSlice / CanvasWidth, 1f - CanvasSlice / CanvasWidth, 1f,
    };

    private static readonly float[] RowUv =
    {
        0f, CanvasSlice / CanvasHeight, 1f - CanvasSlice / CanvasHeight, 1f,
    };

    private static Rect Bounds(Rect body, bool landscape)
    {
        var silhouette = Silhouette(body);
        var margin = CanvasMargin * Scale(silhouette, landscape);
        return new Rect(silhouette.Min - new Vector2(margin, margin), silhouette.Max + new Vector2(margin, margin));
    }

    public static void Quad(ImDrawListPtr drawList, ImTextureID texture, Rect body, bool landscape, uint tint)
    {
        drawList.PushClipRectFullScreen();
        QuadClipped(drawList, texture, body, landscape, tint);
        drawList.PopClipRect();
    }

    public static void QuadClipped(ImDrawListPtr drawList, ImTextureID texture, Rect body, bool landscape, uint tint)
    {
        var silhouette = Silhouette(body);
        var scale = Scale(silhouette, landscape);
        Span<float> columns = stackalloc float[EdgeCount];
        Span<float> rows = stackalloc float[EdgeCount];
        SliceEdges(silhouette.Min.X, silhouette.Max.X, scale, columns);
        SliceEdges(silhouette.Min.Y, silhouette.Max.Y, scale, rows);
        if (landscape)
        {
            DrawRotated(drawList, texture, columns, rows, tint);
            return;
        }

        DrawUpright(drawList, texture, columns, rows, tint);
    }

    public static void QuadExcluding(ImDrawListPtr drawList, ImTextureID texture, Rect body, Rect exclude,
        bool landscape)
    {
        var art = Bounds(body, landscape);
        Clipped(drawList, texture, body, new Rect(art.Min, new Vector2(art.Max.X, exclude.Min.Y)), landscape);
        Clipped(drawList, texture, body, new Rect(new Vector2(art.Min.X, exclude.Max.Y), art.Max), landscape);
        Clipped(drawList, texture, body,
            new Rect(new Vector2(art.Min.X, exclude.Min.Y), new Vector2(exclude.Min.X, exclude.Max.Y)), landscape);
        Clipped(drawList, texture, body,
            new Rect(new Vector2(exclude.Max.X, exclude.Min.Y), new Vector2(art.Max.X, exclude.Max.Y)), landscape);
    }

    public static uint Tint(float alpha) =>
        alpha >= 1f ? Opaque : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));

    public static void SliceEdges(float min, float max, float scale, Span<float> edges)
    {
        var corner = MathF.Min(CanvasCorner * scale, (max - min) * 0.5f);
        edges[0] = min - CanvasMargin * scale;
        edges[1] = min + corner;
        edges[2] = max - corner;
        edges[3] = max + CanvasMargin * scale;
    }

    private static Rect Silhouette(Rect body) => body.Inset(SilhouetteInset * UiScale.Current);

    private static float Scale(Rect silhouette, bool landscape) =>
        (landscape ? silhouette.Height : silhouette.Width) / CanvasBodyWidth;

    private static void DrawUpright(ImDrawListPtr drawList, ImTextureID texture, ReadOnlySpan<float> columns,
        ReadOnlySpan<float> rows, uint tint)
    {
        for (var row = 0; row < SliceCount; row++)
        {
            for (var column = 0; column < SliceCount; column++)
            {
                drawList.AddImage(texture, new Vector2(columns[column], rows[row]),
                    new Vector2(columns[column + 1], rows[row + 1]), new Vector2(ColumnUv[column], RowUv[row]),
                    new Vector2(ColumnUv[column + 1], RowUv[row + 1]), tint);
            }
        }
    }

    private static void DrawRotated(ImDrawListPtr drawList, ImTextureID texture, ReadOnlySpan<float> columns,
        ReadOnlySpan<float> rows, uint tint)
    {
        for (var row = 0; row < SliceCount; row++)
        {
            for (var column = 0; column < SliceCount; column++)
            {
                var left = columns[row];
                var right = columns[row + 1];
                var top = rows[SliceCount - 1 - column];
                var bottom = rows[SliceCount - column];
                var nearU = ColumnUv[column];
                var farU = ColumnUv[column + 1];
                var nearV = RowUv[row];
                var farV = RowUv[row + 1];
                drawList.AddImageQuad(texture, new Vector2(left, top), new Vector2(right, top),
                    new Vector2(right, bottom), new Vector2(left, bottom), new Vector2(farU, nearV),
                    new Vector2(farU, farV), new Vector2(nearU, farV), new Vector2(nearU, nearV), tint);
            }
        }
    }

    private static void Clipped(ImDrawListPtr drawList, ImTextureID texture, Rect body, Rect clip, bool landscape)
    {
        if (clip.Width <= 0.5f || clip.Height <= 0.5f)
        {
            return;
        }

        drawList.PushClipRect(clip.Min, clip.Max, false);
        QuadClipped(drawList, texture, body, landscape, Opaque);
        drawList.PopClipRect();
    }
}
