using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed class VenueCasinoPill
{
    public const string CasinoAppId = "casino";

    private const float Height = 30f;
    private const float Inset = 12f;
    private const float PadX = 12f;
    private const float DotRadius = 4f;

    private readonly CasinoVenueStore casino;
    private readonly CasinoLauncher launcher;
    private readonly Dictionary<CasinoTableRowDto, string> labels = new(ReferenceEqualityComparer.Instance);
    private LanguageInfo? language;

    public VenueCasinoPill(CasinoVenueStore casino, CasinoLauncher launcher)
    {
        this.casino = casino;
        this.launcher = launcher;
    }

    public void Refresh()
    {
        casino.EnsureListed();
    }

    public bool Draw(ImDrawListPtr drawList, Rect card, float heroHeight, VenueAddress address, INavigator navigation,
        float scale)
    {
        var table = casino.LiveTableAt(address);
        if (table is null)
        {
            return false;
        }

        var text = LabelFor(table);
        var style = TextStyles.FootnoteEmphasized;
        var height = Height * scale;
        var inset = Inset * scale;
        var maxWidth = card.Width - inset * 2f;
        var fitted = Typography.FitText(text, maxWidth - PadX * 2f * scale - DotRadius * 3f * scale, style);
        var width = Typography.Measure(fitted, style).X + PadX * 2f * scale + DotRadius * 3f * scale;
        var min = new Vector2(card.Min.X + inset, card.Min.Y + heroHeight - inset - height);
        var max = new Vector2(min.X + width, min.Y + height);
        var hovered = UiInteract.Hover(min, max);
        Material.LiquidGlass(drawList, min, max, height * 0.5f, scale, GlassTone.Dark, hovered ? 0.2f : 0f);
        var dot = new Vector2(min.X + PadX * scale, (min.Y + max.Y) * 0.5f);
        drawList.AddCircleFilled(dot, DotRadius * scale, ImGui.GetColorU32(new Vector4(1f, 0.239f, 0.604f, 1f)), 16);
        Typography.Draw(drawList, new Vector2(dot.X + DotRadius * 2f * scale, dot.Y - Typography.LineHeight(style) * 0.5f),
            fitted, new Vector4(1f, 1f, 1f, 1f), style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(min, max, hovered))
        {
            return false;
        }

        launcher.RequestTable(table.TableId);
        navigation.Open(CasinoAppId);
        return true;
    }

    private string LabelFor(CasinoTableRowDto table)
    {
        if (!ReferenceEquals(language, Loc.Current) || labels.Count > 64)
        {
            labels.Clear();
            language = Loc.Current;
        }

        if (labels.TryGetValue(table, out var cached))
        {
            return cached;
        }

        var text = table.MaxSeats > 0
            ? Loc.T(L.Venue.LiveTableSeats, table.SeatedCount.ToString(Loc.Culture),
                table.MaxSeats.ToString(Loc.Culture))
            : Loc.T(L.Venue.LiveRoomHere);
        labels[table] = text;
        return text;
    }
}
