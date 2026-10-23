using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Announcements;

internal sealed partial class AnnouncementsApp
{
    private const uint OpaqueWhite = 0xFFFFFFFFu;
    private const float BrokenImageGlyphScale = 1.1f;

    private bool DrawImage(ImDrawListPtr drawList, string url, Rect frame, float rounding, bool topCap, float focusY)
    {
        var texture = images.Sized(url, frame.Width);
        if (texture is null)
        {
            DrawImageSlot(drawList, frame, rounding, topCap, images.Failed(url));
            return false;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Width, texture.Height, frame.Width, frame.Height, focusY);
        if (!topCap)
        {
            Squircle.FillImage(drawList, frame.Min, frame.Max, rounding, texture.Handle, OpaqueWhite, uv0, uv1);
            return true;
        }

        var overhang = rounding * 2f;
        var stretch = (frame.Height + overhang) / MathF.Max(frame.Height, 1f);
        var overhangUv = new Vector2(uv1.X, uv0.Y + (uv1.Y - uv0.Y) * stretch);
        drawList.PushClipRect(frame.Min, frame.Max, true);
        Squircle.FillImage(drawList, frame.Min, new Vector2(frame.Max.X, frame.Max.Y + overhang), rounding,
            texture.Handle, OpaqueWhite, uv0, overhangUv);
        drawList.PopClipRect();
        return true;
    }

    private void DrawImageSlot(ImDrawListPtr drawList, Rect frame, float rounding, bool topCap, bool failed)
    {
        var fill = ImGui.GetColorU32(failed ? ui.TitleInk with { W = SkeletonAlphaLow } : SkeletonInk());
        if (topCap)
        {
            Squircle.FillCap(drawList, frame.Min, frame.Max, rounding, fill, true);
        }
        else
        {
            Squircle.Fill(drawList, frame.Min, frame.Max, rounding, fill);
        }

        if (failed)
        {
            AppSkin.Icon(drawList, frame.Center, IconGlyph.Of(FontAwesomeIcon.Image), ui.MutedInk,
                BrokenImageGlyphScale);
        }
    }

    private void OpenPhotos(string[] urls, int startIndex)
    {
        photoViewer.Open(this, urls.Length, startIndex, page => images.Get(urls[page]));
    }
}
