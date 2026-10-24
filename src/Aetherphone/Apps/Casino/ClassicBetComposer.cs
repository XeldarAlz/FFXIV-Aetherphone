using System.Globalization;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed class ClassicBetComposer
{
    public const float FieldHeight = BetDeckLayout.AmountHeight;

    public const float ConfirmHeight = DeckActions.PrimaryHeight;

    private const int BufferLength = 8;
    private const float FlashSeconds = 0.5f;
    private const string ConfirmId = "casino.deck.classic";

    private readonly string fieldId;

    private string buffer = string.Empty;
    private long amount;
    private float clampFlash;

    public ClassicBetComposer(string fieldId)
    {
        this.fieldId = fieldId;
    }

    public long Amount => amount;

    public static long Ceiling(long maximumBet, long stack)
    {
        var ceiling = maximumBet < stack ? maximumBet : stack;
        return ceiling < 0 ? 0 : ceiling;
    }

    public static long Clamp(long value, long minimumBet, long maximumBet, long stack)
    {
        var ceiling = Ceiling(maximumBet, stack);
        if (ceiling < minimumBet)
        {
            return 0;
        }

        if (value < minimumBet)
        {
            return minimumBet;
        }

        return value > ceiling ? ceiling : value;
    }

    public static long Half(long value, long minimumBet, long maximumBet, long stack)
    {
        return Clamp(value / 2, minimumBet, maximumBet, stack);
    }

    public static long Double(long value, long minimumBet, long maximumBet, long stack)
    {
        var doubled = value > long.MaxValue / 2 ? long.MaxValue : value * 2;
        return Clamp(doubled, minimumBet, maximumBet, stack);
    }

    public static float HeightFor(float scale)
    {
        return (FieldHeight + DeckActions.Gap + ConfirmHeight) * scale;
    }

    public void Reset(long value)
    {
        amount = value;
        buffer = value > 0 ? value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        clampFlash = 0f;
    }

    public void Prefill(long value)
    {
        if (buffer.Length > 0)
        {
            return;
        }

        Reset(value);
    }

    public bool Draw(AppSkin skin, in Rect bounds, long minimumBet, long maximumBet, long stack, bool enabled,
        string confirmLabel, float deltaSeconds)
    {
        var scale = UiScale.Current;
        DrawAmount(skin, bounds, minimumBet, maximumBet, stack, enabled, deltaSeconds, scale);
        var confirmTop = MathF.Max(bounds.Min.Y, bounds.Max.Y - ConfirmHeight * scale);
        var confirmRect = new Rect(new Vector2(bounds.Min.X, confirmTop), new Vector2(bounds.Max.X, bounds.Max.Y));
        var confirmEnabled = enabled && amount >= minimumBet && amount <= Ceiling(maximumBet, stack);
        return Button.Draw(confirmRect, confirmLabel, skin.Ink, ButtonStyle.Prominent, enabled: confirmEnabled,
            id: ConfirmId);
    }

    private void DrawAmount(AppSkin skin, in Rect bounds, long minimumBet, long maximumBet, long stack, bool enabled,
        float deltaSeconds, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (clampFlash > 0f)
        {
            clampFlash -= deltaSeconds;
        }

        var typed = Parse();
        amount = Clamp(typed, minimumBet, maximumBet, stack);
        var row = new Rect(bounds.Min, new Vector2(bounds.Max.X, bounds.Min.Y + FieldHeight * scale));
        var layout = DeckAmountRow.Compute(row, DeckAmountRow.Measure(row.Height), scale);
        DrawField(drawList, skin, layout.Field, scale, enabled);
        var tapped = layout.DrawChips(skin.Ink, enabled);
        if (tapped != QuickChip.None)
        {
            Reset(tapped switch
            {
                QuickChip.Half => Half(amount, minimumBet, maximumBet, stack),
                QuickChip.Double => Double(amount, minimumBet, maximumBet, stack),
                _ => Clamp(Ceiling(maximumBet, stack), minimumBet, maximumBet, stack),
            });
            UiFeedback.Play(UiSound.ChipSlide);
            return;
        }

        if (typed > 0 && typed != amount)
        {
            clampFlash = FlashSeconds;
        }
    }

    private void DrawField(ImDrawListPtr drawList, AppSkin skin, Rect field, float scale, bool enabled)
    {
        var capsule = DeckAmountRow.FieldSurface(drawList, field, skin.Ink);
        if (clampFlash > 0f)
        {
            var strength = clampFlash / FlashSeconds;
            Squircle.Stroke(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(skin.Accent, strength)), Metrics.Stroke.Ring * scale);
        }

        if (!enabled)
        {
            Typography.DrawCentered(drawList, field.Center, buffer.Length > 0 ? buffer : "0", skin.MutedInk,
                TextStyles.Headline);
            return;
        }

        var inset = capsule.Height * 0.4f;
        ImGui.SetCursorScreenPos(new Vector2(capsule.Min.X + inset, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, capsule.Width - inset * 2f));
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, skin.TitleInk))
        {
            ImGui.InputText(fieldId, ref buffer, BufferLength,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }
    }

    private long Parse()
    {
        return long.TryParse(buffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
