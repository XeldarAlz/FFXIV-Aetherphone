using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float FacetRowHeight = 44f;
    private const string RadioSortMenuId = "music.radioSort";

    private static readonly Vector4 FacetClearFill = new(0.62f, 0.63f, 0.64f, 1f);
    private static readonly Dictionary<int, string> FacetCountCache = new();

    private void DrawRadioFilterChips(Rect content, Rect barRect, float scale)
    {
        var centerY = barRect.Max.Y + ScopeRowHeight * scale * 0.5f;
        var cursorX = content.Min.X + 16f * scale;
        var gap = 8f * scale;
        var sortStart = cursorX;
        if (ui.FlowChip(ref cursorX, centerY, gap, SortLabel(radioOrder), radioOrder != RadioOrder.Popular))
        {
            var anchor = new Rect(new Vector2(sortStart, centerY - 16f * scale),
                new Vector2(cursorX - gap, centerY + 16f * scale));
            radioSortMenu.Toggle(RadioSortMenuId, anchor);
        }

        var countryLabel = radioCountryName.Length > 0 ? radioCountryName : Loc.T(L.Music.FilterCountry);
        if (ui.FlowChip(ref cursorX, centerY, gap, countryLabel, radioCountryCode.Length > 0))
        {
            OpenFacetPicker(View.CountryFilter);
        }

        var languageLabel = radioLanguageName.Length > 0 ? radioLanguageName : Loc.T(L.Music.FilterLanguage);
        if (ui.FlowChip(ref cursorX, centerY, gap, languageLabel, radioLanguage.Length > 0))
        {
            OpenFacetPicker(View.LanguageFilter);
        }
    }

    private void DrawRadioSortMenu(Rect content)
    {
        if (!radioSortMenu.IsOpenFor(RadioSortMenuId))
        {
            return;
        }

        var items = new[]
        {
            new DropdownMenu.Item(Loc.T(L.Music.SortPopular), Selected: radioOrder == RadioOrder.Popular),
            new DropdownMenu.Item(Loc.T(L.Music.SortTrending), Selected: radioOrder == RadioOrder.Trending),
            new DropdownMenu.Item(Loc.T(L.Music.SortTopVoted), Selected: radioOrder == RadioOrder.TopVoted),
            new DropdownMenu.Item(Loc.T(L.Music.SortName), Selected: radioOrder == RadioOrder.Name),
            new DropdownMenu.Item(Loc.T(L.Music.SortBitrate), Selected: radioOrder == RadioOrder.Bitrate),
        };
        var clicked = radioSortMenu.Draw(content, theme, items);
        if (clicked < 0)
        {
            return;
        }

        var next = (RadioOrder)clicked;
        if (next == radioOrder)
        {
            return;
        }

        radioOrder = next;
        RefetchRadio();
    }

    private static string SortLabel(RadioOrder order)
    {
        return order switch
        {
            RadioOrder.Trending => Loc.T(L.Music.SortTrending),
            RadioOrder.TopVoted => Loc.T(L.Music.SortTopVoted),
            RadioOrder.Name => Loc.T(L.Music.SortName),
            RadioOrder.Bitrate => Loc.T(L.Music.SortBitrate),
            _ => Loc.T(L.Music.SortPopular),
        };
    }

    private void DrawFacetPicker(in PhoneContext context, bool isCountry)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        DrawTopBar(context, Loc.T(isCountry ? L.Music.FilterCountry : L.Music.FilterLanguage), CloseFacetPicker);
        var barRect = SearchBarRect(content, scale);
        var pillRect = new Rect(new Vector2(barRect.Min.X + 16f * scale, barRect.Min.Y),
            new Vector2(barRect.Max.X - 16f * scale, barRect.Max.Y));
        SearchField.Draw(pillRect, "##facetSearch", Loc.T(L.Common.Search), ref facetSearchDraft, SearchFieldSurface,
            SearchFieldHint, SearchFieldInk, FacetClearFill, SearchFieldSurface, 40);
        var body = new Rect(new Vector2(content.Min.X, barRect.Max.Y),
            new Vector2(content.Max.X, BodyBottom(content, scale)));
        var facets = isCountry ? radioCountries : radioLanguages;
        if (facets.Length == 0)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Center.Y - 14f * scale), 13f * scale, ui.Accent,
                ui.MutedInk, Loc.T(L.Common.Loading));
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var activeValue = isCountry ? radioCountryCode : radioLanguage;
            var anyLabel = Loc.T(isCountry ? L.Music.AllCountries : L.Music.AllLanguages);
            if (DrawFacetRow(scale, anyLabel, string.Empty, activeValue.Length == 0))
            {
                ApplyFacet(isCountry, default);
            }

            var draft = facetSearchDraft.Trim();
            for (var index = 0; index < facets.Length; index++)
            {
                var facet = facets[index];
                if (draft.Length > 0 && facet.Display.IndexOf(draft, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (DrawFacetRow(scale, facet.Display, CountText(facet.Count),
                        string.Equals(activeValue, facet.Value, StringComparison.OrdinalIgnoreCase)))
                {
                    ApplyFacet(isCountry, facet);
                }
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
        }
    }

    private void CloseFacetPicker()
    {
        router.Pop();
    }

    private void ApplyFacet(bool isCountry, RadioFacet facet)
    {
        if (isCountry)
        {
            radioCountryCode = facet.Value ?? string.Empty;
            radioCountryName = facet.Display ?? string.Empty;
        }
        else
        {
            radioLanguage = facet.Value ?? string.Empty;
            radioLanguageName = facet.Display ?? string.Empty;
        }

        router.Pop();
        RefetchRadio();
    }

    private bool DrawFacetRow(float scale, string label, string count, bool selected)
    {
        var rowHeight = FacetRowHeight * scale;
        var width = ImGui.GetContentRegionAvail().X;
        if (!ImGui.IsRectVisible(new Vector2(width, rowHeight)))
        {
            ImGui.Dummy(new Vector2(width, rowHeight));
            return false;
        }

        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var textLeft = min.X + 16f * scale;
        var centerY = min.Y + rowHeight * 0.5f;
        var countWidth = 0f;
        if (count.Length > 0)
        {
            var countSize = Typography.Measure(count, TextStyles.Caption1);
            countWidth = countSize.X + 12f * scale;
            Typography.Draw(new Vector2(max.X - 16f * scale - countSize.X, centerY - countSize.Y * 0.5f), count,
                ui.MutedInk, TextStyles.Caption1);
        }

        var labelWidth = MathF.Max(1f, max.X - textLeft - countWidth - 16f * scale);
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Marquee.DrawLeft("music.facetRow." + label, label, textLeft, centerY - labelSize.Y * 0.5f, labelWidth,
            TextStyles.Body, selected ? ui.Accent : ui.TitleInk, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        return UiInteract.Click(min, max, hovered);
    }

    private static string CountText(int count)
    {
        if (FacetCountCache.TryGetValue(count, out var cached))
        {
            return cached;
        }

        var text = count.ToString(Loc.Culture);
        FacetCountCache[count] = text;
        return text;
    }
}
