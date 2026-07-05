using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetApp
{
    private void DrawOnboarding(Rect content)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ui.Backdrop(screen);
        var centerX = content.Center.X;
        var textWidth = MathF.Min(content.Width - 52f * scale, 300f * scale);
        var title = onboardStep switch
        {
            1 => Loc.T(L.Velvet.OnboardVibe),
            2 => Loc.T(L.Velvet.OnboardPrivacy),
            _ => Loc.T(L.Velvet.OnboardIntent),
        };
        var hint = onboardStep switch
        {
            1 => Loc.T(L.Velvet.OnboardVibeHint),
            2 => Loc.T(L.Velvet.OnboardPrivacyHint),
            _ => Loc.T(L.Velvet.OnboardIntentHint),
        };
        Typography.DrawCentered(new Vector2(centerX, content.Min.Y + 60f * scale), title, VelvetUi.TitleInk, 1.5f,
            FontWeight.SemiBold);
        VelvetUi.WrappedCentered(centerX, content.Min.Y + 84f * scale, hint, textWidth, VelvetUi.MutedInk, scale, 0.9f);
        var bodyTop = content.Min.Y + 150f * scale;
        if (onboardStep == 0)
        {
            DrawOnboardIntent(content, bodyTop);
        }
        else if (onboardStep == 1)
        {
            DrawOnboardVibe(content, bodyTop);
        }
        else
        {
            DrawOnboardPrivacy(content, bodyTop);
        }

        var buttonWidth = MathF.Min(content.Width - 48f * scale, 300f * scale);
        var buttonHeight = 46f * scale;
        var nextMin = new Vector2(centerX - buttonWidth * 0.5f, content.Max.Y - buttonHeight - 24f * scale);
        var nextRect = new Rect(nextMin, new Vector2(nextMin.X + buttonWidth, nextMin.Y + buttonHeight));
        var isLast = onboardStep >= 2;
        if (ui.PillButton(nextRect, isLast ? Loc.T(L.Velvet.EnterVelvet) : Loc.T(L.Velvet.Next), true))
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

        if (onboardStep > 0)
        {
            var backMin = new Vector2(centerX - buttonWidth * 0.5f, nextMin.Y - buttonHeight - 8f * scale);
            if (ui.GhostButton(new Rect(backMin, new Vector2(backMin.X + buttonWidth, backMin.Y + buttonHeight)),
                    Loc.T(L.Velvet.Back)))
            {
                onboardStep--;
            }
        }
    }

    private void DrawOnboardIntent(Rect content, float top)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var cursorY = top;
        var chipHeight = 40f * scale;
        var left = content.Min.X + 24f * scale;
        var width = content.Width - 48f * scale;
        for (var index = 1; index < VelvetLookingFor.All.Length; index++)
        {
            var value = VelvetLookingFor.All[index];
            var rect = new Rect(new Vector2(left, cursorY), new Vector2(left + width, cursorY + chipHeight));
            if (ui.Chip(rect, VelvetLookingFor.Label(value), editLookingFor == value))
            {
                editLookingFor = value;
            }

            cursorY += chipHeight + 8f * scale;
        }
    }

    private void DrawOnboardVibe(Rect content, float top)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var left = content.Min.X + 24f * scale;
        var width = content.Width - 48f * scale;
        ImGui.SetCursorScreenPos(new Vector2(left, top));
        var fieldRect = new Rect(new Vector2(left, top), new Vector2(left + width, top + 88f * scale));
        DrawInlineField("##obIntro", ref editIntro, IntroMax, true, fieldRect, Loc.T(L.Velvet.IntroHint));
        var tagsTop = top + 100f * scale;
        var tagsRect = new Rect(new Vector2(left, tagsTop), new Vector2(left + width, tagsTop + 34f * scale));
        DrawInlineField("##obTags", ref editTags, TagsMax, false, tagsRect, Loc.T(L.Velvet.TagsHint));
        DrawSuggestionChips(new Vector2(left, tagsTop + 44f * scale), width, TagSuggestions, ref editTags);
    }

    private void DrawOnboardPrivacy(Rect content, float top)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var left = content.Min.X + 24f * scale;
        var width = content.Width - 48f * scale;
        ImGui.SetCursorScreenPos(new Vector2(left, top));
        using (AppSurface.Begin(new Rect(new Vector2(left, top), new Vector2(left + width, top + 120f * scale))))
        {
            ui.ToggleRow(Loc.T(L.Velvet.DiscoverableLabel), ref editDiscoverable);
            VelvetUi.HelpText(Loc.T(L.Velvet.AppearHelp));
        }
    }

    private void FinishOnboarding()
    {
        configuration.VelvetOnboarded = true;
        configuration.Save();
        var request = new UpdateVelvetProfileRequest(editIntro.Trim(), null, null, VelvetTags.Parse(editTags), null,
            editLookingFor, null, editDiscoverable);
        store.UpdateProfile(request, _ => { });
    }

    private void DrawGate(Rect content)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ui.Backdrop(screen);
        var centerX = content.Center.X;
        var textWidth = MathF.Min(content.Width - 52f * scale, 300f * scale);
        var moonCenter = new Vector2(centerX, content.Min.Y + 96f * scale);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(moonCenter, 46f * scale, ImGui.GetColorU32(new Vector4(0.90f, 0.24f, 0.46f, 0.16f)),
            40);
        drawList.AddCircleFilled(moonCenter, 30f * scale, ImGui.GetColorU32(new Vector4(0.95f, 0.42f, 0.60f, 0.22f)),
            36);
        VelvetUi.Icon(moonCenter, FontAwesomeIcon.Moon.ToIconString(), new Vector4(0.99f, 0.72f, 0.82f, 1f), 1.7f);
        Typography.DrawCentered(new Vector2(centerX, moonCenter.Y + 66f * scale), Loc.T(L.Velvet.GateTitle),
            VelvetUi.TitleInk, 1.7f, FontWeight.SemiBold);
        var bodyTop = moonCenter.Y + 96f * scale;
        var bodyHeight = VelvetUi.WrappedCentered(centerX, bodyTop, Loc.T(L.Velvet.GateBody), textWidth,
            VelvetUi.BodyInk, scale, 1.05f);
        VelvetUi.WrappedCentered(centerX, bodyTop + bodyHeight + 16f * scale, Loc.T(L.Velvet.GateDiscretion), textWidth,
            VelvetUi.MutedInk, scale, 0.88f);
        var buttonWidth = MathF.Min(content.Width - 48f * scale, 300f * scale);
        var buttonHeight = 46f * scale;
        var enterMin = new Vector2(centerX - buttonWidth * 0.5f, content.Max.Y - buttonHeight * 2f - 30f * scale);
        var enterRect = new Rect(enterMin, new Vector2(enterMin.X + buttonWidth, enterMin.Y + buttonHeight));
        if (ui.PillButton(enterRect, gateBusy ? Loc.T(L.Velvet.GateWorking) : Loc.T(L.Velvet.GateEnter), true) &&
            !gateBusy)
        {
            AcceptGate();
        }

        var leaveMin = new Vector2(centerX - buttonWidth * 0.5f, enterRect.Max.Y + 10f * scale);
        var leaveRect = new Rect(leaveMin, new Vector2(leaveMin.X + buttonWidth, leaveMin.Y + buttonHeight));
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
    }
}
