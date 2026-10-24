using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp
{
    private const float UnitsCardPad = 12f;
    private const float UnitsStripHeight = 32f;
    private const double StrideStep = 0.05d;
    private const double MinStride = 0.30d;
    private const double MaxStride = 1.50d;
    private const double HeightStepCm = 0.5d;
    private const double MinHeightCm = 50d;
    private const double MaxHeightCm = 260d;
    private const double FallbackHeightCm = 170d;
    private const int DataRows = 6;

    private static readonly StepperIds HeightIds = StepperIds.For("health.settings.height");
    private static readonly StepperIds StrideIds = StepperIds.For("health.settings.stride");

    private readonly string[] unitLabels = new string[3];
    private CachedText heightText;
    private CachedText manualHeightText;
    private CachedText strideText;

    private void DrawSettings(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("health.settings"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawUnitsCard(drawList, origin, width, scale);
            cursorY = DrawAdventurerCard(drawList, origin.X, cursorY, width, scale);
            cursorY = DrawDataCard(drawList, origin.X, cursorY, width, scale);
            cursorY += Footnote(new Vector2(origin.X, cursorY), width, Loc.T(L.Health.Disclaimer), scale);
            ReserveTo(origin, width, cursorY + HealthArt.BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "health.nav.settings", Loc.T(L.Health.Settings),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private float DrawUnitsCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var cursorY = origin.Y;
        cursorY += CardSectionHeader.Draw(drawList, new Vector2(origin.X, cursorY), width, Loc.T(L.Health.Units),
            ui.TitleInk) + HealthArt.HeaderGap * scale;
        var pad = UnitsCardPad * scale;
        var max = new Vector2(origin.X + width, cursorY + pad * 2f + UnitsStripHeight * scale);
        ui.Card(drawList, new Vector2(origin.X, cursorY), max, Metrics.Radius.Grouped * scale);
        unitLabels[0] = Loc.T(L.Health.UnitEorzean);
        unitLabels[1] = Loc.T(L.Health.UnitMetric);
        unitLabels[2] = Loc.T(L.Health.UnitImperial);
        var strip = new Rect(new Vector2(origin.X + pad, cursorY + pad),
            new Vector2(max.X - pad, cursorY + pad + UnitsStripHeight * scale));
        var selected = SegmentStrip.Draw("health.settings.units", strip, unitLabels, (int)Units, ui.Palette,
            UnitsStripHeight);
        if (selected != (int)Units)
        {
            UiFeedback.Play(UiSound.Tap);
            Profile.Units = (HealthUnits)selected;
            tracker.SaveNow();
        }

        return max.Y + Footnote(new Vector2(origin.X, max.Y), width, Loc.T(UnitsHint(Units)), scale);
    }

    private static LocString UnitsHint(HealthUnits units) => units switch
    {
        HealthUnits.Metric => L.Health.UnitMetricSub,
        HealthUnits.Imperial => L.Health.UnitImperialSub,
        _ => L.Health.UnitEorzeanSub,
    };

    private float DrawAdventurerCard(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Health.Adventurer), scale);
        var manual = Profile.ManualHeightCm is not null;
        var energy = Profile.CaloriesEnabled;
        const int rows = 4;
        var bottom = RowsCard(drawList, new Vector2(left, cursorY), width, rows, scale, out var card);
        var row = 0;
        var useGame = ToggleRow(drawList, CardRow(drawList, card, row++, scale), "health.settings.gameHeight",
            Loc.T(L.Health.UseGameHeight), !manual, scale);
        if (useGame == manual)
        {
            Profile.ManualHeightCm = useGame ? null : tracker.HeightCm > 0d ? tracker.HeightCm : FallbackHeightCm;
            tracker.RefreshHeight();
            tracker.SaveNow();
        }

        var heightRow = CardRow(drawList, card, row++, scale);
        if (Profile.ManualHeightCm is { } manualCm)
        {
            var delta = StepperRow(drawList, heightRow, HeightIds, Loc.T(L.Health.Height), ManualHeightText(manualCm),
                manualCm > MinHeightCm, manualCm < MaxHeightCm, scale);
            if (delta != 0)
            {
                Profile.ManualHeightCm = Math.Clamp(manualCm + delta * HeightStepCm, MinHeightCm, MaxHeightCm);
                tracker.RefreshHeight();
                tracker.SaveNow();
            }
        }
        else
        {
            RowLabel(drawList, heightRow, Loc.T(L.Health.Height), heightRow.Width * 0.45f, ui.TitleInk);
            RowValue(drawList, heightRow, HeightText(), ui.MutedInk);
        }

        var stride = Profile.StrideYalms;
        var strideDelta = StepperRow(drawList, CardRow(drawList, card, row++, scale), StrideIds,
            Loc.T(L.Health.StrideLength), StrideText(stride), stride > MinStride + 0.001d, stride < MaxStride - 0.001d,
            scale);
        if (strideDelta != 0)
        {
            Profile.StrideYalms = Math.Clamp(Math.Round((stride + strideDelta * StrideStep) * 100d) / 100d, MinStride,
                MaxStride);
            tracker.SaveNow();
        }

        var toggled = ToggleRow(drawList, CardRow(drawList, card, row, scale), "health.settings.energy",
            Loc.T(L.Health.EstimateActivityEnergy), energy, scale);
        if (toggled != energy)
        {
            Profile.CaloriesEnabled = toggled;
            tracker.SaveNow();
        }

        var hint = Typography.DrawWrappedLeft(new Vector2(left, bottom + HealthArt.TileGap * scale),
            Loc.T(L.Health.AdventurerHint), ui.MutedInk, TextStyles.Footnote, width);
        return bottom + HealthArt.TileGap * scale + hint;
    }

    private string HeightText()
    {
        var key = (long)Math.Round(tracker.HeightCm * 10d) * 16L + (long)Units * 4L + (long)tracker.HeightSource;
        if (heightText.IsCurrent(key))
        {
            return heightText.Value;
        }

        var source = Loc.T(tracker.HeightSource switch
        {
            HeightSource.Manual => L.Health.HeightSourceManual,
            HeightSource.Game => L.Health.HeightSourceGame,
            _ => L.Health.HeightSourceUnavailable,
        });
        return heightText.Store(key, tracker.HeightCm > 0d
            ? Loc.T(L.Health.HeightWithSource, HealthFormat.Height(tracker.HeightCm, Units), source)
            : source);
    }

    private string ManualHeightText(double centimetres)
    {
        var key = (long)Math.Round(centimetres * 10d) * 4L + (long)Units;
        return manualHeightText.IsCurrent(key)
            ? manualHeightText.Value
            : manualHeightText.Store(key, HealthFormat.Height(centimetres, Units));
    }

    private string StrideText(double stride)
    {
        var key = (long)Math.Round(stride * 100d);
        return strideText.IsCurrent(key)
            ? strideText.Value
            : strideText.Store(key, string.Concat(stride.ToString("0.00", Loc.Culture), Loc.T(L.Health.UnitYalms)));
    }

    private float DrawDataCard(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Health.Data), scale);
        var bottom = RowsCard(drawList, new Vector2(left, cursorY), width, DataRows, scale, out var card);
        if (ActionRow(drawList, CardRow(drawList, card, 0, scale), Loc.T(L.Health.ResetSession), false))
        {
            tracker.ResetSession();
            UiFeedback.Play(UiSound.ToggleOff);
        }

        if (ActionRow(drawList, CardRow(drawList, card, 1, scale), Loc.T(L.Health.ResetToday), true))
        {
            AskReset(Loc.T(L.Health.ResetTodayConfirm), tracker.ResetToday);
        }

        if (ActionRow(drawList, CardRow(drawList, card, 2, scale), Loc.T(L.Health.ResetTodayHydration), true))
        {
            AskReset(Loc.T(L.Health.ResetTodayHydrationConfirm), tracker.ResetTodayHydration);
        }

        if (ActionRow(drawList, CardRow(drawList, card, 3, scale), Loc.T(L.Health.ResetHistory), true))
        {
            AskReset(Loc.T(L.Health.ResetHistoryConfirm), tracker.ResetHistory);
        }

        if (ActionRow(drawList, CardRow(drawList, card, 4, scale), Loc.T(L.Health.ResetRecords), true))
        {
            AskReset(Loc.T(L.Health.ResetRecordsConfirm), tracker.ResetRecords);
        }

        if (ActionRow(drawList, CardRow(drawList, card, 5, scale), Loc.T(L.Health.ResetAll), true))
        {
            AskReset(Loc.T(L.Health.ResetAllConfirm), tracker.ResetAll);
        }

        return bottom;
    }

    private bool ActionRow(ImDrawListPtr drawList, Rect row, string label, bool destructive)
    {
        var scale = UiScale.Current;
        var pad = HealthArt.CardPad * scale;
        var hit = new Rect(new Vector2(row.Min.X - pad, row.Min.Y), new Vector2(row.Max.X + pad, row.Max.Y));
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        var ink = destructive ? theme.Danger : ui.Accent;
        if (hovered)
        {
            ink = Palette.Lighten(ink, 0.15f);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        RowLabel(drawList, row, label, 0f, ink);
        return UiInteract.Click(hit.Min, hit.Max, hovered);
    }

    private void AskReset(string message, Action confirmed)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Health.Confirm),
            Message = message,
            ConfirmLabel = Loc.T(L.Health.Reset),
            CancelLabel = Loc.T(L.Health.Cancel),
            Confirm = () =>
            {
                confirmed();
                digest.Invalidate();
                weightDigest.Invalidate();
            },
        });
    }
}
