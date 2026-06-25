using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal struct GroupCard
{
    public const float DefaultRowHeight = 46f;

    private readonly PhoneTheme theme;
    private readonly float scale;
    private readonly float rowHeight;
    private readonly float left;
    private readonly float right;
    private readonly float startY;
    private readonly int rowCount;
    private int rowIndex;

    private GroupCard(PhoneTheme theme, float scale, float rowHeight, float left, float right, float startY, int rowCount)
    {
        this.theme = theme;
        this.scale = scale;
        this.rowHeight = rowHeight;
        this.left = left;
        this.right = right;
        this.startY = startY;
        this.rowCount = rowCount;
        rowIndex = 0;
    }

    public static GroupCard Begin(PhoneTheme theme, int rowCount, float rowHeight = DefaultRowHeight)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var height = rowCount * rowHeight * scale;
        var cardMax = new Vector2(right, origin.Y + height);
        var dl = ImGui.GetWindowDrawList();
        Squircle.Fill(dl, origin, cardMax, 12f * scale, ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(dl, origin, cardMax, 12f * scale, scale);
        return new GroupCard(theme, scale, rowHeight, origin.X, right, origin.Y, rowCount);
    }

    public Rect NextRow()
    {
        var rowTop = startY + rowIndex * rowHeight * scale;
        if (rowIndex > 0)
        {
            var separatorX = left + 16f * scale;
            ImGui.GetWindowDrawList().AddLine(new Vector2(separatorX, rowTop), new Vector2(right, rowTop), ImGui.GetColorU32(theme.Separator), 1f);
        }

        rowIndex++;
        var padding = 16f * scale;
        return new Rect(new Vector2(left + padding, rowTop), new Vector2(right - padding, rowTop + rowHeight * scale));
    }

    public void End()
    {
        ImGui.SetCursorScreenPos(new Vector2(left, startY));
        ImGui.Dummy(new Vector2(right - left, rowCount * rowHeight * scale));
    }
}
