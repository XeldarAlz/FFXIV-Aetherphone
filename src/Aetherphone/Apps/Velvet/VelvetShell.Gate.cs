using System.Numerics;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
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

    private void GateMenus()
    {
        postMenu.Gate();
        threadMenu.Gate();
    }

    private void DrawGate(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(area, theme, scale);
        ui.Backdrop(screen);
        var drawList = ImGui.GetWindowDrawList();
        VelvetArt.Bloom(drawList, area, 1.15f);

        var moonCenter = new Vector2(area.Center.X, area.Min.Y + area.Height * 0.24f);
        VelvetArt.Moon(drawList, moonCenter, 24f * scale, VelvetTheme.Moonlight, VelvetTheme.GroundTop);
        Typography.DrawCentered(new Vector2(area.Center.X, moonCenter.Y + 64f * scale), "Velvet", VelvetTheme.TitleInk,
            TextStyles.LargeTitle);

        var textWidth = MathF.Min(area.Width - 60f * scale, 320f * scale);
        var bodyHeight = Typography.DrawWrappedCentered(new Vector2(area.Center.X, moonCenter.Y + 106f * scale),
            "A private, adults only corner of the suite. Moonlit, unhurried, yours.", VelvetTheme.BodyInk,
            TextStyles.Callout, textWidth);
        Typography.DrawWrappedCentered(new Vector2(area.Center.X, moonCenter.Y + 106f * scale + bodyHeight + 14f * scale),
            "By entering you confirm you are 18 or older. Be kind, be discreet.", VelvetTheme.MutedInk,
            TextStyles.Footnote, textWidth);

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
            TextStyles.Callout, MathF.Min(area.Width - 52f * scale, 320f * scale));

        var buttonWidth = MathF.Min(area.Width - 48f * scale, 300f * scale);
        var buttonHeight = 46f * scale;
        var bodyTop = area.Min.Y + 150f * scale;
        var bodyRect = new Rect(new Vector2(area.Min.X + 24f * scale, bodyTop),
            new Vector2(area.Max.X - 24f * scale, area.Max.Y - buttonHeight * 2f - 40f * scale));
        using (AppSurface.Begin(bodyRect))
        {
            if (onboardStep == 0)
            {
                DrawOnboardIntent();
            }
            else if (onboardStep == 1)
            {
                DrawOnboardHello();
            }
            else
            {
                DrawOnboardPrivacy();
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

    private void DrawOnboardIntent()
    {
        var width = ImGui.GetContentRegionAvail().X;
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        var models = new VChipModel[VelvetIntent.All.Length];
        for (var index = 0; index < models.Length; index++)
        {
            var def = VelvetIntent.All[index];
            var selected = VelvetIntent.Has(onboardIntent, def.Flag);
            models[index] = new VChipModel(def.Label, selected ? VChipStyle.Solid : VChipStyle.Ghost,
                selected ? def.Hue : VelvetTheme.Moonlight, def.Icon);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked >= 0)
        {
            onboardIntent = VelvetIntent.Toggle(onboardIntent, VelvetIntent.All[clicked].Flag);
        }
    }

    private void DrawOnboardHello()
    {
        ui.Field("Introduce yourself", "##ob_intro", ref onboardIntro, 400, true);
        ImGui.Dummy(new Vector2(0f, ImGuiHelpers.GlobalScale * 8f));
        DrawTokenEditor(onboardTags, VelvetSuggestions.Tags, VelvetTheme.Rose);
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
        var request = new UpdateVelvetProfileRequest(onboardIntro.Trim(), null, null, onboardTags.ToArray(), null,
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
