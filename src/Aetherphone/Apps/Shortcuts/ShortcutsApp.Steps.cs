using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private enum BlockMode : byte
    {
        Editable,
        ReadOnly,
        Floating,
    }

    private const float BlockHeight = 82f;
    private const float BlockGap = 10f;
    private const float BlockPad = 12f;
    private const float BlockTextGap = 10f;
    private const float TitleRowHeight = 22f;
    private const float ValueTop = 38f;
    private const float ValueHeight = 32f;
    private const float GripWidth = 40f;
    private const float RemoveRadius = 11f;
    private const float RemoveGlyph = 10f;
    private const float GripGlyph = 14f;
    private const float StepperButton = 13f;
    private const float StepperWidth = 172f;
    private const float StepperGlyph = 11f;
    private const float TokenIcon = 20f;
    private const float TokenGlyph = 10f;
    private const float ConnectorWidth = 2f;
    private const float ConnectorAlpha = 0.32f;
    private const float FloatingLift = 0.05f;
    private const float FloatingShadowAlpha = 0.22f;
    private const float FloatingShadowDrop = 6f;
    private const float AutoScrollZone = 72f;
    private const float AutoScrollSpeed = 720f;
    private const float MaxFrameSeconds = 0.1f;
    private const float EmptyStepsHeight = 96f;

    private readonly Spring[] blockSprings = new Spring[ShortcutStore.MaxSteps];
    private readonly CachedText[] waitLabels = new CachedText[ShortcutStore.MaxSteps];
    private int dragIndex = -1;
    private int dragTarget;
    private float dragPointer;
    private float dragGrab;
    private int pendingStepRemoval = -1;
    private int focusStepIndex = -1;

    private float DrawEditableSteps(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var steps = draft!.Steps;
        var count = steps.Count;
        if (count == 0)
        {
            dragIndex = -1;
            return DrawEmptySteps(drawList, origin, width, scale);
        }

        var pitch = (BlockHeight + BlockGap) * scale;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        if (dragIndex >= count)
        {
            dragIndex = -1;
        }

        if (dragIndex >= 0)
        {
            UpdateStepDrag(steps, origin, pitch, count, scale, delta);
        }

        pendingStepRemoval = -1;
        var clipTop = drawList.GetClipRectMin().Y;
        var clipBottom = drawList.GetClipRectMax().Y;
        for (var index = 0; index < count; index++)
        {
            var displacement = 0f;
            if (dragIndex >= 0 && index != dragIndex)
            {
                if (index > dragIndex && index <= dragTarget)
                {
                    displacement = -pitch;
                }
                else if (index < dragIndex && index >= dragTarget)
                {
                    displacement = pitch;
                }
            }

            var offset = blockSprings[index].Step(displacement, Motion.PageSettle, delta);
            if (index == dragIndex)
            {
                continue;
            }

            var top = origin.Y + index * pitch + offset;
            if (index < count - 1 && dragIndex < 0)
            {
                DrawConnector(drawList, origin.X, top + BlockHeight * scale, BlockGap * scale, steps[index].Kind,
                    scale);
            }

            if (top + pitch < clipTop || top > clipBottom)
            {
                continue;
            }

            var rect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + BlockHeight * scale));
            if (DrawStepBlock(drawList, rect, steps[index], index, BlockMode.Editable, scale))
            {
                pendingStepRemoval = index;
            }
        }

        if (dragIndex >= 0)
        {
            var top = origin.Y + dragPointer - dragGrab;
            var rect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + BlockHeight * scale));
            DrawStepBlock(drawList, rect, steps[dragIndex], dragIndex, BlockMode.Floating, scale);
        }

        if (pendingStepRemoval >= 0 && dragIndex < 0)
        {
            steps.RemoveAt(pendingStepRemoval);
            ResetBlockSprings();
        }

        return origin.Y + count * pitch - BlockGap * scale;
    }

    private float DrawReadOnlySteps(ImDrawListPtr drawList, Vector2 origin, float width, List<ShortcutStep> steps,
        float scale)
    {
        var pitch = (BlockHeight + BlockGap) * scale;
        for (var index = 0; index < steps.Count; index++)
        {
            var top = origin.Y + index * pitch;
            if (index < steps.Count - 1)
            {
                DrawConnector(drawList, origin.X, top + BlockHeight * scale, BlockGap * scale, steps[index].Kind,
                    scale);
            }

            var rect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + BlockHeight * scale));
            if (ImGui.IsRectVisible(rect.Min, rect.Max))
            {
                DrawStepBlock(drawList, rect, steps[index], index, BlockMode.ReadOnly, scale);
            }
        }

        return origin.Y + Math.Max(0, steps.Count * pitch - BlockGap * scale);
    }

    private float DrawEmptySteps(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + EmptyStepsHeight * scale));
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Grouped * scale);
        var hovered = ShortcutsArt.RowInteraction(drawList, ui, rect, scale);
        var pad = Metrics.Space.Lg * scale;
        var badge = ShortcutsArt.BadgeSize * scale;
        var badgeCenter = new Vector2(rect.Min.X + pad + badge * 0.5f, rect.Center.Y);
        var half = new Vector2(badge * 0.5f);
        IconTile.FillShaded(drawList, badgeCenter - half, badgeCenter + half, badge * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, badgeCenter, FontAwesomeIcon.Plus, AccentRing.Ink, badge * 0.45f);
        var textLeft = badgeCenter.X + badge * 0.5f + ShortcutsArt.TextGap * scale;
        var textWidth = MathF.Max(1f, rect.Max.X - pad - textLeft);
        var title = Typography.FitText(Loc.T(L.Shortcuts.StepsEmptyTitle), textWidth, TextStyles.Headline);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var body = WidgetText.Clamp(Loc.T(L.Shortcuts.StepsEmptyBody), TextStyles.Footnote, textWidth, 2);
        var bodyLine = Typography.LineHeight(TextStyles.Footnote);
        var top = rect.Center.Y - (titleHeight + body.Length * bodyLine) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), title, ui.TitleInk, TextStyles.Headline);
        WidgetText.Lines(drawList, body, new Vector2(textLeft, top + titleHeight), ui.MutedInk, TextStyles.Footnote,
            bodyLine);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            OpenActionSheet();
        }

        return rect.Max.Y;
    }

    private void DrawConnector(ImDrawListPtr drawList, float left, float top, float height, ShortcutStepKind kind,
        float scale)
    {
        var x = left + (BlockPad + ShortcutsArt.BadgeSize * 0.5f) * scale;
        drawList.AddLine(new Vector2(x, top), new Vector2(x, top + height),
            ImGui.GetColorU32(Palette.WithAlpha(ShortcutsArt.KindTint(kind), ConnectorAlpha)), ConnectorWidth * scale);
    }

    private bool DrawStepBlock(ImDrawListPtr drawList, Rect rect, ShortcutStep step, int index, BlockMode mode,
        float scale)
    {
        var radius = Metrics.Radius.Lg * scale;
        if (mode == BlockMode.Floating)
        {
            var drop = new Vector2(0f, FloatingShadowDrop * scale);
            Squircle.Fill(drawList, rect.Min + drop, rect.Max + drop, radius,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, FloatingShadowAlpha)));
            ui.Card(drawList, rect.Min, rect.Max, radius);
            Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, FloatingLift)));
        }
        else
        {
            ui.Card(drawList, rect.Min, rect.Max, radius);
        }

        var pad = BlockPad * scale;
        var badge = ShortcutsArt.BadgeSize * scale;
        ShortcutsArt.KindBadge(drawList, new Vector2(rect.Min.X + pad + badge * 0.5f, rect.Center.Y), badge,
            step.Kind);
        var left = rect.Min.X + pad + badge + BlockTextGap * scale;
        var editable = mode == BlockMode.Editable;
        var right = rect.Max.X - (mode == BlockMode.ReadOnly ? pad : GripWidth * scale);
        var titleCenterY = rect.Min.Y + pad + TitleRowHeight * scale * 0.5f - 2f * scale;
        var titleWidth = MathF.Max(1f, right - left - (editable ? RemoveRadius * 2f * scale : 0f));
        var title = Typography.FitText(KindTitle(step.Kind), titleWidth, TextStyles.SubheadlineEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, titleCenterY - titleHeight * 0.5f), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);

        var value = new Rect(new Vector2(left, rect.Min.Y + ValueTop * scale),
            new Vector2(right, rect.Min.Y + (ValueTop + ValueHeight) * scale));
        DrawStepValue(drawList, value, step, index, editable, scale);
        if (mode == BlockMode.ReadOnly)
        {
            return false;
        }

        var gripCenter = new Vector2(rect.Max.X - GripWidth * scale * 0.5f, rect.Center.Y);
        ProgressRing.CenterIcon(drawList, gripCenter, FontAwesomeIcon.GripLines, ui.MutedInk, GripGlyph * scale);
        if (!editable)
        {
            return false;
        }

        DrawGrip(index, new Vector2(rect.Max.X - GripWidth * scale, value.Min.Y - 4f * scale), rect.Max, rect.Min.Y);
        var removeCenter = new Vector2(right - RemoveRadius * scale, titleCenterY);
        return DrawRemove(drawList, removeCenter, index, scale);
    }

    private bool DrawRemove(ImDrawListPtr drawList, Vector2 center, int index, float scale)
    {
        var radius = RemoveRadius * scale;
        var hit = new Vector2(radius + 4f * scale);
        var hovered = dragIndex < 0 && UiInteract.Hover(center - hit, center + hit);
        var fill = hovered ? Palette.WithAlpha(theme.Danger, 0.9f) : Palette.WithAlpha(ui.MutedInk, 0.22f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 24);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Times,
            hovered ? new Vector4(1f, 1f, 1f, 1f) : ui.MutedInk, RemoveGlyph * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), Loc.T(L.Shortcuts.RemoveStep), HoverLabelSide.Above);
        return UiInteract.Click(center - hit, center + hit, hovered);
    }

    private void DrawStepValue(ImDrawListPtr drawList, Rect rect, ShortcutStep step, int index, bool editable,
        float scale)
    {
        switch (step.Kind)
        {
            case ShortcutStepKind.Wait:
                DrawWaitStepper(drawList, rect, step, index, editable, scale);
                return;
            case ShortcutStepKind.OpenPlugin:
                DrawPluginToken(drawList, rect, step, index, editable, scale);
                return;
            default:
                DrawTextValue(drawList, rect, step, index, editable, scale);
                return;
        }
    }

    private void DrawTextValue(ImDrawListPtr drawList, Rect rect, ShortcutStep step, int index, bool editable,
        float scale)
    {
        var radius = Metrics.Radius.Field * scale;
        var malformed = ShortcutRules.IsBadLink(step);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        if (malformed)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.75f)), Metrics.Stroke.Thin * scale);
        }

        var inset = Metrics.Space.Sm * scale;
        var ink = malformed ? theme.Danger : ui.TitleInk;
        var isLink = step.Kind == ShortcutStepKind.OpenUrl;
        if (!editable)
        {
            var shown = step.Text.Length > 0 ? step.Text : isLink ? Loc.T(L.Shortcuts.UrlHint) : Loc.T(L.Shortcuts.CommandHint);
            var fitted = Typography.FitText(shown, rect.Width - inset * 2f, TextStyles.Body);
            var height = Typography.LineHeight(TextStyles.Body);
            Typography.Draw(drawList, new Vector2(rect.Min.X + inset, rect.Center.Y - height * 0.5f), fitted,
                step.Text.Length > 0 ? ink : ui.MutedInk, TextStyles.Body);
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + inset, rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Width - inset * 2f);
        var text = step.Text;
        Plugin.Fonts.NoticeText(text);
        var hint = isLink ? Loc.T(L.Shortcuts.UrlHint) : Loc.T(L.Shortcuts.CommandHint);
        if (index == focusStepIndex && !actionSheet.CapturesPointer)
        {
            focusStepIndex = -1;
            ImGui.SetKeyboardFocusHere();
        }

        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ink))
        {
            if (ImGui.InputTextWithHint($"##shortcutStep{index}", hint, ref text, ShortcutStore.CommandMaxLength,
                    ImGuiInputTextFlags.None))
            {
                step.Text = text;
            }
        }
    }

    private void DrawWaitStepper(ImDrawListPtr drawList, Rect rect, ShortcutStep step, int index, bool editable,
        float scale)
    {
        var width = MathF.Min(rect.Width, StepperWidth * scale);
        var capsule = new Rect(rect.Min, new Vector2(rect.Min.X + width, rect.Max.Y));
        Squircle.Fill(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        var label = WaitLabel(index, step.Seconds);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, capsule.Center - labelSize * 0.5f, label, ui.TitleInk, TextStyles.BodyEmphasized);
        var buttonRadius = StepperButton * scale;
        var minusCenter = new Vector2(capsule.Min.X + capsule.Height * 0.5f, capsule.Center.Y);
        var plusCenter = new Vector2(capsule.Max.X - capsule.Height * 0.5f, capsule.Center.Y);
        var canLower = editable && step.Seconds > ShortcutRunner.MinWaitSeconds + 0.001f;
        var canRaise = editable && step.Seconds < ShortcutRunner.MaxWaitSeconds - 0.001f;
        if (StepperButtonDraw(drawList, index * 2, minusCenter, buttonRadius, FontAwesomeIcon.Minus, canLower,
                editable, scale))
        {
            step.Seconds = ShortcutRules.NudgeWait(step.Seconds, -1);
        }

        if (StepperButtonDraw(drawList, index * 2 + 1, plusCenter, buttonRadius, FontAwesomeIcon.Plus, canRaise,
                editable, scale))
        {
            step.Seconds = ShortcutRules.NudgeWait(step.Seconds, 1);
        }
    }

    private bool StepperButtonDraw(ImDrawListPtr drawList, int slot, Vector2 center, float radius,
        FontAwesomeIcon glyph, bool enabled, bool visible, float scale)
    {
        if (!visible)
        {
            return false;
        }

        var key = unchecked(ImGui.GetID("shortcuts.wait.stepper") + (uint)slot);
        return RoundButton.FontIcon(drawList, key, center, radius, glyph, StepperGlyph * scale,
            ui.Ink.WithAccent(ShortcutsArt.WaitTint), ButtonStyle.Tinted, enabled: enabled && dragIndex < 0);
    }

    private void DrawPluginToken(ImDrawListPtr drawList, Rect rect, ShortcutStep step, int index, bool editable,
        float scale)
    {
        var loaded = catalog.IsLoaded(step.Text);
        var name = catalog.DisplayName(step.Text);
        var iconSize = TokenIcon * scale;
        var inset = Metrics.Space.Sm * scale;
        var nameWidth = Typography.Measure(name, TextStyles.BodyEmphasized).X;
        var chevron = editable ? TokenGlyph * scale + inset : 0f;
        var natural = inset + iconSize + inset + nameWidth + inset + chevron;
        var width = MathF.Min(rect.Width, natural);
        var token = new Rect(rect.Min, new Vector2(rect.Min.X + width, rect.Max.Y));
        var hovered = editable && dragIndex < 0 && UiInteract.Hover(token.Min, token.Max);
        var fill = Surfaces.Fill(ui.TitleInk, hovered ? FillLevel.Secondary : FillLevel.Tertiary);
        Squircle.Fill(drawList, token.Min, token.Max, token.Height * 0.5f, ImGui.GetColorU32(fill));
        var iconMin = new Vector2(token.Min.X + inset, token.Center.Y - iconSize * 0.5f);
        var icon = catalog.Icon(step.Text);
        if (icon is not null)
        {
            Squircle.FillImage(drawList, iconMin, iconMin + new Vector2(iconSize), iconSize * Metrics.Radius.TileFactor,
                icon.Handle, loaded ? 0xFFFFFFFFu : 0x80FFFFFFu);
        }
        else
        {
            ShortcutsArt.KindBadge(drawList, iconMin + new Vector2(iconSize * 0.5f), iconSize, ShortcutStepKind.OpenPlugin);
        }

        var textLeft = iconMin.X + iconSize + inset;
        var textWidth = MathF.Max(1f, token.Max.X - inset - chevron - textLeft);
        var fitted = Typography.FitText(name, textWidth, TextStyles.BodyEmphasized);
        var height = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, token.Center.Y - height * 0.5f), fitted,
            loaded ? ui.TitleInk : theme.Danger, TextStyles.BodyEmphasized);
        if (!editable)
        {
            return;
        }

        ProgressRing.CenterIcon(drawList, new Vector2(token.Max.X - inset - TokenGlyph * scale * 0.5f, token.Center.Y),
            FontAwesomeIcon.ChevronDown, ui.MutedInk, TokenGlyph * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(token.Min, token.Max, hovered))
        {
            OpenPluginPicker(PickerPurpose.ReplaceStep, index);
        }
    }

    private string WaitLabel(int index, float seconds)
    {
        var key = (long)MathF.Round(seconds * 10f);
        ref var cache = ref waitLabels[Math.Clamp(index, 0, waitLabels.Length - 1)];
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, Loc.T(L.Shortcuts.WaitSeconds, Seconds(seconds)));
    }

    private static string KindTitle(ShortcutStepKind kind) => kind switch
    {
        ShortcutStepKind.Wait => Loc.T(L.Shortcuts.AddWait),
        ShortcutStepKind.OpenPlugin => Loc.T(L.Shortcuts.ActionPlugin),
        ShortcutStepKind.OpenUrl => Loc.T(L.Shortcuts.ActionLink),
        _ => Loc.T(L.Shortcuts.AddCommand),
    };

    private void DrawGrip(int index, Vector2 gripMin, Vector2 gripMax, float blockTop)
    {
        var saved = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(gripMin);
        ImGui.InvisibleButton($"##shortcutGrip{index}", gripMax - gripMin);
        var hovered = ImGui.IsItemHovered() && UiInteract.Hover(gripMin, gripMax);
        var activated = hovered && ImGui.IsItemActivated();
        ImGui.SetCursorScreenPos(saved);
        if (hovered || dragIndex == index)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
        }

        HoverTooltip.Show(new Rect(gripMin, gripMax), Loc.T(L.Shortcuts.Reorder), HoverLabelSide.Above);
        if (!activated || dragIndex >= 0)
        {
            return;
        }

        var mouseY = ImGui.GetMousePos().Y;
        var pitch = (BlockHeight + BlockGap) * UiScale.Current;
        var listTop = blockTop - index * pitch - blockSprings[index].Value;
        dragIndex = index;
        dragTarget = index;
        dragPointer = mouseY - listTop;
        dragGrab = mouseY - blockTop;
        UiInteract.CancelPendingTap();
        UiFeedback.Play(UiSound.Tap);
    }

    private void UpdateStepDrag(List<ShortcutStep> steps, Vector2 origin, float pitch, int count, float scale,
        float delta)
    {
        var mouseY = ImGui.GetMousePos().Y;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var target = Math.Clamp(dragTarget, 0, count - 1);
            var floatingTop = dragPointer - dragGrab;
            var from = dragIndex;
            dragIndex = -1;
            ResetBlockSprings();
            blockSprings[target].SnapTo(floatingTop - target * pitch);
            if (ShortcutRules.Move(steps, from, target))
            {
                UiFeedback.Play(UiSound.Tap);
            }

            return;
        }

        UiInteract.CancelPendingTap();
        AutoScroll(mouseY, scale, delta);
        dragPointer = mouseY - origin.Y;
        dragTarget = Math.Clamp((int)MathF.Round((dragPointer - dragGrab) / pitch), 0, count - 1);
    }

    private static void AutoScroll(float mouseY, float scale, float delta)
    {
        var windowTop = ImGui.GetWindowPos().Y;
        var windowBottom = windowTop + ImGui.GetWindowHeight() - TabBar.ContentInset(scale);
        var zone = AutoScrollZone * scale;
        var speed = 0f;
        if (mouseY < windowTop + zone)
        {
            speed = -(windowTop + zone - mouseY) / zone;
        }
        else if (mouseY > windowBottom - zone)
        {
            speed = (mouseY - windowBottom + zone) / zone;
        }

        if (speed == 0f)
        {
            return;
        }

        var step = Math.Clamp(speed, -1f, 1f) * AutoScrollSpeed * scale * delta;
        ImGui.SetScrollY(Math.Clamp(ImGui.GetScrollY() + step, 0f, ImGui.GetScrollMaxY()));
    }

    private void ResetBlockSprings()
    {
        for (var index = 0; index < blockSprings.Length; index++)
        {
            blockSprings[index].SnapTo(0f);
        }
    }
}
