using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Polls;

internal sealed class PollsApp : IPhoneApp
{
    private const float RefreshSeconds = 30f;
    private const float FillSmoothTime = 0.26f;
    private const float CheckSmoothTime = 0.16f;
    private const float RevealSmoothTime = 0.28f;
    private const float OptionFontScale = 0.98f;
    private const float CountFontScale = 0.92f;
    private const float RadioRadius = 9f;
    private const float LabelGap = 10f;
    private const float CountGap = 8f;
    private const float BarHeight = 7f;
    private const float BarGap = 10f;
    private const float RowTopPad = 2f;
    private const float RowBottomPad = 4f;
    private const float HiddenRowPad = 7f;
    private const float FooterFontScale = 0.85f;
    private const float RevealEpsilon = 0.01f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public string Id => "polls";
    public string DisplayName => Loc.T(L.Apps.Polls);
    public string Glyph => "Po";
    public int BadgeCount => store.UnvotedCount;
    public bool HasBadge => true;

    private readonly PollsStore store;
    private readonly AppSkin ui = new(AppPalettes.Polls);
    private readonly PullToRefresh listRefresh = new();
    private readonly Dictionary<string, PollMotion> motions = new();
    private readonly Dictionary<string, LocalizedPoll> localized = new();

    private float[] rowHeights = Array.Empty<float>();

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private float sinceRefresh;

    public PollsApp(AethernetSession session, PollsClient client, AppInstaller installer, RealtimeSignalBus signals)
    {
        store = new PollsStore(session, client, installer.Gate("polls"), signals);
    }

    public void OnOpened()
    {
        sinceRefresh = 0f;
        store.Refresh();
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var area = context.Content;
        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(area, theme, scale);
        ui.Backdrop(screen);
        ui.Body(area);
        AppHeader.Draw(context, Loc.T(L.Apps.Polls), navigation.Back);

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        if (!store.IsSignedIn)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Polls.SignInRequired), ui.MutedInk);
            return;
        }

        TickRefresh();

        using (var surface = AppSurface.BeginEdgeToEdge(body))
        {
            listRefresh.Draw(body, surface.Pull, surface.Dragging, store.Loading, ui.MutedInk, store.Refresh);
            var polls = store.Polls;
            if (polls.Length == 0)
            {
                DrawEmptyState(body, scale);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 2f * scale));
            for (var index = 0; index < polls.Length; index++)
            {
                DrawPollCard(polls[index], scale, index == 0);
            }

            if (store.LoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
            }
            else if (store.HasMore && InfiniteScroll.ReachedBottom())
            {
                store.LoadMore();
            }
        }
    }

    private void TickRefresh()
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh < RefreshSeconds || store.Loading)
        {
            return;
        }

        sinceRefresh = 0f;
        store.Refresh();
    }

    private void DrawEmptyState(Rect body, float scale)
    {
        if (store.Loading && !store.LoadedOnce)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), ui.MutedInk);
            return;
        }

        var centerY = body.Min.Y + body.Height * 0.42f;
        Typography.DrawCentered(new Vector2(body.Center.X, centerY), Loc.T(L.Polls.Empty), ui.TitleInk, 1.1f,
            FontWeight.SemiBold);
        Typography.DrawCentered(new Vector2(body.Center.X, centerY + 26f * scale), Loc.T(L.Polls.EmptySubtitle),
            ui.MutedInk, 0.9f);
    }

    private void DrawPollCard(PollDto poll, float scale, bool isFirstCard)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FeedCell.PadX * scale;
        var contentLeft = origin.X + pad;
        var contentRight = origin.X + width - pad;
        var contentWidth = contentRight - contentLeft;

        var text = LocalizedFor(poll);
        var questionHeight = Typography.MeasureWrapped(text.Question, contentWidth, 1.08f, FontWeight.SemiBold);
        var optionGap = 10f * scale;
        var footerHeight = Typography.MeasureWrapped(text.Footer, contentWidth, FooterFontScale);
        var optionsTop = origin.Y + pad + questionHeight + 14f * scale;

        var motion = MotionFor(poll);
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var revealTarget = ResultsVisible(poll) ? 1f : 0f;
        if (!motion.Primed)
        {
            motion.Primed = true;
            motion.Reveal.SnapTo(revealTarget);
        }

        var reveal = motion.Reveal.Step(revealTarget, RevealSmoothTime, deltaSeconds);
        var showResults = revealTarget > 0f || reveal > RevealEpsilon;

        var heights = EnsureRowBuffer(poll.Options.Length);
        var optionsHeight = 0f;
        for (var index = 0; index < poll.Options.Length; index++)
        {
            heights[index] = OptionRowHeight(text, index, contentLeft, contentWidth, scale, showResults, reveal);
            optionsHeight += heights[index];
            if (index > 0)
            {
                optionsHeight += optionGap;
            }
        }

        var footerTop = optionsTop + optionsHeight + 12f * scale;
        var cardBottom = footerTop + footerHeight + pad * 0.75f;

        var cell = FeedCell.Begin(drawList, cardBottom - origin.Y, ui.HoverWash, false);
        if (isFirstCard)
        {
            UiAnchors.Report("polls.card", cell.Bounds);
        }

        ImGui.SetCursorScreenPos(new Vector2(contentLeft, origin.Y + pad));
        using (Typography.WrapAt(contentRight))
        using (Plugin.Fonts.Push(1.08f, FontWeight.SemiBold))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            Typography.Wrapped(text.Question);
        }

        var rowTop = optionsTop;
        for (var index = 0; index < poll.Options.Length; index++)
        {
            DrawOption(drawList, poll, text, motion, index, contentLeft, rowTop, contentWidth, heights[index], scale,
                deltaSeconds, showResults, reveal);
            rowTop += heights[index] + optionGap;
        }

        ImGui.SetCursorScreenPos(new Vector2(contentLeft, footerTop));
        using (Typography.WrapAt(contentRight))
        using (Plugin.Fonts.Push(FooterFontScale, FontWeight.Regular))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.MutedInk))
        {
            Typography.Wrapped(text.Footer);
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void DrawOption(ImDrawListPtr drawList, PollDto poll, LocalizedPoll text, PollMotion motion,
        int optionIndex, float left, float top, float width, float height, float scale, float deltaSeconds,
        bool showResults, float reveal)
    {
        var optionLabel = text.Options[optionIndex];
        var selected = poll.MyVote == optionIndex;
        var interactive = !poll.Closed;
        var rowMin = new Vector2(left - 8f * scale, top - 4f * scale);
        var rowMax = new Vector2(left + width + 8f * scale, top + height);
        var hovered = interactive && UiInteract.Hover(rowMin, rowMax);
        if (hovered)
        {
            Squircle.Fill(drawList, rowMin, rowMax, 12f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var weight = selected ? FontWeight.SemiBold : FontWeight.Medium;
        var inkAlpha = poll.Closed ? 0.7f : 1f;
        var lineHeight = Typography.Measure(optionLabel, OptionFontScale, weight).Y;
        var firstLineCenterY = top + RowTopPad * scale + lineHeight * 0.5f;

        var radioRadius = RadioRadius * scale;
        var radioCenter = new Vector2(left + radioRadius, firstLineCenterY);
        var check = motion.Checks[optionIndex].Step(selected ? 1f : 0f, CheckSmoothTime, deltaSeconds);

        drawList.AddCircle(radioCenter, radioRadius,
            ImGui.GetColorU32(Palette.WithAlpha(selected ? ui.Accent : ui.MutedInk, inkAlpha)), 32, 1.6f * scale);
        if (check > 0.01f)
        {
            drawList.AddCircleFilled(radioCenter, radioRadius * check,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, inkAlpha)), 32);
            var markScale = radioRadius * check;
            var markColor = ImGui.GetColorU32(Palette.WithAlpha(White, check));
            drawList.AddLine(radioCenter + new Vector2(-0.42f * markScale, 0.05f * markScale),
                radioCenter + new Vector2(-0.10f * markScale, 0.38f * markScale), markColor, 1.8f * scale);
            drawList.AddLine(radioCenter + new Vector2(-0.10f * markScale, 0.38f * markScale),
                radioCenter + new Vector2(0.46f * markScale, -0.30f * markScale), markColor, 1.8f * scale);
        }

        var labelLeft = LabelLeft(left, scale);
        var labelInk = selected ? ui.TitleInk : ui.BodyInk;
        var labelWidth = LabelWidth(text, optionIndex, left, width, scale, showResults);

        ImGui.SetCursorScreenPos(new Vector2(labelLeft, top + RowTopPad * scale));
        using (Typography.WrapAt(labelLeft + labelWidth))
        using (Plugin.Fonts.Push(OptionFontScale, weight))
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(labelInk, inkAlpha)))
        {
            Typography.Wrapped(optionLabel);
        }

        if (showResults && reveal > RevealEpsilon)
        {
            var count = text.Counts[optionIndex];
            var countSize = Typography.Measure(count, CountFontScale, FontWeight.Medium);
            Typography.Draw(new Vector2(left + width - countSize.X, firstLineCenterY - countSize.Y * 0.5f), count,
                Palette.WithAlpha(ui.MutedInk, inkAlpha * reveal), CountFontScale, FontWeight.Medium);

            var barHeight = BarHeight * scale * reveal;
            var barTop = top + height - barHeight - RowBottomPad * scale;
            var barMin = new Vector2(labelLeft, barTop);
            var barMax = new Vector2(left + width, barTop + barHeight);
            var rounding = barHeight * 0.5f;
            Squircle.Fill(drawList, barMin, barMax, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.FieldSurface, ui.FieldSurface.W * reveal)));

            var fraction = poll.TotalVotes > 0 ? (float)poll.VoteCounts[optionIndex] / poll.TotalVotes : 0f;
            var animated = motion.Fills[optionIndex].Step(fraction, FillSmoothTime, deltaSeconds);
            if (animated > 0.001f)
            {
                var fillWidth = MathF.Max((barMax.X - barMin.X) * animated, BarHeight * scale);
                var fillColor = selected ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.45f);
                Squircle.Fill(drawList, barMin, new Vector2(barMin.X + fillWidth, barMax.Y), rounding,
                    ImGui.GetColorU32(Palette.WithAlpha(fillColor, fillColor.W * inkAlpha * reveal)));
            }
        }

        if (UiInteract.Click(rowMin, rowMax, hovered))
        {
            store.Vote(poll, optionIndex);
        }
    }

    private LocalizedPoll LocalizedFor(PollDto poll)
    {
        if (!localized.TryGetValue(poll.Id, out var cached))
        {
            cached = new LocalizedPoll();
            localized[poll.Id] = cached;
        }

        var code = Loc.Current.Code;
        if (ReferenceEquals(cached.Source, poll) && cached.LangCode == code)
        {
            return cached;
        }

        cached.Source = poll;
        cached.LangCode = code;
        cached.Question = poll.Question;
        cached.Options = poll.Options;
        cached.Counts = CountLabels(cached.Counts, poll.VoteCounts);
        cached.Footer = FooterText(poll);

        var translations = poll.Translations ?? Array.Empty<PollTranslationDto>();
        for (var index = 0; index < translations.Length; index++)
        {
            var translation = translations[index];
            if (translation.Lang != code)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(translation.Question))
            {
                cached.Question = translation.Question;
            }

            var count = Math.Min(poll.Options.Length, translation.Options.Length);
            var merged = (string[])poll.Options.Clone();
            var replaced = false;
            for (var optionIndex = 0; optionIndex < count; optionIndex++)
            {
                if (string.IsNullOrEmpty(translation.Options[optionIndex]))
                {
                    continue;
                }

                merged[optionIndex] = translation.Options[optionIndex];
                replaced = true;
            }

            if (replaced)
            {
                cached.Options = merged;
            }

            break;
        }

        return cached;
    }

    private PollMotion MotionFor(PollDto poll)
    {
        if (!motions.TryGetValue(poll.Id, out var motion))
        {
            motion = new PollMotion();
            motions[poll.Id] = motion;
        }

        if (motion.Fills.Length != poll.Options.Length)
        {
            motion.Fills = new Spring[poll.Options.Length];
            motion.Checks = new Spring[poll.Options.Length];
            motion.Primed = false;
        }

        return motion;
    }

    private float[] EnsureRowBuffer(int count)
    {
        if (rowHeights.Length < count)
        {
            rowHeights = new float[count];
        }

        return rowHeights;
    }

    private static float OptionRowHeight(LocalizedPoll text, int optionIndex, float left, float width, float scale,
        bool showResults, float reveal)
    {
        var labelWidth = LabelWidth(text, optionIndex, left, width, scale, showResults);
        var labelHeight = Typography.MeasureWrapped(text.Options[optionIndex], labelWidth, OptionFontScale,
            FontWeight.SemiBold);
        return labelHeight + (RowTopPad + RowBottomPad) * scale + (BarGap + BarHeight) * scale * reveal
               + HiddenRowPad * scale * (1f - reveal);
    }

    private static float LabelLeft(float left, float scale)
    {
        return left + RadioRadius * scale * 2f + LabelGap * scale;
    }

    private static float LabelWidth(LocalizedPoll text, int optionIndex, float left, float width, float scale,
        bool showResults)
    {
        var labelLeft = LabelLeft(left, scale);
        if (!showResults)
        {
            return left + width - labelLeft;
        }

        var countSize = Typography.Measure(text.Counts[optionIndex], CountFontScale, FontWeight.Medium);
        return left + width - countSize.X - CountGap * scale - labelLeft;
    }

    private static bool ResultsVisible(PollDto poll)
    {
        return poll.MyVote >= 0;
    }

    private static string[] CountLabels(string[] reuse, int[] counts)
    {
        var labels = reuse.Length == counts.Length ? reuse : new string[counts.Length];
        for (var index = 0; index < counts.Length; index++)
        {
            labels[index] = counts[index].ToString(Loc.Culture);
        }

        return labels;
    }

    private static string FooterText(PollDto poll)
    {
        var stamp = poll.Closed ? Loc.T(L.Polls.FinalResults) : TimeText.Short(poll.CreatedAtUnix);
        if (ResultsVisible(poll))
        {
            return $"{Loc.Plural(L.Polls.Votes, poll.TotalVotes)} · {stamp}";
        }

        return poll.Closed ? stamp : $"{Loc.T(L.Polls.HiddenResults)} · {stamp}";
    }

    private sealed class PollMotion
    {
        public Spring[] Fills = Array.Empty<Spring>();
        public Spring[] Checks = Array.Empty<Spring>();
        public Spring Reveal;
        public bool Primed;
    }

    private sealed class LocalizedPoll
    {
        public PollDto? Source;
        public string LangCode = string.Empty;
        public string Question = string.Empty;
        public string[] Options = Array.Empty<string>();
        public string[] Counts = Array.Empty<string>();
        public string Footer = string.Empty;
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
