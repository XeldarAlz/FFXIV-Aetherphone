using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float SearchFieldHeight = 36f;
    private const float SearchFieldTop = 8f;
    private const float SearchDebounceSeconds = 0.45f;
    private const int SearchMaxLength = 64;
    private const float SearchRowHeight = 64f;
    private const float SearchTagRowHeight = 56f;
    private const float SearchAvatarRadius = 22f;
    private const int SearchTagLimit = 8;

    private static readonly Vector4 SearchClearFill = new(1f, 1f, 1f, 0.14f);
    private static readonly TextStyle SearchSectionStyle = TextStyles.FootnoteEmphasized;

    private readonly List<string> searchTagTokens = new();
    private readonly List<string> searchTagLabels = new();
    private string searchQuery = string.Empty;
    private string searchApplied = string.Empty;
    private float searchDebounce;
    private bool searchFocusPending;

    private void OpenSearch()
    {
        searchQuery = string.Empty;
        searchApplied = string.Empty;
        searchDebounce = 0f;
        searchFocusPending = true;
        searchTagTokens.Clear();
        searchTagLabels.Clear();
        store.ClearSearch();
        router.Push(VelvetView.Search);
    }

    private void DrawSearch(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Common.Search)))
        {
            router.Pop();
            return;
        }

        var pad = SocialChrome.CellPadX * scale;
        var fieldTop = area.Min.Y + VHeader.Height * scale + SearchFieldTop * scale;
        var fieldRect = new Rect(new Vector2(area.Min.X + pad, fieldTop),
            new Vector2(area.Max.X - pad, fieldTop + SearchFieldHeight * scale));
        var palette = VelvetTheme.Palette;
        SearchField.Draw(fieldRect, "##velvetPeopleSearch", Loc.T(L.Velvet.SearchPeopleHint), ref searchQuery,
            palette.FieldSurface, palette.MutedInk, palette.TitleInk, SearchClearFill, palette.BackdropBottom,
            SearchMaxLength, searchFocusPending);
        searchFocusPending = false;
        TickSearch();

        var list = new Rect(new Vector2(area.Min.X, fieldRect.Max.Y + SearchFieldTop * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(list))
        {
            var tagsOnly = TagsOnlyQuery(searchApplied);
            var results = tagsOnly ? Array.Empty<VelvetProfileDto>() : store.SearchResults;
            if (searchApplied.Length == 0)
            {
                DrawEmpty(list, Loc.T(L.Velvet.SearchPeopleHint), string.Empty);
                return;
            }

            if (results.Length == 0 && searchTagTokens.Count == 0)
            {
                if (!tagsOnly && (store.LoadingSearch || !store.SearchLoaded))
                {
                    DrawEmpty(list, Loc.T(L.Common.Searching), string.Empty);
                }
                else
                {
                    DrawEmpty(list, Loc.T(L.Velvet.SearchNone), Loc.T(L.Velvet.SearchNoneHint));
                }

                return;
            }

            Gap(2f);
            if (searchTagTokens.Count > 0)
            {
                SocialChrome.DrawSectionLabel(Loc.T(L.Velvet.PostTagsTitle), VelvetInk.Shared, SearchSectionStyle);
                for (var index = 0; index < searchTagTokens.Count; index++)
                {
                    DrawSearchTagRow(index);
                }
            }

            if (results.Length > 0)
            {
                SocialChrome.DrawSectionLabel(Loc.T(L.Velvet.SearchPeopleSection), VelvetInk.Shared,
                    SearchSectionStyle);
                for (var index = 0; index < results.Length; index++)
                {
                    DrawSearchRow(results[index]);
                }
            }

            Gap(40f);
        }
    }

    private void TickSearch()
    {
        if (searchQuery == searchApplied)
        {
            return;
        }

        searchDebounce += ImGui.GetIO().DeltaTime;
        if (searchDebounce < SearchDebounceSeconds)
        {
            return;
        }

        searchApplied = searchQuery;
        searchDebounce = 0f;
        RebuildSearchTags();
        if (TagsOnlyQuery(searchApplied))
        {
            store.ClearSearch();
            return;
        }

        store.SearchPeople(searchApplied, MutesFilter());
    }

    private static bool TagsOnlyQuery(string query)
    {
        var trimmed = query.AsSpan().TrimStart();
        return trimmed.Length > 0 && trimmed[0] == '#';
    }

    private void RebuildSearchTags()
    {
        searchTagTokens.Clear();
        searchTagLabels.Clear();
        var trimmed = searchApplied.AsSpan().Trim();
        if (trimmed.Length > 0 && trimmed[0] == '#')
        {
            trimmed = trimmed[1..].TrimStart();
        }

        if (trimmed.Length == 0)
        {
            return;
        }

        var needle = trimmed.ToString();
        var tokens = VelvetSuggestions.PostTagTokens;
        for (var index = 0; index < tokens.Length && searchTagTokens.Count < SearchTagLimit; index++)
        {
            var label = VelvetTokenLabels.Of(tokens[index]);
            if (!label.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            searchTagTokens.Add(tokens[index]);
            searchTagLabels.Add("#" + label);
        }
    }

    private void DrawSearchTagRow(int index)
    {
        var model = new VRowModel
        {
            Title = searchTagLabels[index],
            Height = SearchTagRowHeight,
            Leading = VRowLeading.IconTile,
            TileIcon = PhoneIcons.Hash,
            TileTint = VelvetTheme.Rose,
            Chevron = true,
        };
        if (VRow.Cell(in model, ui, theme, images, lodestone) == VRowHit.Body)
        {
            OpenTagPosts(searchTagTokens[index]);
        }
    }

    private void DrawSearchRow(VelvetProfileDto user)
    {
        var name = DisplayNameOf(user.DisplayName, user.Handle);
        var model = new VRowModel
        {
            Title = name,
            Subtitle = SocialIdentity.ProfileMeta(user.Handle, RegionCodeOf(user)),
            Height = SearchRowHeight,
            Leading = VRowLeading.Avatar,
            AvatarRadius = SearchAvatarRadius,
            Name = name,
            World = string.Empty,
            AvatarUrl = user.AvatarUrl,
            Presence = user.Presence,
            FrameId = user.FrameId,
            RoleBadges = user.Badges,
            RoleBadgeIds = user.BadgeIds,
            UserId = user.UserId,
            Chevron = true,
        };
        if (VRow.Cell(in model, ui, theme, images, lodestone) == VRowHit.Body)
        {
            OpenProfile(user.UserId);
        }
    }
}
