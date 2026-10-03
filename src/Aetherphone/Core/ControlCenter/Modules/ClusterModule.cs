using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed class ClusterModule : IControlModule
{
    private const int Columns = 2;
    private const float CircleFraction = 0.40f;
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Large };

    private readonly ToggleModule[] members;
    private readonly string[] circleIds;
    private readonly string[] detailCircleIds;

    public ClusterModule(string id, ToggleModule[] members)
    {
        Id = id;
        this.members = members;
        circleIds = new string[members.Length];
        detailCircleIds = new string[members.Length];
        for (var index = 0; index < members.Length; index++)
        {
            circleIds[index] = "cc.cluster." + members[index].Id;
            detailCircleIds[index] = "cc.cluster.detail." + members[index].Id;
        }
    }

    public string Id { get; }
    public string GalleryLabel => Loc.T(L.ControlCenter.QuickToggles);
    public FontAwesomeIcon GalleryIcon => FontAwesomeIcon.ThLarge;
    public IReadOnlyList<ControlSpan> Sizes => SpanOptions;
    public ControlSpan DefaultSpan => ControlSpan.Large;

    public void Draw(in ControlModuleContext context)
    {
        var drawList = context.DrawList;
        var rect = context.Rect;
        var scale = context.Scale;
        var theme = context.Theme;
        var opacity = context.Opacity;
        ControlTile.Surface(drawList, rect, theme, opacity);
        if (members.Length == 0)
        {
            return;
        }

        var expanded = context.Expanded;
        var ids = expanded ? detailCircleIds : circleIds;
        var padding = Metrics.Space.Md * scale;
        var rows = (members.Length + Columns - 1) / Columns;
        var labelStyle = TextStyles.FootnoteEmphasized;
        var labelHeight = expanded ? Typography.LineHeight(labelStyle) + Metrics.Space.Xs * scale : 0f;
        var cellWidth = (rect.Width - 2f * padding) / Columns;
        var cellHeight = (rect.Height - 2f * padding) / rows;
        var radius = MathF.Max(1f, MathF.Min(cellWidth, cellHeight - labelHeight) * CircleFraction);
        for (var index = 0; index < members.Length; index++)
        {
            var column = index % Columns;
            var row = index / Columns;
            var cellCenterX = rect.Min.X + padding + cellWidth * (column + 0.5f);
            var cellTop = rect.Min.Y + padding + cellHeight * row;
            var circleCenter = new Vector2(cellCenterX, cellTop + (cellHeight - labelHeight) * 0.5f);
            var member = members[index];
            var active = member.IsActive;
            if (ControlTile.Circle(drawList, ids[index], circleCenter, radius, member.GalleryIcon, active, theme.Accent,
                    theme, opacity, context.Interactive, expanded ? null : member.GalleryLabel, false))
            {
                member.Activate();
            }

            if (!expanded)
            {
                continue;
            }

            var labelCenterY = circleCenter.Y + radius + labelHeight * 0.5f;
            Typography.DrawCentered(drawList, new Vector2(cellCenterX, labelCenterY),
                Typography.FitText(member.GalleryLabel, cellWidth - Metrics.Space.Sm * scale, labelStyle),
                ControlTile.Glyph(active, opacity), labelStyle);
        }
    }
}
