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
    private const float CardHoverGrow = 0.06f;
    private const float CardHoverTopLift = 0.18f;
    private const float CardHoverBottomLift = 0.10f;
    private const float CardRimAlpha = 0.30f;
    private const float CardRimWeight = 1.5f;
    private const float CardGlowReach = 8f;
    private const float FilterSummaryHeight = 30f;
    private const float FilterSummaryGlyphGap = 8f;
    private const string FilterSummarySeparator = " · ";
    private const float EndActionWidth = 168f;
    private const float EndActionHeight = 38f;
    private const float EndActionGap = 10f;
    private const float EndActionTop = 22f;

    private static readonly Vector4 CardShadow = new(0f, 0f, 0f, 0.30f);
    private static readonly TextStyle FilterSummaryStyle = TextStyles.FootnoteEmphasized;

    private readonly List<VelvetProfileDto> cards = new();
    private readonly List<int> cardScores = new();
    private readonly System.Text.StringBuilder filterSummaryBuilder = new();
    private VelvetProfileDto[] cardSource = Array.Empty<VelvetProfileDto>();
    private VelvetProfileDto? cardViewer;
    private int cardsVersion;
    private string filterSummary = string.Empty;
    private bool filterSummaryDirty = true;
    private LanguageInfo? filterSummaryLanguage;

    private void DrawDiscover(Rect area)
    {
        var body = discoverInclude.Any || mutes.Any ? DrawFilterSummary(area) : area;
        if (!store.DiscoverLoaded && !store.LoadingDiscover)
        {
            ApplyDiscoverFilters();
        }

        SyncCards();
        DrawDiscoverGrid(body);
    }

    private void ResetCards()
    {
        cards.Clear();
        cardScores.Clear();
        cardSource = Array.Empty<VelvetProfileDto>();
        cardViewer = null;
        filterSummaryDirty = true;
        gridLabels.Clear();
        cardsVersion++;
    }

    private void SyncCards()
    {
        var source = store.DiscoverResults;
        var me = store.Me;
        var sameSource = ReferenceEquals(source, cardSource);
        if (sameSource && ReferenceEquals(me, cardViewer))
        {
            return;
        }

        cardsVersion++;
        if (sameSource && cardViewer is not null)
        {
            cardViewer = me;
            return;
        }

        if (!sameSource && TryPatchCards(source))
        {
            cardSource = source;
            cardViewer = me;
            return;
        }

        var appended = ReferenceEquals(me, cardViewer) && ExtendsCardSource(source);
        var firstNew = appended ? cardSource.Length : 0;
        cardSource = source;
        cardViewer = me;
        if (!appended)
        {
            cards.Clear();
            cardScores.Clear();
        }

        var floor = cards.Count;
        var allowedRegions = AllowedRegions(discoverInclude);
        var skippedConnected = 0;
        var skippedRegion = 0;
        for (var index = firstNew; index < source.Length; index++)
        {
            var profile = source[index];
            if (profile.ConnectionState != VelvetConnectionState.None)
            {
                skippedConnected++;
                continue;
            }

            if (!RegionAllowed(profile, allowedRegions))
            {
                skippedRegion++;
                continue;
            }

            InsertByScore(profile, VelvetFit.Score(me, profile), floor);
        }

        AepLog.Info($"Velvet discover {(appended ? "extended" : "rebuilt")}: {cards.Count} cards from {source.Length} "
            + $"profiles, {skippedConnected} already connected, {skippedRegion} out of region");
    }

    private bool TryPatchCards(VelvetProfileDto[] source)
    {
        if (cards.Count == 0 || source.Length != cardSource.Length)
        {
            return false;
        }

        for (var index = 0; index < cards.Count; index++)
        {
            var replacement = FindProfile(source, cards[index].UserId);
            if (replacement is null)
            {
                return false;
            }

            cards[index] = replacement;
        }

        return true;
    }

    private static VelvetProfileDto? FindProfile(VelvetProfileDto[] source, string userId)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].UserId == userId)
            {
                return source[index];
            }
        }

        return null;
    }

    private bool ExtendsCardSource(VelvetProfileDto[] source)
    {
        var known = cardSource.Length;
        return known > 0 && source.Length > known && ReferenceEquals(source[0], cardSource[0])
            && ReferenceEquals(source[known - 1], cardSource[known - 1]);
    }

    private bool RegionAllowed(VelvetProfileDto profile, int allowedRegions)
    {
        if (allowedRegions == SocialRegion.AllMask)
        {
            return true;
        }

        var regionIndex = Array.IndexOf(SocialRegion.Codes, RegionCodeOf(profile));
        return regionIndex >= 0 && (allowedRegions & (1 << regionIndex)) != 0;
    }

    private void InsertByScore(VelvetProfileDto profile, int score, int floor)
    {
        var at = cards.Count;
        while (at > floor && cardScores[at - 1] < score)
        {
            at--;
        }

        cards.Insert(at, profile);
        cardScores.Insert(at, score);
    }

    private void RefillCards(int below)
    {
        if (cards.Count < below && store.HasMoreDiscover && !store.LoadingDiscover && !store.LoadingMoreDiscover)
        {
            store.LoadMoreDiscover();
        }
    }

    private void RefreshDiscover()
    {
        if (!store.IsSignedIn || store.LoadingDiscover)
        {
            return;
        }

        gridScrollTopPending = true;
        ApplyDiscoverFilters();
    }

    private static Vector4 CardBodyTone(Vector4 tone, float lift, float alpha) =>
        VelvetTheme.Alpha(VelvetTheme.Lerp(tone, VelvetTheme.OnAccent, lift), alpha);

    private void DrawDiscoverEmpty(Rect body)
    {
        var scale = UiScale.Current;
        var loading = store.LoadingDiscover || store.LoadingMoreDiscover;
        var failed = !loading && store.DiscoverFailed;
        if (failed)
        {
            discoverFailure.Set(store.DiscoverFailure);
        }

        var title = loading ? Loc.T(L.Velvet.DiscoverLoading)
            : failed ? Loc.T(L.Failure.CouldNotLoad) : Loc.T(L.Velvet.DiscoverEndTitle);
        var hint = loading ? string.Empty : failed ? discoverFailure.Text() : Loc.T(L.Velvet.DiscoverEndHint);
        var bottom = DrawEmpty(body, title, hint);
        if (loading)
        {
            return;
        }

        var actionTop = bottom + EndActionTop * scale;
        if (failed)
        {
            if (DrawEndAction(body, ref actionTop, Loc.T(L.Common.Retry), ConfirmButtonTone.Primary,
                    "velvet.discover.retry"))
            {
                ApplyDiscoverFilters();
            }

            return;
        }

        var lead = true;
        if ((discoverInclude.RegionMask != 0 || mutes.RegionMask != 0)
            && DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.DiscoverWidenRegion), NextTone(ref lead),
                "velvet.discover.widen"))
        {
            discoverInclude.RegionMask = 0;
            mutes.RegionMask = 0;
            ApplyMutesEverywhere();
        }

        if (discoverInclude.AnyBesidesRegion && DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.FilterClearAll),
                NextTone(ref lead), "velvet.discover.clear"))
        {
            discoverInclude.Clear();
            ApplyDiscoverFilters();
        }

        if (!discoverInclude.Any && DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.DiscoverCheckAgain),
                NextTone(ref lead), "velvet.discover.again"))
        {
            ApplyDiscoverFilters();
        }
    }

    private static ConfirmButtonTone NextTone(ref bool lead)
    {
        if (!lead)
        {
            return ConfirmButtonTone.Neutral;
        }

        lead = false;
        return ConfirmButtonTone.Primary;
    }

    private bool DrawEndAction(Rect body, ref float top, string label, ConfirmButtonTone tone, string id)
    {
        var scale = UiScale.Current;
        var halfWidth = EndActionWidth * scale * 0.5f;
        var rect = new Rect(new Vector2(body.Center.X - halfWidth, top),
            new Vector2(body.Center.X + halfWidth, top + EndActionHeight * scale));
        top = rect.Max.Y + EndActionGap * scale;
        return ConfirmDialog.DrawPillButton(rect, label, true, theme, 1f, 1f, tone, id);
    }

    private Rect DrawFilterSummary(Rect area)
    {
        var scale = UiScale.Current;
        var row = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + FilterSummaryHeight * scale));
        EnsureFilterSummary();
        var drawList = ImGui.GetWindowDrawList();
        var pad = SocialChrome.CellPadX * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, VelvetTheme.HoverWash.Packed());
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var glyphSize = VIcon.Chip * scale;
        var glyphCenter = new Vector2(row.Min.X + pad + glyphSize * 0.5f, row.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, PhoneIcons.AdjustmentsHorizontal, VelvetTheme.RoseInk, glyphSize);
        var textLeft = glyphCenter.X + glyphSize * 0.5f + FilterSummaryGlyphGap * scale;
        var maxWidth = MathF.Max(1f, row.Max.X - pad - textLeft);
        Typography.Draw(drawList,
            new Vector2(textLeft, row.Center.Y - Typography.LineHeight(FilterSummaryStyle) * 0.5f),
            Typography.FitText(filterSummary, maxWidth, FilterSummaryStyle), VelvetTheme.RoseInk, FilterSummaryStyle);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenFilters(VelvetPage.Discover);
        }

        return new Rect(new Vector2(area.Min.X, row.Max.Y), area.Max);
    }

    private void EnsureFilterSummary()
    {
        if (!filterSummaryDirty && ReferenceEquals(filterSummaryLanguage, Loc.Current))
        {
            return;
        }

        filterSummaryDirty = false;
        filterSummaryLanguage = Loc.Current;
        var builder = filterSummaryBuilder;
        builder.Clear();
        var include = discoverInclude;
        for (var index = 0; index < SocialRegion.Codes.Length; index++)
        {
            if ((include.RegionMask & (1 << index)) != 0)
            {
                AppendSummary(SocialRegion.Codes[index]);
            }
        }

        var intents = VelvetIntent.All;
        for (var index = 0; index < intents.Length; index++)
        {
            if ((include.Intent & intents[index].Flag) != 0)
            {
                AppendSummary(Loc.T(intents[index].Label));
            }
        }

        var races = VelvetRace.All;
        for (var index = 0; index < races.Length; index++)
        {
            if (VelvetRace.Has(include.Race, races[index]))
            {
                AppendSummary(VelvetRace.Label(gameData, races[index]));
            }
        }

        AppendMaskSummary(include.Gender, VelvetGender.All, GenderLabelOf);
        AppendMaskSummary(include.Sexuality, VelvetSexuality.All, SexualityLabelOf);
        AppendMaskSummary(include.Languages, VelvetLanguages.All, LanguageLabelOf);
        var statuses = VelvetRelationship.All;
        for (var index = 0; index < statuses.Length; index++)
        {
            if ((include.Relationship & (1 << statuses[index])) != 0)
            {
                AppendSummary(VelvetRelationship.Label(statuses[index]));
            }
        }

        AppendTokenSummary(include.Roles);
        AppendTokenSummary(include.Kinks);
        AppendTokenSummary(include.Limits);
        AppendTokenSummary(include.Tags);
        var hidden = CountSelections(mutes);
        if (hidden > 0)
        {
            AppendSummary(Loc.T(L.Velvet.FilterHiddenCount, hidden));
        }

        filterSummary = builder.ToString();
    }

    private static readonly Func<int, string> GenderLabelOf = VelvetGender.Label;
    private static readonly Func<int, string> SexualityLabelOf = VelvetSexuality.Label;
    private static readonly Func<int, string> LanguageLabelOf = VelvetLanguages.Label;

    private void AppendMaskSummary(int mask, int[] options, Func<int, string> labelOf)
    {
        for (var index = 0; index < options.Length; index++)
        {
            if ((mask & options[index]) != 0)
            {
                AppendSummary(labelOf(options[index]));
            }
        }
    }

    private void AppendTokenSummary(HashSet<string> tokens)
    {
        foreach (var token in tokens)
        {
            AppendSummary(VelvetTokenLabels.Of(token));
        }
    }

    private void AppendSummary(string part)
    {
        if (filterSummaryBuilder.Length > 0)
        {
            filterSummaryBuilder.Append(FilterSummarySeparator);
        }

        filterSummaryBuilder.Append(part);
    }

    private static int CountSelections(VelvetFilterSelection selection) =>
        BitOperations.PopCount((uint)selection.Intent) + BitOperations.PopCount((uint)selection.Gender)
        + BitOperations.PopCount((uint)selection.Sexuality) + BitOperations.PopCount((uint)selection.Relationship)
        + BitOperations.PopCount((uint)selection.Race) + BitOperations.PopCount((uint)selection.RegionMask)
        + BitOperations.PopCount((uint)selection.Languages)
        + selection.Roles.Count + selection.Kinks.Count + selection.Limits.Count + selection.Tags.Count;

    private string RegionCodeOf(VelvetProfileDto profile) =>
        SocialRegion.Resolve(profile.Region, profile.World, gameData);

    private string RegionCodeOf(UserDto user) =>
        SocialRegion.Resolve(user.Region, user.World, gameData);

    private static string DisplayNameOf(string displayName, string handle) =>
        string.IsNullOrWhiteSpace(displayName) ? "@" + handle : displayName;
}
