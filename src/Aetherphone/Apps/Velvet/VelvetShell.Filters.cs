using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private static readonly Vector4 KinkViolet = new(0.647f, 0.482f, 0.839f, 1f);

    private int filterIntentInclude;
    private int filterIntentExclude;
    private int filterGenderInclude;
    private int filterGenderExclude;
    private int filterSexualityInclude;
    private int filterSexualityExclude;
    private int filterRelationshipInclude;
    private int filterRelationshipExclude;
    private readonly HashSet<string> filterRolesInclude = new();
    private readonly HashSet<string> filterRolesExclude = new();
    private readonly HashSet<string> filterKinksInclude = new();
    private readonly HashSet<string> filterKinksExclude = new();
    private readonly HashSet<string> filterLimitsInclude = new();
    private readonly HashSet<string> filterLimitsExclude = new();

    private bool DiscoverFilterActive =>
        filterIntentInclude != 0 || filterIntentExclude != 0
        || filterGenderInclude != 0 || filterGenderExclude != 0
        || filterSexualityInclude != 0 || filterSexualityExclude != 0
        || filterRelationshipInclude != 0 || filterRelationshipExclude != 0
        || filterRolesInclude.Count > 0 || filterRolesExclude.Count > 0
        || filterKinksInclude.Count > 0 || filterKinksExclude.Count > 0
        || filterLimitsInclude.Count > 0 || filterLimitsExclude.Count > 0;

    private VelvetDiscoverFilter BuildDiscoverFilter() =>
        new(VelvetIntent.Sanitize(filterIntentInclude), VelvetIntent.Sanitize(filterIntentExclude),
            VelvetGender.Sanitize(filterGenderInclude), VelvetGender.Sanitize(filterGenderExclude),
            VelvetSexuality.Sanitize(filterSexualityInclude), VelvetSexuality.Sanitize(filterSexualityExclude),
            filterRelationshipInclude, filterRelationshipExclude,
            filterRolesInclude.ToArray(), filterRolesExclude.ToArray(),
            filterKinksInclude.ToArray(), filterKinksExclude.ToArray(),
            filterLimitsInclude.ToArray(), filterLimitsExclude.ToArray());

    private void ApplyDiscoverFilters() =>
        store.RefreshDiscover(BuildDiscoverFilter(), discoverApplied.Trim(), discoverRegion);

    private void ClearDiscoverFilters()
    {
        filterIntentInclude = 0;
        filterIntentExclude = 0;
        filterGenderInclude = 0;
        filterGenderExclude = 0;
        filterSexualityInclude = 0;
        filterSexualityExclude = 0;
        filterRelationshipInclude = 0;
        filterRelationshipExclude = 0;
        filterRolesInclude.Clear();
        filterRolesExclude.Clear();
        filterKinksInclude.Clear();
        filterKinksExclude.Clear();
        filterLimitsInclude.Clear();
        filterLimitsExclude.Clear();
        discoverRegion = string.Empty;
    }

    private void DrawDiscoverFilters(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (VHeader.Push(area, Loc.T(L.Velvet.FiltersTitle), theme))
        {
            router.Pop();
            return;
        }

        if ((DiscoverFilterActive || discoverRegion.Length > 0) &&
            ui.HeaderAction(area, Loc.T(L.Velvet.FilterClearAll), true))
        {
            ClearDiscoverFilters();
            ApplyDiscoverFilters();
        }

        var changed = false;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(8f);
            ui.HelpText(Loc.T(L.Velvet.FilterHint));
            Gap(14f);

            VSectionHeader.Card(FontAwesomeIcon.Globe, Loc.T(L.Velvet.RegionLabel));
            Gap(8f);
            changed |= DrawRegionFilterRow();
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Compass, Loc.T(L.Velvet.CardIntent));
            Gap(6f);
            changed |= DrawIntentFilterChips();
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.VenusMars, Loc.T(L.Velvet.CardGender));
            Gap(6f);
            changed |= DrawGenderFilterChips();
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Rainbow, Loc.T(L.Velvet.CardSexuality));
            Gap(6f);
            changed |= DrawSexualityFilterChips();
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Heart, Loc.T(L.Velvet.CardRole));
            Gap(6f);
            changed |= DrawTriStateTokenChips(VelvetSuggestions.Roles, VelvetTheme.Rose, filterRolesInclude,
                filterRolesExclude);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Fire, Loc.T(L.Velvet.CardKinks));
            Gap(6f);
            changed |= DrawTriStateTokenChips(VelvetSuggestions.Kinks, KinkViolet, filterKinksInclude,
                filterKinksExclude);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.ShieldAlt, Loc.T(L.Velvet.CardLimits));
            Gap(6f);
            changed |= DrawTriStateTokenChips(VelvetSuggestions.Limits, VelvetTheme.Gold, filterLimitsInclude,
                filterLimitsExclude);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.HandHoldingHeart, Loc.T(L.Velvet.CardRelationship));
            Gap(6f);
            changed |= DrawRelationshipFilterChips();
            Gap(24f);

            if (ui.PillButton(Reserve(46f), Loc.T(L.Velvet.FilterDone), true))
            {
                router.Pop();
            }

            Gap(40f);
        }

        if (changed)
        {
            ApplyDiscoverFilters();
        }
    }

    private bool DrawRegionFilterRow()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var codes = SocialRegion.Codes;
        var labels = new string[codes.Length + 1];
        labels[0] = Loc.T(L.Velvet.RegionAny);
        var current = 0;
        for (var index = 0; index < codes.Length; index++)
        {
            labels[index + 1] = codes[index];
            if (string.Equals(discoverRegion, codes[index], StringComparison.Ordinal))
            {
                current = index + 1;
            }
        }

        var picked = VSegmented.Draw("velvetFilterRegion", Reserve(34f), labels, current, scale);
        if (picked < 0 || picked == current)
        {
            return false;
        }

        discoverRegion = picked == 0 ? string.Empty : codes[picked - 1];
        return true;
    }

    private bool DrawIntentFilterChips()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var defs = VelvetIntent.All;
        var models = new VChipModel[defs.Length];
        for (var index = 0; index < defs.Length; index++)
        {
            var def = defs[index];
            models[index] = TriStateChip(Loc.T(def.Label), def.Hue, filterIntentInclude, filterIntentExclude,
                def.Flag);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return false;
        }

        CycleMaskState(ref filterIntentInclude, ref filterIntentExclude, defs[clicked].Flag);
        return true;
    }

    private bool DrawGenderFilterChips()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetGender.All;
        var models = new VChipModel[options.Length];
        for (var index = 0; index < options.Length; index++)
        {
            models[index] = TriStateChip(VelvetGender.Label(options[index]), VelvetTheme.Rose, filterGenderInclude,
                filterGenderExclude, options[index]);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return false;
        }

        CycleMaskState(ref filterGenderInclude, ref filterGenderExclude, options[clicked]);
        return true;
    }

    private bool DrawSexualityFilterChips()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetSexuality.All;
        var models = new VChipModel[options.Length];
        for (var index = 0; index < options.Length; index++)
        {
            models[index] = TriStateChip(VelvetSexuality.Label(options[index]), VelvetTheme.Rose,
                filterSexualityInclude, filterSexualityExclude, options[index]);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return false;
        }

        CycleMaskState(ref filterSexualityInclude, ref filterSexualityExclude, options[clicked]);
        return true;
    }

    private bool DrawRelationshipFilterChips()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var statuses = VelvetRelationship.All;
        var models = new VChipModel[statuses.Length];
        for (var index = 0; index < statuses.Length; index++)
        {
            models[index] = TriStateChip(VelvetRelationship.Label(statuses[index]), VelvetTheme.Rose,
                filterRelationshipInclude, filterRelationshipExclude, 1 << statuses[index]);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked < 0)
        {
            return false;
        }

        CycleMaskState(ref filterRelationshipInclude, ref filterRelationshipExclude, 1 << statuses[clicked]);
        return true;
    }

    private bool DrawTriStateTokenChips(string[] options, Vector4 accent, HashSet<string> include,
        HashSet<string> exclude)
    {
        var scale = ImGuiHelpers.GlobalScale;
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
            return false;
        }

        CycleTokenState(include, exclude, options[clicked]);
        return true;
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

    private static void CycleMaskState(ref int include, ref int exclude, int flag)
    {
        if ((include & flag) != 0)
        {
            include &= ~flag;
            exclude |= flag;
            return;
        }

        if ((exclude & flag) != 0)
        {
            exclude &= ~flag;
            return;
        }

        include |= flag;
    }

    private static void CycleTokenState(HashSet<string> include, HashSet<string> exclude, string token)
    {
        if (include.Remove(token))
        {
            exclude.Add(token);
            return;
        }

        if (exclude.Remove(token))
        {
            return;
        }

        include.Add(token);
    }
}
