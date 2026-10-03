using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell.Home;

internal sealed class StackEditSheet
{
    private enum MemberAction : byte
    {
        None,
        Up,
        Down,
        Remove,
        Show,
    }

    private const string ToggleId = "home.stack.smartRotate";
    private const float MemberRowUnits = 56f;
    private const float IconUnits = 32f;
    private const float IconRadiusFraction = 0.26f;
    private const float ButtonUnits = 28f;
    private const float ButtonGapUnits = 4f;
    private const float ToggleWidthUnits = 50f;
    private const float ToggleHeightUnits = 30f;
    private const float DisabledAlpha = 0.28f;
    private const float MinimumFraction = 0.5f;
    private const float MaximumFraction = 0.92f;

    private readonly Sheet sheet = new();
    private readonly HomeLayoutService layout;
    private HomeTile? stack;
    private float scrollY;

    public StackEditSheet(HomeLayoutService layout)
    {
        this.layout = layout;
    }

    public bool Active => stack is not null && (sheet.IsOpen || sheet.CapturesPointer);

    public void Open(HomeTile target)
    {
        if (!target.IsStack)
        {
            return;
        }

        stack = target;
        scrollY = 0f;
        sheet.Open();
    }

    public void CloseImmediately()
    {
        sheet.CloseImmediately();
        stack = null;
    }

    public void Draw(Rect screen, PhoneTheme theme)
    {
        if (stack is null)
        {
            return;
        }

        var current = stack;
        if (sheet.IsOpen && layout.Locate(current).Page < 0)
        {
            sheet.Close();
        }

        var scale = UiScale.Current;
        var detents = SheetDetents.Fitted(FittedHeight(screen, current.Stack.Count, scale));
        var frame = sheet.Begin(ImGui.GetWindowDrawList(), screen, theme, detents, SheetMetrics.HomeVeil);
        if (!frame.Visible)
        {
            if (!sheet.IsOpen)
            {
                stack = null;
            }

            return;
        }

        var drawList = frame.DrawList;
        var content = frame.Content;
        var opacity = frame.Opacity;
        var interactive = frame.Interactive && sheet.IsOpen;
        var action = WidgetSheetKit.Header(drawList, content, Loc.T(L.WidgetStacks.EditStack), frame.Ink,
            theme.Accent, opacity, scale, interactive, false);
        if (action == SheetHeaderAction.Done)
        {
            sheet.Close();
        }

        var pad = WidgetSheetKit.SidePadUnits * scale;
        var top = content.Min.Y + WidgetSheetKit.HeaderUnits * scale;
        var rotateCard = new Rect(new Vector2(content.Min.X + pad, top),
            new Vector2(content.Max.X - pad, top + WidgetSheetKit.RowUnits * scale));
        DrawSmartRotate(drawList, rotateCard, current, theme, frame.Ink, opacity, scale, interactive);
        var listTop = rotateCard.Max.Y + WidgetSheetKit.SectionGapUnits * scale;
        var view = new Rect(new Vector2(rotateCard.Min.X, listTop),
            new Vector2(rotateCard.Max.X, content.Max.Y - WidgetSheetKit.BottomInsetUnits * scale));
        if (view.Height > 1f)
        {
            DrawMembers(drawList, view, current, theme, frame.Ink, opacity, scale, interactive);
        }

        sheet.End(in frame);
    }

    private void DrawSmartRotate(ImDrawListPtr drawList, Rect card, HomeTile current, PhoneTheme theme,
        Vector4 ink, float opacity, float scale, bool interactive)
    {
        WidgetSheetKit.Card(drawList, card, ink, opacity, scale);
        var toggleWidth = ToggleWidthUnits * scale;
        var toggleHeight = ToggleHeightUnits * scale;
        var toggleRight = card.Max.X - WidgetSheetKit.RowPadUnits * scale;
        var labelWidth = card.Width - toggleWidth - WidgetSheetKit.RowPadUnits * 3f * scale -
                         Metrics.Size.HintIconHeight * scale * 2f;
        var labelRight = WidgetSheetKit.Label(drawList, card, Loc.T(L.WidgetStacks.SmartRotate),
            WidgetSheetKit.Faded(ink, opacity), labelWidth, scale);
        HintIcon.Draw(new Vector2(labelRight + Metrics.Size.HintIconHeight * scale, card.Center.Y),
            Loc.T(L.WidgetStacks.SmartRotateHint), theme, scale);
        var toggle = new Rect(new Vector2(toggleRight - toggleWidth, card.Center.Y - toggleHeight * 0.5f),
            new Vector2(toggleRight, card.Center.Y + toggleHeight * 0.5f));
        var enabled = Toggle.Draw(ToggleId, toggle, current.SmartRotate, theme, opacity, interactive);
        if (enabled != current.SmartRotate)
        {
            layout.SetSmartRotate(current, enabled);
        }
    }

    private void DrawMembers(ImDrawListPtr drawList, Rect view, HomeTile current, PhoneTheme theme, Vector4 ink,
        float opacity, float scale, bool interactive)
    {
        var members = current.Stack;
        var rowHeight = MemberRowUnits * scale;
        var card = new Rect(view.Min, new Vector2(view.Max.X, view.Min.Y + rowHeight * members.Count));
        WidgetSheetKit.WheelScroll(ref scrollY, view, card.Height, scale, interactive);
        card = card.Translate(new Vector2(0f, -scrollY));
        drawList.PushClipRect(view.Min, view.Max, true);
        WidgetSheetKit.Card(drawList, card, ink, opacity, scale);
        var visible = current.StackIndex;
        var action = MemberAction.None;
        var actionIndex = -1;
        for (var index = 0; index < members.Count; index++)
        {
            var row = new Rect(new Vector2(card.Min.X, card.Min.Y + index * rowHeight),
                new Vector2(card.Max.X, card.Min.Y + (index + 1) * rowHeight));
            if (index > 0)
            {
                WidgetSheetKit.Hairline(drawList, card, row.Min.Y, ink, opacity, scale);
            }

            var live = interactive && row.Max.Y > view.Min.Y && row.Min.Y < view.Max.Y;
            var picked = DrawMember(drawList, row, current, index, index == visible, theme, ink, opacity, scale,
                live);
            if (picked != MemberAction.None)
            {
                action = picked;
                actionIndex = index;
            }
        }

        drawList.PopClipRect();
        Apply(current, action, actionIndex);
    }

    private void Apply(HomeTile current, MemberAction action, int index)
    {
        switch (action)
        {
            case MemberAction.Up:
                layout.MoveStackMember(current, index, index - 1);
                return;
            case MemberAction.Down:
                layout.MoveStackMember(current, index, index + 1);
                return;
            case MemberAction.Remove:
                layout.RemoveStackMember(current, current.Stack[index]);
                return;
            case MemberAction.Show:
                layout.SetStackIndex(current, index);
                return;
        }
    }

    private static MemberAction DrawMember(ImDrawListPtr drawList, Rect row, HomeTile current, int index, bool isVisible,
        PhoneTheme theme, Vector4 ink, float opacity, float scale, bool interactive)
    {
        var member = current.Stack[index];
        var widget = member.Widget!;
        var button = ButtonUnits * scale;
        var gap = ButtonGapUnits * scale;
        var rowPad = WidgetSheetKit.RowPadUnits * scale;
        var removeCenter = new Vector2(row.Max.X - rowPad - button * 0.5f, row.Center.Y);
        var downCenter = removeCenter - new Vector2(button + gap, 0f);
        var upCenter = downCenter - new Vector2(button + gap, 0f);
        var buttonsLeft = upCenter.X - button * 0.5f;
        var overButtons = interactive && UiInteract.Hover(new Vector2(buttonsLeft, row.Min.Y), row.Max);
        var hovered = WidgetSheetKit.HoverWash(drawList, row, ink, opacity, scale, interactive && !overButtons);
        var iconSize = IconUnits * scale;
        var iconMin = new Vector2(row.Min.X + rowPad, row.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        var accent = AppAccents.For(widget.AppId);
        if (!AppIconTile.TryDraw(drawList, widget.AppId, accent, iconMin, iconMax, iconSize * IconRadiusFraction,
                opacity, false))
        {
            Squircle.Fill(drawList, iconMin, iconMax, iconSize * IconRadiusFraction,
                ImGui.GetColorU32(WidgetSheetKit.Faded(accent, opacity)));
        }

        var textRow = new Rect(new Vector2(iconMax.X, row.Min.Y), row.Max);
        var nameInk = isVisible ? theme.Accent : ink;
        WidgetSheetKit.Label(drawList, textRow, widget.DisplayName, WidgetSheetKit.Faded(nameInk, opacity),
            buttonsLeft - iconMax.X - rowPad * 2f, scale);
        var count = current.Stack.Count;
        var action = MemberAction.None;
        if (RoundButton(drawList, upCenter, FontAwesomeIcon.ChevronUp, ink, opacity, scale,
                interactive && index > 0))
        {
            action = MemberAction.Up;
        }

        if (RoundButton(drawList, downCenter, FontAwesomeIcon.ChevronDown, ink, opacity, scale,
                interactive && index < count - 1))
        {
            action = MemberAction.Down;
        }

        if (RoundButton(drawList, removeCenter, FontAwesomeIcon.MinusCircle, theme.Danger, opacity, scale,
                interactive))
        {
            action = MemberAction.Remove;
        }

        if (!overButtons && UiInteract.Click(row.Min, row.Max, hovered))
        {
            action = MemberAction.Show;
        }

        return action;
    }

    private static bool RoundButton(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon icon, Vector4 ink,
        float opacity, float scale, bool enabled)
    {
        var radius = ButtonUnits * 0.5f * scale;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(min, max);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ink with { W = 0.10f * opacity }), 24);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var alpha = enabled ? opacity : opacity * DisabledAlpha;
        WidgetSheetKit.Glyph(drawList, center, icon, WidgetSheetKit.Faded(ink, alpha), scale, 14f);
        return UiInteract.Click(min, max, hovered);
    }

    private static float FittedHeight(Rect screen, int members, float scale)
    {
        var height = (SheetMetrics.GrabberZone + WidgetSheetKit.HeaderUnits + WidgetSheetKit.RowUnits +
                      WidgetSheetKit.SectionGapUnits + WidgetSheetKit.BottomInsetUnits +
                      MemberRowUnits * members) * scale;
        return Math.Clamp(height, screen.Height * MinimumFraction, screen.Height * MaximumFraction);
    }
}
