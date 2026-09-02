using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private VelvetPage filterSurface = VelvetPage.Discover;

    private VelvetFilterSelection IncludeFor(VelvetPage surface) =>
        surface == VelvetPage.Feed ? feedInclude : discoverInclude;

    private VelvetFilterSelection ExcludeFor(VelvetPage surface) =>
        surface == VelvetPage.Feed ? feedExclude : discoverExclude;

    private VelvetFilterPreferences IncludePreferencesFor(VelvetPage surface) =>
        surface == VelvetPage.Feed ? storedFilters.FeedInclude : storedFilters.DiscoverInclude;

    private VelvetFilterPreferences ExcludePreferencesFor(VelvetPage surface) =>
        surface == VelvetPage.Feed ? storedFilters.FeedExclude : storedFilters.DiscoverExclude;

    private void LoadInclude(VelvetPage surface) => IncludeFor(surface).LoadFrom(IncludePreferencesFor(surface));

    private void LoadExclude(VelvetPage surface) => ExcludeFor(surface).LoadFrom(ExcludePreferencesFor(surface));

    private void SaveInclude(VelvetPage surface) => IncludeFor(surface).SaveInto(IncludePreferencesFor(surface));

    private void SaveExclude(VelvetPage surface) => ExcludeFor(surface).SaveInto(ExcludePreferencesFor(surface));

    private void LoadMutes() => mutes.LoadFrom(storedFilters.Mutes);

    private void SaveMutes() => mutes.SaveInto(storedFilters.Mutes);

    private void SaveFilters() => _ = SaveFiltersAsync(storedFilters.Clone());

    private Task<bool> SaveFiltersAsync(StoredVelvetFilters snapshot)
    {
        var accountId = filtersAccountId;
        return Task.Run(() => filterArchive.Save(accountId, snapshot));
    }

    private void SyncSurface(VelvetPage surface)
    {
        SaveInclude(surface);
        SaveExclude(surface);
        SaveMutes();
        SaveFilters();
    }

    private void LoadFiltersForAccount(string accountId) => _ = LoadFiltersForAccountAsync(accountId);

    private async Task LoadFiltersForAccountAsync(string accountId)
    {
        filtersAccountId = accountId;
        var loaded = await Task.Run(() => filterArchive.Load(accountId)).ConfigureAwait(false);
        if (!string.Equals(filtersAccountId, accountId, StringComparison.Ordinal))
        {
            return;
        }

        var migrating = false;
        var merged = false;
        var snapshot = new StoredVelvetFilters();
        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            storedFilters.DiscoverInclude = loaded.DiscoverInclude;
            storedFilters.DiscoverExclude = loaded.DiscoverExclude;
            storedFilters.FeedInclude = loaded.FeedInclude;
            storedFilters.FeedExclude = loaded.FeedExclude;
            storedFilters.Mutes = loaded.Mutes;
            migrating = accountId.Length > 0 && !configuration.VelvetMutesMigratedAccountIds.Contains(accountId);
            if (migrating)
            {
                merged = MergeLegacyMutes();
            }

            LoadInclude(VelvetPage.Discover);
            LoadExclude(VelvetPage.Discover);
            LoadInclude(VelvetPage.Feed);
            LoadExclude(VelvetPage.Feed);
            LoadMutes();
            if (GateAccepted && configuration.IsVelvetOnboarded())
            {
                ApplyDiscoverFilters();
                ApplyFeedFilters();
            }

            snapshot = storedFilters.Clone();
        }).ConfigureAwait(false);

        if (!migrating)
        {
            return;
        }

        if (!merged || await SaveFiltersAsync(snapshot).ConfigureAwait(false))
        {
            await Plugin.Framework.RunOnFrameworkThread(() =>
            {
                configuration.VelvetMutesMigratedAccountIds.Add(accountId);
                configuration.Save();
            }).ConfigureAwait(false);
        }
    }

    private bool MergeLegacyMutes()
    {
        var legacy = new VelvetFilterSelection();
        legacy.LoadFrom(configuration.VelvetMutes);
        if (!legacy.Any)
        {
            return false;
        }

        legacy.MergeInto(storedFilters.DiscoverExclude);
        legacy.MergeInto(storedFilters.FeedExclude);
        legacy.MergeInto(storedFilters.Mutes);
        return true;
    }

    private void ApplyDiscoverFilters() =>
        store.RefreshDiscover(VelvetFilterSelection.Combine(discoverInclude, discoverExclude, mutes),
            discoverApplied.Trim(), discoverInclude.Region);

    private void ApplyFeedFilters() =>
        store.SetFeedFilter(VelvetFilterSelection.Combine(feedInclude, feedExclude, mutes), feedInclude.Region);

    private void ApplyFilters(VelvetPage surface)
    {
        if (surface == VelvetPage.Feed)
        {
            ApplyFeedFilters();
            return;
        }

        ApplyDiscoverFilters();
    }

    private void ApplyBothFilters()
    {
        ApplyDiscoverFilters();
        ApplyFeedFilters();
    }

    private void OpenFilters(VelvetPage surface)
    {
        filterSurface = surface;
        router.Push(VelvetView.Filters);
    }

    private void DrawFilters(Rect area)
    {
        var scale = UiScale.Current;
        var surface = filterSurface;
        var include = IncludeFor(surface);
        var exclude = ExcludeFor(surface);
        if (VHeader.Push(area, Loc.T(L.Velvet.FiltersTitle), theme))
        {
            router.Pop();
            return;
        }

        if ((include.Any || exclude.Any || mutes.Any) && ui.HeaderAction(area, Loc.T(L.Velvet.FilterClearAll), true))
        {
            include.Clear();
            exclude.Clear();
            mutes.Clear();
            SyncSurface(surface);
            ApplyBothFilters();
        }

        var changedInclude = false;
        var changedExclude = false;
        var changedMutes = false;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(8f);
            ui.HelpText(Loc.T(L.Velvet.FilterHint));
            Gap(14f);

            VSectionHeader.Card(FontAwesomeIcon.Globe, Loc.T(L.Velvet.RegionLabel));
            Gap(8f);
            changedInclude |= DrawRegionFilterRow(include);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Compass, Loc.T(L.Velvet.CardIntent));
            Gap(6f);
            DrawIntentFilterChips(include, exclude, ref changedInclude, ref changedExclude);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.VenusMars, Loc.T(L.Velvet.CardGender));
            Gap(6f);
            DrawGenderFilterChips(include, mutes, ref changedInclude, ref changedMutes);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Rainbow, Loc.T(L.Velvet.CardSexuality));
            Gap(6f);
            DrawSexualityFilterChips(include, exclude, ref changedInclude, ref changedExclude);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Heart, Loc.T(L.Velvet.CardRole));
            Gap(6f);
            DrawTriStateTokenChips(VelvetSuggestions.Roles, VelvetTheme.Rose, include.Roles, exclude.Roles,
                ref changedInclude, ref changedExclude);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Fire, Loc.T(L.Velvet.CardKinks));
            Gap(6f);
            DrawTriStateTokenChips(VelvetSuggestions.Kinks, VelvetSuggestions.KinkHue, include.Kinks, mutes.Kinks,
                ref changedInclude, ref changedMutes);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.ShieldAlt, Loc.T(L.Velvet.CardLimits));
            Gap(6f);
            DrawTriStateTokenChips(VelvetSuggestions.Limits, VelvetTheme.Gold, include.Limits, mutes.Limits,
                ref changedInclude, ref changedMutes);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.HandHoldingHeart, Loc.T(L.Velvet.CardRelationship));
            Gap(6f);
            DrawRelationshipFilterChips(include, exclude, ref changedInclude, ref changedExclude);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Hashtag, Loc.T(L.Velvet.CardTags));
            Gap(6f);
            DrawTagsFilterChips(include, exclude, ref changedInclude, ref changedExclude);
            Gap(24f);

            if (ui.PillButton(Reserve(46f), Loc.T(L.Velvet.FilterDone), true))
            {
                router.Pop();
            }

            Gap(40f);
        }

        if (changedInclude || changedExclude || changedMutes)
        {
            SyncSurface(surface);
            if (changedMutes)
            {
                ApplyBothFilters();
            }
            else
            {
                ApplyFilters(surface);
            }
        }
    }

    private bool DrawRegionFilterRow(VelvetFilterSelection include)
    {
        var scale = UiScale.Current;
        var codes = SocialRegion.Codes;
        var labels = new string[codes.Length + 1];
        labels[0] = Loc.T(L.Velvet.RegionAny);
        var current = 0;
        for (var index = 0; index < codes.Length; index++)
        {
            labels[index + 1] = codes[index];
            if (string.Equals(include.Region, codes[index], StringComparison.Ordinal))
            {
                current = index + 1;
            }
        }

        var picked = VSegmented.Draw("velvetFilterRegion", Reserve(34f), labels, current, scale);
        if (picked < 0 || picked == current)
        {
            return false;
        }

        include.Region = picked == 0 ? string.Empty : codes[picked - 1];
        return true;
    }

    private void DrawIntentFilterChips(VelvetFilterSelection include, VelvetFilterSelection exclude,
        ref bool changedInclude, ref bool changedExclude)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var defs = VelvetIntent.All;
        var models = new VChipModel[defs.Length];
        for (var index = 0; index < defs.Length; index++)
        {
            var def = defs[index];
            models[index] = TriStateChip(Loc.T(def.Label), def.Hue, include.Intent, exclude.Intent, def.Flag);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleMaskState(ref include.Intent, ref exclude.Intent, defs[clicked].Flag, ref changedInclude,
            ref changedExclude);
    }

    private void DrawGenderFilterChips(VelvetFilterSelection include, VelvetFilterSelection exclude,
        ref bool changedInclude, ref bool changedExclude)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetGender.All;
        var models = new VChipModel[options.Length];
        for (var index = 0; index < options.Length; index++)
        {
            models[index] = TriStateChip(VelvetGender.Label(options[index]), VelvetTheme.Rose, include.Gender,
                exclude.Gender, options[index]);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleMaskState(ref include.Gender, ref exclude.Gender, options[clicked], ref changedInclude,
            ref changedExclude);
    }

    private void DrawSexualityFilterChips(VelvetFilterSelection include, VelvetFilterSelection exclude,
        ref bool changedInclude, ref bool changedExclude)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetSexuality.All;
        var models = new VChipModel[options.Length];
        for (var index = 0; index < options.Length; index++)
        {
            models[index] = TriStateChip(VelvetSexuality.Label(options[index]), VelvetTheme.Rose, include.Sexuality,
                exclude.Sexuality, options[index]);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleMaskState(ref include.Sexuality, ref exclude.Sexuality, options[clicked], ref changedInclude,
            ref changedExclude);
    }

    private void DrawRelationshipFilterChips(VelvetFilterSelection include, VelvetFilterSelection exclude,
        ref bool changedInclude, ref bool changedExclude)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var statuses = VelvetRelationship.All;
        var models = new VChipModel[statuses.Length];
        for (var index = 0; index < statuses.Length; index++)
        {
            models[index] = TriStateChip(VelvetRelationship.Label(statuses[index]), VelvetTheme.Rose,
                include.Relationship, exclude.Relationship, 1 << statuses[index]);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleMaskState(ref include.Relationship, ref exclude.Relationship, 1 << statuses[clicked],
            ref changedInclude, ref changedExclude);
    }

    private void DrawTagsFilterChips(VelvetFilterSelection include, VelvetFilterSelection exclude,
        ref bool changedInclude, ref bool changedExclude)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var categories = VelvetSuggestions.TagCategories;
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            var headerOrigin = ImGui.GetCursorScreenPos();
            Typography.Draw(headerOrigin, Loc.Culture.TextInfo.ToUpper(Loc.T(category.Title)),
                VelvetTheme.Lerp(category.Hue, VelvetTheme.OnAccent, 0.30f), TextStyles.SubheadlineEmphasized);
            ImGui.SetCursorScreenPos(headerOrigin);
            ImGui.Dummy(new Vector2(width, 24f * scale));
            DrawTriStateTokenChips(category.Tags, category.Hue, include.Tags, exclude.Tags, ref changedInclude,
                ref changedExclude);
            if (index < categories.Length - 1)
            {
                Gap(12f);
            }
        }
    }

    private void DrawTriStateTokenChips(string[] options, Vector4 accent, HashSet<string> include,
        HashSet<string> exclude, ref bool changedInclude, ref bool changedExclude)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var models = new VChipModel[options.Length];
        for (var index = 0; index < options.Length; index++)
        {
            var token = options[index];
            if (include.Contains(token))
            {
                models[index] = new VChipModel(token, VChipStyle.Solid, accent, FontAwesomeIcon.Check);
            }
            else if (exclude.Contains(token))
            {
                models[index] = new VChipModel(token, VChipStyle.Solid, VelvetTheme.Danger, FontAwesomeIcon.Ban);
            }
            else
            {
                models[index] = new VChipModel(token, VChipStyle.Ghost, VelvetTheme.Moonlight);
            }
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleTokenState(include, exclude, options[clicked], ref changedInclude, ref changedExclude);
    }

    private static VChipModel TriStateChip(string label, Vector4 accent, int include, int exclude, int flag)
    {
        if ((include & flag) != 0)
        {
            return new VChipModel(label, VChipStyle.Solid, accent, FontAwesomeIcon.Check);
        }

        if ((exclude & flag) != 0)
        {
            return new VChipModel(label, VChipStyle.Solid, VelvetTheme.Danger, FontAwesomeIcon.Ban);
        }

        return new VChipModel(label, VChipStyle.Ghost, VelvetTheme.Moonlight);
    }

    private static void CycleMaskState(ref int include, ref int exclude, int flag, ref bool changedInclude,
        ref bool changedExclude)
    {
        if ((include & flag) != 0)
        {
            include &= ~flag;
            exclude |= flag;
            changedInclude = true;
            changedExclude = true;
            return;
        }

        if ((exclude & flag) != 0)
        {
            exclude &= ~flag;
            changedExclude = true;
            return;
        }

        include |= flag;
        changedInclude = true;
    }

    private static void CycleTokenState(HashSet<string> include, HashSet<string> exclude, string token,
        ref bool changedInclude, ref bool changedExclude)
    {
        if (include.Remove(token))
        {
            exclude.Add(token);
            changedInclude = true;
            changedExclude = true;
            return;
        }

        if (exclude.Remove(token))
        {
            changedExclude = true;
            return;
        }

        include.Add(token);
        changedInclude = true;
    }
}
