using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Dailies;

internal sealed class DailiesApp : IPhoneApp
{
    private const float RefreshIntervalSeconds = 2f;
    private const float RowHeight = 64f;
    private const float TileSize = 32f;
    private const float CardGap = 12f;
    private const float SegmentBand = 38f;
    private const float SegmentTrack = 34f;
    private const float CheckRadius = 13f;
    private const float MarkColumn = 34f;
    private const float BarWidth = 48f;
    private const float HeroTopPad = 6f;
    private const float HeroRadius = 52f;
    private const float HeroThickness = 7f;
    private const float HeroTitleGap = 18f;
    private const float HeroSubtitleGap = 40f;
    private const float HeroBottomPad = 16f;

    private static readonly Vector4 DailiesTint = AppAccents.For("dailies");
    private static readonly Vector4 HoverIdle = new(1f, 1f, 1f, 0.05f);
    private static readonly Vector4 HoverPressed = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);

    public string Id => "dailies";

    public string DisplayName => Loc.T(L.Apps.Dailies);

    public string Glyph => "D";

    public int BadgeCount => outstandingCount;
    public bool HasBadge => true;

    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly DailyCheckStore checkStore;
    private readonly DailyAutoStatus[] autoStatuses;
    private readonly AppSkin ui = new(AppPalettes.Dailies);

    private int outstandingCount;
    private float sinceRefresh;
    private int cadenceIndex;
    private TimerWindow fashionReportWindow;
    private DateTime nextJumboCactpot;

    public DailiesApp(Configuration configuration, GameData gameData)
    {
        this.configuration = configuration;
        this.gameData = gameData;
        checkStore = new DailyCheckStore(configuration);
        autoStatuses = new DailyAutoStatus[DailyCatalog.Items.Length];
        RefreshAuto();
    }

    public void OnOpened() => RefreshAuto();

    public void OnClosed()
    {
    }

    private void RefreshAuto()
    {
        var items = DailyCatalog.Items;
        var utcNow = DateTime.UtcNow;
        var outstanding = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var status = ReadStatus(item);
            autoStatuses[index] = status;

            if (IsOutstanding(item, status, utcNow))
            {
                outstanding++;
            }
        }

        outstandingCount = outstanding;
        fashionReportWindow = DailiesReader.ReadFashionReportWindow(utcNow);
        nextJumboCactpot = DailiesReader.ReadNextJumboCactpot(utcNow);
        sinceRefresh = 0f;
    }

    private DailyAutoStatus ReadStatus(in DailyItem item)
    {
        return item.Tracking switch
        {
            DailyTracking.Manual => DailyAutoStatus.Unavailable,
            DailyTracking.DutyRoulettes => DailiesReader.ReadDutyRoulettes(gameData.DailyBonusRouletteRowIds()),
            DailyTracking.HuntBills => DailiesReader.ReadHuntBills(gameData.WeeklyHuntBillIndices(),
                gameData.HuntOrderTypeSheet(), gameData.HuntOrderSheet()),
            _ => DailiesReader.Read(item.Tracking, item.Goal),
        };
    }

    private static bool IsValueTracking(DailyTracking tracking) =>
        tracking is DailyTracking.BeastTribeAllowances or DailyTracking.CustomDeliveries
            or DailyTracking.WondrousTails or DailyTracking.Levequests or DailyTracking.HuntBills
            or DailyTracking.DomanEnclave;

    private bool IsOutstanding(in DailyItem item, in DailyAutoStatus status, DateTime utcNow)
    {
        if (item.Tracking == DailyTracking.Levequests)
        {
            return false;
        }

        if (item.Tracking != DailyTracking.Manual && status.Available)
        {
            return !status.Complete;
        }

        return !checkStore.IsChecked(item, utcNow);
    }

    public void Draw(in PhoneContext context)
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh >= RefreshIntervalSeconds)
        {
            RefreshAuto();
        }

        if (GuideIntents.Consume("dailies.tab.weekly"))
        {
            cadenceIndex = 1;
        }

        var scale = UiScale.Current;
        var content = context.Content;
        var utcNow = DateTime.UtcNow;

        ui.Theme = context.Theme;
        var screen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ui.Backdrop(screen);
        AppHeader.Draw(context, DisplayName);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);

        using (AppSurface.Begin(body))
        {
            DrawCadence(context.Theme, scale);
            var cadence = cadenceIndex == 0 ? DailyCadence.Daily : DailyCadence.Weekly;
            DrawHero(cadence, utcNow, scale);
            DrawSection(cadence, autoGroup: true, utcNow, scale);
            DrawSection(cadence, autoGroup: false, utcNow, scale);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawHero(DailyCadence cadence, DateTime utcNow, float scale)
    {
        var items = DailyCatalog.Items;
        var tracked = 0;
        var done = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.Cadence != cadence || item.Tracking == DailyTracking.Levequests)
            {
                continue;
            }

            tracked++;
            if (!IsOutstanding(item, autoStatuses[index], utcNow))
            {
                done++;
            }
        }

        var fraction = tracked == 0 ? 1f : done / (float)tracked;
        var remaining = tracked - done;
        var nextReset = cadence == DailyCadence.Daily
            ? GameSchedule.NextDailyReset(utcNow)
            : GameSchedule.NextWeeklyReset(utcNow);
        var resetLine = Loc.T(L.Dailies.Resets, TimeFormat.Relative(nextReset - utcNow));
        var titleLine = remaining <= 0 ? Loc.T(L.Dailies.AllDone) : Loc.T(L.Dailies.Remaining, remaining);

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = HeroRadius * scale;
        var thickness = HeroThickness * scale;
        var ringCenter = new Vector2(origin.X + width * 0.5f, origin.Y + (HeroTopPad + HeroRadius) * scale);

        ProgressRing.Glow(ringCenter, radius, DailiesTint, 0.45f + 0.30f * Pulse.Wave(Pulse.Breath));
        ProgressRing.Track(ringCenter, radius, thickness, Palette.WithAlpha(AppPalettes.Dailies.TitleInk, 0.10f));
        ProgressRing.Fill(ringCenter, radius, thickness, fraction, DailiesTint);
        if (remaining <= 0)
        {
            ProgressRing.CenterIcon(ringCenter, FontAwesomeIcon.Check, DailiesTint, radius * 0.60f);
        }
        else
        {
            ProgressRing.CenterValue(ringCenter, done.ToString(Loc.Culture), $"/ {tracked.ToString(Loc.Culture)}",
                AppPalettes.Dailies.TitleInk, AppPalettes.Dailies.MutedInk, TextStyles.LargeTitle);
        }

        var ringBottom = ringCenter.Y + radius;
        Typography.DrawCentered(new Vector2(ringCenter.X, ringBottom + HeroTitleGap * scale), titleLine,
            AppPalettes.Dailies.TitleInk, TextStyles.Title3);
        Typography.DrawCentered(new Vector2(ringCenter.X, ringBottom + HeroSubtitleGap * scale), resetLine,
            AppPalettes.Dailies.MutedInk, TextStyles.Footnote);

        var heroHeight = (HeroTopPad + HeroRadius * 2f + HeroSubtitleGap + HeroBottomPad) * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, heroHeight));
    }

    private void DrawCadence(PhoneTheme theme, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var stripRect = new Rect(origin, origin + new Vector2(width, SegmentBand * scale));
        UiAnchors.Report("dailies.cadence", stripRect);
        var cadenceLabels = new[] { Loc.T(L.Dailies.Daily), Loc.T(L.Dailies.Weekly) };
        cadenceIndex = SegmentStrip.Draw("dailies.cadence", stripRect, cadenceLabels, cadenceIndex, AppPalettes.Dailies,
            SegmentTrack, 0.92f);
        ImGui.SetCursorScreenPos(stripRect.Min);
        ImGui.Dummy(new Vector2(width, (SegmentBand + 8f) * scale));
    }

    private void DrawSection(DailyCadence cadence, bool autoGroup, DateTime utcNow, float scale)
    {
        var items = DailyCatalog.Items;
        var count = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (Matches(items[index], cadence, autoGroup))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(autoGroup ? L.Dailies.AutoSection : L.Dailies.ManualSection),
            TextStyles.FootnoteEmphasized, 8f);

        var card = GroupCard.Begin(ui, count, RowHeight);
        for (var index = 0; index < items.Length; index++)
        {
            if (!Matches(items[index], cadence, autoGroup))
            {
                continue;
            }

            DrawRow(card.NextRow(), items[index], index, utcNow, scale);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, CardGap * scale));
    }

    private static bool Matches(in DailyItem item, DailyCadence cadence, bool autoGroup) =>
        item.Cadence == cadence && (item.Tracking != DailyTracking.Manual) == autoGroup;

    private void DrawRow(Rect row, in DailyItem item, int itemIndex, DateTime utcNow, float scale)
    {
        var status = autoStatuses[itemIndex];
        var isValue = IsValueTracking(item.Tracking);
        var tappable = item.Tracking == DailyTracking.Manual;
        var complete = tappable ? checkStore.IsChecked(item, utcNow) : status.Available && status.Complete;

        var band = new Rect(new Vector2(row.Min.X - Metrics.Space.Lg * scale, row.Min.Y),
            new Vector2(row.Max.X + Metrics.Space.Lg * scale, row.Max.Y));
        var innerLeft = row.Min.X;
        var innerRight = row.Max.X;

        var hovered = tappable && UiInteract.Hover(band.Min, band.Max);
        if (hovered)
        {
            var inset = new Vector2(6f * scale, 3f * scale);
            var pressed = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            Squircle.Fill(ImGui.GetWindowDrawList(), band.Min + inset, band.Max - inset, 12f * scale,
                ImGui.GetColorU32(pressed ? HoverPressed : HoverIdle));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tile = TileSize * scale;
        IconTile.Draw(new Vector2(innerLeft + tile * 0.5f, band.Center.Y), tile, item.Accent, item.Icon);

        float controlLeft;
        if (isValue)
        {
            controlLeft = DrawValue(band, innerRight, status, item.Tracking, complete, scale);
        }
        else
        {
            var markCenter = new Vector2(innerRight - CheckRadius * scale, band.Center.Y);
            DrawCheck(markCenter, CheckRadius * scale, complete, readOnly: !tappable, item.Accent);
            controlLeft = innerRight - MarkColumn * scale;
        }

        var textLeft = innerLeft + tile + 12f * scale;
        var textRight = MathF.Max(textLeft, controlLeft - 12f * scale);
        var textMaxWidth = MathF.Max(1f, textRight - textLeft);
        var name = Loc.T(item.Label);
        var sublabel = BuildSublabel(item, utcNow);
        var nameColor = complete && tappable ? AppPalettes.Dailies.MutedInk : AppPalettes.Dailies.TitleInk;

        if (sublabel.Length > 0)
        {
            Marquee.DrawLeftAuto(new MarqueeId("dailies.row.", item.Id), name, textLeft, band.Center.Y - 17f * scale, textMaxWidth,
                TextStyles.Headline, nameColor);
            Marquee.DrawLeftAuto(new MarqueeId("dailies.row.sub.", item.Id), sublabel, textLeft, band.Center.Y + 4f * scale, textMaxWidth,
                TextStyles.Subheadline, AppPalettes.Dailies.MutedInk);
        }
        else
        {
            var nameSize = Typography.Measure(name, TextStyles.Headline);
            Marquee.DrawLeftAuto(new MarqueeId("dailies.row.", item.Id), name, textLeft, band.Center.Y - nameSize.Y * 0.5f, textMaxWidth,
                TextStyles.Headline, nameColor);
        }

        if (tappable && UiInteract.Click(band.Min, band.Max, hovered))
        {
            checkStore.SetChecked(item, !complete, utcNow);
            RefreshAuto();
        }
    }

    private static float DrawValue(Rect band, float innerRight, in DailyAutoStatus status, DailyTracking tracking,
        bool complete, float scale)
    {
        var isInfo = tracking == DailyTracking.Levequests;
        var goal = status.Goal;
        var done = isInfo ? status.Remaining : Math.Clamp(goal - status.Remaining, 0, goal);
        var text = status.Available
            ? string.Concat(done.ToString(Loc.Culture), " / ", goal.ToString(Loc.Culture))
            : "--";
        var style = TextStyles.SubheadlineEmphasized;
        var color = isInfo
            ? AppPalettes.Dailies.BodyInk
            : complete ? DailiesTint : AppPalettes.Dailies.TitleInk;
        var size = Typography.Measure(text, style);
        var valueX = innerRight - size.X;
        var valueY = isInfo ? band.Center.Y - size.Y * 0.5f : band.Center.Y - size.Y - 1f * scale;
        Typography.Draw(new Vector2(valueX, valueY), text, color, style);

        if (isInfo || !status.Available)
        {
            return valueX;
        }

        var barWidth = BarWidth * scale;
        var barHeight = 5f * scale;
        var barLeft = innerRight - barWidth;
        var barY = band.Center.Y + 8f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var trackMin = new Vector2(barLeft, barY);
        var trackMax = new Vector2(innerRight, barY + barHeight);
        Squircle.Fill(drawList, trackMin, trackMax, barHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(DailiesTint, 0.16f)));
        var fraction = goal <= 0 ? 0f : Math.Clamp(done / (float)goal, 0f, 1f);
        if (fraction > 0f)
        {
            Squircle.Fill(drawList, trackMin, new Vector2(barLeft + barWidth * fraction, trackMax.Y), barHeight * 0.5f,
                ImGui.GetColorU32(DailiesTint));
        }

        return MathF.Min(valueX, barLeft);
    }

    private static void DrawCheck(Vector2 center, float radius, bool complete, bool readOnly, Vector4 accent)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;

        if (complete)
        {
            var fill = readOnly ? Palette.WithAlpha(accent, 0.82f) : accent;
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 32);
            var tip = new Vector2(center.X - 1.5f * scale, center.Y + 4f * scale);
            var mark = ImGui.GetColorU32(Ink);
            drawList.AddLine(tip - new Vector2(4f * scale, 4f * scale), tip, mark, 2f * scale);
            drawList.AddLine(tip, new Vector2(tip.X + 7f * scale, tip.Y - 8f * scale), mark, 2f * scale);
            return;
        }

        var ringColor = readOnly
            ? Palette.WithAlpha(AppPalettes.Dailies.MutedInk, 0.45f)
            : AppPalettes.Dailies.MutedInk;
        drawList.AddCircle(center, radius, ImGui.GetColorU32(ringColor), 32, 2f * scale);
    }

    private string BuildSublabel(in DailyItem item, DateTime utcNow)
    {
        return item.Id switch
        {
            "weekly.fashionReport" => fashionReportWindow.Active
                ? Loc.T(L.Dailies.VotingOpenCloses, TimeFormat.Relative(fashionReportWindow.NextChangeUtc - utcNow))
                : Loc.T(L.Dailies.VotingOpensIn, TimeFormat.Relative(fashionReportWindow.NextChangeUtc - utcNow)),
            "weekly.jumboCactpot" => Loc.T(L.Dailies.NextDrawing, TimeFormat.Relative(nextJumboCactpot - utcNow)),
            _ => string.Empty,
        };
    }

    public void Dispose()
    {
    }
}
