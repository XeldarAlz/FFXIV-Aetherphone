using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Jobs;

internal sealed partial class JobsApp
{
    private const ImGuiWindowFlags HostFlags = ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration |
                                                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                ImGuiWindowFlags.NoMove;

    private const string HexFieldPrefix = "##jobsHexField";
    private const float PickerWidth = 252f;
    private const float PickerRounding = 18f;
    private const float PickerScrim = 0.42f;
    private const float PickerTitleHeight = 18f;
    private const float PickerFieldHeight = 30f;
    private const float PickerButtonWidth = 84f;
    private const float PresetRadius = 12f;
    private const float PresetRowHeight = 32f;
    private const int PresetColumns = 6;
    private const int HexDigitCount = 6;

    private static readonly Vector4 PickerInkOnLight = new(0.08f, 0.08f, 0.10f, 1f);
    private static readonly Vector4 PickerInkOnDark = new(1f, 1f, 1f, 0.96f);

    private readonly record struct ColorPreset
    {
        public ColorPreset(string digits)
        {
            Digits = digits;
            HexColor.TryParse(digits, out var color);
            Color = color;
        }

        public string Digits { get; }

        public Vector4 Color { get; }
    }

    private static readonly ColorPreset[] Presets =
    {
        new("FF453A"), new("FF9F0A"), new("FFD60A"), new("32D74B"), new("2FD4C8"), new("0A84FF"),
        new("5E5CE6"), new("BF5AF2"), new("FF2D92"), new("A2845E"), new("8E8E93"), new("E5E9F0"),
    };

    private Rect colorButtonRect;
    private bool pickerOpen;
    private int pickerOpenedFrame;
    private int pickerSavedIndex = -1;
    private string pickerDigits = string.Empty;
    private string pickerName = string.Empty;
    private string appliedDigits = string.Empty;
    private string hexFieldId = HexFieldPrefix;
    private int hexFieldRevision;
    private bool focusHexField;

    private void OpenColorPicker(int savedIndex)
    {
        pickerSavedIndex = savedIndex;
        var startColor = savedIndex >= 0 &&
                         HexColor.TryParse(configuration.JobsCustomColors[savedIndex].Hex, out var saved)
            ? saved
            : Accent;
        SetPickerDigits(HexColor.ToDigits(startColor));
        pickerName = savedIndex >= 0 ? configuration.JobsCustomColors[savedIndex].Name : string.Empty;
        appliedDigits = string.Empty;
        pickerOpenedFrame = ImGui.GetFrameCount();
        pickerOpen = true;
    }

    private void CloseColorPicker()
    {
        pickerOpen = false;
        pickerSavedIndex = -1;
    }

    private bool PickerClicked() =>
        pickerOpenedFrame != ImGui.GetFrameCount() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);

    private void SetPickerDigits(string digits)
    {
        pickerDigits = digits;
        hexFieldRevision++;
        hexFieldId = HexFieldPrefix + hexFieldRevision.ToString(CultureInfo.InvariantCulture);
        focusHexField = true;
    }

    private void DeleteSavedColor(int index)
    {
        if (index < 0 || index >= configuration.JobsCustomColors.Count)
        {
            return;
        }

        var entry = configuration.JobsCustomColors[index];
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Jobs.DeleteColorConfirm, entry.Name),
            ConfirmLabel = Loc.T(L.Jobs.DeleteColor),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = () =>
            {
                configuration.JobsCustomColors.Remove(entry);
                configuration.Save();
            },
        });
    }

    private void DrawColorPicker(Rect content, float scale)
    {
        var theme = ui.Theme;
        var pad = Metrics.Space.Md * scale;
        var gap = Metrics.Space.Md * scale;
        var tight = Metrics.Space.Sm * scale;
        var width = PickerWidth * scale;
        var titleHeight = PickerTitleHeight * scale;
        var fieldHeight = PickerFieldHeight * scale;
        var presetRows = (Presets.Length + PresetColumns - 1) / PresetColumns;
        var presetsHeight = PresetRowHeight * scale * presetRows;
        var height = pad * 2f + titleHeight + gap + presetsHeight + gap + fieldHeight + tight + fieldHeight + gap +
                     fieldHeight;
        var left = MathF.Max(content.Min.X + tight, colorButtonRect.Max.X - width);
        var min = new Vector2(left, colorButtonRect.Max.Y + tight);
        var max = min + new Vector2(width, height);
        var justOpened = pickerOpenedFrame == ImGui.GetFrameCount();

        var titleTop = min.Y + pad;
        var presetsTop = titleTop + titleHeight + gap;
        var hexTop = presetsTop + presetsHeight + gap;
        var nameTop = hexTop + fieldHeight + tight;
        var buttonTop = nameTop + fieldHeight + gap;
        var swatchRadius = fieldHeight * 0.5f;
        var swatchCenter = new Vector2(min.X + pad + swatchRadius, hexTop + swatchRadius);
        var hexRect = new Rect(new Vector2(swatchCenter.X + swatchRadius + tight, hexTop),
            new Vector2(max.X - pad, hexTop + fieldHeight));
        var nameRect = new Rect(new Vector2(min.X + pad, nameTop), new Vector2(max.X - pad, nameTop + fieldHeight));
        var saveRect = new Rect(new Vector2(max.X - pad - PickerButtonWidth * scale, buttonTop),
            new Vector2(max.X - pad, buttonTop + fieldHeight));

        DrawPickerFieldHosts(hexRect, nameRect, scale);
        if (pickerDigits.Length == HexDigitCount && pickerDigits != appliedDigits)
        {
            appliedDigits = pickerDigits;
            configuration.JobsAccentName = "#" + pickerDigits;
            configuration.Save();
        }

        var drawList = ImGui.GetForegroundDrawList();
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        Material.Veil(drawList, screen.Min, screen.Max, PickerScrim, theme.ScreenRounding * scale);
        PopoverSurface.Draw(drawList, min, max, PickerRounding * scale, theme, scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, titleTop), Loc.T(L.Jobs.BackgroundColor), theme.TextStrong,
            TextStyles.SubheadlineEmphasized);

        var presetsRect = new Rect(new Vector2(min.X + pad, presetsTop), new Vector2(max.X - pad, hexTop - gap));
        var pickedPreset = DrawPresets(drawList, presetsRect, theme, scale);
        if (pickedPreset >= 0)
        {
            SetPickerDigits(Presets[pickedPreset].Digits);
        }

        var validHex = HexColor.TryParse(pickerDigits, out var previewColor);
        drawList.AddCircleFilled(swatchCenter, swatchRadius,
            ImGui.GetColorU32(validHex ? previewColor : Palette.WithAlpha(theme.TextMuted, 0.3f)), 32);
        drawList.AddCircle(swatchCenter, swatchRadius, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.2f)), 32,
            Metrics.Stroke.Hairline * scale);

        DrawPickerField(drawList, hexRect, theme, scale);
        var hexDisplay = "#" + pickerDigits.PadRight(HexDigitCount, '_');
        Typography.Draw(drawList, FieldTextOrigin(hexRect, hexDisplay, TextStyles.BodyEmphasized, scale), hexDisplay,
            theme.TextStrong, TextStyles.BodyEmphasized);

        DrawPickerField(drawList, nameRect, theme, scale);
        var named = pickerName.Length > 0;
        var nameText = Typography.FitText(named ? pickerName : Loc.T(L.Jobs.ColorNamePlaceholder),
            nameRect.Width - Metrics.Space.Md * 2f * scale, TextStyles.Body);
        Typography.Draw(drawList, FieldTextOrigin(nameRect, nameText, TextStyles.Body, scale), nameText,
            named ? theme.TextStrong : theme.TextMuted, TextStyles.Body);

        DrawSaveButton(drawList, saveRect, validHex, previewColor, theme, scale);
        if (justOpened)
        {
            return;
        }

        var clickedOutside = pickedPreset < 0 && PickerClicked() &&
                             !new Rect(min, max).Contains(ImGui.GetMousePos());
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) || ImGui.IsKeyPressed(ImGuiKey.Enter) || clickedOutside)
        {
            CloseColorPicker();
        }
    }

    private void DrawPickerFieldHosts(Rect hexRect, Rect nameRect, float scale)
    {
        var inset = Metrics.Space.Md * scale;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, default(Vector4))
                   .Push(ImGuiCol.FrameBgHovered, default(Vector4))
                   .Push(ImGuiCol.FrameBgActive, default(Vector4))
                   .Push(ImGuiCol.Text, default(Vector4))
                   .Push(ImGuiCol.Border, default(Vector4)))
        {
            ImGui.SetCursorScreenPos(new Vector2(hexRect.Min.X + inset, hexRect.Min.Y));
            using (ImRaii.Child("##jobsHexHost", new Vector2(hexRect.Width - inset, hexRect.Height), false, HostFlags))
            {
                if (focusHexField)
                {
                    focusHexField = false;
                    ImGui.SetKeyboardFocusHere();
                }

                ImGui.SetNextItemWidth(-1f);
                ImGui.InputText(hexFieldId, ref pickerDigits, HexDigitCount,
                    ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase);
            }

            ImGui.SetCursorScreenPos(new Vector2(nameRect.Min.X + inset, nameRect.Min.Y));
            using (ImRaii.Child("##jobsColorNameHost", new Vector2(nameRect.Width - inset, nameRect.Height), false,
                       HostFlags))
            {
                ImGui.SetNextItemWidth(-1f);
                ImGui.InputText("##jobsColorNameField", ref pickerName, 24);
            }
        }
    }

    private int DrawPresets(ImDrawListPtr drawList, Rect area, PhoneTheme theme, float scale)
    {
        var radius = PresetRadius * scale;
        var rowHeight = PresetRowHeight * scale;
        var cellWidth = area.Width / PresetColumns;
        var picked = -1;
        for (var index = 0; index < Presets.Length; index++)
        {
            var center = new Vector2(area.Min.X + cellWidth * (index % PresetColumns + 0.5f),
                area.Min.Y + rowHeight * (index / PresetColumns + 0.5f));
            var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius), center + new Vector2(radius));
            var selected = string.Equals(Presets[index].Digits, pickerDigits, StringComparison.Ordinal);
            drawList.AddCircleFilled(center, hovered ? radius + scale : radius,
                ImGui.GetColorU32(Presets[index].Color), 32);
            if (selected)
            {
                drawList.AddCircle(center, radius + 3f * scale, ImGui.GetColorU32(theme.TextStrong), 32,
                    Metrics.Stroke.Ring * scale);
            }

            if (!hovered)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (PickerClicked())
            {
                picked = index;
            }
        }

        return picked;
    }

    private void DrawSaveButton(ImDrawListPtr drawList, Rect rect, bool enabled, Vector4 color, PhoneTheme theme,
        float scale)
    {
        var editing = pickerSavedIndex >= 0 && pickerSavedIndex < configuration.JobsCustomColors.Count;
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var fill = !enabled
            ? Palette.WithAlpha(theme.TextMuted, 0.2f)
            : hovered
                ? Palette.Mix(color, PickerInkOnDark, 0.14f)
                : color;
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        var label = Loc.T(editing ? L.Jobs.UpdateColor : L.Jobs.SaveColor);
        var ink = enabled
            ? Palette.Luminance(fill) > 0.62f ? PickerInkOnLight : PickerInkOnDark
            : theme.TextMuted;
        Typography.DrawCentered(drawList, rect.Center, label, ink, TextStyles.SubheadlineEmphasized);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!PickerClicked())
        {
            return;
        }

        var name = pickerName.Trim();
        if (name.Length == 0)
        {
            name = "#" + pickerDigits;
        }

        if (editing)
        {
            var entry = configuration.JobsCustomColors[pickerSavedIndex];
            entry.Name = name;
            entry.Hex = "#" + pickerDigits;
        }
        else
        {
            configuration.JobsCustomColors.Add(new JobsCustomColor { Name = name, Hex = "#" + pickerDigits });
        }

        configuration.Save();
        CloseColorPicker();
    }

    private static void DrawPickerField(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float scale)
    {
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Field * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.07f)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, Metrics.Radius.Field * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.1f)), Metrics.Stroke.Hairline * scale);
    }

    private static Vector2 FieldTextOrigin(Rect rect, string text, in TextStyle style, float scale) =>
        new(rect.Min.X + Metrics.Space.Md * scale, rect.Center.Y - Typography.Measure(text, style).Y * 0.5f);
}
