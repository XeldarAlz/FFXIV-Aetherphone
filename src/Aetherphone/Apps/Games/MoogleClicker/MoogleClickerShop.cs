using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.MoogleClicker;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.MoogleClicker;

internal enum ShopCommand : byte
{
    None,
    Buy,
    Denied,
    Stats,
    Ledger,
}

internal readonly struct ShopAction
{
    public readonly ShopCommand Command;
    public readonly int Building;
    public readonly int Count;
    public readonly Vector2 Point;

    public ShopAction(ShopCommand command, int building, int count, Vector2 point)
    {
        Command = command;
        Building = building;
        Count = count;
        Point = point;
    }
}

internal sealed class MoogleClickerShop
{
    public const int ModeOne = 0;
    public const int ModeTen = 1;
    public const int ModeMax = 2;
    private const int ModeCount = 3;
    private const int TenCount = 10;
    private const string SurfaceId = "moogleclicker.shop";
    private const string StatsId = "moogleclicker.stats";
    private const string LedgerId = "moogleclicker.ledger";
    private const float HeaderHeight = 46f;
    private const float RowHeight = 56f;
    private const float IconSize = 38f;
    private const float IconRadius = 10f;
    private const float ButtonWidth = 88f;
    private const float ButtonHeight = 30f;
    private const float ToggleWidth = 118f;
    private const float ToggleHeight = 26f;
    private const float RoundRadius = 15f;
    private const float WheelStep = 56f;
    private const float FlashDecay = 2.4f;
    private const float ShakeDecay = 3.4f;
    private const float ShakeDistance = 5f;
    private const float UnknownAlpha = 0.35f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);

    private readonly KineticScroller scroller = new();
    private readonly float[] flash = new float[KupoBuildings.Count];
    private readonly float[] shake = new float[KupoBuildings.Count];
    private readonly TextSlot[] rateLabels = new TextSlot[KupoBuildings.Count];
    private readonly LabelSlot[] countLabels = new LabelSlot[KupoBuildings.Count];
    private readonly string[] modeLabels = new string[ModeCount];
    private LanguageInfo? modeLanguage;
    private bool pressing;
    private float time;

    public int Mode { get; private set; }

    public void Flash(int building) => flash[building] = 1f;

    public void Shake(int building) => shake[building] = 1f;

    public void Reset()
    {
        scroller.Reset();
        pressing = false;
        Array.Clear(flash);
        Array.Clear(shake);
    }

    public ShopAction Draw(ImDrawListPtr drawList, Rect rect, KupoWorkshop workshop, Vector4 accent, StageInk ink,
        PhoneTheme theme, bool interactive, float deltaSeconds, float scale)
    {
        time += deltaSeconds;
        Decay(deltaSeconds);
        SyncModeLabels();
        if (rect.Height <= HeaderHeight * scale)
        {
            return default;
        }

        BoardPlate.Draw(drawList, rect, BoardPlate.Radius * scale, scale, accent, ink);
        var padding = BoardPlate.Padding * scale;
        var header = new Rect(new Vector2(rect.Min.X + padding, rect.Min.Y),
            new Vector2(rect.Max.X - padding, rect.Min.Y + HeaderHeight * scale));
        var action = DrawHeader(drawList, header, workshop, accent, theme, interactive, scale);
        drawList.AddLine(new Vector2(header.Min.X, header.Max.Y), new Vector2(header.Max.X, header.Max.Y),
            ImGui.GetColorU32(White with { W = 0.08f }), Metrics.Stroke.Hairline * scale);
        var list = new Rect(new Vector2(header.Min.X, header.Max.Y), new Vector2(header.Max.X, rect.Max.Y - padding));
        if (list.Height <= 0f)
        {
            return action;
        }

        var rowHeight = RowHeight * scale;
        scroller.Scale = scale;
        scroller.SetBounds(KupoBuildings.Count * rowHeight - list.Height);
        if (interactive)
        {
            Scroll(list, deltaSeconds);
        }
        else if (pressing)
        {
            scroller.Release();
            pressing = false;
        }

        scroller.Tick(deltaSeconds);
        var top = list.Min.Y - scroller.Offset + scroller.PullDistance;
        drawList.PushClipRect(list.Min, list.Max, true);
        for (var building = 0; building < KupoBuildings.Count; building++)
        {
            var rowMin = new Vector2(list.Min.X, top + building * rowHeight);
            var row = new Rect(rowMin, rowMin + new Vector2(list.Width, rowHeight));
            if (row.Max.Y < list.Min.Y || row.Min.Y > list.Max.Y)
            {
                continue;
            }

            var rowAction = DrawRow(drawList, row, list, building, workshop, accent, theme, interactive, scale);
            if (rowAction.Command != ShopCommand.None)
            {
                action = rowAction;
            }
        }

        drawList.PopClipRect();
        return action;
    }

    private ShopAction DrawHeader(ImDrawListPtr drawList, Rect header, KupoWorkshop workshop, Vector4 accent,
        PhoneTheme theme, bool interactive, float scale)
    {
        var centerY = header.Center.Y;
        var title = Loc.T(L.MoogleClicker.Workshop);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var radius = RoundRadius * scale;
        var ledgerCenter = new Vector2(header.Max.X - radius, centerY);
        var statsCenter = new Vector2(ledgerCenter.X - radius * 2f - Metrics.Space.Sm * scale, centerY);
        var toggleRight = statsCenter.X - radius - Metrics.Space.Sm * scale;
        var toggle = new Rect(new Vector2(toggleRight - ToggleWidth * scale, centerY - ToggleHeight * 0.5f * scale),
            new Vector2(toggleRight, centerY + ToggleHeight * 0.5f * scale));
        var titleWidth = MathF.Max(0f, toggle.Min.X - header.Min.X - Metrics.Space.Sm * scale);
        Typography.Draw(drawList, new Vector2(header.Min.X, centerY - titleHeight * 0.5f),
            Typography.FitText(title, titleWidth, TextStyles.Headline), StageInks.Strong, TextStyles.Headline);
        DrawToggle(drawList, toggle, accent, interactive, scale);
        var controlInk = MoogleClickerText.Controls(accent, theme);
        var glyph = radius * 0.95f;
        var action = default(ShopAction);
        if (RoundButton.FontIcon(drawList, StatsId, statsCenter, radius, FontAwesomeIcon.ChartBar, glyph, controlInk,
                ButtonStyle.Gray, Loc.T(L.MoogleClicker.StatsTitle), enabled: interactive))
        {
            action = new ShopAction(ShopCommand.Stats, -1, 0, statsCenter);
        }

        var pending = workshop.PendingStamps >= 1d;
        if (pending)
        {
            ProgressRing.Glow(ledgerCenter, radius * 1.5f, Gold, 0.45f + 0.35f * Pulse.Wave(Pulse.Medium));
        }

        if (RoundButton.FontIcon(drawList, LedgerId, ledgerCenter, radius, FontAwesomeIcon.BookOpen, glyph, controlInk,
                pending ? ButtonStyle.Prominent : ButtonStyle.Tinted, Loc.T(L.MoogleClicker.LedgerTitle),
                enabled: interactive))
        {
            action = new ShopAction(ShopCommand.Ledger, -1, 0, ledgerCenter);
        }

        return action;
    }

    private void DrawToggle(ImDrawListPtr drawList, Rect toggle, Vector4 accent, bool interactive, float scale)
    {
        var radius = toggle.Height * 0.5f;
        Squircle.Fill(drawList, toggle.Min, toggle.Max, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)));
        var segment = toggle.Width / ModeCount;
        var inset = 2f * scale;
        for (var mode = 0; mode < ModeCount; mode++)
        {
            var min = new Vector2(toggle.Min.X + segment * mode, toggle.Min.Y);
            var max = new Vector2(min.X + segment, toggle.Max.Y);
            var hovered = interactive && UiInteract.Hover(min, max);
            if (mode == Mode)
            {
                Squircle.Fill(drawList, min + new Vector2(inset, inset), max - new Vector2(inset, inset), radius - inset,
                    ImGui.GetColorU32(accent));
            }
            else if (hovered)
            {
                Squircle.Fill(drawList, min + new Vector2(inset, inset), max - new Vector2(inset, inset), radius - inset,
                    ImGui.GetColorU32(White with { W = 0.08f }));
            }

            var ink = mode == Mode ? GamePalette.InkOn(accent) : StageInks.Muted;
            Typography.DrawCentered(drawList, (min + max) * 0.5f, modeLabels[mode], ink, TextStyles.Caption1.Scale,
                FontWeight.SemiBold);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (hovered && UiInteract.Click(min, max, hovered))
            {
                Mode = mode;
            }
        }
    }

    private ShopAction DrawRow(ImDrawListPtr drawList, Rect row, Rect list, int building, KupoWorkshop workshop,
        Vector4 accent, PhoneTheme theme, bool interactive, float scale)
    {
        var owned = workshop.Owned(building);
        var revealed = building < 2 || owned > 0 || workshop.Owned(building - 1) > 0;
        var tint = MoogleClickerText.BuildingTints[building];
        if (building > 0)
        {
            drawList.AddLine(row.Min, new Vector2(row.Max.X, row.Min.Y), ImGui.GetColorU32(White with { W = 0.06f }),
                Metrics.Stroke.Hairline * scale);
        }

        if (flash[building] > 0f)
        {
            Squircle.Fill(drawList, row.Min, row.Max, Metrics.Space.Md * scale,
                ImGui.GetColorU32(tint with { W = 0.26f * flash[building] }));
        }

        var iconSize = IconSize * scale;
        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconSize * 0.5f);
        var iconRect = new Rect(iconMin, iconMin + new Vector2(iconSize, iconSize));
        var pop = 1f + 0.12f * flash[building];
        var poppedIcon = iconRect.Scaled(pop);
        StageCell.Draw(drawList, poppedIcon, revealed ? tint : GamePalette.Cell, CellDepth.Raised, IconRadius * scale,
            scale);
        ProgressRing.CenterIcon(drawList, poppedIcon.Center,
            revealed ? MoogleClickerText.BuildingIcons[building] : FontAwesomeIcon.Question,
            revealed ? White : White with { W = UnknownAlpha }, iconSize * 0.48f);

        var count = Mode switch
        {
            ModeTen => TenCount,
            ModeMax => Math.Max(1, workshop.Affordable(building)),
            _ => 1,
        };
        var cost = KupoBuildings.BulkCost(building, owned, count);
        var affordable = cost <= workshop.Kupo && owned + count <= KupoBuildings.MaxOwned;
        var shakeOffset = MathF.Sin(time * 44f) * ShakeDistance * scale * shake[building];
        var buttonMin = new Vector2(row.Max.X - ButtonWidth * scale, row.Center.Y - ButtonHeight * 0.5f * scale);
        var resting = new Rect(buttonMin, buttonMin + new Vector2(ButtonWidth * scale, ButtonHeight * scale));
        var button = resting.Translate(new Vector2(shakeOffset, 0f));
        var visibleMin = Vector2.Max(resting.Min, list.Min);
        var visibleMax = Vector2.Min(resting.Max, list.Max);
        var pointer = interactive && visibleMax.Y > visibleMin.Y && UiInteract.Hover(visibleMin, visibleMax);
        var controlInk = MoogleClickerText.Controls(accent, theme);
        var buyId = MoogleClickerText.BuyIds[building];
        var face = Button.Surface(drawList, button, controlInk, ButtonStyle.Prominent, ButtonRole.Normal, affordable,
            pointer && affordable, ImGui.GetID(buyId));
        Button.DrawLabel(drawList, face, KupoFormat.Amount(cost), buyId);
        if (Mode != ModeOne)
        {
            var tag = countLabels[building].Get(L.Stage.Times, count);
            var tagSize = Typography.Measure(tag, TextStyles.Caption2);
            Typography.Draw(drawList, new Vector2(button.Max.X - tagSize.X - Metrics.Space.Xs * scale,
                button.Min.Y - tagSize.Y), tag, affordable ? Gold : StageInks.Muted, TextStyles.Caption2);
        }

        var ownedLabel = GameNumber.Label(owned);
        var ownedSize = Typography.Measure(ownedLabel, TextStyles.Title3);
        var ownedRight = resting.Min.X - Metrics.Space.Md * scale;
        Typography.Draw(drawList, new Vector2(ownedRight - ownedSize.X, row.Center.Y - ownedSize.Y * 0.5f), ownedLabel,
            owned > 0 ? StageInks.Strong : StageInks.Muted with { W = 0.5f }, TextStyles.Title3);
        var textLeft = iconRect.Max.X + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(0f, ownedRight - ownedSize.X - Metrics.Space.Sm * scale - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var captionHeight = Typography.LineHeight(TextStyles.Caption1);
        var blockTop = row.Center.Y - (nameHeight + captionHeight) * 0.5f;
        var name = revealed ? Loc.T(MoogleClickerText.BuildingNames[building]) : Loc.T(L.MoogleClicker.Unknown);
        Typography.Draw(drawList, new Vector2(textLeft, blockTop), Typography.FitText(name, textWidth, TextStyles.Headline),
            revealed ? StageInks.Strong : StageInks.Muted, TextStyles.Headline);
        if (revealed)
        {
            var rate = rateLabels[building].Get(L.MoogleClicker.EachRate, KupoFormat.Rate(workshop.UnitRate(building)));
            Typography.Draw(drawList, new Vector2(textLeft, blockTop + nameHeight),
                Typography.FitText(rate, textWidth, TextStyles.Caption1), StageInks.Muted, TextStyles.Caption1);
        }

        if (!pointer || !UiInteract.Click(visibleMin, visibleMax, pointer, false))
        {
            return default;
        }

        return new ShopAction(affordable ? ShopCommand.Buy : ShopCommand.Denied, building, count, resting.Center);
    }

    private void Scroll(Rect list, float deltaSeconds)
    {
        var hovered = PressSurface.Claim(SurfaceId, list, out var activated);
        var pointerY = ImGui.GetMousePos().Y;
        if (activated)
        {
            scroller.Press(pointerY);
            pressing = true;
        }

        if (pressing)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                scroller.Move(pointerY, deltaSeconds);
            }
            else
            {
                scroller.Release();
                pressing = false;
            }
        }

        if (scroller.IsDragging)
        {
            UiInteract.CancelPendingTap();
        }

        var wheel = ImGui.GetIO().MouseWheel;
        if (!hovered || wheel == 0f)
        {
            return;
        }

        scroller.CancelMomentum();
        scroller.SyncOffset(scroller.Offset - wheel * WheelStep * scroller.Scale);
    }

    private void Decay(float deltaSeconds)
    {
        for (var building = 0; building < KupoBuildings.Count; building++)
        {
            flash[building] = MathF.Max(0f, flash[building] - deltaSeconds * FlashDecay);
            shake[building] = MathF.Max(0f, shake[building] - deltaSeconds * ShakeDecay);
        }
    }

    private void SyncModeLabels()
    {
        if (ReferenceEquals(modeLanguage, Loc.Current))
        {
            return;
        }

        modeLanguage = Loc.Current;
        modeLabels[ModeOne] = Loc.T(L.Stage.Times, GameNumber.Label(1));
        modeLabels[ModeTen] = Loc.T(L.Stage.Times, GameNumber.Label(TenCount));
        modeLabels[ModeMax] = Loc.T(L.MoogleClicker.BuyMax);
    }
}
