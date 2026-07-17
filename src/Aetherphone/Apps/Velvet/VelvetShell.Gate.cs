using System.Numerics;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private bool gateBusy;

    private void GateMenus()
    {
        postMenu.Gate();
        threadMenu.Gate();
        filterSheet.Gate();
        threadView.GateMenus();
    }

    private void DrawGate(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(area, theme, scale);
        ui.Backdrop(screen);
        var drawList = ImGui.GetWindowDrawList();
        VelvetArt.Bloom(drawList, screen, theme.ScreenRounding * scale, 1.15f);

        var moonCenter = new Vector2(area.Center.X, area.Min.Y + area.Height * 0.24f);
        VelvetArt.Moon(drawList, moonCenter, 24f * scale, VelvetTheme.Moonlight, VelvetTheme.GroundTop);
        Typography.DrawCentered(new Vector2(area.Center.X, moonCenter.Y + 64f * scale), "Velvet", VelvetTheme.TitleInk,
            TextStyles.LargeTitle);

        var textWidth = MathF.Min(area.Width - 60f * scale, 320f * scale);
        var bodyHeight = Typography.DrawWrappedCentered(new Vector2(area.Center.X, moonCenter.Y + 106f * scale),
            Loc.T(L.Velvet.GateTagline), VelvetTheme.BodyInk, TextStyles.Body, textWidth);
        Typography.DrawWrappedCentered(new Vector2(area.Center.X, moonCenter.Y + 106f * scale + bodyHeight + 14f * scale),
            Loc.T(L.Velvet.GateConsent), VelvetTheme.MutedInk, TextStyles.Subheadline, textWidth);

        var buttonWidth = MathF.Min(area.Width - 48f * scale, 300f * scale);
        var buttonHeight = 46f * scale;
        var enterMin = new Vector2(area.Center.X - buttonWidth * 0.5f, area.Max.Y - buttonHeight * 2f - 30f * scale);
        var enterRect = new Rect(enterMin, new Vector2(enterMin.X + buttonWidth, enterMin.Y + buttonHeight));
        if (ui.PillButton(enterRect, gateBusy ? Loc.T(L.Velvet.GateWorking) : Loc.T(L.Velvet.GateEnterAction), true) &&
            !gateBusy)
        {
            AcceptGate();
        }

        var leaveRect = new Rect(new Vector2(enterMin.X, enterRect.Max.Y + 10f * scale),
            new Vector2(enterMin.X + buttonWidth, enterRect.Max.Y + 10f * scale + buttonHeight));
        if (ui.GhostButton(leaveRect, Loc.T(L.Velvet.GateLeave)))
        {
            navigation.GoHome();
        }
    }

    private void AcceptGate()
    {
        gateBusy = true;
        configuration.VelvetAcknowledgedGate = true;
        configuration.VelvetAcknowledgedGateVersion = Configuration.VelvetGateVersion;
        configuration.Save();
        store.AcceptGate(Configuration.VelvetGateVersion, _ => gateBusy = false);
        store.EnsureMe();
        stories.RefreshTray();
    }

    private void DrawCategoryPicker(VelvetTagCategory[] categories, List<string> target)
    {
        var scale = ImGuiHelpers.GlobalScale;
        for (var index = 0; index < categories.Length; index++)
        {
            DrawTagCategory(categories[index], target);
            if (index < categories.Length - 1)
            {
                ImGui.Dummy(new Vector2(0f, 18f * scale));
            }
        }
    }

    private void DrawTagCategory(in VelvetTagCategory category, List<string> selected)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;

        var headerOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(headerOrigin, Loc.Culture.TextInfo.ToUpper(Loc.T(category.Title)),
            VelvetTheme.Lerp(category.Hue, VelvetTheme.OnAccent, 0.30f), TextStyles.SubheadlineEmphasized);
        ImGui.SetCursorScreenPos(headerOrigin);
        ImGui.Dummy(new Vector2(width, 26f * scale));

        var origin = ImGui.GetCursorScreenPos();
        var height = 40f * scale;
        var rowGap = 8f * scale;
        var chipGap = 8f * scale;
        var x = origin.X;
        var y = origin.Y;
        var toggled = -1;
        for (var index = 0; index < category.Tags.Length; index++)
        {
            var tag = category.Tags[index];
            var chipWidth = Typography.Measure(tag, TextStyles.Callout).X + 32f * scale;
            if (x + chipWidth > origin.X + width && x > origin.X)
            {
                x = origin.X;
                y += height + rowGap;
            }

            if (DrawTagPill(new Vector2(x, y), new Vector2(chipWidth, height), tag, category.Hue,
                    selected.Contains(tag), scale))
            {
                toggled = index;
            }

            x += chipWidth + chipGap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y + height - origin.Y));

        if (toggled >= 0)
        {
            var tag = category.Tags[toggled];
            if (!selected.Remove(tag) && selected.Count < 12)
            {
                selected.Add(tag);
            }
        }
    }

    private static bool DrawTagPill(Vector2 min, Vector2 size, string label, Vector4 hue, bool selected, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var max = min + size;
        var radius = size.Y * 0.5f;
        var hovered = UiInteract.Hover(min, max);

        Vector4 fill;
        Vector4 ink;
        if (selected)
        {
            fill = hovered ? VelvetTheme.Lerp(hue, VelvetTheme.OnAccent, 0.14f) : hue;
            ink = VelvetTheme.OnAccent;
        }
        else
        {
            fill = VelvetTheme.Alpha(hue, hovered ? 0.24f : 0.14f);
            ink = VelvetTheme.Lerp(hue, VelvetTheme.OnAccent, 0.60f);
        }

        Squircle.Fill(drawList, min, max, radius, fill.Packed());
        var textSize = Typography.Measure(label, TextStyles.Callout);
        Typography.Draw(new Vector2((min.X + max.X) * 0.5f - textSize.X * 0.5f, (min.Y + max.Y) * 0.5f - textSize.Y * 0.5f),
            label, ink, TextStyles.Callout);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawTokenEditor(List<string> items, string[] suggestions, Vector4 tone)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        if (items.Count > 0)
        {
            var models = new VChipModel[items.Count];
            for (var index = 0; index < items.Count; index++)
            {
                models[index] = new VChipModel(items[index], VChipStyle.Tint, tone, null, true);
            }

            var removed = VChipFlow.Draw(models, width, scale);
            if (removed >= 0)
            {
                items.RemoveAt(removed);
            }

            ImGui.Dummy(new Vector2(0f, 6f * scale));
        }

        var pool = new List<VChipModel>(suggestions.Length);
        var map = new List<int>(suggestions.Length);
        for (var index = 0; index < suggestions.Length; index++)
        {
            if (items.Contains(suggestions[index]))
            {
                continue;
            }

            pool.Add(new VChipModel(suggestions[index], VChipStyle.Ghost, VelvetTheme.Moonlight));
            map.Add(index);
        }

        if (pool.Count == 0)
        {
            return;
        }

        var added = VChipFlow.Draw(pool.ToArray(), width, scale);
        if (added >= 0 && items.Count < 12)
        {
            items.Add(suggestions[map[added]]);
        }
    }
}
