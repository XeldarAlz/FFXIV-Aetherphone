using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Coin;

internal sealed class CoinTreatCard
{
    private const float TileSize = 44f;
    private const float TileGlyph = 24f;
    private const float PipHeight = 22f;
    private const float CandySize = 4.5f;
    private const float RingRadius = 6.5f;
    private const float LockedRadius = 3f;
    private const float RingStroke = 1.6f;
    private const float RestAlpha = 0.14f;
    private const float BreathBase = 0.35f;
    private const float BreathRange = 0.40f;
    private const int StatusHiding = 100;
    private const int StatusLater = 200;
    private const int StatusTomorrow = 300;
    private const int StatusAllFound = 1000;

    private CachedText count;
    private CachedText status;

    public float Draw(AppSkin ui, Vector2 origin, float width, string ownName)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var padding = Metrics.Space.Lg * scale;
        var tile = TileSize * scale;
        var found = Treats.Found;
        var complete = found >= Treats.Total;
        var rewarded = complete && Treats.Rewarded && ownName.Length > 0;
        var textWidth = width - padding * 2f;
        var statusText = Status(found);
        var statusHeight = Typography.MeasureWrappedBlock(statusText, TextStyles.Footnote, textWidth).Y;
        var hint = complete ? string.Empty : Loc.T(L.Seasonal.TreatsRewardHint, Treats.Total);
        var hintHeight = hint.Length > 0
            ? Metrics.Space.Xxs * scale + Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textWidth).Y
            : 0f;
        var nameHeight = rewarded ? Metrics.Space.Sm * scale + Typography.LineHeight(TextStyles.Title3) : 0f;
        var pipsHeight = PipHeight * scale;
        var height = padding + tile + Metrics.Space.Lg * scale + pipsHeight + nameHeight +
                     Metrics.Space.Md * scale + statusHeight + hintHeight + padding;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        CoinArt.Card(drawList, ui, min, max, scale);

        var tileMin = new Vector2(min.X + padding, min.Y + padding);
        var tileMax = tileMin + new Vector2(tile, tile);
        IconTile.FillShaded(drawList, tileMin, tileMax, tile * Metrics.Radius.TileFactor,
            IconTile.Surface(Spooks.Pumpkin));
        PhoneIcon.Draw(drawList, (tileMin + tileMax) * 0.5f, PhoneIcons.Pumpkin, CoinArt.White, TileGlyph * scale);
        var rowCenterY = tileMin.Y + tile * 0.5f;
        CoinArt.Labels(drawList, tileMax.X + Metrics.Space.Md * scale, max.X - padding, rowCenterY,
            Loc.T(L.Seasonal.TreatsFoundLabel), Count(found), ui.TitleInk, ui.MutedInk, scale);

        var pipsCenterY = tileMax.Y + Metrics.Space.Lg * scale + pipsHeight * 0.5f;
        DrawPips(drawList, ui, min.X + padding, max.X - padding, pipsCenterY, found, scale);
        var cursorY = pipsCenterY + pipsHeight * 0.5f;
        if (rewarded)
        {
            cursorY += Metrics.Space.Sm * scale;
            var light = Palette.Luminance(ui.TitleInk) < 0.5f;
            var name = Typography.FitText(ownName, textWidth, TextStyles.Title3);
            Typography.Draw(drawList, new Vector2(min.X + padding, cursorY), name, NameEffects.HallowedInk(light),
                TextStyles.Title3, NameEffects.Hallowed(light));
            cursorY += Typography.LineHeight(TextStyles.Title3);
        }

        cursorY += Metrics.Space.Md * scale;
        Typography.DrawWrappedLeft(new Vector2(min.X + padding, cursorY), statusText, ui.TitleInk, TextStyles.Footnote,
            textWidth);
        if (hint.Length > 0)
        {
            var hintTop = cursorY + statusHeight + Metrics.Space.Xxs * scale;
            Typography.DrawWrappedLeft(new Vector2(min.X + padding, hintTop), hint, ui.MutedInk, TextStyles.Footnote,
                textWidth);
        }

        return max.Y;
    }

    private static void DrawPips(ImDrawListPtr drawList, AppSkin ui, float left, float right, float centerY,
        int found, float scale)
    {
        var unlocked = Treats.Unlocked;
        var edge = CandySize * 1.7f * scale;
        var first = left + edge;
        var step = (right - edge - first) / (Treats.Total - 1);
        var breath = BreathBase + BreathRange * Pulse.Wave(Pulse.Breath);
        for (var pipIndex = 0; pipIndex < Treats.Total; pipIndex++)
        {
            var center = new Vector2(first + step * pipIndex, centerY);
            if (pipIndex < found)
            {
                Treats.DrawStill(drawList, center, CandySize * scale, pipIndex);
                continue;
            }

            if (pipIndex < unlocked)
            {
                drawList.AddCircle(center, RingRadius * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(Spooks.Pumpkin, breath)), 24, RingStroke * scale);
                continue;
            }

            drawList.AddCircleFilled(center, LockedRadius * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, RestAlpha)), 12);
        }
    }

    private string Count(int found) => count.IsCurrent(found)
        ? count.Value
        : count.Store(found, Loc.T(L.Seasonal.TreatsCount, found, Treats.Total));

    private string Status(int found)
    {
        var waiting = Treats.ActiveSpot();
        var key = found >= Treats.Total ? StatusAllFound : waiting != Treats.NoSpot ? StatusHiding + waiting
            : found < Treats.Unlocked ? StatusLater : StatusTomorrow;
        if (status.IsCurrent(key))
        {
            return status.Value;
        }

        var text = key switch
        {
            StatusAllFound => Loc.T(L.Seasonal.TreatsAllFound),
            StatusLater => Loc.T(L.Seasonal.TreatLater),
            StatusTomorrow => Loc.T(L.Seasonal.TreatTomorrow),
            _ => HidingText((TreatSpot)waiting),
        };
        return status.Store(key, text);
    }

    private static string HidingText(TreatSpot spot) => spot switch
    {
        TreatSpot.HomeTop or TreatSpot.HomeLow => Loc.T(L.Seasonal.TreatHidingHome),
        TreatSpot.EmptyState => Loc.T(L.Seasonal.TreatHidingEmpty),
        TreatSpot.ChirperFeed or TreatSpot.ChirperDeep => Loc.T(L.Seasonal.TreatHidingIn, Loc.T(L.Apps.Chirper)),
        TreatSpot.AethergramFeed or TreatSpot.AethergramDeep =>
            Loc.T(L.Seasonal.TreatHidingIn, Loc.T(L.Apps.Aethergram)),
        TreatSpot.VelvetFeed or TreatSpot.VelvetDeep => Loc.T(L.Seasonal.TreatHidingIn, Loc.T(L.Apps.Velvet)),
        TreatSpot.Skywatcher => Loc.T(L.Seasonal.TreatHidingIn, Loc.T(L.Apps.Skywatcher)),
        TreatSpot.Calendar => Loc.T(L.Seasonal.TreatHidingIn, Loc.T(L.Apps.Calendar)),
        TreatSpot.Clock => Loc.T(L.Seasonal.TreatHidingIn, Loc.T(L.Apps.Clock)),
        _ => Loc.T(L.Seasonal.TreatHidingIn, Loc.T(L.Apps.Message)),
    };
}
