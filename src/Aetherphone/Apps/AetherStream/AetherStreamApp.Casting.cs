using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float CastingHeroHeight = 96f;
    private const float RequestRowHeight = 44f;

    private string screenPresetName = "";

    private void DrawScreenContent(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawScreenStateRow(width, scale);

            var windowCard = GroupCard.Begin(accentedTheme, 2);
            var windowOpen = SettingsRow.Bool(windowCard.NextRow(), Loc.T(L.AetherStream.OpenScreenWindow),
                screenWindow.IsOpen, accentedTheme);
            windowCard.End();
            
            var screenVisible = SettingsRow.Bool(windowCard.NextRow(),
                Loc.T(L.AetherStream.InGameScreen), configuration.VideoScreenVisible, accentedTheme);
            if (screenVisible != configuration.VideoScreenVisible)
            {
                configuration.VideoScreenVisible = screenVisible;
                configuration.Save();
                screen.Engine.ScreenVisible = screenVisible;
                if (screenVisible)
                {
                    screen.Engine.RecenterScreen();
                }
            }

            if (windowOpen != screenWindow.IsOpen)
            {
                screenWindow.IsOpen = windowOpen;
            }

            DrawScreenPositionSection(width, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawScreenStateRow(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = 26f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var stateColor = screen.Engine.IsActive ? theme.ToggleOn : ui.MutedInk;
        var dotCenter = new Vector2(origin.X + 4f * scale, origin.Y + rowHeight * 0.5f);
        drawList.AddCircleFilled(dotCenter, 3.5f * scale, ImGui.GetColorU32(stateColor), 16);
        var labelHeight = Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(dotCenter.X + 9f * scale, origin.Y + rowHeight * 0.5f - labelHeight * 0.5f),
            Typography.FitText(StateLabel(), width - 14f * scale, TextStyles.Subheadline), stateColor,
            TextStyles.Subheadline);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Sm * scale));
    }

    private void DrawPendingRequests(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var labelOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(ImGui.GetWindowDrawList(), labelOrigin,
            Loc.Culture.TextInfo.ToUpper(Loc.T(L.AetherStream.CastingPendingRequestsHeader)),
            ui.Palette.HeaderInk, TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(labelOrigin);
        ImGui.Dummy(new Vector2(width,
            Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Xs * scale));

        var requests = watchAlong.PendingRequests;
        for (var index = 0; index < requests.Count; index++)
        {
            DrawPendingRequestRow(width, scale, requests[index]);
        }
    }

    private void DrawPendingRequestRow(float width, float scale, PendingJoinRequest request)
    {
        var origin = ImGui.GetCursorScreenPos();
        var row = new Rect(origin, origin + new Vector2(width, RequestRowHeight * scale));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, row.Min, row.Max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(accentedTheme.GroupedCard));

        var avatarRadius = 14f * scale;
        var avatarCenter = new Vector2(row.Min.X + Metrics.Space.Md * scale + avatarRadius, row.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, request.DisplayName, string.Empty,
            request.AvatarUrl, remoteImages, lodestone, 0.7f, 20);

        var delta = ImGui.GetIO().DeltaTime;
        var circleRadius = 14f * scale;
        var denyCenter = new Vector2(row.Max.X - Metrics.Space.Md * scale - circleRadius, row.Center.Y);
        var approveCenter = new Vector2(denyCenter.X - circleRadius * 2f - Metrics.Space.Sm * scale, row.Center.Y);

        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textWidth = approveCenter.X - circleRadius - Metrics.Space.Md * scale - textLeft;
        var nameHeight = Typography.LineHeight(TextStyles.Body);
        Marquee.DrawLeft(drawList, new MarqueeId("aetherstream.request.", request.UserId), request.DisplayName, textLeft,
            row.Center.Y - nameHeight * 0.5f, textWidth, TextStyles.Body, ui.TitleInk,
            UiInteract.Hover(row.Min, row.Max));

        if (HoverButton.Circle(drawList, "aetherstream.request.approve." + request.UserId, approveCenter,
                circleRadius, FontAwesomeIcon.Check, Palette.WithAlpha(ui.Accent, 0.16f), ui.Accent, delta, 1f,
                true, Loc.T(L.AetherStream.CastingApprove)))
        {
            watchAlong.ApproveRequest(request.UserId);
        }

        if (HoverButton.Circle(drawList, "aetherstream.request.deny." + request.UserId, denyCenter, circleRadius,
                FontAwesomeIcon.Times, Palette.WithAlpha(theme.Danger, 0.14f), theme.Danger, delta, 1f, true,
                Loc.T(L.AetherStream.CastingDeny)))
        {
            watchAlong.DenyRequest(request.UserId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, RequestRowHeight * scale));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    private void DrawScreenPositionSection(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Header(Loc.T(L.AetherStream.CastingScreenPositionHeader), accentedTheme);

        if (!screen.Engine.IsActive || !screen.Engine.ScreenVisible)
        {
            SettingsSection.Hint(Loc.T(L.AetherStream.CastingScreenPositionHint), accentedTheme);
            return;
        }

        DrawScreenPositionEditor(width, scale);
        DrawScreenPresets(width, scale);
    }

    private void DrawScreenPositionEditor(float width, float scale)
    {
        var pos = screen.Engine.ScreenPosition;
        var yaw = screen.Engine.ScreenYaw;
        var pitch = screen.Engine.ScreenPitch;
        var roll = screen.Engine.ScreenRoll;
        var positionScale = screen.Engine.ScreenScale;
        var anchor = screen.Engine.ScreenSpawnAnchor;
        const float range = VideoEngine.ScreenPositionSliderRange;
        const float yawRange = 2f * MathF.PI;
        const float tiltRange = MathF.PI;
        var transformChanged = false;

        var card = GroupCard.Begin(accentedTheme, 7);
        transformChanged |= DrawTransformRow(card.NextRow(), "aetherstream.screen.x", "X", ref pos.X,
            anchor.X - range, anchor.X + range, $"{pos.X:F1}", scale);
        transformChanged |= DrawTransformRow(card.NextRow(), "aetherstream.screen.y", "Y", ref pos.Y,
            anchor.Y - range, anchor.Y + range, $"{pos.Y:F1}", scale);
        transformChanged |= DrawTransformRow(card.NextRow(), "aetherstream.screen.z", "Z", ref pos.Z,
            anchor.Z - range, anchor.Z + range, $"{pos.Z:F1}", scale);
        transformChanged |= DrawTransformRow(card.NextRow(), "aetherstream.screen.scale",
            Loc.T(L.AetherStream.CastingScale), ref positionScale, VideoEngine.MinScreenScale,
            VideoEngine.MaxScreenScale, $"{positionScale:F1}x", scale);
        transformChanged |= DrawTransformRow(card.NextRow(), "aetherstream.screen.yaw",
            Loc.T(L.AetherStream.CastingRotate), ref yaw, -yawRange, yawRange,
            $"{yaw * 180f / MathF.PI:F0}°", scale);
        transformChanged |= DrawTransformRow(card.NextRow(), "aetherstream.screen.pitch",
            Loc.T(L.AetherStream.CastingTilt), ref pitch, -tiltRange, tiltRange,
            $"{pitch * 180f / MathF.PI:F0}°", scale);
        transformChanged |= DrawTransformRow(card.NextRow(), "aetherstream.screen.roll",
            Loc.T(L.AetherStream.CastingRoll), ref roll, -tiltRange, tiltRange,
            $"{roll * 180f / MathF.PI:F0}°", scale);
        card.End();

        if (transformChanged)
        {
            screen.Engine.SetScreenTransform(pos, yaw, pitch, roll, positionScale);
        }

        DrawScreenShapeRow(scale);

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var recenterOrigin = ImGui.GetCursorScreenPos();
        var recenterRect = new Rect(recenterOrigin, recenterOrigin + new Vector2(width, 36f * scale));
        if (ui.GhostButton(recenterRect, Loc.T(L.AetherStream.CastingRecenter)))
        {
            screen.Engine.RecenterScreen();
        }

        ImGui.SetCursorScreenPos(recenterOrigin);
        ImGui.Dummy(new Vector2(width, 36f * scale));
    }

    private void DrawScreenShapeRow(float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var wasFlat = !screen.Engine.ScreenCurved;
        var card = GroupCard.Begin(accentedTheme, 1);
        var flat = SettingsRow.Bool(card.NextRow(), Loc.T(L.AetherStream.CastingFlatScreen), wasFlat, accentedTheme,
            hint: Loc.T(L.AetherStream.CastingFlatScreenHint));
        card.End();

        if (flat == wasFlat)
        {
            return;
        }

        screen.Engine.ScreenCurved = !flat;
        configuration.VideoScreenCurved = !flat;
        configuration.Save();
    }

    private bool DrawTransformRow(Rect row, string id, string label, ref float value, float min, float max,
        string valueText, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var labelColumn = 58f * scale;
        var valueColumn = 54f * scale;
        var labelHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, labelColumn - Metrics.Space.Xs * scale, TextStyles.BodyEmphasized),
            accentedTheme.TextStrong, TextStyles.BodyEmphasized);

        var span = MathF.Max(max - min, 0.001f);
        var normalized = Math.Clamp((value - min) / span, 0f, 1f);
        var result = Slider.Draw(id, row, normalized, accentedTheme, labelColumn, valueColumn);

        var valueSize = Typography.Measure(valueText, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f),
            valueText, accentedTheme.TextMuted, TextStyles.Caption1);

        if (!result.Dragging && !result.Released)
        {
            return false;
        }

        var updated = min + result.Value * span;
        if (MathF.Abs(updated - value) <= 0.0001f)
        {
            return false;
        }

        value = updated;
        return true;
    }

    private void DrawScreenPresets(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Header(Loc.T(L.AetherStream.CastingSavedPresets), accentedTheme);

        var origin = ImGui.GetCursorScreenPos();
        var saveRadius = 15f * scale;
        var fieldRect = new Rect(origin,
            origin + new Vector2(width - saveRadius * 2f - Metrics.Space.Md * scale, 44f * scale));
        var submitted = SubmitField.Draw(fieldRect, "##aetherstreamScreenPresetName",
            Loc.T(L.AetherStream.CastingPresetNameHint), ref screenPresetName, accentedTheme, 50,
            FontAwesomeIcon.Bookmark);
        var canSave = !string.IsNullOrWhiteSpace(screenPresetName);
        var saveCenter = new Vector2(origin.X + width - saveRadius, origin.Y + 22f * scale);
        var saveBackground = canSave ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.35f);
        var saveInk = canSave ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(1f, 1f, 1f, 0.6f);
        if (ui.IconButton(saveCenter, saveRadius, IconGlyph.Of(FontAwesomeIcon.Plus), saveInk, saveBackground,
                0.55f, Loc.T(L.AetherStream.CastingSavePreset)) && canSave)
        {
            submitted = true;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 44f * scale));

        if (submitted && canSave)
        {
            screen.Engine.SaveScreenPreset(screenPresetName);
            screenPresetName = "";
        }

        var presets = screen.Engine.GetScreenPresets();
        if (presets.Count == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
        var card = GroupCard.Begin(accentedTheme, presets.Count);
        for (var index = 0; index < presets.Count; index++)
        {
            var preset = presets[index];
            var row = card.NextRow();
            var removeCenter = new Vector2(row.Max.X - 10f * scale, row.Center.Y);
            var applyMax = new Vector2(removeCenter.X - 22f * scale, row.Max.Y);
            var hovered = UiInteract.Hover(row.Min, applyMax);
            if (hovered)
            {
                Squircle.Fill(ImGui.GetWindowDrawList(),
                    new Vector2(row.Min.X - Metrics.Space.Sm * scale, row.Min.Y + 3f * scale),
                    new Vector2(applyMax.X, row.Max.Y - 3f * scale), Metrics.Radius.Sm * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(accentedTheme.TextStrong, 0.05f)));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
            Marquee.DrawLeft(new MarqueeId("aetherstream.preset.", preset.Name), preset.Name, row.Min.X,
                row.Center.Y - nameHeight * 0.5f, applyMax.X - row.Min.X - Metrics.Space.Md * scale,
                TextStyles.BodyEmphasized, accentedTheme.TextStrong, hovered);

            if (ui.IconButton(removeCenter, 12f * scale, IconGlyph.Of(FontAwesomeIcon.TrashAlt), theme.Danger,
                    AppSkin.Transparent, 0.5f))
            {
                screen.Engine.RemoveScreenPreset(preset.Name);
                card.End();
                return;
            }

            if (UiInteract.Click(row.Min, applyMax, hovered))
            {
                screen.Engine.ApplyScreenPreset(preset);
                configuration.VideoScreenCurved = screen.Engine.ScreenCurved;
                configuration.Save();
            }
        }

        card.End();
    }

    private string StateLabel() => screen.Engine.IsActive
        ? Loc.T(L.AetherStream.CastingStateReady)
        : Loc.T(L.AetherStream.CastingStateNotReady);
}
