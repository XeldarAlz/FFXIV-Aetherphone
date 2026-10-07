using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.MoogleClicker;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.MoogleClicker;

internal readonly struct UpgradePick
{
    public readonly int Upgrade;
    public readonly bool Denied;
    public readonly Vector2 Point;

    public UpgradePick(int upgrade, bool denied, Vector2 point)
    {
        Upgrade = upgrade;
        Denied = denied;
        Point = point;
    }

    public bool Picked => Upgrade >= 0;

    public static UpgradePick None => new(-1, false, Vector2.Zero);
}

internal sealed class MoogleClickerUpgrades
{
    public const int MaxTiles = 6;
    private const float TileRadius = 21f;
    private const float TileGap = 10f;
    private const float AppearSpeed = 3.2f;
    private const float ShakeDecay = 3.4f;
    private const float ShakeDistance = 4f;
    private const float RingThickness = 2.5f;
    private const float PipRadius = 2.2f;
    private const float CaptionPadX = 12f;
    private const float CaptionPadY = 7f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);

    private readonly int[] candidates = new int[MaxTiles];
    private readonly float[] appear = new float[KupoUpgrades.Count];
    private readonly float[] shake = new float[KupoUpgrades.Count];
    private readonly bool[] shown = new bool[KupoUpgrades.Count];
    private TextSlot tierName;
    private TextSlot shareEffect;
    private float time;

    public void Shake(int upgrade) => shake[upgrade] = 1f;

    public void Reset()
    {
        Array.Clear(appear);
        Array.Clear(shake);
    }

    public UpgradePick Draw(ImDrawListPtr drawList, Rect rect, KupoWorkshop workshop, Vector4 accent,
        bool interactive, float deltaSeconds, float scale)
    {
        time += deltaSeconds;
        var radius = MathF.Min(TileRadius * scale, rect.Height * 0.4f);
        var gap = TileGap * scale;
        var fit = Math.Clamp((int)((rect.Width + gap) / (radius * 2f + gap)), 1, MaxTiles);
        var count = workshop.NextUpgrades(candidates.AsSpan(0, fit));
        Array.Clear(shown);
        for (var index = 0; index < count; index++)
        {
            shown[candidates[index]] = true;
        }

        for (var upgrade = 0; upgrade < KupoUpgrades.Count; upgrade++)
        {
            appear[upgrade] = shown[upgrade] ? GameJuice.Advance(appear[upgrade], deltaSeconds, AppearSpeed) : 0f;
            shake[upgrade] = MathF.Max(0f, shake[upgrade] - deltaSeconds * ShakeDecay);
        }

        if (count == 0)
        {
            var maxWidth = rect.Width - StagePill.Width(string.Empty, false, scale);
            var hint = Typography.FitText(Loc.T(L.MoogleClicker.UpgradesHint), maxWidth, TextStyles.FootnoteEmphasized);
            var pill = StagePill.Around(rect.Center, StagePill.Width(hint, false, scale), scale);
            StagePill.Draw(drawList, pill, hint, MoogleClickerText.Muted, 0.85f, scale);
            return UpgradePick.None;
        }

        var total = count * radius * 2f + (count - 1) * gap;
        var left = rect.Center.X - total * 0.5f + radius;
        var pick = UpgradePick.None;
        var hovered = -1;
        var hoveredCenter = Vector2.Zero;
        for (var index = 0; index < count; index++)
        {
            var upgrade = candidates[index];
            var resting = new Vector2(left + index * (radius * 2f + gap), rect.Center.Y);
            var corner = new Vector2(radius, radius);
            var pointer = interactive && UiInteract.Hover(resting - corner, resting + corner);
            var lift = pointer ? 2f * scale : 0f;
            var shakeOffset = MathF.Sin(time * 44f) * ShakeDistance * scale * shake[upgrade];
            var center = resting + new Vector2(shakeOffset, -lift);
            var size = radius * GameJuice.PopIn(appear[upgrade]);
            DrawTile(drawList, center, size, upgrade, workshop, accent, scale);
            if (pointer)
            {
                hovered = upgrade;
                hoveredCenter = resting;
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (pointer && UiInteract.Click(resting - corner, resting + corner, pointer, false))
            {
                pick = new UpgradePick(upgrade, !workshop.CanBuyUpgrade(upgrade), resting);
            }
        }

        if (hovered >= 0)
        {
            DrawCaption(drawList, hoveredCenter - new Vector2(0f, radius + Metrics.Space.Sm * scale), hovered, workshop,
                accent, rect, scale);
        }

        return pick;
    }

    private void DrawTile(ImDrawListPtr drawList, Vector2 center, float radius, int upgrade, KupoWorkshop workshop,
        Vector4 accent, float scale)
    {
        if (radius <= 0.5f)
        {
            return;
        }

        var cost = KupoUpgrades.Cost(upgrade);
        var affordable = workshop.CanBuyUpgrade(upgrade);
        var tint = MoogleClickerText.UpgradeTint(upgrade, accent);
        if (affordable)
        {
            ProgressRing.Glow(center, radius * 1.55f, tint, 0.45f + 0.35f * Pulse.Wave(Pulse.Medium));
        }

        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.3f)),
            32);
        var fill = affordable ? tint : GamePalette.Darken(tint, 0.45f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 32);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.34f), radius * 0.42f,
            ImGui.GetColorU32(White with { W = 0.16f }), 20);
        var thickness = RingThickness * scale;
        if (affordable)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Lighten(tint, 0.4f)), 32, thickness);
        }
        else
        {
            var fraction = cost > 0d ? (float)Math.Clamp(workshop.Kupo / cost, 0d, 1d) : 1f;
            ProgressRing.Track(drawList, center, radius, thickness, White with { W = 0.12f });
            ProgressRing.Fill(drawList, center, radius, thickness, fraction, Gold);
        }

        ProgressRing.CenterIcon(drawList, center - new Vector2(0f, radius * 0.08f), MoogleClickerText.UpgradeIcon(upgrade),
            affordable ? White : White with { W = 0.6f }, radius * 0.82f);
        if (!KupoUpgrades.IsBuilding(upgrade))
        {
            return;
        }

        var pips = KupoUpgrades.Tier(upgrade) + 1;
        var pipRadius = PipRadius * scale;
        var spacing = pipRadius * 2.6f;
        var startX = center.X - (pips - 1) * spacing * 0.5f;
        var pipY = center.Y + radius * 0.62f;
        for (var pip = 0; pip < pips; pip++)
        {
            drawList.AddCircleFilled(new Vector2(startX + pip * spacing, pipY), pipRadius,
                ImGui.GetColorU32(affordable ? Gold : White with { W = 0.5f }), 10);
        }
    }

    private void DrawCaption(ImDrawListPtr drawList, Vector2 anchor, int upgrade, KupoWorkshop workshop,
        Vector4 accent, Rect row, float scale)
    {
        var name = UpgradeName(upgrade);
        var effect = UpgradeEffect(upgrade);
        var cost = KupoFormat.Amount(KupoUpgrades.Cost(upgrade));
        var nameSize = Typography.Measure(name, TextStyles.FootnoteEmphasized);
        var effectSize = Typography.Measure(effect, TextStyles.Caption1);
        var costSize = Typography.Measure(cost, TextStyles.Caption1);
        var gap = Metrics.Space.Sm * scale;
        var detailWidth = effectSize.X + gap + costSize.X;
        var width = MathF.Min(MathF.Max(nameSize.X, detailWidth) + CaptionPadX * 2f * scale, row.Width);
        var height = nameSize.Y + effectSize.Y + CaptionPadY * 2f * scale;
        var centerX = Math.Clamp(anchor.X, row.Min.X + width * 0.5f, row.Max.X - width * 0.5f);
        var min = new Vector2(centerX - width * 0.5f, anchor.Y - height);
        var max = new Vector2(centerX + width * 0.5f, anchor.Y);
        var radius = MathF.Min(height * 0.5f, Metrics.Space.Md * scale);
        Elevation.Floating(drawList, min, max, radius, scale);
        Material.Frosted(drawList, min, max, radius, scale);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.35f }), 1f * scale);
        var innerWidth = width - CaptionPadX * 2f * scale;
        var top = min.Y + CaptionPadY * scale;
        Typography.DrawCentered(drawList, new Vector2(centerX, top + nameSize.Y * 0.5f),
            Typography.FitText(name, innerWidth, TextStyles.FootnoteEmphasized), MoogleClickerText.Ink,
            TextStyles.FootnoteEmphasized);
        var detailLeft = centerX - MathF.Min(detailWidth, innerWidth) * 0.5f;
        var detailY = top + nameSize.Y;
        var affordable = workshop.CanBuyUpgrade(upgrade);
        var effectWidth = MathF.Min(effectSize.X, MathF.Max(0f, innerWidth - gap - costSize.X));
        Typography.Draw(drawList, new Vector2(detailLeft, detailY),
            Typography.FitText(effect, effectWidth + 1f, TextStyles.Caption1), MoogleClickerText.Muted, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(detailLeft + effectWidth + gap, detailY), cost,
            affordable ? Gold : MoogleClickerText.Muted, TextStyles.Caption1);
    }

    private string UpgradeName(int upgrade)
    {
        if (!KupoUpgrades.IsBuilding(upgrade))
        {
            return Loc.T(MoogleClickerText.TapUpgradeNames[KupoUpgrades.TapIndex(upgrade)]);
        }

        var building = Loc.T(MoogleClickerText.BuildingNames[KupoUpgrades.Building(upgrade)]);
        return tierName.Get(L.MoogleClicker.UpgradeTier, building, GameNumber.Label(KupoUpgrades.Tier(upgrade) + 1));
    }

    private string UpgradeEffect(int upgrade)
    {
        if (KupoUpgrades.IsBuilding(upgrade))
        {
            return Loc.T(L.MoogleClicker.EffectDouble);
        }

        var tapIndex = KupoUpgrades.TapIndex(upgrade);
        if (tapIndex < KupoUpgrades.TapDoublings)
        {
            return Loc.T(L.MoogleClicker.EffectTapDouble);
        }

        var percent = (int)Math.Round(KupoUpgrades.TapShare(tapIndex) * 100d);
        return shareEffect.Get(L.MoogleClicker.EffectTapShare, GameNumber.Label(percent));
    }
}
