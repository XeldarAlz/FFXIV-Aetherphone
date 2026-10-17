using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Notifications.Widgets;

internal sealed class DailySummaryWidget : IHomeWidget
{
    private const int CardCount = 3;
    private const float CardRadiusUnits = 14f;
    private const float CardPadUnits = 9f;
    private const float CardIconUnits = 22f;
    private const float Relevant = 0.55f;

    private static readonly Vector4[] OneCard = { new(0f, 0f, 1f, 1f) };
    private static readonly Vector4[] TwoCards = { new(0f, 0f, 0.58f, 1f), new(0.46f, 0.18f, 1f, 1f) };
    private static readonly Vector4[] ThreeCards =
        { new(0.70f, 0f, 1f, 0.48f), new(0f, 0f, 0.56f, 1f), new(0.40f, 0.24f, 0.90f, 1f) };
    private static readonly int[] ThreeCardItems = { 2, 0, 1 };
    private static readonly string[] SampleApps = { "chirper", "dailies", "coin" };

    private readonly DailySummary summary;
    private readonly DailySummaryItem[] samples = new DailySummaryItem[SampleApps.Length];
    private CultureInfo? samplesCulture;

    public DailySummaryWidget(WidgetServices services)
    {
        summary = new DailySummary(services);
    }

    public string Id => "notifications.summary";
    public string DisplayName => Loc.T(L.WidgetsPeople.DailySummary);
    public string Description => Loc.T(L.WidgetsPeople.DailySummaryDescription);
    public string AppId => NotificationChannels.NotificationsAppId;
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium;

    public WidgetRoute Target(in WidgetContext context) =>
        WidgetRoute.To(AppId, WidgetRouteKind.DailySummary, string.Empty);

    public float Relevance(string config)
    {
        summary.Refresh();
        return summary.Count > 0 ? Relevant : 0f;
    }

    public void Draw(in WidgetContext context)
    {
        summary.Refresh();
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        var accent = AppAccents.For(AppId);
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.WidgetsPeople.DailySummary, accent);
        var items = summary.Count == 0 && context.Preview ? Samples() : summary.Items;
        var body = new Rect(new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * context.Scale),
            content.Max);
        if (items.Length == 0)
        {
            WidgetChrome.Message(context, ink, body, FontAwesomeIcon.CheckCircle, accent,
                Loc.T(L.WidgetsPeople.CaughtUp), string.Empty);
            return;
        }

        PeopleWidgetChrome.Badge(context, ink, content.Max.X, content.Min.Y + (headerBottom - content.Min.Y) * 0.5f,
            PeopleWidgetChrome.CountLabel(items.Length), false);
        var slots = items.Length switch
        {
            1 => OneCard,
            2 => TwoCards,
            _ => ThreeCards,
        };

        for (var index = 0; index < slots.Length; index++)
        {
            var itemIndex = slots.Length == CardCount ? ThreeCardItems[index] : index;
            var slot = slots[index];
            var card = new Rect(
                new Vector2(body.Min.X + body.Width * slot.X, body.Min.Y + body.Height * slot.Y),
                new Vector2(body.Min.X + body.Width * slot.Z, body.Min.Y + body.Height * slot.W));
            DrawCard(context, ink, card, items[itemIndex]);
        }
    }

    private static void DrawCard(in WidgetContext context, in WidgetInk ink, Rect card, in DailySummaryItem item)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var radius = CardRadiusUnits * scale;
        var surface = IconTile.Surface(AppAccents.For(item.AppId));
        Elevation.Floating(drawList, card.Min, card.Max, radius, scale, ink.Opacity);
        IconTile.FillShaded(drawList, card.Min, card.Max, radius, surface, ink.Opacity);
        var pad = CardPadUnits * scale;
        var iconSize = MathF.Min(CardIconUnits * scale, card.Height * 0.4f);
        IconTile.DrawApp(drawList, item.AppId, card.Min + new Vector2(pad + iconSize * 0.5f, pad + iconSize * 0.5f),
            iconSize, surface, ink.Opacity);
        var width = MathF.Max(1f, card.Width - pad * 2f);
        var white = new Vector4(1f, 1f, 1f, ink.Opacity);
        var nameHeight = WidgetText.LineHeight(WidgetType.Caption);
        var textHeight = WidgetText.LineHeight(WidgetType.Headline);
        var room = card.Height - pad * 3f - iconSize - nameHeight;
        var lines = WidgetText.Clamp(item.Text, WidgetType.Headline, width, Math.Clamp((int)(room / textHeight), 0, 2));
        if (lines.Length == 0)
        {
            var nameLeft = card.Min.X + pad * 1.6f + iconSize;
            WidgetText.Draw(drawList, new Vector2(nameLeft, card.Min.Y + pad + (iconSize - nameHeight) * 0.5f),
                item.Name, white, WidgetType.Caption, MathF.Max(1f, card.Max.X - pad - nameLeft));
            return;
        }

        var bottom = card.Max.Y - pad;
        var textTop = bottom - lines.Length * textHeight;
        WidgetText.Lines(drawList, lines, new Vector2(card.Min.X + pad, textTop), white, WidgetType.Headline,
            textHeight);
        WidgetText.Draw(drawList, new Vector2(card.Min.X + pad, textTop - nameHeight), item.Name,
            white with { W = white.W * 0.78f }, WidgetType.Caption, width);
    }

    private ReadOnlySpan<DailySummaryItem> Samples()
    {
        if (ReferenceEquals(samplesCulture, Loc.Culture))
        {
            return samples;
        }

        samplesCulture = Loc.Culture;
        samples[0] = new DailySummaryItem(SampleApps[0], summary.NameOf(SampleApps[0]), Loc.T(L.Linkpearl.NewCount, 3));
        samples[1] = new DailySummaryItem(SampleApps[1], summary.NameOf(SampleApps[1]), Loc.T(L.WidgetsAdventure.ToDo, 4));
        samples[2] = new DailySummaryItem(SampleApps[2], summary.NameOf(SampleApps[2]), Loc.T(L.WidgetsLife.CheckInReady));
        return samples;
    }

    public void Dispose()
    {
    }
}
