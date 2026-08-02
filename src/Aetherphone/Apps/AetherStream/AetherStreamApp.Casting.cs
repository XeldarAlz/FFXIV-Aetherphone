using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private string screenPresetName = "";

    // Only one target exists right now (Stage 7 scope - see spec's "Render target" section), so
    // this shows that single screen plainly rather than a "1 of N" list implying more should be
    // here. Watch-party features (viewer lists, handing control to another player) are out of
    // scope until the networking question is settled - this tab has nothing referencing them.
    //
    // The screen is a world-anchored quad drawn by ScreenPainter (ported from AlphaChannel's
    // v1.1.20260725.1088 revamp, replacing the earlier VFX/companion/Penumbra mounting - see
    // port/alphachannel-engine Stage 4 and the ScreenPainter port that followed it), so State just
    // reflects whether the engine is currently drawing anything at all.
    private void DrawCastingTab(Rect body, float scale)
    {
        var margin = Metrics.Space.Lg * scale;
        var content = new Rect(new Vector2(body.Min.X + margin, body.Min.Y + 8f * scale),
            new Vector2(body.Max.X - margin, body.Max.Y));

        var cardHeight = 150f * scale;
        var card = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + cardHeight));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, card.Min, card.Max, 12f * scale, ImGui.GetColorU32(ui.FieldSurface));

        var iconCenter = new Vector2(card.Min.X + 44f * scale, card.Min.Y + 44f * scale);
        drawList.AddCircleFilled(iconCenter, 26f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(StateColor(), 0.18f)), 32);
        AppSkin.Icon(iconCenter, FontAwesomeIcon.Tv.ToIconString(), StateColor(), 1.1f);

        var textLeft = card.Min.X + 84f * scale;
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 18f * scale), Loc.T(L.AetherStream.CastingTarget),
            ui.MutedInk, TextStyles.Caption1);
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 36f * scale), Loc.T(L.AetherStream.CastingThisScreen),
            ui.TitleInk, TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 62f * scale), StateLabel(), StateColor(),
            TextStyles.Subheadline);

        var bodyTop = card.Max.Y + 16f * scale;

        // See WatchAlongSession.PendingRequests. Sits above screen placement since a pending
        // join is more time-sensitive.
        if (watchAlong.IsHosting && watchAlong.PendingRequests.Count > 0)
        {
            bodyTop = DrawPendingRequests(content, bodyTop, scale);
            bodyTop += 12f * scale;
        }

        Typography.Draw(new Vector2(content.Min.X, bodyTop), Loc.T(L.AetherStream.CastingScreenPositionHeader),
            ui.TitleInk, TextStyles.FootnoteEmphasized);
        bodyTop += 22f * scale;

        bodyTop = screen.Engine.IsActive
            ? DrawScreenPositionEditor(content, bodyTop, scale)
            : DrawScreenPositionHint(content, bodyTop, scale);

        bodyTop += 12f * scale;

        // A second, independent render option - a plain resizable, movable window showing the
        // same decoded frames, for whenever the in-world quad isn't what's wanted.
        var windowRect = new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + 38f * scale));
        var windowLabel = screenWindow.IsOpen ? Loc.T(L.AetherStream.CloseScreenWindow) : Loc.T(L.AetherStream.OpenScreenWindow);
        if (SmallButton(windowRect, windowLabel, true, scale))
        {
            screenWindow.IsOpen = !screenWindow.IsOpen;
        }

        bodyTop = windowRect.Max.Y + 12f * scale;

        // Same "Leave, not Stop" reasoning as DrawCastingStatus in the Player tab - queue.Clear()
        // would desync a viewer's local screen from the host's without actually leaving the
        // session.
        if (!watchAlong.IsViewing)
        {
            var stopRect = new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + 38f * scale));
            var canStop = queue.Current is not null || video.State != VideoPlaybackState.Idle;
            if (SmallButton(stopRect, Loc.T(L.AetherStream.Stop), canStop, scale, danger: true) && canStop)
            {
                queue.Clear();
            }
        }
    }

    private float DrawPendingRequests(Rect content, float bodyTop, float scale)
    {
        Typography.Draw(new Vector2(content.Min.X, bodyTop), Loc.T(L.AetherStream.CastingPendingRequestsHeader),
            ui.TitleInk, TextStyles.FootnoteEmphasized);
        bodyTop += 22f * scale;

        var rowHeight = 40f * scale;
        foreach (var request in watchAlong.PendingRequests)
        {
            var rowRect = new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + rowHeight));
            DrawPendingRequestRow(rowRect, request, scale);
            bodyTop = rowRect.Max.Y + 6f * scale;
        }

        return bodyTop;
    }

    private void DrawPendingRequestRow(Rect rect, PendingJoinRequest request, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, 8f * scale, ImGui.GetColorU32(ui.FieldSurface));

        var denyRect = new Rect(new Vector2(rect.Max.X - 76f * scale, rect.Min.Y + 5f * scale),
            new Vector2(rect.Max.X - 6f * scale, rect.Max.Y - 5f * scale));
        var approveRect = new Rect(new Vector2(denyRect.Min.X - 76f * scale, rect.Min.Y + 5f * scale),
            new Vector2(denyRect.Min.X - 6f * scale, rect.Max.Y - 5f * scale));

        // DisplayName only - the server never carries real name/world on stream.joinRequest (see
        // PendingJoinRequest's doc comment), so a Name/World caption line here would always
        // render empty. Single line, vertically centered instead of stacked with a second row.
        var textLeft = rect.Min.X + 10f * scale;
        var textWidth = approveRect.Min.X - 8f * scale - textLeft;
        Typography.Draw(new Vector2(textLeft, rect.Center.Y - 8f * scale), Ellipsize(request.DisplayName, textWidth),
            ui.TitleInk, TextStyles.Body);

        if (SmallButton(denyRect, Loc.T(L.AetherStream.CastingDeny), true, scale, danger: true))
        {
            watchAlong.DenyRequest(request.UserId);
        }

        if (SmallButton(approveRect, Loc.T(L.AetherStream.CastingApprove), true, scale))
        {
            watchAlong.ApproveRequest(request.UserId);
        }
    }

    private float DrawScreenPositionHint(Rect content, float bodyTop, float scale)
    {
        Typography.DrawWrappedLeft(new Vector2(content.Min.X, bodyTop), Loc.T(L.AetherStream.CastingScreenPositionHint),
            ui.MutedInk, TextStyles.Footnote, content.Width);
        return bodyTop + 32f * scale;
    }

    // X/Y/Z/Scale fields + named presets, ported from AlphaChannel's ControlWindow.
    // DrawScreenPositionSettings (Voudi, GPL-3.0, tag v1.1.20260725.1088) - raw ImGui widgets
    // rather than a dedicated component since this is a one-off field, same precedent as
    // MarketApp.Detail.cs's alert-threshold InputInt. DragFloat/SliderFloat/SliderAngle instead of
    // upstream's plain InputFloat text boxes - click-and-drag to adjust live is far more fluid than
    // typing a number and pressing enter, and a bounded slider for Scale/Rotate makes the [0.1x, 8x]
    // range and the full turn discoverable instead of a blank text box.
    private float DrawScreenPositionEditor(Rect content, float bodyTop, float scale)
    {
        var pos = screen.Engine.ScreenPosition;
        var yaw = screen.Engine.ScreenYaw;
        var positionScale = screen.Engine.ScreenScale;
        var transformChanged = false;

        ImGui.SetCursorScreenPos(new Vector2(content.Min.X, bodyTop));
        var fieldWidth = (content.Width - 16f * scale) / 3f;
        var anchor = screen.Engine.ScreenSpawnAnchor;
        const float range = VideoEngine.ScreenPositionSliderRange;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, ImGui.GetColorU32(ui.FieldSurface)))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.SetNextItemWidth(fieldWidth);
            transformChanged |= ImGui.SliderFloat("X##aetherstreamScreenPosX", ref pos.X,
                anchor.X - range, anchor.X + range, "%.1f");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(fieldWidth);
            transformChanged |= ImGui.SliderFloat("Y##aetherstreamScreenPosY", ref pos.Y,
                anchor.Y - range, anchor.Y + range, "%.1f");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(fieldWidth);
            transformChanged |= ImGui.SliderFloat("Z##aetherstreamScreenPosZ", ref pos.Z,
                anchor.Z - range, anchor.Z + range, "%.1f");
        }

        bodyTop += ImGui.GetFrameHeightWithSpacing() + 4f * scale;

        ImGui.SetCursorScreenPos(new Vector2(content.Min.X, bodyTop));
        var wideFieldWidth = (content.Width - 8f * scale) * 0.5f;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, ImGui.GetColorU32(ui.FieldSurface)))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.SetNextItemWidth(wideFieldWidth);
            transformChanged |= ImGui.SliderFloat(Loc.T(L.AetherStream.CastingScale) + "##aetherstreamScreenScale",
                ref positionScale, VideoEngine.MinScreenScale, VideoEngine.MaxScreenScale, "%.1fx");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(wideFieldWidth);
            transformChanged |= ImGui.SliderAngle(Loc.T(L.AetherStream.CastingRotate) + "##aetherstreamScreenYaw",
                ref yaw, -360f, 360f);
        }

        if (transformChanged)
        {
            screen.Engine.SetScreenTransform(pos, yaw, positionScale);
        }

        bodyTop += ImGui.GetFrameHeightWithSpacing() + 4f * scale;

        var recenterRect = new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + 32f * scale));
        if (SmallButton(recenterRect, Loc.T(L.AetherStream.CastingRecenter), true, scale))
        {
            screen.Engine.RecenterScreen();
        }

        bodyTop = recenterRect.Max.Y + 10f * scale;

        var presetNameRect = new Rect(new Vector2(content.Min.X, bodyTop),
            new Vector2(content.Min.X + 150f * scale, bodyTop + 32f * scale));
        ImGui.SetCursorScreenPos(presetNameRect.Min);
        ImGui.SetNextItemWidth(presetNameRect.Width);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, ImGui.GetColorU32(ui.FieldSurface)))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##aetherstreamScreenPresetName", Loc.T(L.AetherStream.CastingPresetNameHint),
                ref screenPresetName, 50);
        }

        var savePresetRect = new Rect(new Vector2(presetNameRect.Max.X + 8f * scale, bodyTop),
            new Vector2(content.Max.X, bodyTop + 32f * scale));
        if (SmallButton(savePresetRect, Loc.T(L.AetherStream.CastingSavePreset), !string.IsNullOrWhiteSpace(screenPresetName), scale)
            && !string.IsNullOrWhiteSpace(screenPresetName))
        {
            screen.Engine.SaveScreenPreset(screenPresetName);
            screenPresetName = "";
        }

        bodyTop = savePresetRect.Max.Y + 12f * scale;

        var presets = screen.Engine.GetScreenPresets();
        if (presets.Count == 0)
        {
            return bodyTop;
        }

        Typography.Draw(new Vector2(content.Min.X, bodyTop), Loc.T(L.AetherStream.CastingSavedPresets), ui.MutedInk,
            TextStyles.Caption1);
        bodyTop += 20f * scale;

        foreach (var preset in presets)
        {
            var removeSize = 28f * scale;
            var applyRect = new Rect(new Vector2(content.Min.X, bodyTop),
                new Vector2(content.Max.X - removeSize - 8f * scale, bodyTop + 30f * scale));
            if (SmallButton(applyRect, preset.Name, true, scale))
            {
                screen.Engine.ApplyScreenPreset(preset);
            }

            var removeCenter = new Vector2(content.Max.X - removeSize * 0.5f, bodyTop + 15f * scale);
            if (ui.IconButton(removeCenter, removeSize * 0.5f, FontAwesomeIcon.Times.ToIconString(), theme.Danger,
                    Palette.WithAlpha(theme.Danger, 0.14f), 0.85f))
            {
                screen.Engine.RemoveScreenPreset(preset.Name);
            }

            bodyTop = applyRect.Max.Y + 6f * scale;
        }

        return bodyTop;
    }

    private Vector4 StateColor() => screen.Engine.IsActive
        ? new Vector4(0.4f, 1f, 0.5f, 1f)
        : ui.MutedInk;

    private string StateLabel() => screen.Engine.IsActive
        ? Loc.T(L.AetherStream.CastingStateReady)
        : Loc.T(L.AetherStream.CastingStateNotReady);
}
