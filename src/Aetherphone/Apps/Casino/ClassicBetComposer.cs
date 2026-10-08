using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed class ClassicBetComposer
{
    public const float FieldHeight = 40f;

    public const float QuickHeight = Button.RegularHeight;

    public const float ConfirmHeight = Button.LargeHeight;

    private const int BufferLength = 8;
    private const float RowGap = 8f;
    private const float StepperRadius = RoundButton.RegularRadius;
    private const float FlashSeconds = 0.5f;

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

    public static long Half(long minimumBet, long maximumBet, long stack)
    {
        return Clamp(Ceiling(maximumBet, stack) / 2, minimumBet, maximumBet, stack);
    }

    public static float HeightFor(float scale)
    {
        return (FieldHeight + QuickHeight + ConfirmHeight + RowGap * 2f) * scale;
    }

    public static float AmountHeightFor(float scale)
    {
        return (FieldHeight + QuickHeight + RowGap) * scale;
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

    public bool Draw(AppSkin skin, in Rect bounds, long minimumBet, long maximumBet, long stack, long step,
        bool enabled, string confirmLabel, float deltaSeconds)
    {
        var scale = UiScale.Current;
        var quickBottom = DrawAmount(skin, bounds, minimumBet, maximumBet, stack, step, enabled, deltaSeconds);
        var confirmRect = new Rect(new Vector2(bounds.Min.X, quickBottom + RowGap * scale),
            new Vector2(bounds.Max.X, quickBottom + (RowGap + ConfirmHeight) * scale));
        var confirmEnabled = enabled && amount >= minimumBet && amount <= Ceiling(maximumBet, stack);
        return Button.Draw(confirmRect, confirmLabel, skin.Ink, ButtonStyle.Prominent, enabled: confirmEnabled);
    }

    public float DrawAmount(AppSkin skin, in Rect bounds, long minimumBet, long maximumBet, long stack, long step,
        bool enabled, float deltaSeconds)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        if (clampFlash > 0f)
        {
            clampFlash -= deltaSeconds;
        }

        var typed = Parse();
        amount = Clamp(typed, minimumBet, maximumBet, stack);
        var ceiling = Ceiling(maximumBet, stack);
        var y = bounds.Min.Y;

        var stepperRadius = StepperRadius * scale;
        var stepperWidth = stepperRadius * 2f;
        var fieldMin = new Vector2(bounds.Min.X + stepperWidth + RowGap * scale, y);
        var fieldMax = new Vector2(bounds.Max.X - stepperWidth - RowGap * scale, y + FieldHeight * scale);
        var rowCenterY = (fieldMin.Y + fieldMax.Y) * 0.5f;
        if (DrawStepper(drawList, skin, new Vector2(bounds.Min.X + stepperRadius, rowCenterY), stepperRadius,
                IconGlyph.Of(FontAwesomeIcon.Minus), enabled))
        {
            Reset(Clamp(amount - step, minimumBet, maximumBet, stack));
        }

        DrawField(drawList, skin, fieldMin, fieldMax, scale, enabled);

        if (DrawStepper(drawList, skin, new Vector2(bounds.Max.X - stepperRadius, rowCenterY), stepperRadius,
                IconGlyph.Of(FontAwesomeIcon.Plus), enabled))
        {
            Reset(Clamp(amount + step, minimumBet, maximumBet, stack));
        }

        y = fieldMax.Y + RowGap * scale;
        var quickHeight = QuickHeight * scale;
        var quickWidth = (bounds.Width - RowGap * scale * 2f) / 3f;
        if (DrawQuick(skin, new Rect(new Vector2(bounds.Min.X, y),
                new Vector2(bounds.Min.X + quickWidth, y + quickHeight)), Loc.T(L.Casino.BetMin), enabled))
        {
            Reset(Clamp(minimumBet, minimumBet, maximumBet, stack));
        }

        var middleLeft = bounds.Min.X + quickWidth + RowGap * scale;
        if (DrawQuick(skin, new Rect(new Vector2(middleLeft, y),
                new Vector2(middleLeft + quickWidth, y + quickHeight)), Loc.T(L.Casino.BetHalf), enabled))
        {
            Reset(Half(minimumBet, maximumBet, stack));
        }

        if (DrawQuick(skin, new Rect(new Vector2(bounds.Max.X - quickWidth, y),
                new Vector2(bounds.Max.X, y + quickHeight)), Loc.T(L.Casino.BetMax), enabled))
        {
            Reset(Clamp(ceiling, minimumBet, maximumBet, stack));
        }

        if (typed > 0 && typed != amount)
        {
            clampFlash = FlashSeconds;
        }

        return y + quickHeight;
    }

    private void DrawField(ImDrawListPtr drawList, AppSkin skin, Vector2 min, Vector2 max, float scale, bool enabled)
    {
        var field = new Rect(min, max);
        SearchBar.Surface(drawList, field, skin.Ink);
        if (clampFlash > 0f)
        {
            var strength = clampFlash / FlashSeconds;
            var capsule = SearchBar.Capsule(field);
            Squircle.Stroke(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(skin.Accent, strength)), Metrics.Stroke.Ring * scale);
        }

        if (!enabled)
        {
            Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f),
                buffer.Length > 0 ? buffer : "0", skin.MutedInk, TextStyles.Subheadline);
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X + 10f * scale,
            (min.Y + max.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(max.X - min.X - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, skin.TitleInk))
        {
            ImGui.InputText(fieldId, ref buffer, BufferLength,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }
    }

    private static bool DrawStepper(ImDrawListPtr drawList, AppSkin skin, Vector2 center, float radius, string glyph,
        bool enabled) =>
        RoundButton.Icon(drawList, center, radius, glyph, skin.Ink, ButtonStyle.Gray, enabled: enabled);

    private static bool DrawQuick(AppSkin skin, in Rect rect, string label, bool enabled) =>
        Button.Draw(rect, label, skin.Ink, ButtonStyle.Gray, enabled: enabled);

    private long Parse()
    {
        return long.TryParse(buffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
