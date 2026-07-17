using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

// Profile screens: the shared SocialProfilePages component draws the header/stats/edit/user-list
// surfaces; this partial keeps the Aethergram-specific photo grid, search tab, and home top bar.
internal sealed partial class AethergramApp
{
    private void DrawProfileGrid() => DrawProfileGrid(store.ProfilePosts, L.Aethergram.Empty);

    private void DrawProfileGrid(PostDto[] posts, LocString emptyMessage)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (posts.Length == 0)
        {
            Typography.DrawCentered(
                new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X * 0.5f,
                    ImGui.GetCursorScreenPos().Y + 40f * scale), Loc.T(emptyMessage), AppPalettes.Aethergram.MutedInk);
            return;
        }

        var gap = 3f * scale;
        var cell = (ScrollLayout.StableContentWidth() - gap * (GridColumns - 1)) / GridColumns;
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        {
            for (var index = 0; index < posts.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    var clicked = ImGui.InvisibleButton("gram", new Vector2(cell, cell));
                    DrawGridThumbnail(posts[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                    if (clicked)
                    {
                        OpenDetail(posts[index]);
                    }
                }

                if (index % GridColumns != GridColumns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }

        ImGui.Dummy(new Vector2(0f, 24f * scale));
    }

    private void DrawGridThumbnail(PostDto post, Vector2 min, Vector2 max)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 8f * scale;
        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        var texture = images.Get(photos.Length > 0 ? photos[0] : null);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(AppPalettes.Aethergram.FieldSurface));
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (photos.Length > 1)
        {
            MultiPhotoBadge.Draw(drawList, new Vector2(max.X - 8f * scale, min.Y + 8f * scale), scale);
        }

        if (ImGui.IsItemHovered())
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawSearchTab(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var searchHeight = 52f * scale;
        profile.DrawSearchBar(new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + searchHeight)));
        profile.DrawSearchResults(new Rect(new Vector2(area.Min.X, area.Min.Y + searchHeight), area.Max), theme,
            false);
    }

    private void DrawHomeTopBar(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var logoSize = Typography.Measure(DisplayName, 1.3f, FontWeight.Bold);
        var logoPos = new Vector2(area.Min.X + 16f * scale, rowCenterY - logoSize.Y * 0.5f);
        Typography.Draw(logoPos, DisplayName, AppPalettes.Aethergram.TitleInk, 1.3f, FontWeight.Bold);
        if (!store.IsSignedIn)
        {
            return;
        }

        var chevronCenter = new Vector2(logoPos.X + logoSize.X + 17f * scale, rowCenterY + 2f * scale);
        var chevron = scopeMenu.IsOpenFor(ScopeMenuId) ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.ChevronDown;
        var anchor = new Rect(new Vector2(logoPos.X, rowCenterY - 12f * scale),
            chevronCenter + new Vector2(12f * scale, 12f * scale));
        if (ui.IconButton(chevronCenter, 12f * scale, chevron.ToIconString(), AppPalettes.Aethergram.MutedInk,
                AppSkin.Transparent, 0.85f))
        {
            scopeMenu.Toggle(ScopeMenuId, anchor);
        }

        var composeCenter = new Vector2(area.Max.X - 24f * scale, rowCenterY);
        UiAnchors.Report("aethergram.compose", new Rect(composeCenter - new Vector2(18f * scale, 18f * scale),
            composeCenter + new Vector2(18f * scale, 18f * scale)));
        if (ui.IconButton(composeCenter, 16f * scale, FontAwesomeIcon.PlusSquare.ToIconString(),
                AppPalettes.Aethergram.BodyInk, AppSkin.Transparent, 1.25f, Loc.T(L.Aethergram.NewPost),
                HoverLabelSide.Below))
        {
            StartCompose(false);
        }
    }
}
