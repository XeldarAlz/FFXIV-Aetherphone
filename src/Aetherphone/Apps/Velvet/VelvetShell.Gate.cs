using System.Numerics;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private bool gateBusy;
    private int onboardStep;
    private int onboardIntent;
    private string onboardIntro = string.Empty;
    private bool onboardDiscoverable = true;
    private readonly List<string> onboardTags = new();
    private readonly List<string> onboardRole = new();

    private void GateMenus()
    {
        postMenu.Gate();
        threadMenu.Gate();
        filterSheet.Gate();
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
            "A private, adults only corner of the suite. Moonlit, unhurried, yours.", VelvetTheme.BodyInk,
            TextStyles.Body, textWidth);
        Typography.DrawWrappedCentered(new Vector2(area.Center.X, moonCenter.Y + 106f * scale + bodyHeight + 14f * scale),
            "By entering you confirm you are 18 or older. Be kind, be discreet.", VelvetTheme.MutedInk,
            TextStyles.Subheadline, textWidth);

        var buttonWidth = MathF.Min(area.Width - 48f * scale, 300f * scale);
        var buttonHeight = 46f * scale;
        var enterMin = new Vector2(area.Center.X - buttonWidth * 0.5f, area.Max.Y - buttonHeight * 2f - 30f * scale);
        var enterRect = new Rect(enterMin, new Vector2(enterMin.X + buttonWidth, enterMin.Y + buttonHeight));
        if (ui.PillButton(enterRect, gateBusy ? "One moment" : "Enter", true) && !gateBusy)
        {
            AcceptGate();
        }

        var leaveRect = new Rect(new Vector2(enterMin.X, enterRect.Max.Y + 10f * scale),
            new Vector2(enterMin.X + buttonWidth, enterRect.Max.Y + 10f * scale + buttonHeight));
        if (ui.GhostButton(leaveRect, "Not now"))
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
    }

    private void DrawOnboarding(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(area, theme, scale);
        ui.Backdrop(screen);
        var drawList = ImGui.GetWindowDrawList();

        var dotY = area.Min.Y + 40f * scale;
        var gap = 15f * scale;
        for (var index = 0; index < 3; index++)
        {
            var center = new Vector2(area.Center.X - gap + index * gap, dotY);
            var color = index == onboardStep ? VelvetTheme.Rose : VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.30f);
            drawList.AddCircleFilled(center, 3.5f * scale, color.Packed(), 16);
        }

        var title = onboardStep switch
        {
            1 => "Say hello",
            2 => "Your privacy",
            _ => "What are you here for?",
        };
        var hint = onboardStep switch
        {
            1 => "A short intro and a few tags help the right people find you.",
            2 => "You choose who can see you.",
            _ => "Pick one or more. You can change this later.",
        };
        Typography.DrawCentered(new Vector2(area.Center.X, area.Min.Y + 70f * scale), title, VelvetTheme.TitleInk,
            TextStyles.Title1);
        Typography.DrawWrappedCentered(new Vector2(area.Center.X, area.Min.Y + 100f * scale), hint, VelvetTheme.MutedInk,
            TextStyles.Body, MathF.Min(area.Width - 52f * scale, 320f * scale));

        var buttonWidth = MathF.Min(area.Width - 48f * scale, 300f * scale);
        var buttonHeight = 46f * scale;
        var bodyTop = area.Min.Y + 150f * scale;
        var bodyRect = new Rect(new Vector2(area.Min.X + 24f * scale, bodyTop),
            new Vector2(area.Max.X - 24f * scale, area.Max.Y - buttonHeight * 2f - 40f * scale));
        if (onboardStep == 0)
        {
            DrawOnboardIntent(bodyRect);
        }
        else
        {
            using (AppSurface.Begin(bodyRect))
            {
                if (onboardStep == 1)
                {
                    DrawOnboardHello();
                }
                else
                {
                    DrawOnboardPrivacy();
                }
            }
        }

        var nextMin = new Vector2(area.Center.X - buttonWidth * 0.5f, area.Max.Y - buttonHeight - 24f * scale);
        var nextRect = new Rect(nextMin, new Vector2(nextMin.X + buttonWidth, nextMin.Y + buttonHeight));
        var isLast = onboardStep >= 2;
        var canNext = onboardStep != 0 || VelvetIntent.Sanitize(onboardIntent) != 0;
        var nextLabel = isLast ? "Enter Velvet" : "Next";
        if (canNext)
        {
            if (ui.PillButton(nextRect, nextLabel, true))
            {
                if (isLast)
                {
                    FinishOnboarding();
                }
                else
                {
                    onboardStep++;
                }
            }
        }
        else
        {
            Squircle.Fill(drawList, nextRect.Min, nextRect.Max, buttonHeight * 0.5f,
                VelvetTheme.Alpha(VelvetTheme.Rose, 0.35f).Packed());
            Typography.DrawCentered(nextRect.Center, nextLabel, VelvetTheme.Alpha(VelvetTheme.OnAccent, 0.6f), 0.9f,
                FontWeight.SemiBold);
        }

        if (onboardStep > 0)
        {
            var backMin = new Vector2(nextMin.X, nextMin.Y - buttonHeight - 8f * scale);
            if (ui.GhostButton(new Rect(backMin, new Vector2(backMin.X + buttonWidth, backMin.Y + buttonHeight)),
                    "Back"))
            {
                onboardStep--;
            }
        }
    }

    private void DrawOnboardIntent(Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var defs = VelvetIntent.All;
        var count = defs.Length;
        var sidePad = 8f * scale;
        var available = body.Height;
        var rowHeight = Math.Clamp((available - 12f * scale * (count - 1)) / count, 52f * scale, 88f * scale);
        var gap = Math.Clamp((available - rowHeight * count) / (count - 1), 12f * scale, 22f * scale);
        var blockHeight = rowHeight * count + gap * (count - 1);
        var startY = body.Min.Y + MathF.Max(0f, (available - blockHeight) * 0.5f);
        var left = body.Min.X + sidePad;
        var right = body.Max.X - sidePad;

        for (var index = 0; index < count; index++)
        {
            var def = defs[index];
            var top = startY + index * (rowHeight + gap);
            var rect = new Rect(new Vector2(left, top), new Vector2(right, top + rowHeight));
            if (DrawIntentSelectRow(rect, def, VelvetIntent.Has(onboardIntent, def.Flag)))
            {
                onboardIntent = VelvetIntent.Toggle(onboardIntent, def.Flag);
            }
        }
    }

    private bool DrawIntentSelectRow(Rect rect, in VelvetIntentDef def, bool selected)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = Metrics.Radius.Card * scale;

        var fill = selected
            ? VelvetTheme.Alpha(def.Hue, hovered ? 0.22f : 0.16f)
            : hovered ? VelvetTheme.CardHi : VelvetTheme.Card;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, fill.Packed());
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            (selected ? VelvetTheme.Alpha(def.Hue, 0.75f) : VelvetTheme.CardStroke).Packed(),
            (selected ? 1.5f : 1f) * scale);

        var tileSize = rect.Height * 0.56f;
        var tileMin = new Vector2(rect.Min.X + 14f * scale, rect.Center.Y - tileSize * 0.5f);
        var tileMax = new Vector2(tileMin.X + tileSize, tileMin.Y + tileSize);
        Squircle.Fill(drawList, tileMin, tileMax, tileSize * 0.32f, def.Hue.Packed());
        AppSkin.Icon(new Vector2((tileMin.X + tileMax.X) * 0.5f, (tileMin.Y + tileMax.Y) * 0.5f),
            def.Icon.ToIconString(), VelvetTheme.OnAccent, 0.92f);

        var textLeft = tileMax.X + 14f * scale;
        var textWidth = rect.Max.X - 46f * scale - textLeft;
        var label = Typography.FitText(def.Label, textWidth, TextStyles.Headline);
        var blurb = Typography.FitText(def.Blurb, textWidth, TextStyles.Subheadline);
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        var blurbSize = Typography.Measure(blurb, TextStyles.Subheadline);
        var textTop = rect.Center.Y - (labelSize.Y + 3f * scale + blurbSize.Y) * 0.5f;
        Typography.Draw(new Vector2(textLeft, textTop), label, VelvetTheme.TitleInk, TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, textTop + labelSize.Y + 3f * scale), blurb, VelvetTheme.MutedInk,
            TextStyles.Subheadline);

        DrawSelectCheck(drawList, new Vector2(rect.Max.X - 26f * scale, rect.Center.Y), selected, def.Hue, scale);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawSelectCheck(ImDrawListPtr drawList, Vector2 center, bool selected, Vector4 hue, float scale)
    {
        var radius = 11f * scale;
        if (!selected)
        {
            drawList.AddCircle(center, radius, VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.30f).Packed(), 24, 1.5f * scale);
            return;
        }

        drawList.AddCircleFilled(center, radius, hue.Packed(), 24);
        var tick = VelvetTheme.OnAccent.Packed();
        var thickness = 2f * scale;
        drawList.AddLine(center + new Vector2(-4.5f * scale, 0.2f * scale), center + new Vector2(-1.4f * scale, 3.6f * scale),
            tick, thickness);
        drawList.AddLine(center + new Vector2(-1.4f * scale, 3.6f * scale), center + new Vector2(5f * scale, -3.8f * scale),
            tick, thickness);
    }

    private void DrawOnboardHello()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ui.Field("Introduce yourself", "##ob_intro", ref onboardIntro, 400, true, 132f);
        ImGui.Dummy(new Vector2(0f, 20f * scale));
        DrawCategoryPicker(VelvetSuggestions.DynamicCategories, onboardRole);
        ImGui.Dummy(new Vector2(0f, 18f * scale));
        DrawCategoryPicker(VelvetSuggestions.TagCategories, onboardTags);
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
        Typography.Draw(headerOrigin, category.Title.ToUpperInvariant(),
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

    private void DrawOnboardPrivacy()
    {
        ui.ToggleRow("Appear in Discover", ref onboardDiscoverable);
        ui.HelpText("When on, your profile can be found by others in Discover. When off, only people you have " +
            "connected with can see you.");
    }

    private void FinishOnboarding()
    {
        configuration.VelvetOnboarded = true;
        configuration.Save();
        var dynamic = VelvetTags.Join(onboardRole.ToArray());
        var request = new UpdateVelvetProfileRequest(onboardIntro.Trim(), null, dynamic, onboardTags.ToArray(), null,
            VelvetIntent.Sanitize(onboardIntent), null, onboardDiscoverable);
        store.UpdateProfile(request, _ => { });
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
