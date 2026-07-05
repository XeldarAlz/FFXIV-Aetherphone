using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Dailies;

internal sealed class DailiesApp : IPhoneApp
{
    private const float TileSize = 30f;
    private const float RefreshIntervalSeconds = 2f;
    private const float ItemCardHeight = 62f;
    private const float ItemCardRounding = 18f;
    private const float ItemCardGap = 10f;
    private const float ItemPadding = 14f;

    private static readonly Vector4 DailiesTint = AppAccents.For("dailies");

    public string Id => "dailies";

    public string DisplayName => Loc.T(L.Apps.Dailies);

    public string Glyph => "D";

    public int BadgeCount => outstandingCount;

    private readonly Configuration configuration;
    private readonly DailyCheckStore checkStore;
    private readonly DailyAutoStatus[] autoStatuses;
    private readonly AppSkin ui = new(AppPalettes.Dailies);

    private int outstandingCount;
    private float sinceRefresh;
    private int cadenceIndex;
    private TimerWindow fashionReportWindow;
    private DateTime nextJumboCactpot;
    private int huntSealBalance = -1;

    public DailiesApp(Configuration configuration)
    {
        this.configuration = configuration;
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
            var status = item.Tracking == DailyTracking.Manual
                ? DailyAutoStatus.Unavailable
                : DailiesReader.Read(item.Tracking, item.Goal);
            autoStatuses[index] = status;

            if (IsOutstanding(item, status, utcNow))
            {
                outstanding++;
            }
        }

        outstandingCount = outstanding;
        fashionReportWindow = DailiesReader.ReadFashionReportWindow(utcNow);
        nextJumboCactpot = DailiesReader.ReadNextJumboCactpot(utcNow);
        huntSealBalance = DailiesReader.ReadHuntSealBalance();
        sinceRefresh = 0f;
    }

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

        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var utcNow = DateTime.UtcNow;

        ui.Theme = theme;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ui.Backdrop(screen);
        DrawHeader(content, scale);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);

        using (AppSurface.Begin(body))
        {
            DrawHero(utcNow, scale);

            var stripRect = NextRowRect(36f, scale);
            var cadenceLabels = new[] { Loc.T(L.Dailies.Daily), Loc.T(L.Dailies.Weekly) };
            cadenceIndex = SegmentStrip.Draw("dailies.cadence", stripRect, cadenceLabels, cadenceIndex, theme);
            ImGui.SetCursorScreenPos(stripRect.Min);
            ImGui.Dummy(stripRect.Size);

            var cadence = cadenceIndex == 0 ? DailyCadence.Daily : DailyCadence.Weekly;
            var nextReset = cadence == DailyCadence.Daily
                ? GameSchedule.NextDailyReset(utcNow)
                : GameSchedule.NextWeeklyReset(utcNow);
            DrawResetLine(utcNow, nextReset, scale);

            DrawGroup(utcNow, cadence, autoGroup: true, scale);
            DrawGroup(utcNow, cadence, autoGroup: false, scale);
            DrawNotify(scale);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawHeader(Rect content, float scale)
    {
        var rowCenterY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, rowCenterY), DisplayName, AppPalettes.Dailies.TitleInk, 1.15f,
            FontWeight.SemiBold);
    }

    private static Rect NextRowRect(float height, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        return new Rect(origin, origin + new Vector2(width, height * scale));
    }

    private void DrawHero(DateTime utcNow, float scale)
    {
        var items = DailyCatalog.Items;
        var tracked = 0;
        var done = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.Tracking == DailyTracking.Levequests)
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
        var title = remaining <= 0 ? Loc.T(L.Dailies.AllDone) : Loc.T(L.Dailies.Remaining, remaining);
        var sub = remaining <= 0 ? Loc.T(L.Dailies.NothingLeft) : $"{done} / {tracked}";
        if (remaining <= 0)
        {
            HeroRing.Draw(fraction, DailiesTint, AppPalettes.Dailies.TitleInk, AppPalettes.Dailies.MutedInk,
                FontAwesomeIcon.Star, title, sub);
        }
        else
        {
            HeroRing.Draw(fraction, DailiesTint, AppPalettes.Dailies.TitleInk, AppPalettes.Dailies.MutedInk,
                remaining.ToString(Loc.Culture), $"/ {tracked}", title, sub);
        }
    }

    private static void DrawResetLine(DateTime utcNow, DateTime nextReset, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var text = Loc.T(L.Dailies.Resets, TimeFormat.Relative(nextReset - utcNow));
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 11f * scale), text, AppPalettes.Dailies.MutedInk,
            TextStyles.Subheadline);
        ImGui.Dummy(new Vector2(width, 26f * scale));
    }

    private void DrawGroup(DateTime utcNow, DailyCadence cadence, bool autoGroup, float scale)
    {
        var items = DailyCatalog.Items;
        var any = false;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Cadence != cadence || items[index].Tracking != DailyTracking.Manual != autoGroup)
            {
                continue;
            }

            any = true;
            break;
        }

        if (!any)
        {
            return;
        }

        ui.SectionLabel(Loc.T(autoGroup ? L.Dailies.AutoSection : L.Dailies.ManualSection), TextStyles.FootnoteEmphasized, 6f);

        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Cadence != cadence || items[index].Tracking != DailyTracking.Manual != autoGroup)
            {
                continue;
            }

            DrawItemCard(items[index], index, utcNow, scale);
            ImGui.Dummy(new Vector2(0f, ItemCardGap * scale));
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
    }

    private void DrawItemCard(in DailyItem item, int itemIndex, DateTime utcNow, float scale)
    {
        var status = autoStatuses[itemIndex];
        var width = ImGui.GetContentRegionAvail().X;
        var height = ItemCardHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = origin + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, min, max, ItemCardRounding * scale, elevated: true);

        var padding = ItemPadding * scale;
        var row = new Rect(new Vector2(min.X + padding, min.Y), new Vector2(max.X - padding, max.Y));

        var tile = TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile(tileCenter, tile, item.Accent, item.Icon);

        var auto = item.Tracking != DailyTracking.Manual && status.Available;
        var complete = auto ? status.Complete : checkStore.IsChecked(item, utcNow);
        var info = item.Tracking == DailyTracking.Levequests;

        var textLeft = row.Min.X + tile + 12f * scale;
        var sublabel = BuildSublabel(item, status, utcNow);
        if (sublabel.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, row.Center.Y - 16f * scale), Loc.T(item.Label), AppPalettes.Dailies.TitleInk,
                TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y + 5f * scale), sublabel, AppPalettes.Dailies.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            var nameSize = Typography.Measure(Loc.T(item.Label), TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), Loc.T(item.Label),
                AppPalettes.Dailies.TitleInk, TextStyles.Headline);
        }

        if (info)
        {
            DrawCountValue(row, status);
            ImGui.SetCursorScreenPos(min);
            ImGui.Dummy(new Vector2(width, height));
            return;
        }

        var markCenter = new Vector2(row.Max.X - 13f * scale, row.Center.Y);
        DrawCheckmark(markCenter, 13f * scale, complete, auto, item.Accent);

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, height));

        if (auto)
        {
            return;
        }

        var hovered = ImGui.IsMouseHoveringRect(min, max);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            checkStore.SetChecked(item, !complete, utcNow);
            RefreshAuto();
        }
    }

    private string BuildSublabel(in DailyItem item, in DailyAutoStatus status, DateTime utcNow)
    {
        if (item.Tracking == DailyTracking.Levequests)
        {
            return Loc.T(L.Dailies.AutoTracked);
        }

        if (item.Tracking != DailyTracking.Manual)
        {
            if (!status.Available)
            {
                return string.Empty;
            }

            return status.Complete
                ? Loc.T(L.Dailies.AutoTracked)
                : string.Concat(Loc.T(L.Dailies.AutoTracked), " · ", Loc.T(L.Dailies.Remaining, status.Remaining));
        }

        return item.Id switch
        {
            "weekly.fashionReport" => fashionReportWindow.Active
                ? Loc.T(L.Dailies.VotingOpenCloses, TimeFormat.Relative(fashionReportWindow.NextChangeUtc - utcNow))
                : Loc.T(L.Dailies.VotingOpensIn, TimeFormat.Relative(fashionReportWindow.NextChangeUtc - utcNow)),
            "weekly.jumboCactpot" => Loc.T(L.Dailies.NextDrawing, TimeFormat.Relative(nextJumboCactpot - utcNow)),
            "weekly.huntBills" when huntSealBalance >= 0 => Loc.T(L.Dailies.SealBalance,
                huntSealBalance.ToString("N0", Loc.Culture)),
            _ => string.Empty,
        };
    }

    private static void DrawCountValue(Rect row, in DailyAutoStatus status)
    {
        var value = status.Available ? $"{status.Remaining} / {status.Goal}" : "--";
        var valueSize = Typography.Measure(value, TextStyles.Headline);
        Typography.Draw(new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value,
            AppPalettes.Dailies.TitleInk, TextStyles.Headline);
    }

    private static void DrawCheckmark(Vector2 center, float radius, bool complete, bool auto, Vector4 accent)
    {
        var dl = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;

        if (complete)
        {
            dl.AddCircleFilled(center, radius, ImGui.GetColorU32(accent), 32);
            var tip = new Vector2(center.X - 1.5f * scale, center.Y + 4f * scale);
            var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
            dl.AddLine(tip - new Vector2(4f * scale, 4f * scale), tip, color, 2f * scale);
            dl.AddLine(tip, new Vector2(tip.X + 7f * scale, tip.Y - 8f * scale), color, 2f * scale);
            return;
        }

        var ringColor = auto ? Palette.WithAlpha(AppPalettes.Dailies.MutedInk, 0.55f) : AppPalettes.Dailies.MutedInk;
        dl.AddCircle(center, radius, ImGui.GetColorU32(ringColor), 32, 2f * scale);
    }

    private void DrawNotify(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var height = 56f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = origin + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, min, max, 16f * scale, elevated: true);

        var padding = 16f * scale;
        var label = Loc.T(L.Dailies.NotifyReset);
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(new Vector2(min.X + padding, min.Y + height * 0.5f - labelSize.Y * 0.5f), label,
            AppPalettes.Dailies.BodyInk, TextStyles.Body);

        var toggleWidth = 46f * scale;
        var toggleHeight = 28f * scale;
        var toggleMin = new Vector2(max.X - padding - toggleWidth, min.Y + height * 0.5f - toggleHeight * 0.5f);
        var notify = Toggle.Draw(new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight)),
            configuration.NotifyDailiesReset, PhoneTheme.Default);

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, height));

        if (notify == configuration.NotifyDailiesReset)
        {
            return;
        }

        configuration.NotifyDailiesReset = notify;
        configuration.Save();
    }

    private static void IconTile(Vector2 center, float size, Vector4 tint, FontAwesomeIcon icon)
    {
        var dl = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        Squircle.Fill(dl, center - new Vector2(half, half), center + new Vector2(half, half), size * 0.30f,
            ImGui.GetColorU32(tint));
        ProgressRing.CenterIcon(center, icon, new Vector4(1f, 1f, 1f, 1f), size * 0.50f);
    }

    public void Dispose()
    {
    }
}
