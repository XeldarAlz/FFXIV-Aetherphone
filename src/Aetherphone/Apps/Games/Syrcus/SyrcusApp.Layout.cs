using System.Reflection;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Emulation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games.Syrcus;

internal sealed partial class SyrcusApp
{    private void DrawLayoutEditor(in GameContext context)
    {
        var body = context.Body;
        var theme = context.Theme;
        var scale = UiScale.Current;
        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 25f * scale),
            Loc.T(L.Games.LayoutEditor), theme.TextStrong, TextStyles.Title2);
        var hint = Typography.FitText(Loc.T(L.Games.LayoutEditorHint), body.Width - 32f * scale,
            TextStyles.Footnote);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 51f * scale), hint, theme.TextMuted,
            TextStyles.Footnote);

        var previewBounds = new Rect(new Vector2(body.Min.X + 14f * scale, body.Min.Y + 70f * scale),
            new Vector2(body.Max.X - 14f * scale, body.Max.Y - 116f * scale));
        var gameplayBodySize = TargetGameplayBodySize(theme);
        var previewAspect = gameplayBodySize.X / MathF.Max(1f, gameplayBodySize.Y);
        var preview = FitEditorPreview(previewBounds, previewAspect);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, preview.Min, preview.Max, 18f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Surface, 0.92f)));
        Squircle.Stroke(drawList, preview.Min, preview.Max, 18f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, 0.35f)), 1f * scale);

        var previewScale = scale * preview.Width / MathF.Max(1f, gameplayBodySize.X);
        var videoWidth = session is { VideoWidth: > 0 } ? session.VideoWidth : 240;
        var videoHeight = session is { VideoHeight: > 0 } ? session.VideoHeight : 160;
        var videoAspect = session?.VideoAspectRatio ?? videoWidth / (float)Math.Max(1, videoHeight);
        DrawLayoutPreview(preview, videoWidth, videoHeight, videoAspect, theme, previewScale);
        HandleLayoutDrag(preview, videoWidth, videoHeight, videoAspect, previewScale);

        var selectedScale = $"{MathF.Round(CurrentLayout.For(selectedLayoutElement).SafeScale * 100f):0}%";
        var selectedLabel = $"{LayoutElementLabel(selectedLayoutElement)}  ·  {selectedScale}";
        Typography.DrawCentered(new Vector2(body.Center.X, preview.Max.Y + 16f * scale), selectedLabel,
            theme.TextStrong, TextStyles.FootnoteEmphasized);
        var scaler = new Rect(new Vector2(body.Min.X + 34f * scale, preview.Max.Y + 31f * scale),
            new Vector2(body.Max.X - 34f * scale, preview.Max.Y + 61f * scale));
        DrawLayoutScaleStepper(scaler, theme, scale);

        var buttonY = body.Max.Y - 25f * scale;
        if (GameHud.Button(new Vector2(body.Center.X - 72f * scale, buttonY), new Vector2(126f * scale, 32f * scale),
                Loc.T(L.Games.ResetInterface), theme.TextMuted, theme))
        {
            if (LandscapeMode)
            {
                CurrentLayout.ResetLandscape();
            }
            else
            {
                CurrentLayout.Reset();
            }
            selectedLayoutElement = IsNintendoDs
                ? EmulatorLayoutElement.DsTopScreen
                : EmulatorLayoutElement.Screen;
            layoutDirty = true;
            SaveLayoutIfDirty();
        }

        if (GameHud.Button(new Vector2(body.Center.X + 72f * scale, buttonY), new Vector2(126f * scale, 32f * scale),
                Loc.T(L.Games.FinishEditing), Accent, theme))
        {
            FinishLayoutEditing();
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            draggedLayoutElement = null;
            SaveLayoutIfDirty();
        }
    }

    private static Rect FitEditorPreview(Rect bounds, float aspect)
    {
        var width = MathF.Max(1f, bounds.Width);
        var height = width / MathF.Max(0.1f, aspect);
        if (height > bounds.Height)
        {
            height = MathF.Max(1f, bounds.Height);
            width = height * aspect;
        }

        return CenteredRect(bounds.Center, new Vector2(width, height));
    }

    private Vector2 TargetGameplayBodySize(PhoneTheme theme)
    {
        var phoneWidth = PhoneBounds.ClampWidth(configuration.PhoneWidth);
        var zoom = MathF.Max(0.01f, PhoneSizeCatalog.ZoomFor(phoneWidth));
        var portrait = PhoneSizeCatalog.SizeFor(phoneWidth) / zoom;
        var window = LandscapeMode ? new Vector2(portrait.Y, portrait.X) : portrait;
        var width = window.X;
        var height = window.Y;
        if (LandscapeMode)
        {
            height -= theme.RailWidth * 2f;
        }
        else
        {
            width -= theme.RailWidth * 2f;
        }

        width -= theme.BezelThickness * 2f + ShellScreenPainter.ImmersiveInset * 2f;
        height -= theme.BezelThickness * 2f + ShellScreenPainter.ImmersiveInset * 2f;
        return new Vector2(MathF.Max(1f, width), MathF.Max(1f, height));
    }

    private void DrawLayoutPreview(Rect preview, int videoWidth, int videoHeight, float videoAspect,
        PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (IsNintendoDs)
        {
            DrawDsLayoutPreviewScreen(preview, EmulatorLayoutElement.DsTopScreen, Vector2.Zero,
                new Vector2(1f, 0.5f), Loc.T(L.Games.DsTopScreen), theme, scale);
            DrawDsLayoutPreviewScreen(preview, EmulatorLayoutElement.DsBottomScreen, new Vector2(0f, 0.5f),
                Vector2.One, Loc.T(L.Games.DsTouchScreen), theme, scale);
        }
        else
        {
            var screenArea = GameplayScreenArea(preview, scale);
            var screen = CalculateScreenOuter(screenArea, videoWidth, videoHeight, videoAspect, scale, false);
            Squircle.Fill(drawList, screen.Min, screen.Max, 6f * scale,
                ImGui.GetColorU32(new Vector4(0.025f, 0.03f, 0.04f, 1f)));
            Squircle.Stroke(drawList, screen.Min, screen.Max, 6f * scale,
                ImGui.GetColorU32(Accent with { W = 0.22f }), 1f * scale);
            var wrap = video.Wrap;
            if (wrap is not null && session is { VideoWidth: > 0, VideoHeight: > 0 })
            {
                var image = CalculateImageRect(screen, videoWidth, videoHeight, videoAspect, scale,
                    EmulatorVideoFilter.Smooth);
                drawList.AddImage(wrap.Handle, image.Min, image.Max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu);
            }
            else
            {
                Typography.DrawCentered(screen.Center, Loc.T(L.Games.LayoutScreen),
                    new Vector4(1f, 1f, 1f, 0.56f), TextStyles.Caption1);
            }
        }

        _ = DrawControls(preview, theme, scale, CurrentSystem.Controls);
        _ = DrawAnalogControls(preview, theme, scale, false);
        if (CurrentSystem.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            _ = DrawCButtons(preview, theme, scale);
        }
        DrawFastForwardControl(preview, theme, scale, false);

        var selected = LayoutElementRect(selectedLayoutElement, preview, videoWidth, videoHeight, scale,
            videoAspect);
        var expand = new Vector2(4f * scale);
        Squircle.Stroke(drawList, selected.Min - expand, selected.Max + expand, 10f * scale,
            ImGui.GetColorU32(GamePalette.Lighten(Accent, 0.28f)), 2f * scale);
    }

    private void DrawDsLayoutPreviewScreen(Rect preview, EmulatorLayoutElement element, Vector2 uvMin,
        Vector2 uvMax, string placeholder, PhoneTheme theme, float scale)
    {
        var screenArea = GameplayScreenArea(preview, scale);
        var outer = CalculateDsScreenOuter(element, screenArea, scale, false);
        var image = CalculateDsImageRect(outer, scale, EmulatorVideoFilter.Smooth);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, outer.Min, outer.Max, 6f * scale,
            ImGui.GetColorU32(new Vector4(0.025f, 0.03f, 0.04f, 1f)));
        Squircle.Stroke(drawList, outer.Min, outer.Max, 6f * scale,
            ImGui.GetColorU32(Accent with { W = 0.22f }), 1f * scale);

        var wrap = video.Wrap;
        if (wrap is not null && session is { VideoWidth: > 0, VideoHeight: > 0 })
        {
            drawList.AddImage(wrap.Handle, image.Min, image.Max, uvMin, uvMax, 0xFFFFFFFFu);
        }
        else
        {
            Typography.DrawCentered(outer.Center, placeholder,
                new Vector4(1f, 1f, 1f, 0.56f), TextStyles.Caption1);
        }
    }

    private void HandleLayoutDrag(Rect preview, int videoWidth, int videoHeight, float videoAspect, float scale)
    {
        var mouse = ImGui.GetMousePos();
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            ImGui.IsMouseHoveringRect(preview.Min, preview.Max, false))
        {
            for (var index = 0; index < EditorHitOrder.Length; index++)
            {
                var candidate = EditorHitOrder[index];
                if (!IsLayoutElementVisible(candidate))
                {
                    continue;
                }

                var rect = LayoutElementRect(candidate, preview, videoWidth, videoHeight, scale, videoAspect);
                if (!ImGui.IsMouseHoveringRect(rect.Min, rect.Max, false))
                {
                    continue;
                }

                selectedLayoutElement = candidate;
                draggedLayoutElement = candidate;
                layoutDragOffset = mouse - rect.Center;
                break;
            }
        }

        if (draggedLayoutElement is not { } dragged || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            return;
        }

        var currentRect = LayoutElementRect(dragged, preview, videoWidth, videoHeight, scale, videoAspect);
        var dragArea = IsScreenLayoutElement(dragged) ? GameplayScreenArea(preview, scale) : preview;
        var center = ClampCenter(mouse - layoutDragOffset, currentRect.Size * 0.5f, dragArea);
        var element = CurrentLayout.For(dragged);
        element.X = Math.Clamp((center.X - dragArea.Min.X) / MathF.Max(1f, dragArea.Width), 0f, 1f);
        element.Y = Math.Clamp((center.Y - dragArea.Min.Y) / MathF.Max(1f, dragArea.Height), 0f, 1f);
        layoutDirty = true;
    }

    private void DrawLayoutScaleStepper(Rect row, PhoneTheme theme, float scale)
    {
        var element = CurrentLayout.For(selectedLayoutElement);
        var buttonSize = new Vector2(48f, 30f) * scale;
        var minus = CenteredRect(new Vector2(row.Center.X - 72f * scale, row.Center.Y), buttonSize);
        var plus = CenteredRect(new Vector2(row.Center.X + 72f * scale, row.Center.Y), buttonSize);
        if (DrawScaleStepButton("layoutScaleMinus", minus, "-", theme, scale))
        {
            ChangeLayoutScale(element, -5);
        }

        if (DrawScaleStepButton("layoutScalePlus", plus, "+", theme, scale))
        {
            ChangeLayoutScale(element, 5);
        }

        Typography.DrawCentered(row.Center, $"{MathF.Round(element.SafeScale * 100f):0}%", theme.TextStrong,
            TextStyles.Headline);
    }

    private bool DrawScaleStepButton(string id, Rect rect, string label, PhoneTheme theme, float scale)
    {
        ImGui.SetCursorScreenPos(rect.Min);
        var clicked = ImGui.InvisibleButton($"##{id}", rect.Size);
        var hovered = ImGui.IsItemHovered();
        var pressed = ImGui.IsItemActive();
        var fill = pressed ? Accent with { W = 0.82f } :
            hovered ? Accent with { W = 0.58f } : theme.GroupedCard with { W = 0.72f };
        Squircle.Fill(ImGui.GetWindowDrawList(), rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        Squircle.Stroke(ImGui.GetWindowDrawList(), rect.Min, rect.Max, rect.Height * 0.5f,
            ImGui.GetColorU32(hovered ? Accent : theme.Separator), 1f * scale);
        Typography.DrawCentered(rect.Center, label, theme.TextStrong, TextStyles.Title3);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return clicked;
    }

    private void ChangeLayoutScale(EmulatorElementLayout element, int deltaPercent)
    {
        var current = (int)MathF.Round(element.SafeScale * 100f / 5f) * 5;
        var next = Math.Clamp(current + deltaPercent, 50, 200);
        if (next == current)
        {
            return;
        }

        element.Scale = next / 100f;
        layoutDirty = true;
        SaveLayoutIfDirty();
    }

    private static string LayoutElementLabel(EmulatorLayoutElement element) => element switch
    {
        EmulatorLayoutElement.Screen => Loc.T(L.Games.LayoutScreen),
        EmulatorLayoutElement.Dpad => "D-pad",
        EmulatorLayoutElement.A => "A",
        EmulatorLayoutElement.B => "B",
        EmulatorLayoutElement.X => "X",
        EmulatorLayoutElement.Y => "Y",
        EmulatorLayoutElement.L => "L",
        EmulatorLayoutElement.R => "R",
        EmulatorLayoutElement.L2 => "L2 / Z",
        EmulatorLayoutElement.R2 => "R2",
        EmulatorLayoutElement.L3 => "L3",
        EmulatorLayoutElement.R3 => "R3",
        EmulatorLayoutElement.Dpad2 => "Y cursor",
        EmulatorLayoutElement.CUp => "C Up",
        EmulatorLayoutElement.CDown => "C Down",
        EmulatorLayoutElement.CLeft => "C Left",
        EmulatorLayoutElement.CRight => "C Right",
        EmulatorLayoutElement.Select => "Select",
        EmulatorLayoutElement.Start => "Start",
        EmulatorLayoutElement.FastForward => "FF",
        EmulatorLayoutElement.LeftAnalog => Loc.T(L.Games.LeftAnalog),
        EmulatorLayoutElement.RightAnalog => Loc.T(L.Games.RightAnalog),
        EmulatorLayoutElement.DsTopScreen => Loc.T(L.Games.DsTopScreen),
        EmulatorLayoutElement.DsBottomScreen => Loc.T(L.Games.DsTouchScreen),
        _ => string.Empty,
    };

    private bool IsLayoutElementVisible(EmulatorLayoutElement element) => element switch
    {
        EmulatorLayoutElement.Screen => !IsNintendoDs,
        EmulatorLayoutElement.DsTopScreen or EmulatorLayoutElement.DsBottomScreen => IsNintendoDs,
        EmulatorLayoutElement.Dpad or EmulatorLayoutElement.FastForward => true,
        EmulatorLayoutElement.LeftAnalog => HasLeftAnalog,
        EmulatorLayoutElement.RightAnalog => HasRightAnalog,
        EmulatorLayoutElement.Dpad2 => CurrentSystem.InputProfile == EmulatorInputProfile.WonderSwan,
        EmulatorLayoutElement.CUp or EmulatorLayoutElement.CDown or EmulatorLayoutElement.CLeft or
            EmulatorLayoutElement.CRight => CurrentSystem.InputProfile == EmulatorInputProfile.Nintendo64,
        EmulatorLayoutElement.A => (CurrentSystem.Controls & EmulatorButtons.A) != 0,
        EmulatorLayoutElement.B => (CurrentSystem.Controls & EmulatorButtons.B) != 0,
        EmulatorLayoutElement.X => (CurrentSystem.Controls & EmulatorButtons.X) != 0,
        EmulatorLayoutElement.Y => (CurrentSystem.Controls & EmulatorButtons.Y) != 0,
        EmulatorLayoutElement.L => (CurrentSystem.Controls & EmulatorButtons.L) != 0,
        EmulatorLayoutElement.R => (CurrentSystem.Controls & EmulatorButtons.R) != 0,
        EmulatorLayoutElement.L2 => (CurrentSystem.Controls & EmulatorButtons.L2) != 0,
        EmulatorLayoutElement.R2 => (CurrentSystem.Controls & EmulatorButtons.R2) != 0,
        EmulatorLayoutElement.L3 => (CurrentSystem.Controls & EmulatorButtons.L3) != 0,
        EmulatorLayoutElement.R3 => (CurrentSystem.Controls & EmulatorButtons.R3) != 0,
        EmulatorLayoutElement.Select => (CurrentSystem.Controls & EmulatorButtons.Select) != 0,
        EmulatorLayoutElement.Start => (CurrentSystem.Controls & EmulatorButtons.Start) != 0,
        _ => false,
    };

    private void FinishLayoutEditing()
    {
        editingLayout = false;
        draggedLayoutElement = null;
        SaveLayoutIfDirty();
    }

    private void SaveLayoutIfDirty()
    {
        if (!layoutDirty)
        {
            return;
        }

        layoutDirty = false;
        configuration.Save();
    }

}
