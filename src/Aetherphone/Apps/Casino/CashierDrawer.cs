using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierDrawer
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const ImGuiWindowFlags BodyFlags = ImGuiWindowFlags.NoBackground;

    private const float MaxDim = 0.45f;
    private const float GrabberTop = 8f;
    private const float PadX = 18f;
    private const float SectionGap = 12f;
    private const float BottomPad = 18f;

    private readonly CasinoStore store;
    private readonly CoinStore coins;
    private readonly ConfirmService confirm;
    private readonly CashierPanel panel;

    private Spring reveal;
    private bool open;
    private int openedFrame;

    public CashierDrawer(CasinoStore store, CoinStore coins, ConfirmService confirm)
    {
        this.store = store;
        this.coins = coins;
        this.confirm = confirm;
        panel = new CashierPanel(store, coins, confirm);
    }

    public bool IsOpen => open;

    public CashierPanel Panel => panel;

    public void Open()
    {
        Open(0);
    }

    public void Open(long suggestedChips)
    {
        if (!open)
        {
            open = true;
            openedFrame = ImGui.GetFrameCount();
            UiFeedback.Play(UiSound.SheetPresent);
            store.RefreshNow();
            coins.EnsureFresh();
        }

        panel.Reset(suggestedChips);
    }

    public void Close()
    {
        if (!open)
        {
            return;
        }

        open = false;
        UiFeedback.Play(UiSound.SheetDismiss);
    }

    public void Gate()
    {
        if (open && confirm.Active is null)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui, Action openLimits)
    {
        ConsumeResults(openLimits);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, Motion.Sheet, delta);
        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return;
        }

        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##cashierDrawer", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            Material.Veil(drawList, screen.Min, screen.Max, MaxDim * opacity);
            var interactive = open && confirm.Active is null && opacity > 0.5f;
            var panelRect = DrawPanel(screen, ui, drawList, opacity, interactive);
            if (!interactive)
            {
                return;
            }

            if (ImGui.GetFrameCount() != openedFrame && UiInteract.ClickedOutside(panelRect.Min, panelRect.Max))
            {
                Close();
            }
        }
    }

    private void ConsumeResults(Action openLimits)
    {
        var sitting = store.TakeSittingResult();
        if (sitting is not null)
        {
            if (sitting.Granted)
            {
                UiFeedback.Play(UiSound.CasinoChips);
                panel.ClearBuy();
                panel.ClearNote();
            }

            HandleOutcome(sitting.Reason, openLimits);
        }

        var closed = store.TakeCloseResult();
        if (closed is not null)
        {
            if (closed.Granted)
            {
                UiFeedback.Play(UiSound.Payout);
                Announce(panel.CashOutLine(closed));
            }

            HandleOutcome(closed.Reason, openLimits);
        }

        if (store.TakeMoneyMoveFailure())
        {
            HandleOutcome(CasinoReasons.Unreachable, openLimits);
        }
    }

    private void Announce(string line)
    {
        if (open || panel.Visible)
        {
            panel.ShowNote(line, true);
            return;
        }

        ShellToast.Show(line);
    }

    private void HandleOutcome(string reason, Action openLimits)
    {
        if (reason.Length == 0)
        {
            return;
        }

        if (string.Equals(reason, CasinoReasons.LossLimit, StringComparison.Ordinal))
        {
            Close();
            openLimits();
            return;
        }

        var message = CasinoReasons.Text(reason, store.Ceiling.MaxBet);
        if (open || panel.Visible)
        {
            panel.ShowNote(message, false);
            return;
        }

        confirm.Alert(null, message, Loc.T(L.Common.Close));
    }

    private Rect DrawPanel(Rect screen, AppSkin ui, ImDrawListPtr drawList, float slide, bool interactive)
    {
        var scale = UiScale.Current;
        var innerWidth = screen.Width - PadX * 2f * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var grabberBlock = (GrabberTop + Metrics.Size.GrabberHeight + Metrics.Space.Md) * scale;
        var head = grabberBlock + titleHeight + SectionGap * scale;
        var bodyHeight = panel.Height(innerWidth, scale);
        var panelHeight = MathF.Min(head + bodyHeight + BottomPad * scale, screen.Height - Metrics.Space.Xl * scale);

        var panelBottom = screen.Max.Y + panelHeight * (1f - slide);
        var panelTop = panelBottom - panelHeight;
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, panelBottom);
        var rounding = ui.Theme.ScreenRounding * scale;
        var skin = CasinoArt.Sheet(ui);
        Squircle.Fill(drawList, panelMin, panelMax, rounding, ImGui.GetColorU32(skin.Panel));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding, ImGui.GetColorU32(skin.Stroke),
            Metrics.Stroke.Hairline);
        var grabberWidth = Metrics.Size.GrabberWidth * scale;
        var grabberHeight = Metrics.Size.GrabberHeight * scale;
        var grabberMin = new Vector2(screen.Center.X - grabberWidth * 0.5f, panelTop + GrabberTop * scale);
        drawList.AddRectFilled(grabberMin, grabberMin + new Vector2(grabberWidth, grabberHeight),
            ImGui.GetColorU32(skin.Grabber), grabberHeight * 0.5f);

        var titleTop = panelTop + grabberBlock;
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, titleTop + titleHeight * 0.5f),
            Loc.T(L.Casino.Cashier), ui.TitleInk, TextStyles.Headline);

        var bodyMin = new Vector2(panelMin.X + PadX * scale, panelTop + head);
        var bodySize = new Vector2(innerWidth, MathF.Max(1f, panelBottom - BottomPad * scale - bodyMin.Y));
        ImGui.SetCursorScreenPos(bodyMin);
        using (ImRaii.Child("##cashierBody", bodySize, false, BodyFlags))
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.NativeScrollContentWidth();
            var bottom = panel.Draw(ImGui.GetWindowDrawList(), ui, origin.X, origin.Y, width, scale, interactive,
                true);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, MathF.Max(1f, bottom - origin.Y)));
        }

        return new Rect(panelMin, panelMax);
    }
}
