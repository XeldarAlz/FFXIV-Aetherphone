using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Polls;

internal sealed class PollsApp : IPhoneApp
{
    private const float RefreshSeconds = 30f;
    private const float FillSmoothTime = 0.26f;
    private const float CheckSmoothTime = 0.16f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public string Id => "polls";
    public string DisplayName => Loc.T(L.Apps.Polls);
    public string Glyph => "Po";
    public int BadgeCount => store.UnvotedCount;

    private readonly PollsStore store;
    private readonly AppSkin ui = new(AppPalettes.Polls);
    private readonly Dictionary<string, PollMotion> motions = new();

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private float sinceRefresh;

    public PollsApp(AethernetSession session, AethernetClient client)
    {
        store = new PollsStore(session, client);
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
        var scale = ImGuiHelpers.GlobalScale;
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

        using (AppSurface.Begin(body))
        {
            var polls = store.Polls;
            if (polls.Length == 0)
            {
                DrawEmptyState(body, scale);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 2f * scale));
            for (var index = 0; index < polls.Length; index++)
            {
                DrawPollCard(polls[index], scale);
                ImGui.Dummy(new Vector2(0f, 12f * scale));
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

    private void DrawPollCard(PollDto poll, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 16f * scale;
        var contentLeft = origin.X + pad;
        var contentRight = origin.X + width - pad;
        var contentWidth = contentRight - contentLeft;

        var questionHeight = MeasureWrapped(poll.Question, contentWidth, 1.08f, FontWeight.SemiBold);
        var optionHeight = 44f * scale;
        var optionGap = 10f * scale;
        var footerHeight = 18f * scale;
        var optionsTop = origin.Y + pad + questionHeight + 14f * scale;
        var optionsHeight = poll.Options.Length * optionHeight + (poll.Options.Length - 1) * optionGap;
        var footerTop = optionsTop + optionsHeight + 12f * scale;
        var cardBottom = footerTop + footerHeight + pad * 0.75f;

        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 18f * scale, true);

        ImGui.SetCursorScreenPos(new Vector2(contentLeft, origin.Y + pad));
        var wrapPos = contentRight - ImGui.GetWindowPos().X;
        ImGui.PushTextWrapPos(wrapPos);
        using (Plugin.Fonts.Push(1.08f, FontWeight.SemiBold))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.TextWrapped(poll.Question);
        }

        ImGui.PopTextWrapPos();

        var motion = MotionFor(poll);
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        for (var index = 0; index < poll.Options.Length; index++)
        {
            var rowTop = optionsTop + index * (optionHeight + optionGap);
            DrawOption(drawList, poll, motion, index, contentLeft, rowTop, contentWidth, optionHeight, scale,
                deltaSeconds);
        }

        var votesLabel = Loc.Plural(L.Polls.Votes, poll.TotalVotes);
        var footer = poll.Closed
            ? $"{votesLabel} · {Loc.T(L.Polls.FinalResults)}"
            : $"{votesLabel} · {RelativeTime(poll.CreatedAtUnix)}";
        Typography.Draw(new Vector2(contentLeft, footerTop), footer, ui.MutedInk, 0.85f);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y));
    }

    private void DrawOption(ImDrawListPtr drawList, PollDto poll, PollMotion motion, int optionIndex, float left,
        float top, float width, float height, float scale, float deltaSeconds)
    {
        var selected = poll.MyVote == optionIndex;
        var interactive = !poll.Closed;
        var rowMin = new Vector2(left - 8f * scale, top - 4f * scale);
        var rowMax = new Vector2(left + width + 8f * scale, top + height);
        var hovered = interactive && ImGui.IsMouseHoveringRect(rowMin, rowMax);
        if (hovered)
        {
            Squircle.Fill(drawList, rowMin, rowMax, 12f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var radioRadius = 9f * scale;
        var labelCenterY = top + 11f * scale;
        var radioCenter = new Vector2(left + radioRadius, labelCenterY);
        var check = motion.Checks[optionIndex].Step(selected ? 1f : 0f, CheckSmoothTime, deltaSeconds);
        var inkAlpha = poll.Closed ? 0.7f : 1f;

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

        var labelLeft = radioCenter.X + radioRadius + 10f * scale;
        var count = poll.VoteCounts[optionIndex].ToString(Loc.Culture);
        var countSize = Typography.Measure(count, 0.92f, FontWeight.Medium);
        var countLeft = left + width - countSize.X;
        var labelInk = selected ? ui.TitleInk : ui.BodyInk;
        var labelMax = countLeft - labelLeft - 8f * scale;
        var label = TruncateLabel(poll.Options[optionIndex], labelMax, 0.98f,
            selected ? FontWeight.SemiBold : FontWeight.Medium);
        var labelSize = Typography.Measure(label, 0.98f, selected ? FontWeight.SemiBold : FontWeight.Medium);
        Typography.Draw(new Vector2(labelLeft, labelCenterY - labelSize.Y * 0.5f), label,
            Palette.WithAlpha(labelInk, inkAlpha), 0.98f, selected ? FontWeight.SemiBold : FontWeight.Medium);
        Typography.Draw(new Vector2(countLeft, labelCenterY - countSize.Y * 0.5f), count,
            Palette.WithAlpha(ui.MutedInk, inkAlpha), 0.92f, FontWeight.Medium);

        var barHeight = 7f * scale;
        var barTop = top + height - barHeight - 4f * scale;
        var barMin = new Vector2(labelLeft, barTop);
        var barMax = new Vector2(left + width, barTop + barHeight);
        var rounding = barHeight * 0.5f;
        Squircle.Fill(drawList, barMin, barMax, rounding, ImGui.GetColorU32(ui.FieldSurface));

        var fraction = poll.TotalVotes > 0 ? (float)poll.VoteCounts[optionIndex] / poll.TotalVotes : 0f;
        var animated = motion.Fills[optionIndex].Step(fraction, FillSmoothTime, deltaSeconds);
        if (animated > 0.001f)
        {
            var fillWidth = MathF.Max((barMax.X - barMin.X) * animated, barHeight);
            var fillColor = selected ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.45f);
            Squircle.Fill(drawList, barMin, new Vector2(barMin.X + fillWidth, barMax.Y), rounding,
                ImGui.GetColorU32(Palette.WithAlpha(fillColor, fillColor.W * inkAlpha)));
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            store.Vote(poll, optionIndex);
        }
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
        }

        return motion;
    }

    private static string TruncateLabel(string label, float maxWidth, float fontScale, FontWeight weight)
    {
        if (maxWidth <= 0f || Typography.Measure(label, fontScale, weight).X <= maxWidth)
        {
            return label;
        }

        var length = label.Length;
        while (length > 1 && Typography.Measure($"{label[..length]}…", fontScale, weight).X > maxWidth)
        {
            length--;
        }

        return $"{label[..length]}…";
    }

    private static float MeasureWrapped(string text, float wrapWidth, float fontScale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(fontScale, weight))
        {
            return ImGui.CalcTextSize(text, false, wrapWidth).Y;
        }
    }

    private static string RelativeTime(long unixSeconds)
    {
        var moment = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        var span = DateTime.UtcNow - moment;
        if (span.TotalSeconds < 60)
        {
            return Loc.T(L.Time.Now);
        }

        if (span.TotalMinutes < 60)
        {
            return Loc.T(L.Time.MinutesShort, (int)span.TotalMinutes);
        }

        if (span.TotalHours < 24)
        {
            return Loc.T(L.Time.HoursShort, (int)span.TotalHours);
        }

        if (span.TotalDays < 7)
        {
            return Loc.T(L.Time.DaysShort, (int)span.TotalDays);
        }

        return moment.ToString("MMM d", Loc.Culture);
    }

    private sealed class PollMotion
    {
        public Spring[] Fills = Array.Empty<Spring>();
        public Spring[] Checks = Array.Empty<Spring>();
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
