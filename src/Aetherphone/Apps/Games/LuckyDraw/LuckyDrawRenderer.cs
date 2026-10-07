using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.LuckyDraw;

internal readonly struct LuckyPlateView
{
    public readonly string Name;
    public readonly string Detail;
    public readonly Vector4 DetailInk;
    public readonly int Total;
    public readonly int Hand;
    public readonly LuckySeatState State;
    public readonly Vector4 Color;
    public readonly bool Active;
    public readonly bool Bot;
    public readonly bool Targetable;
    public readonly bool Chance;
    public readonly int ForcedLeft;
    public readonly float Pop;

    public LuckyPlateView(string name, string detail, Vector4 detailInk, int total, int hand, LuckySeatState state,
        Vector4 color, bool active, bool bot, bool targetable, bool chance, int forcedLeft, float pop)
    {
        Name = name;
        Detail = detail;
        DetailInk = detailInk;
        Total = total;
        Hand = hand;
        State = state;
        Color = color;
        Active = active;
        Bot = bot;
        Targetable = targetable;
        Chance = chance;
        ForcedLeft = forcedLeft;
        Pop = pop;
    }
}

internal static class LuckyDrawRenderer
{
    public static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    public static readonly Vector4 Ice = new(0.58f, 0.86f, 1f, 1f);
    public static readonly Vector4 Mint = new(0.40f, 0.88f, 0.58f, 1f);
    public static readonly Vector4 Gold = new(1f, 0.82f, 0.36f, 1f);
    public static readonly Vector4 Ember = new(1f, 0.58f, 0.24f, 1f);
    public static readonly Vector4 FreezeTint = new(0.32f, 0.66f, 0.96f, 1f);
    public static readonly Vector4 FlipTint = new(0.96f, 0.52f, 0.20f, 1f);
    public static readonly Vector4 ChanceTint = new(0.26f, 0.70f, 0.44f, 1f);
    public static readonly Vector4 PlusTint = new(0.98f, 0.80f, 0.34f, 1f);
    public static readonly Vector4 TimesTint = new(1f, 0.46f, 0.58f, 1f);
    private const float PlateRadiusFraction = 0.5f;
    private const float AvatarFraction = 0.34f;
    private const float BarHeight = 2.5f;
    private const float BarInset = 10f;
    private const float PopGrow = 0.08f;
    private const float FlashOverlay = 0.5f;
    private const float BlinkSeconds = 0.12f;
    private const int TableSegments = 72;
    private const float SheetWidth = 320f;
    private const float SheetRowHeight = 36f;
    private const float SheetPad = 16f;
    private const float SheetButtonHeight = 46f;
    private const float SheetButtonWidth = 200f;
    private const float SheetLift = 40f;
    private const float DotRadius = 1.8f;
    private const float DotStep = 5.5f;
    private const int DotCount = 3;
    private const float DotSpeed = 7f;
    private static readonly TextStyle NameStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle DetailStyle = TextStyles.Caption2;
    private static readonly TextStyle TotalStyle = TextStyles.Headline;
    private static readonly TextStyle HandStyle = TextStyles.Caption1;

    public static void BuildDesigns(CardDesign[] designs)
    {
        for (var number = 0; number <= LuckyCards.MaxNumber; number++)
        {
            designs[number] = CardDesign.Numbered(number);
        }

        designs[LuckyCards.Freeze] = CardDesign.Action(FontAwesomeIcon.Snowflake, FreezeTint);
        designs[LuckyCards.FlipThree] = CardDesign.Action(FontAwesomeIcon.LayerGroup, FlipTint);
        designs[LuckyCards.SecondChance] = CardDesign.Action(FontAwesomeIcon.ShieldAlt, ChanceTint);
        for (var face = LuckyCards.PlusTwo; face <= LuckyCards.PlusTen; face++)
        {
            designs[face] = CardDesign.Modifier(GameNumber.Signed(LuckyCards.PlusValue(face)), PlusTint);
        }

        designs[LuckyCards.Times] = CardDesign.Modifier(Loc.T(L.Stage.Times, GameNumber.Label(2)), TimesTint);
    }

    public static void DrawTable(ImDrawListPtr drawList, Rect ring, Vector4 accent, float scale)
    {
        var center = ring.Center;
        var radius = new Vector2(ring.Width * 0.5f, ring.Height * 0.5f) * 0.82f;
        TracePath(drawList, center + new Vector2(0f, 6f * scale), radius);
        drawList.PathFillConvex(ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)));
        TracePath(drawList, center, radius);
        drawList.PathFillConvex(ImGui.GetColorU32(GamePalette.Darken(accent, 0.62f) with { W = 0.55f }));
        TracePath(drawList, center, radius * 0.94f);
        drawList.PathFillConvex(ImGui.GetColorU32(GamePalette.Darken(accent, 0.48f) with { W = 0.35f }));
        TracePath(drawList, center, radius);
        drawList.PathStroke(ImGui.GetColorU32(GamePalette.Lighten(accent, 0.25f) with { W = 0.35f }), ImDrawFlags.Closed,
            2f * scale);
        TracePath(drawList, center, radius * 0.94f);
        drawList.PathStroke(ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f)), ImDrawFlags.Closed, 1f * scale);
    }

    public static void DrawCard(ImDrawListPtr drawList, in CardPose pose, in CardDesign design, Vector4 accent,
        float scale, float flash, bool glow, float alpha = 1f)
    {
        if (flash > 0f)
        {
            ProgressRing.Glow(pose.Center, pose.Width * 1.1f, Danger, flash * 0.9f);
        }
        else if (glow)
        {
            ProgressRing.Glow(pose.Center, pose.Width * 0.95f, design.Tint, 0.35f + 0.3f * Pulse.Wave(Pulse.Fast));
        }

        var blink = flash > 0f && (int)(flash / BlinkSeconds) % 2 == 0;
        CardFace.Draw(drawList, pose, design, accent, scale, alpha, blink);
        if (flash <= 0f)
        {
            return;
        }

        var firstVertex = drawList.VtxBuffer.Size;
        var half = new Vector2(pose.Width * 0.5f, pose.Height * 0.5f);
        Squircle.Fill(drawList, pose.Center - half, pose.Center + half, pose.Width * 0.14f,
            ImGui.GetColorU32(Danger with { W = FlashOverlay * MathF.Min(1f, flash * 1.4f) * (blink ? 1f : 0.55f) }));
        Rotate(drawList, firstVertex, pose);
    }

    public static void DrawPlate(ImDrawListPtr drawList, Rect rect, in LuckyPlateView view, float pulse,
        float scale)
    {
        var grow = 1f + PopGrow * view.Pop;
        if (grow != 1f)
        {
            rect = rect.Scaled(grow);
        }

        var center = rect.Center;
        var radius = rect.Height * PlateRadiusFraction;
        var dim = view.State == LuckySeatState.Busted ? 0.55f : 1f;
        if (view.Targetable)
        {
            ProgressRing.Glow(center, rect.Width * 0.62f, Gold, 0.35f + 0.35f * pulse);
        }
        else if (view.Active)
        {
            ProgressRing.Glow(center, rect.Width * 0.6f, view.Color, 0.3f + 0.25f * pulse);
        }

        Material.Frosted(drawList, rect.Min, rect.Max, radius, scale, 0.94f);
        if (view.State == LuckySeatState.Frozen)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Ice with { W = 0.16f }));
        }

        var stroke = view.Targetable ? Gold with { W = 0.6f + 0.4f * pulse }
            : view.Active ? view.Color with { W = 0.85f }
            : view.State == LuckySeatState.Busted ? Danger with { W = 0.5f }
            : new Vector4(1f, 1f, 1f, 0.12f);
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(stroke),
            (view.Active || view.Targetable ? 1.6f : 1f) * scale);

        var avatarRadius = rect.Height * AvatarFraction;
        var avatar = new Vector2(rect.Min.X + rect.Height * 0.5f, center.Y);
        drawList.AddCircleFilled(avatar, avatarRadius, ImGui.GetColorU32(view.Color with { W = dim }), 28);
        drawList.AddCircle(avatar, avatarRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.4f * dim)), 28,
            1f * scale);
        ProgressRing.CenterIcon(drawList, avatar, view.Bot ? FontAwesomeIcon.Robot : FontAwesomeIcon.User,
            GamePalette.InkOn(view.Color) with { W = dim }, avatarRadius * 1.05f);
        if (view.Chance)
        {
            var badge = avatar + new Vector2(avatarRadius * 0.78f, avatarRadius * 0.7f);
            drawList.AddCircleFilled(badge, avatarRadius * 0.48f, ImGui.GetColorU32(ChanceTint), 16);
            ProgressRing.CenterIcon(drawList, badge, FontAwesomeIcon.ShieldAlt, GamePalette.InkLight,
                avatarRadius * 0.55f);
        }

        var totalLabel = GameNumber.Label(view.Total);
        var totalWidth = Typography.Measure(totalLabel, TotalStyle).X;
        var right = rect.Max.X - radius * 0.7f;
        var textLeft = avatar.X + avatarRadius + 6f * scale;
        var nameWidth = MathF.Max(8f * scale, right - totalWidth - 6f * scale - textLeft);
        var nameHeight = Typography.LineHeight(NameStyle);
        var detailHeight = Typography.LineHeight(DetailStyle);
        var top = center.Y - (nameHeight + detailHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(view.Name, nameWidth, NameStyle),
            GamePalette.InkLight with { W = dim }, NameStyle);
        if (view.Detail.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
                Typography.FitText(view.Detail, nameWidth, DetailStyle), view.DetailInk, DetailStyle);
        }

        var totalHeight = Typography.LineHeight(TotalStyle);
        var handVisible = view.Hand > 0 && view.State != LuckySeatState.Busted;
        var handHeight = handVisible ? Typography.LineHeight(HandStyle) : 0f;
        var stackTop = center.Y - (totalHeight + handHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(right - totalWidth, stackTop), totalLabel,
            GamePalette.InkLight with { W = dim }, TotalStyle);
        if (handVisible)
        {
            var handLabel = GameNumber.Signed(view.Hand);
            var handWidth = Typography.Measure(handLabel, HandStyle).X;
            Typography.Draw(drawList, new Vector2(right - handWidth, stackTop + totalHeight), handLabel,
                view.State == LuckySeatState.Active ? Gold : Mint, HandStyle);
        }

        var barLeft = rect.Min.X + BarInset * scale;
        var barRight = rect.Max.X - BarInset * scale;
        var barY = rect.Max.Y - 4f * scale;
        var fraction = Math.Clamp(view.Total / (float)LuckyDrawBoard.WinTarget, 0f, 1f);
        var barHeight = BarHeight * scale;
        drawList.AddRectFilled(new Vector2(barLeft, barY - barHeight), new Vector2(barRight, barY),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)), barHeight);
        if (fraction > 0f)
        {
            drawList.AddRectFilled(new Vector2(barLeft, barY - barHeight),
                new Vector2(barLeft + (barRight - barLeft) * fraction, barY),
                ImGui.GetColorU32(view.Color with { W = 0.85f * dim }), barHeight);
        }

        if (view.ForcedLeft <= 0)
        {
            return;
        }

        for (var pip = 0; pip < LuckyDrawBoard.FlipThreeCards; pip++)
        {
            var pipCenter = new Vector2(center.X + (pip - 1) * 9f * scale, rect.Min.Y - 6f * scale);
            var lit = pip < view.ForcedLeft;
            drawList.AddCircleFilled(pipCenter, 3f * scale,
                ImGui.GetColorU32(lit ? FlipTint : new Vector4(1f, 1f, 1f, 0.2f)), 12);
        }
    }

    public static void DrawThinking(ImDrawListPtr drawList, Vector2 center, float phase, Vector4 color, float scale)
    {
        var half = new Vector2((DotCount - 1) * DotStep * 0.5f + DotRadius + 6f, 7f) * scale;
        Material.Frosted(drawList, center - half, center + half, half.Y, scale, 0.9f);
        var left = center.X - (DotCount - 1) * DotStep * scale * 0.5f;
        for (var dot = 0; dot < DotCount; dot++)
        {
            var bounce = MathF.Sin(phase * DotSpeed - dot * 0.9f);
            var alpha = 0.45f + 0.55f * MathF.Max(0f, bounce);
            var lift = MathF.Max(0f, bounce) * 2f * scale;
            drawList.AddCircleFilled(new Vector2(left + dot * DotStep * scale, center.Y - lift), DotRadius * scale,
                ImGui.GetColorU32(color with { W = alpha }), 10);
        }
    }

    public static void DrawStatus(ImDrawListPtr drawList, Rect controls, string text, Vector4 ink, float scale,
        float alpha)
    {
        var width = MathF.Min(controls.Width, Typography.Measure(text, TextStyles.SubheadlineEmphasized).X + 40f * scale);
        var half = new Vector2(width * 0.5f, controls.Height * 0.42f);
        var center = controls.Center;
        Material.Frosted(drawList, center - half, center + half, half.Y, scale, 0.85f * alpha);
        Typography.DrawCentered(drawList, center,
            Typography.FitText(text, width - 24f * scale, TextStyles.SubheadlineEmphasized), ink with { W = alpha },
            TextStyles.SubheadlineEmphasized);
    }

    public static float SheetHeight(int rows, float scale) =>
        (SheetPad * 2f + Typography.LineHeight(TextStyles.Title2) / scale + 10f + rows * SheetRowHeight + 14f +
         SheetButtonHeight) * scale;

    public static bool DrawSheet(ImDrawListPtr drawList, Rect full, Rect area, LuckyDrawBoard board,
        ReadOnlySpan<int> order, string[] names, string title, string button, float progress, Vector4 accent,
        PhoneTheme theme, float scale, bool interactive)
    {
        if (progress <= 0f)
        {
            return false;
        }

        Material.Veil(drawList, full.Min, full.Max, 0.42f * Easing.Clamp01(progress * 1.5f));
        var pop = GameJuice.PopIn(Easing.Clamp01(progress));
        var width = MathF.Min(area.Width, SheetWidth * scale);
        var height = SheetHeight(order.Length, scale);
        var center = new Vector2(area.Center.X, area.Center.Y + (1f - pop) * SheetLift * scale);
        var min = center - new Vector2(width, height) * 0.5f;
        var max = center + new Vector2(width, height) * 0.5f;
        var alpha = Easing.Clamp01(progress * 2f);
        var radius = Metrics.Radius.Lg * scale;
        Elevation.Floating(drawList, min, max, radius, scale);
        Material.Frosted(drawList, min, max, radius, scale, alpha);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.45f * alpha }), 1.2f * scale);
        var pad = SheetPad * scale;
        var top = min.Y + pad;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(center.X, top + titleHeight * 0.5f),
            Typography.FitText(title, width - pad * 2f, TextStyles.Title2), theme.TextStrong with { W = alpha },
            TextStyles.Title2);
        top += titleHeight + 10f * scale;
        var rowHeight = SheetRowHeight * scale;
        for (var index = 0; index < order.Length; index++)
        {
            var phase = Easing.EaseOutCubic(GameJuice.Stagger(Easing.Clamp01(progress * 1.3f), index, order.Length));
            DrawSheetRow(drawList, new Rect(new Vector2(min.X + pad, top), new Vector2(max.X - pad, top + rowHeight)),
                board, order[index], names[order[index]], phase * alpha, index == 0, theme, scale);
            top += rowHeight;
        }

        top += 14f * scale;
        var buttonSize = new Vector2(MathF.Min(width - pad * 2f, SheetButtonWidth * scale), SheetButtonHeight * scale);
        var buttonCenter = new Vector2(center.X, top + buttonSize.Y * 0.5f);
        if (!interactive || alpha < 0.5f)
        {
            return false;
        }

        return GameHud.Button(buttonCenter, buttonSize, button, accent, theme) && progress >= 0.6f;
    }

    private static void DrawSheetRow(ImDrawListPtr drawList, Rect row, LuckyDrawBoard board, int seat, string name,
        float alpha, bool leader, PhoneTheme theme, float scale)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var color = GameSeats.Color(seat);
        var centerY = row.Min.Y + row.Height * 0.42f;
        if (leader)
        {
            Squircle.Fill(drawList, new Vector2(row.Min.X - 6f * scale, row.Min.Y + 2f * scale),
                new Vector2(row.Max.X + 6f * scale, row.Max.Y - 2f * scale), 10f * scale,
                ImGui.GetColorU32(Gold with { W = 0.10f * alpha }));
        }

        drawList.AddCircleFilled(new Vector2(row.Min.X + 6f * scale, centerY), 5f * scale,
            ImGui.GetColorU32(color with { W = alpha }), 14);
        var totalLabel = GameNumber.Label(board.Total(seat));
        var totalWidth = Typography.Measure(totalLabel, TextStyles.Headline).X;
        var resultLabel = ResultLabel(board, seat, out var resultInk);
        var resultWidth = Typography.Measure(resultLabel, TextStyles.FootnoteEmphasized).X;
        var resultRight = row.Max.X - totalWidth - 14f * scale;
        var nameLeft = row.Min.X + 18f * scale;
        var nameWidth = MathF.Max(8f * scale, resultRight - resultWidth - 10f * scale - nameLeft);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(nameLeft, centerY - lineHeight * 0.5f),
            Typography.FitText(name, nameWidth, TextStyles.Subheadline), theme.TextStrong with { W = alpha },
            TextStyles.Subheadline);
        var resultHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(resultRight - resultWidth, centerY - resultHeight * 0.5f), resultLabel,
            resultInk with { W = alpha }, TextStyles.FootnoteEmphasized);
        var totalHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Max.X - totalWidth, centerY - totalHeight * 0.5f), totalLabel,
            theme.TextStrong with { W = alpha }, TextStyles.Headline);
        var barY = row.Max.Y - 5f * scale;
        var barHeight = 3f * scale;
        var fraction = Math.Clamp(board.Total(seat) / (float)LuckyDrawBoard.WinTarget, 0f, 1f);
        drawList.AddRectFilled(new Vector2(nameLeft, barY - barHeight), new Vector2(row.Max.X, barY),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * alpha)), barHeight);
        if (fraction > 0f)
        {
            drawList.AddRectFilled(new Vector2(nameLeft, barY - barHeight),
                new Vector2(nameLeft + (row.Max.X - nameLeft) * fraction, barY),
                ImGui.GetColorU32(color with { W = 0.9f * alpha }), barHeight);
        }
    }

    private static string ResultLabel(LuckyDrawBoard board, int seat, out Vector4 ink)
    {
        if (board.State(seat) == LuckySeatState.Busted)
        {
            ink = Danger;
            return Loc.T(L.LuckyDraw.Bust);
        }

        ink = board.SevenSeat == seat ? Gold : Mint;
        return GameNumber.Signed(board.LastRoundScore(seat));
    }

    private static void TracePath(ImDrawListPtr drawList, Vector2 center, Vector2 radius)
    {
        for (var segment = 0; segment < TableSegments; segment++)
        {
            var angle = segment * MathF.Tau / TableSegments;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * radius.X, MathF.Sin(angle) * radius.Y));
        }
    }

    private static void Rotate(ImDrawListPtr drawList, int firstVertex, in CardPose pose)
    {
        if (pose.Angle == 0f && pose.Squash == 1f)
        {
            return;
        }

        var sine = MathF.Sin(pose.Angle);
        var cosine = MathF.Cos(pose.Angle);
        var pivot = pose.Center;
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var vertexIndex = Math.Max(0, firstVertex); vertexIndex < vertices.Length; vertexIndex++)
        {
            ref var vertex = ref vertices[vertexIndex];
            var offsetX = (vertex.Pos.X - pivot.X) * pose.Squash;
            var offsetY = vertex.Pos.Y - pivot.Y;
            vertex.Pos = new Vector2(pivot.X + offsetX * cosine - offsetY * sine,
                pivot.Y + offsetX * sine + offsetY * cosine);
        }
    }
}
