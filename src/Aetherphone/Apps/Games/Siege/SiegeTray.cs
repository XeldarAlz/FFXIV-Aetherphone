using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Siege;

internal sealed class SiegeTray
{
    public const int SlotCount = SiegeRules.DefenderKinds + 1;
    public const int ShovelSlot = SiegeRules.DefenderKinds;
    public const int NoSlot = -1;
    public const float Height = 88f;
    private const float Padding = 8f;
    private const float SlotGap = 6f;
    private const float MaxSlotWidth = 60f;
    private const float CardRadius = 12f;
    private const float SelectedLift = 7f;
    private const float LiftSpeed = 14f;
    private const float ShakeDecay = 3.2f;
    private const float ShakeDistance = 5f;
    private const float RingThickness = 2.5f;
    private static readonly TextStyle CostStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle KeyStyle = TextStyles.Caption2;
    private static readonly Vector4 LockedFill = new(0.16f, 0.18f, 0.20f, 1f);
    private static readonly Vector4 Veil = new(0.05f, 0.06f, 0.08f, 0.5f);
    private static readonly Vector4 CostInk = new(1f, 0.95f, 0.80f, 1f);
    private static readonly Vector4 CostShort = new(1f, 0.48f, 0.42f, 1f);
    private static readonly Vector4 KeyInk = new(1f, 1f, 1f, 0.55f);
    private static readonly Vector4 ShovelFill = new(0.30f, 0.32f, 0.36f, 1f);

    private readonly Rect[] slots = new Rect[SlotCount];
    private readonly float[] shakes = new float[SlotCount];
    private readonly float[] lifts = new float[SlotCount];

    public Rect Area { get; private set; }

    public static DefenderKind KindOf(int slot) =>
        slot >= 0 && slot < ShovelSlot ? (DefenderKind)(slot + 1) : DefenderKind.None;

    public void Layout(Rect area, float scale)
    {
        Area = area;
        var padding = Padding * scale;
        var gap = SlotGap * scale;
        var available = area.Width - padding * 2f - gap * (SlotCount - 1);
        var width = MathF.Min(MaxSlotWidth * scale, available / SlotCount);
        var total = width * SlotCount + gap * (SlotCount - 1);
        var left = area.Center.X - total * 0.5f;
        var top = area.Min.Y + padding;
        var bottom = area.Max.Y - padding;
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var x = left + slot * (width + gap);
            slots[slot] = new Rect(new Vector2(x, top), new Vector2(x + width, bottom));
        }
    }

    public Rect Slot(int slot) => slots[slot];

    public int SlotAt(Vector2 point)
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            if (slots[slot].Contains(point))
            {
                return slot;
            }
        }

        return NoSlot;
    }

    public void Shake(int slot)
    {
        if (slot >= 0 && slot < SlotCount)
        {
            shakes[slot] = 1f;
        }
    }

    public void Reset()
    {
        Array.Clear(shakes);
        Array.Clear(lifts);
    }

    public void Update(float deltaSeconds, int selected)
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            shakes[slot] = MathF.Max(0f, shakes[slot] - deltaSeconds * ShakeDecay);
            var target = slot == selected ? 1f : 0f;
            lifts[slot] += (target - lifts[slot]) * MathF.Min(1f, deltaSeconds * LiftSpeed);
        }
    }

    public void Draw(ImDrawListPtr drawList, SiegeBoard board, int selected, float time, float scale, Vector4 accent)
    {
        var area = Area;
        Material.Frosted(drawList, area.Min, area.Max, CardRadius * scale + Padding * scale, scale, 0.85f);
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var offset = new Vector2(MathF.Sin(shakes[slot] * 38f) * shakes[slot] * ShakeDistance * scale,
                -lifts[slot] * SelectedLift * scale);
            var card = slots[slot].Translate(offset);
            if (slot == ShovelSlot)
            {
                DrawShovel(drawList, card, slot == selected, time, scale, accent);
            }
            else
            {
                DrawCard(drawList, board, KindOf(slot), card, slot == selected, time, scale, accent);
            }

            Typography.Draw(drawList, card.Min + new Vector2(5f * scale, 3f * scale), GameNumber.Label(slot + 1), KeyInk,
                KeyStyle);
        }
    }

    private static void DrawCard(ImDrawListPtr drawList, SiegeBoard board, DefenderKind kind, Rect card, bool selected,
        float time, float scale, Vector4 accent)
    {
        var radius = CardRadius * scale;
        if (!board.Unlocked(kind))
        {
            StageCell.Draw(drawList, card, LockedFill, CellDepth.Sunken, radius, scale);
            ProgressRing.CenterIcon(drawList, card.Center, FontAwesomeIcon.Lock, KeyInk, 14f * scale);
            return;
        }

        var ready = board.Ready(kind);
        var affordable = board.Affordable(kind);
        var usable = ready && affordable;
        if (selected)
        {
            ProgressRing.Glow(card.Center, card.Width * 0.75f, accent, 0.7f);
        }

        var fill = GamePalette.Darken(SiegeArt.Tint(kind), usable ? 0.5f : 0.68f);
        StageCell.Draw(drawList, card, fill, selected ? CellDepth.Pressed : CellDepth.Raised, radius, scale);
        var artCenter = new Vector2(card.Center.X, card.Min.Y + card.Height * 0.42f);
        var artUnit = card.Width * 0.9f;
        SiegeArt.Card(drawList, kind, artCenter, artUnit, time, usable ? 1f : 0.55f);
        var cooldown = board.CooldownFraction(kind);
        if (!usable)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(Veil));
        }

        if (cooldown > 0f)
        {
            var ringRadius = card.Width * 0.36f;
            var thickness = RingThickness * scale;
            ProgressRing.Track(drawList, artCenter, ringRadius, thickness, KeyInk with { W = 0.25f });
            ProgressRing.Fill(drawList, artCenter, ringRadius, thickness, 1f - cooldown, accent);
        }

        var cost = GameNumber.Label(SiegeRules.Cost(kind));
        var costSize = Typography.Measure(cost, CostStyle);
        var sunRadius = 4f * scale;
        var rowWidth = sunRadius * 2f + 3f * scale + costSize.X;
        var rowY = card.Max.Y - costSize.Y * 0.5f - 6f * scale;
        var sunCenter = new Vector2(card.Center.X - rowWidth * 0.5f + sunRadius, rowY);
        drawList.AddCircleFilled(sunCenter, sunRadius, ImGui.GetColorU32(SiegeArt.Sun), 12);
        Typography.Draw(drawList, new Vector2(sunCenter.X + sunRadius + 3f * scale, rowY - costSize.Y * 0.5f), cost,
            affordable ? CostInk : CostShort, CostStyle);
        if (selected)
        {
            Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(accent), 2f * scale);
        }
    }

    private static void DrawShovel(ImDrawListPtr drawList, Rect card, bool selected, float time, float scale,
        Vector4 accent)
    {
        var radius = CardRadius * scale;
        if (selected)
        {
            ProgressRing.Glow(card.Center, card.Width * 0.75f, accent, 0.7f);
        }

        StageCell.Draw(drawList, card, ShovelFill, selected ? CellDepth.Pressed : CellDepth.Raised, radius, scale);
        var wobble = selected ? MathF.Sin(time * 9f) * 0.08f : 0f;
        SiegeArt.Shovel(drawList, card.Center + new Vector2(0f, wobble * card.Width), card.Width * 0.9f, 1f);
        if (selected)
        {
            Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(accent), 2f * scale);
        }
    }
}
