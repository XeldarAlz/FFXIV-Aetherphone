using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float DeckActionBarHeight = 100f;
    private const float DeckActionRadius = 27f;
    private const float DeckActionGap = 14f;
    private const float DeckActionBottomPad = 12f;
    private const float DeckSayHeight = 48f;
    private const float DeckSayPad = 12f;
    private const float DeckUndoTop = 6f;
    private const float DeckUndoHeight = 24f;
    private const float DeckUndoPad = 14f;
    private const float DeckCommitFraction = 0.32f;
    private const float DeckExitOverhang = 48f;
    private const float DeckExitSmoothTime = 0.16f;
    private const float DeckSettleEpsilon = 2f;
    private const float DeckPressShrink = 0.94f;
    private const float DeckTooltipSmoothTime = 0.11f;
    private const float DeckHoverSmoothTime = 0.11f;
    private const float DeckHoverGrow = 0.06f;
    private const float DeckHoverTopLift = 0.18f;
    private const float DeckHoverBottomLift = 0.10f;
    private const float DeckRimAlpha = 0.30f;
    private const float DeckRimWeight = 1.5f;
    private const float DeckGlowReach = 8f;
    private const float DeckSayGrow = 2f;
    private const float DeckIdleFillAlpha = 0.6f;
    private const float DeckIdleInkAlpha = 0.5f;
    private const string DeckPassId = "velvetDeckPass";
    private const string DeckSayId = "velvetDeckSay";
    private const string DeckConnectId = "velvetDeckConnect";
    private const int DeckRefillBelow = 4;
    private const int DeckRevisitPenalty = 1000;
    private const float FilterSummaryHeight = 30f;
    private const float FilterSummaryGlyphGap = 8f;
    private const string FilterSummarySeparator = " · ";
    private const float EndActionWidth = 168f;
    private const float EndActionHeight = 38f;
    private const float EndActionGap = 10f;
    private const float EndActionTop = 22f;

    private static readonly Vector4 DeckShadow = new(0f, 0f, 0f, 0.30f);
    private static readonly TextStyle DeckSayStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle DeckUndoStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle FilterSummaryStyle = TextStyles.FootnoteEmphasized;

    private readonly List<VelvetProfileDto> deck = new();
    private readonly List<int> deckScores = new();
    private readonly HashSet<string> revisited = new(StringComparer.Ordinal);
    private readonly System.Text.StringBuilder filterSummaryBuilder = new();
    private VelvetProfileDto[] deckSource = Array.Empty<VelvetProfileDto>();
    private VelvetProfileDto? deckMe;
    private string deckTopId = string.Empty;
    private VelvetProfileDto? lastPassed;
    private bool deckRecycled;
    private Spring cardSlide;
    private Spring passTooltipEase;
    private Spring connectTooltipEase;
    private Spring passHoverEase;
    private Spring sayHoverEase;
    private Spring connectHoverEase;
    private int cardExit;
    private string filterSummary = string.Empty;
    private bool filterSummaryDirty = true;
    private LanguageInfo? filterSummaryLanguage;

    private void DrawDiscover(Rect area)
    {
        var scale = UiScale.Current;
        var body = discoverInclude.Any || mutes.Any ? DrawFilterSummary(area) : area;
        if (!store.DiscoverLoaded && !store.LoadingDiscover)
        {
            ApplyDiscoverFilters();
        }

        SyncDeck();
        RefillDeck();
        RecycleWhenDry();
        StepCard(body.Width);
        if (deck.Count == 0)
        {
            DrawDeckEmpty(body);
            return;
        }

        var top = deck[0];
        var barRect = new Rect(new Vector2(body.Min.X, body.Max.Y - DeckActionBarHeight * scale), body.Max);
        using (var surface = AppSurface.BeginEdgeToEdge(body))
        {
            DrawDeckCard(top, body, in surface);
        }

        DrawDeckActions(barRect, top);
        if (deck.Count > 1)
        {
            images.Get(deck[1].AvatarUrl);
        }
    }

    private void ResetDeck()
    {
        deck.Clear();
        deckScores.Clear();
        deckSource = Array.Empty<VelvetProfileDto>();
        deckMe = null;
        deckTopId = string.Empty;
        lastPassed = null;
        revisited.Clear();
        deckRecycled = false;
        cardSlide.SnapTo(0f);
        cardExit = 0;
        filterSummaryDirty = true;
    }

    private void SyncDeck()
    {
        var source = store.DiscoverResults;
        var me = store.Me;
        if (ReferenceEquals(source, deckSource) && ReferenceEquals(me, deckMe))
        {
            return;
        }

        deckSource = source;
        deckMe = me;
        deck.Clear();
        deckScores.Clear();
        var allowedRegions = AllowedRegions(discoverInclude);
        var skippedConnected = 0;
        var skippedRegion = 0;
        for (var index = 0; index < source.Length; index++)
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

            var score = VelvetFit.Score(me, profile);
            InsertByScore(profile, SeenBefore(profile.UserId) ? score - DeckRevisitPenalty : score);
        }

        AepLog.Info($"Velvet deck rebuilt: {deck.Count} cards from {source.Length} profiles, "
            + $"{skippedConnected} already connected, {skippedRegion} out of region, {store.PassCount} passes held, "
            + $"{revisited.Count} on a second look");
        PinDeckTop();
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

    private void InsertByScore(VelvetProfileDto profile, int score)
    {
        var at = deck.Count;
        while (at > 0 && deckScores[at - 1] < score)
        {
            at--;
        }

        deck.Insert(at, profile);
        deckScores.Insert(at, score);
    }

    private void PinDeckTop()
    {
        if (deckTopId.Length > 0)
        {
            for (var index = 1; index < deck.Count; index++)
            {
                if (deck[index].UserId != deckTopId)
                {
                    continue;
                }

                var pinned = deck[index];
                var score = deckScores[index];
                deck.RemoveAt(index);
                deckScores.RemoveAt(index);
                deck.Insert(0, pinned);
                deckScores.Insert(0, score);
                break;
            }
        }

        var topId = deck.Count > 0 ? deck[0].UserId : string.Empty;
        if (topId == deckTopId)
        {
            return;
        }

        deckTopId = topId;
        OnDeckTopChanged();
    }

    private void RefillDeck()
    {
        if (deck.Count < DeckRefillBelow && store.HasMoreDiscover && !store.LoadingDiscover
            && !store.LoadingMoreDiscover)
        {
            store.LoadMoreDiscover();
        }
    }

    private void RecycleWhenDry()
    {
        if (deck.Count >= DeckRefillBelow || deckRecycled || cardExit != 0 || store.PassCount == 0)
        {
            return;
        }

        if (!store.DiscoverLoaded || store.LoadingDiscover || store.LoadingMoreDiscover || store.HasMoreDiscover
            || store.DiscoverFailed)
        {
            return;
        }

        AepLog.Info($"Velvet deck down to {deck.Count} cards with no pages left, "
            + $"bringing back {store.PassCount} passes for a second look");
        ShowPassesAgain();
    }

    private void ShowPassesAgain()
    {
        deckRecycled = true;
        lastPassed = null;
        store.CopyPassedIds(revisited);
        store.ClearPasses();
        ApplyDiscoverFilters();
    }

    private bool SeenBefore(string userId) => revisited.Count > 0 && revisited.Contains(userId);

    private void StepCard(float width)
    {
        if (cardExit == 0)
        {
            return;
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var target = cardExit * (width + DeckExitOverhang * UiScale.Current);
        cardSlide.Step(target, DeckExitSmoothTime, delta);
        if (MathF.Abs(cardSlide.Value - target) <= DeckSettleEpsilon)
        {
            CommitCardExit();
        }
    }

    private void CommitCardExit()
    {
        var direction = cardExit;
        cardExit = 0;
        cardSlide.SnapTo(0f);
        if (deck.Count == 0)
        {
            return;
        }

        var top = deck[0];
        deckTopId = string.Empty;
        if (direction < 0)
        {
            lastPassed = top;
            store.PassFromDiscover(top.UserId);
        }
        else
        {
            lastPassed = null;
            store.Connect(top.UserId);
        }

        SyncDeck();
    }

    private void PassTop()
    {
        if (cardExit == 0 && deck.Count > 0)
        {
            cardExit = -1;
        }
    }

    private void ConnectTop()
    {
        if (cardExit == 0 && deck.Count > 0)
        {
            cardExit = 1;
        }
    }

    private void UndoPass()
    {
        if (lastPassed is not { } restored)
        {
            return;
        }

        lastPassed = null;
        deckTopId = restored.UserId;
        store.RestoreToDiscover(restored);
        SyncDeck();
        OnDeckTopChanged();
    }

    private void DrawDeckActions(Rect bar, VelvetProfileDto top)
    {
        var scale = UiScale.Current;
        ImGui.SetCursorScreenPos(bar.Min);
        using var overlay = ImRaii.Child("##velvetDeckActions", bar.Size, false,
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.FillVerticalGradient(drawList, bar.Min, bar.Max, 0f,
            VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0f).Packed(),
            VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0.96f).Packed());
        var barHovered = UiInteract.HoverOverlay(bar);
        var live = cardExit == 0;
        var radius = DeckActionRadius * scale;
        var pad = SocialChrome.CellPadX * scale;
        var rowCenterY = bar.Max.Y - DeckActionBottomPad * scale - radius;
        var passCenter = new Vector2(bar.Min.X + pad + radius, rowCenterY);
        var connectCenter = new Vector2(bar.Max.X - pad - radius, rowCenterY);
        var sayHalf = DeckSayHeight * scale * 0.5f;
        var sayRect = new Rect(
            new Vector2(passCenter.X + radius + DeckActionGap * scale, rowCenterY - sayHalf),
            new Vector2(connectCenter.X - radius - DeckActionGap * scale, rowCenterY + sayHalf));

        if (DrawDeckCircle(drawList, DeckPassId, passCenter, radius, PhoneIcons.X, VelvetTheme.CardHi,
                VelvetTheme.Card, VelvetTheme.BodyInk, 0f, Loc.T(L.Velvet.DeckPass), barHovered, live,
                ref passHoverEase, ref passTooltipEase))
        {
            PassTop();
        }

        if (DrawDeckSay(drawList, sayRect, barHovered, live))
        {
            RequestIntro(top.UserId, top.DisplayName, top.Handle, top.AvatarUrl);
        }

        if (DrawDeckCircle(drawList, DeckConnectId, connectCenter, radius, PhoneIcons.HeartFilled, VelvetTheme.Rose,
                VelvetTheme.RoseDeep, VelvetTheme.OnAccent, DeckGlowReach, Loc.T(L.Velvet.Connect), barHovered, live,
                ref connectHoverEase, ref connectTooltipEase))
        {
            ConnectTop();
        }

        if (lastPassed is null)
        {
            return;
        }

        var undoLabel = Loc.T(L.Velvet.DeckUndo);
        var undoWidth = Typography.Measure(undoLabel, DeckUndoStyle).X + DeckUndoPad * 2f * scale;
        var undoTop = bar.Min.Y + DeckUndoTop * scale;
        var undoRect = new Rect(new Vector2(bar.Center.X - undoWidth * 0.5f, undoTop),
            new Vector2(bar.Center.X + undoWidth * 0.5f, undoTop + DeckUndoHeight * scale));
        if (DrawDeckGhost(drawList, undoRect, undoLabel, barHovered, live))
        {
            UndoPass();
        }
    }

    private static bool DrawDeckCircle(ImDrawListPtr drawList, string id, Vector2 center, float radius, string glyph,
        Vector4 fill, Vector4 deep, Vector4 ink, float glowReach, string tooltip, bool barHovered, bool live,
        ref Spring hoverEase, ref Spring tooltipEase)
    {
        var scale = UiScale.Current;
        var extent = new Vector2(radius, radius);
        var hovered = live && barHovered && ImGui.IsMouseHoveringRect(center - extent, center + extent);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var eased = Math.Clamp(hoverEase.Step(hovered ? 1f : 0f, DeckHoverSmoothTime, delta), 0f, 1f);
        var drawRadius = radius * (1f + DeckHoverGrow * eased) * PressFx.Scale(id, pressed, DeckPressShrink);
        var fillAlpha = live ? 1f : DeckIdleFillAlpha;
        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), drawRadius, DeckShadow.Packed(), 40);
        AccentGloss.Circle(drawList, center, drawRadius,
            DeckBodyTone(fill, DeckHoverTopLift * eased, fillAlpha),
            DeckBodyTone(deep, DeckHoverBottomLift * eased, fillAlpha), scale, eased, glowReach);
        if (eased > 0.001f)
        {
            drawList.AddCircle(center, drawRadius,
                VelvetTheme.Alpha(VelvetTheme.OnAccent, DeckRimAlpha * eased).Packed(), 48, DeckRimWeight * scale);
        }

        PhoneIcon.Draw(drawList, center, glyph, live ? ink : VelvetTheme.Alpha(ink, DeckIdleInkAlpha),
            VIcon.CardAction * scale * drawRadius / radius);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Enqueue(new Rect(center - extent, center + extent), tooltip,
            tooltipEase.Step(hovered ? 1f : 0f, DeckTooltipSmoothTime, delta), HoverLabelSide.Above);
        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    private bool DrawDeckSay(ImDrawListPtr drawList, Rect rect, bool barHovered, bool live)
    {
        var scale = UiScale.Current;
        var hovered = live && barHovered && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var eased = Math.Clamp(sayHoverEase.Step(hovered ? 1f : 0f, DeckHoverSmoothTime, delta), 0f, 1f);
        var grow = DeckSayGrow * scale * eased;
        var shrink = rect.Height * 0.5f * (1f - PressFx.Scale(DeckSayId, pressed, DeckPressShrink));
        var inset = new Vector2(shrink - grow, shrink - grow);
        var body = new Rect(rect.Min + inset, rect.Max - inset);
        var rounding = body.Height * 0.5f;
        var fillAlpha = live ? 1f : DeckIdleFillAlpha;
        drawList.AddRectFilled(body.Min + new Vector2(0f, 2f * scale), body.Max + new Vector2(0f, 2f * scale),
            DeckShadow.Packed(), rounding);
        AccentGloss.Pill(drawList, body.Min, body.Max,
            DeckBodyTone(VelvetTheme.Rose, DeckHoverTopLift * eased, fillAlpha),
            DeckBodyTone(VelvetTheme.RoseDeep, DeckHoverBottomLift * eased, fillAlpha), scale, eased, DeckGlowReach);
        if (eased > 0.001f)
        {
            Squircle.Stroke(drawList, body.Min, body.Max, rounding,
                VelvetTheme.Alpha(VelvetTheme.OnAccent, DeckRimAlpha * eased).Packed(), DeckRimWeight * scale);
        }

        var maxLabelWidth = MathF.Max(1f, body.Width - DeckSayPad * 2f * scale);
        Typography.DrawCentered(drawList, body.Center,
            Typography.FitText(Loc.T(L.Velvet.DeckSay), maxLabelWidth, DeckSayStyle),
            live ? VelvetTheme.OnAccent : VelvetTheme.Alpha(VelvetTheme.OnAccent, DeckIdleFillAlpha), DeckSayStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static Vector4 DeckBodyTone(Vector4 tone, float lift, float alpha) =>
        VelvetTheme.Alpha(VelvetTheme.Lerp(tone, VelvetTheme.OnAccent, lift), alpha);

    private static bool DrawDeckGhost(ImDrawListPtr drawList, Rect rect, string label, bool barHovered, bool live)
    {
        var scale = UiScale.Current;
        var hovered = live && barHovered && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var rounding = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
            (hovered ? VelvetTheme.CardHi : VelvetTheme.Card).Packed());
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, VelvetTheme.Hairline.Packed(),
            Metrics.Stroke.Hairline * scale);
        Typography.DrawCentered(drawList, rect.Center, label, VelvetTheme.TitleInk, DeckUndoStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void DrawDeckEmpty(Rect body)
    {
        var scale = UiScale.Current;
        var loading = store.LoadingDiscover || store.LoadingMoreDiscover;
        var failed = !loading && store.DiscoverFailed;
        if (failed)
        {
            discoverFailure.Set(store.DiscoverFailure);
        }

        var passCount = store.PassCount;
        var title = loading ? Loc.T(L.Velvet.DiscoverLoading)
            : failed ? Loc.T(L.Failure.CouldNotLoad) : Loc.T(L.Velvet.DeckEndTitle);
        var hint = loading ? string.Empty
            : failed ? discoverFailure.Text()
            : passCount > 0 ? Loc.T(L.Velvet.DeckPassedHidden, passCount)
            : Loc.T(L.Velvet.DeckEndHint);
        var bottom = DrawEmpty(body, title, hint);
        if (loading)
        {
            return;
        }

        var actionTop = bottom + EndActionTop * scale;
        if (failed)
        {
            if (DrawEndAction(body, ref actionTop, Loc.T(L.Common.Retry), ConfirmButtonTone.Primary,
                    "velvet.deck.retry"))
            {
                ApplyDiscoverFilters();
            }

            return;
        }

        var lead = true;
        if (passCount > 0 && DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.DeckShowAgain), NextTone(ref lead),
                "velvet.deck.showAgain"))
        {
            ShowPassesAgain();
        }

        if ((discoverInclude.RegionMask != 0 || mutes.RegionMask != 0)
            && DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.DeckWidenRegion), NextTone(ref lead),
                "velvet.deck.widen"))
        {
            discoverInclude.RegionMask = 0;
            mutes.RegionMask = 0;
            ApplyMutesEverywhere();
        }

        if (discoverInclude.AnyBesidesRegion && DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.FilterClearAll),
                NextTone(ref lead), "velvet.deck.clear"))
        {
            discoverInclude.Clear();
            ApplyDiscoverFilters();
        }

        if (!discoverInclude.Any && DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.DeckCheckAgain),
                NextTone(ref lead), "velvet.deck.again"))
        {
            ApplyDiscoverFilters();
        }

        if (lastPassed is not null && DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.DeckUndo),
                ConfirmButtonTone.Neutral, "velvet.deck.undo"))
        {
            UndoPass();
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
