using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const int OnboardStepCount = 4;

    private bool onboardSeeded;
    private int onboardStep;
    private int onboardIntent;
    private int onboardGender;
    private int onboardWho;
    private string onboardName = string.Empty;
    private string onboardHandle = string.Empty;
    private string onboardIntro = string.Empty;
    private bool onboardDiscoverable = true;
    private bool onboardAvatarEditing;
    private readonly List<string> onboardTags = new();
    private readonly List<string> onboardRole = new();

    private void DrawOnboarding(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        store.EnsureMe();
        if (!onboardSeeded && store.Me is { } seed)
        {
            onboardName = seed.DisplayName;
            onboardHandle = seed.Handle;
            onboardGender = VelvetGender.Sanitize(seed.Gender);
            onboardSeeded = true;
        }

        if (onboardAvatarEditing)
        {
            var overlay = SceneChrome.ScreenFrom(area, theme, scale);
            ui.Backdrop(overlay);
            var overlayContext = new PhoneContext(area, theme, navigation);
            if (avatar.Draw(area, overlayContext, ui.Accent))
            {
                onboardAvatarEditing = false;
            }

            return;
        }

        var screen = SceneChrome.ScreenFrom(area, theme, scale);
        ui.Backdrop(screen);
        var drawList = ImGui.GetWindowDrawList();
        VelvetArt.Bloom(drawList, screen, theme.ScreenRounding * scale, 0.85f);

        DrawOnboardChrome(area);

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + 132f * scale),
            new Vector2(area.Max.X, area.Max.Y - 96f * scale));
        switch (onboardStep)
        {
            case 1:
                DrawOnboardIntentStep(body);
                break;
            case 2:
                DrawOnboardAboutStep(body);
                break;
            case 3:
                DrawOnboardReadyStep(body);
                break;
            default:
                DrawOnboardIdentityStep(body);
                break;
        }

        DrawOnboardFooter(area);
    }

    private void DrawOnboardChrome(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();

        var trackWidth = MathF.Min(area.Width - 96f * scale, 240f * scale);
        var segGap = 6f * scale;
        var segWidth = (trackWidth - segGap * (OnboardStepCount - 1)) / OnboardStepCount;
        var segHeight = 4f * scale;
        var trackLeft = area.Center.X - trackWidth * 0.5f;
        var trackY = area.Min.Y + 30f * scale;
        for (var index = 0; index < OnboardStepCount; index++)
        {
            var segMin = new Vector2(trackLeft + index * (segWidth + segGap), trackY);
            var segMax = new Vector2(segMin.X + segWidth, segMin.Y + segHeight);
            var color = index <= onboardStep ? VelvetTheme.Rose : VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.18f);
            Squircle.Fill(drawList, segMin, segMax, segHeight * 0.5f, color.Packed());
        }

        if (onboardStep > 0)
        {
            var backCenter = new Vector2(area.Min.X + 24f * scale, area.Min.Y + 68f * scale);
            if (ui.IconButton(backCenter, 16f * scale, FontAwesomeIcon.ChevronLeft.ToIconString(), VelvetTheme.TitleInk,
                    AppSkin.Transparent, 0.85f, Loc.T(L.Velvet.Back), HoverLabelSide.Below))
            {
                onboardStep--;
            }
        }

        var title = onboardStep switch
        {
            1 => Loc.T(L.Velvet.OnboardIntent),
            2 => Loc.T(L.Velvet.ObTitleAbout),
            3 => Loc.T(L.Velvet.ObTitleReady),
            _ => Loc.T(L.Velvet.ObTitleIdentity),
        };
        var subtitle = onboardStep switch
        {
            1 => Loc.T(L.Velvet.ObSubIntent),
            2 => Loc.T(L.Velvet.ObSubAbout),
            3 => Loc.T(L.Velvet.ObSubReady),
            _ => Loc.T(L.Velvet.ObSubIdentity),
        };
        Typography.DrawCentered(new Vector2(area.Center.X, area.Min.Y + 68f * scale), title, VelvetTheme.TitleInk,
            TextStyles.Title1);
        Typography.DrawWrappedCentered(new Vector2(area.Center.X, area.Min.Y + 98f * scale), subtitle,
            VelvetTheme.MutedInk, TextStyles.Subheadline, MathF.Min(area.Width - 64f * scale, 300f * scale));
    }

    private void DrawOnboardFooter(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var buttonWidth = MathF.Min(area.Width - 44f * scale, 320f * scale);
        var buttonHeight = 50f * scale;
        var left = area.Center.X - buttonWidth * 0.5f;
        var top = area.Max.Y - buttonHeight - 26f * scale;
        var rect = new Rect(new Vector2(left, top), new Vector2(left + buttonWidth, top + buttonHeight));
        var isLast = onboardStep >= OnboardStepCount - 1;
        var label = isLast ? Loc.T(L.Velvet.EnterVelvet) : Loc.T(L.Velvet.Continue);
        if (!DrawOnboardPrimary(rect, label, CanAdvanceOnboard()))
        {
            return;
        }

        if (isLast)
        {
            FinishOnboarding();
        }
        else
        {
            onboardStep++;
        }
    }

    private bool DrawOnboardPrimary(Rect rect, string label, bool enabled)
    {
        if (enabled)
        {
            return ui.PillButton(rect, label, true);
        }

        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f,
            VelvetTheme.Alpha(VelvetTheme.Rose, 0.28f).Packed());
        Typography.DrawCentered(rect.Center, label, VelvetTheme.Alpha(VelvetTheme.OnAccent, 0.55f), 0.9f,
            FontWeight.SemiBold);
        return false;
    }

    private bool CanAdvanceOnboard() => onboardStep switch
    {
        0 => onboardName.Trim().Length > 0,
        1 => VelvetIntent.Sanitize(onboardIntent) != 0,
        2 => onboardIntro.Trim().Length > 0,
        _ => true,
    };

    private void DrawOnboardIdentityStep(Rect body)
    {
        using (AppSurface.Begin(body))
        {
            Gap(6f);
            DrawOnboardAvatar();
            Gap(18f);
            ui.Field(Loc.T(L.Velvet.DisplayNameLabel), "##ob_name", ref onboardName, 40, false);
            Gap(10f);
            ui.Field(Loc.T(L.Velvet.HandleLabel), "##ob_handle", ref onboardHandle, 15, false);
            Gap(12f);
            ui.HelpText(Loc.T(L.Velvet.ObHandleHelp));
            Gap(24f);
        }
    }

    private void DrawOnboardAvatar()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var block = Reserve(150f);
        var drawList = ImGui.GetWindowDrawList();
        var radius = 52f * scale;
        var center = new Vector2(block.Center.X, block.Min.Y + 8f * scale + radius);
        var me = store.Me;
        VAvatar.Draw(drawList, center, radius, theme, DisplayNameOf(onboardName, onboardHandle),
            me?.World ?? string.Empty, me?.AvatarUrl, images, lodestone, -1, VelvetTheme.Rose);

        var badgeCenter = new Vector2(center.X + radius * 0.70f, center.Y + radius * 0.70f);
        drawList.AddCircleFilled(badgeCenter, 15f * scale, VelvetTheme.GroundBottom.Packed(), 24);
        drawList.AddCircleFilled(badgeCenter, 13f * scale, VelvetTheme.Rose.Packed(), 24);
        AppSkin.Icon(badgeCenter, FontAwesomeIcon.Camera.ToIconString(), VelvetTheme.OnAccent, 0.6f);

        var avatarMin = new Vector2(center.X - radius, center.Y - radius);
        var avatarMax = new Vector2(center.X + radius, center.Y + radius);
        var hovered = UiInteract.Hover(avatarMin, avatarMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var hint = me?.AvatarUrl is { Length: > 0 } ? Loc.T(L.Velvet.ChangePhoto) : Loc.T(L.Velvet.AddPhoto);
        var hintSize = Typography.Measure(hint, TextStyles.Footnote);
        Typography.Draw(new Vector2(center.X - hintSize.X * 0.5f, center.Y + radius + 12f * scale), hint,
            VelvetTheme.RoseInk, TextStyles.Footnote);

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            avatar.Open();
            onboardAvatarEditing = true;
        }
    }

    private void DrawOnboardIntentStep(Rect body)
    {
        using (AppSurface.Begin(body))
        {
            Gap(2f);
            var defs = VelvetIntent.All;
            for (var index = 0; index < defs.Length; index++)
            {
                var def = defs[index];
                var rect = Reserve(72f);
                if (DrawOnboardIntentCard(rect, def, VelvetIntent.Has(onboardIntent, def.Flag)))
                {
                    onboardIntent = VelvetIntent.Toggle(onboardIntent, def.Flag);
                }

                Gap(10f);
            }

            Gap(20f);
        }
    }

    private bool DrawOnboardIntentCard(Rect rect, in VelvetIntentDef def, bool selected)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = Metrics.Radius.Card * scale;

        var fill = selected
            ? VelvetTheme.Alpha(def.Hue, hovered ? 0.24f : 0.18f)
            : hovered ? VelvetTheme.CardHi : VelvetTheme.Card;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, fill.Packed());
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            (selected ? VelvetTheme.Alpha(def.Hue, 0.85f) : VelvetTheme.CardStroke).Packed(),
            (selected ? 1.5f : 1f) * scale);

        var tileSize = 44f * scale;
        var tileMin = new Vector2(rect.Min.X + 14f * scale, rect.Center.Y - tileSize * 0.5f);
        var tileMax = new Vector2(tileMin.X + tileSize, tileMin.Y + tileSize);
        Squircle.Fill(drawList, tileMin, tileMax, tileSize * 0.32f, def.Hue.Packed());
        AppSkin.Icon(new Vector2((tileMin.X + tileMax.X) * 0.5f, (tileMin.Y + tileMax.Y) * 0.5f),
            def.Icon.ToIconString(), VelvetTheme.OnAccent, 0.95f);

        var textLeft = tileMax.X + 14f * scale;
        var textWidth = rect.Max.X - 46f * scale - textLeft;
        var blurb = Typography.FitText(Loc.T(def.Blurb), textWidth, TextStyles.Subheadline);
        var label = Loc.T(def.Label);
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        var blurbSize = Typography.Measure(blurb, TextStyles.Subheadline);
        var textTop = rect.Center.Y - (labelSize.Y + 3f * scale + blurbSize.Y) * 0.5f;
        Typography.Draw(new Vector2(textLeft, textTop), label, VelvetTheme.TitleInk, TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, textTop + labelSize.Y + 3f * scale), blurb, VelvetTheme.MutedInk,
            TextStyles.Subheadline);

        DrawOnboardCheck(drawList, new Vector2(rect.Max.X - 26f * scale, rect.Center.Y), selected, def.Hue, scale);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawOnboardCheck(ImDrawListPtr drawList, Vector2 center, bool selected, Vector4 hue, float scale)
    {
        var radius = 11f * scale;
        if (!selected)
        {
            drawList.AddCircle(center, radius, VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.30f).Packed(), 24,
                1.5f * scale);
            return;
        }

        drawList.AddCircleFilled(center, radius, hue.Packed(), 24);
        var tick = VelvetTheme.OnAccent.Packed();
        var thickness = 2f * scale;
        drawList.AddLine(center + new Vector2(-4.5f * scale, 0.2f * scale),
            center + new Vector2(-1.4f * scale, 3.6f * scale), tick, thickness);
        drawList.AddLine(center + new Vector2(-1.4f * scale, 3.6f * scale),
            center + new Vector2(5f * scale, -3.8f * scale), tick, thickness);
    }

    private void DrawOnboardAboutStep(Rect body)
    {
        using (AppSurface.Begin(body))
        {
            Gap(2f);
            ui.Field(Loc.T(L.Velvet.YourIntro), "##ob_intro", ref onboardIntro, 400, true, 120f);
            Gap(18f);

            VSectionHeader.Card(FontAwesomeIcon.VenusMars, Loc.T(L.Velvet.CardGender));
            Gap(6f);
            DrawGenderPicker(ref onboardGender);
            Gap(18f);

            if (VelvetIntent.IncludesErp(onboardIntent))
            {
                VSectionHeader.Card(FontAwesomeIcon.Heart, Loc.T(L.Velvet.YourRole));
                Gap(4f);
                ui.HelpText(Loc.T(L.Velvet.RoleErpHelp));
                Gap(8f);
                DrawCategoryPicker(VelvetSuggestions.DynamicCategories, onboardRole);
                Gap(18f);
            }

            VSectionHeader.Card(FontAwesomeIcon.Hashtag, Loc.T(L.Velvet.DynamicLabel));
            Gap(4f);
            ui.HelpText(Loc.T(L.Velvet.VibeOptionalHelp));
            Gap(8f);
            DrawCategoryPicker(VelvetSuggestions.TagCategories, onboardTags);
            Gap(28f);
        }
    }

    private void DrawOnboardReadyStep(Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (AppSurface.Begin(body))
        {
            Gap(6f);
            DrawOnboardSummaryCard();
            Gap(20f);

            VSectionHeader.Overline(Loc.T(L.Velvet.DiscoveryHeader));
            Gap(2f);
            ui.ToggleRow(Loc.T(L.Velvet.DiscoverableLabel), ref onboardDiscoverable);
            Gap(6f);
            ui.HelpText(Loc.T(L.Velvet.ObDiscoverableHelp));
            Gap(22f);

            VSectionHeader.Overline(Loc.T(L.Velvet.WhoCanMessage));
            Gap(10f);
            var whoRect = Reserve(34f);
            var who = VSegmented.Draw("velvetObWho", whoRect,
                new[] { Loc.T(L.Velvet.WhoEveryone), Loc.T(L.Velvet.WhoFriends), Loc.T(L.Velvet.WhoNoOne) }, onboardWho,
                scale);
            if (who >= 0)
            {
                onboardWho = who;
            }

            Gap(10f);
            ui.HelpText(Loc.T(L.Velvet.WhoHelp));
            Gap(22f);

            ui.HelpText(Loc.T(L.Velvet.ObConductHelp));
            Gap(28f);
        }
    }

    private void DrawOnboardSummaryCard()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rect = Reserve(84f);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Card * scale, true);

        var radius = 28f * scale;
        var center = new Vector2(rect.Min.X + 18f * scale + radius, rect.Center.Y);
        var me = store.Me;
        VAvatar.Draw(drawList, center, radius, theme, DisplayNameOf(onboardName, onboardHandle),
            me?.World ?? string.Empty, me?.AvatarUrl, images, lodestone, -1, VelvetTheme.Rose);

        var textLeft = center.X + radius + 16f * scale;
        var textWidth = rect.Max.X - 16f * scale - textLeft;
        var name = Typography.FitText(DisplayNameOf(onboardName, onboardHandle), textWidth, TextStyles.Headline);
        var intent = Typography.FitText(VelvetIntent.Summary(onboardIntent), textWidth, TextStyles.Subheadline);
        Typography.Draw(new Vector2(textLeft, rect.Center.Y - 20f * scale), name, VelvetTheme.TitleInk,
            TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, rect.Center.Y + 3f * scale), intent, VelvetTheme.RoseInk,
            TextStyles.Subheadline);
    }

    private void FinishOnboarding()
    {
        configuration.VelvetOnboarded = true;
        configuration.VelvetOnboardedVersion = Configuration.VelvetOnboardVersion;
        configuration.Save();

        var role = VelvetIntent.IncludesErp(onboardIntent) ? onboardRole.ToArray() : Array.Empty<string>();
        var dynamic = VelvetTags.Join(role);
        var request = new UpdateVelvetProfileRequest(onboardIntro.Trim(), null, dynamic, onboardTags.ToArray(), null,
            VelvetIntent.Sanitize(onboardIntent), null, onboardDiscoverable, onboardWho,
            VelvetGender.Sanitize(onboardGender));

        var me = store.Me;
        var identityChanged = me is not null &&
            (onboardName.Trim() != me.DisplayName || onboardHandle.Trim() != me.Handle);
        if (identityChanged)
        {
            store.UpdateIdentity(onboardName.Trim(), onboardHandle.Trim(),
                _ => store.UpdateProfile(request, _ => { }));
        }
        else
        {
            store.UpdateProfile(request, _ => { });
        }
    }
}
