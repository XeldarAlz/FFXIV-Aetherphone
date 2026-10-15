using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Strip;

internal static class JackpotMarquee
{
    public const float Height = 54f;
    public const float PadX = 18f;
    public const float PadY = 8f;
    public const float SignHeight = 13f;
    public const float SignShare = 0.42f;
    public const float AmountGap = 12f;
    public const float BulbPitch = 21f;
    public const float BulbInset = 4f;
    public const float RimWidth = 1.5f;
    public const float FlickerPeriod = 5f;

    private const string PressId = "casino.jackpot";
    private const float RimAlpha = 0.75f;
    private const float HaloAlpha = 0.10f;
    private const float HaloSpread = 3f;

    private static readonly TextStyle AmountStyle = TextStyles.Title2;
    private static readonly Vector4 BedEdge = new(0.165f, 0.043f, 0.094f, 1f);
    private static readonly Vector4 BedCenter = new(0.251f, 0.071f, 0.165f, 1f);

    public static float HeightFor(float amountLine, float scale) =>
        MathF.Max(Height * scale, amountLine + PadY * 2f * scale);

    public static float BlockHeight(float scale) => HeightFor(Typography.LineHeight(AmountStyle), scale);

    public static void Follow(ref RollingAmount roll, long jackpot, float deltaSeconds)
    {
        if (roll.Target <= 0 && jackpot > 0)
        {
            roll.Snap(jackpot);
            return;
        }

        roll.Update(jackpot, deltaSeconds);
    }

    public static bool Draw(ImDrawListPtr drawList, Vector2 origin, float width, ref RollingAmount roll, long jackpot,
        float phase, float deltaSeconds, float scale, out Rect pressed)
    {
        Follow(ref roll, jackpot, deltaSeconds);
        var height = BlockHeight(scale);
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = CasinoArt.PressCard(ImGui.GetID(PressId), origin, max, out var min, out var shown);
        pressed = new Rect(min, shown);
        var radius = pressed.Height * 0.5f;
        DrawBed(drawList, pressed, radius, scale);
        CasinoLights.BulbChase(drawList, pressed.Inset(BulbInset * scale), radius - BulbInset * scale, scale, phase,
            BulbPitch, CasinoColors.Money, CasinoColors.LightA, 1f);
        var padX = PadX * scale;
        var signHeight = CasinoSigns.HeightToFit(CasinoSign.Jackpot, pressed.Width * SignShare, SignHeight * scale);
        var signWidth = CasinoSigns.Measure(CasinoSign.Jackpot, signHeight);
        CasinoSigns.Draw(drawList, CasinoSign.Jackpot,
            new Vector2(pressed.Min.X + padX + signWidth * 0.5f, pressed.Center.Y), signHeight, CasinoColors.Money,
            NeonFlicker.Level(phase, FlickerPeriod));
        DrawAmount(drawList, pressed, roll, pressed.Min.X + padX + signWidth + AmountGap * scale,
            pressed.Max.X - padX, scale);
        return UiInteract.Click(origin, max, hovered);
    }

    private static void DrawBed(ImDrawListPtr drawList, Rect rect, float radius, float scale)
    {
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(BedEdge));
        var left = rect.Min.X + radius;
        var right = rect.Max.X - radius;
        if (right > left)
        {
            var middle = (left + right) * 0.5f;
            var edge = ImGui.GetColorU32(BedCenter with { W = 0f });
            var center = ImGui.GetColorU32(BedCenter);
            drawList.AddRectFilledMultiColor(new Vector2(left, rect.Min.Y), new Vector2(middle, rect.Max.Y), edge,
                center, center, edge);
            drawList.AddRectFilledMultiColor(new Vector2(middle, rect.Min.Y), new Vector2(right, rect.Max.Y), center,
                edge, edge, center);
        }

        var spread = HaloSpread * scale;
        Squircle.Stroke(drawList, rect.Min - new Vector2(spread, spread), rect.Max + new Vector2(spread, spread),
            radius + spread, ImGui.GetColorU32(CasinoColors.Money with { W = HaloAlpha }), spread * 1.6f);
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(CasinoColors.Money with { W = RimAlpha }),
            MathF.Max(1f, RimWidth * scale));
    }

    private static void DrawAmount(ImDrawListPtr drawList, Rect rect, in RollingAmount roll, float left, float right,
        float scale)
    {
        var available = right - left;
        if (available <= 0f)
        {
            return;
        }

        var lineHeight = Typography.LineHeight(AmountStyle);
        var reserve = CurrencyGlyph.Reserve(lineHeight);
        var amount = NumberText.Group(roll.Display);
        if (Typography.Measure(amount, AmountStyle).X > available - reserve)
        {
            amount = NumberText.Compact(roll.Display);
        }

        var amountScale = Typography.FitScale(amount, MathF.Max(1f, available - reserve),
            AmountStyle.Scale * roll.PopScale, TextStyles.Headline.Scale, AmountStyle.Weight);
        var size = Typography.Measure(amount, amountScale, AmountStyle.Weight);
        var glyph = size.Y * CurrencyGlyph.GlyphFraction;
        var textLeft = right - size.X;
        var glyphCenter = new Vector2(textLeft - CurrencyGlyph.Reserve(size.Y) + glyph * 0.5f, rect.Center.Y);
        CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, glyphCenter, glyph);
        Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - size.Y * 0.5f), amount,
            CasinoColors.MoneyHighlight, amountScale, AmountStyle.Weight);
    }
}
