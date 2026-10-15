using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NameplateColorPage : ISettingsPage
{
    private const string FieldId = "##nameplateColorHex";

    private readonly NameplateTitleService titles;
    private readonly NameplateStage stage = new();
    private NameplateColorSlot slot;
    private HsvColor picker;
    private uint applied;
    private string hexDigits = string.Empty;

    public NameplateColorPage(NameplateTitleService titles)
    {
        this.titles = titles;
    }

    public string Title => Loc.T(Label(slot));

    public string Summary => string.Empty;

    public FontAwesomeIcon Icon => FontAwesomeIcon.Palette;

    public Vector4 Tint => NameplateTitlePage.PageTint;

    public bool IsHidden => true;

    public static LocString Label(NameplateColorSlot slot) => slot switch
    {
        NameplateColorSlot.Glow => L.Nameplate.GlowColor,
        NameplateColorSlot.GradientFirst => L.Nameplate.FirstColor,
        NameplateColorSlot.GradientSecond => L.Nameplate.SecondColor,
        _ => L.Nameplate.TextColor,
    };

    public void Show(NameplateColorSlot shownSlot)
    {
        slot = shownSlot;
        applied = titles.Settings.ColorOf(shownSlot);
        var color = new Vector4(NameplatePalette.Unpack(applied), 1f);
        picker = HsvColor.FromRgb(color);
        hexDigits = HexColor.ToDigits(color);
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        var stageBottom = stage.DrawPinned(body, theme, scale, NameplateStage.NameOf(titles),
            titles.Preview with { Look = titles.Settings.CustomLook() }, NameplateStage.CaptionOf(titles));
        using (var surface = AppSurface.Begin(new Rect(new Vector2(body.Min.X, stageBottom), body.Max)))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            var result = ColorField.DrawCard("nameplate.color", picker, theme);
            if (result.Dragging || result.Released)
            {
                surface.CancelDrag();
                Pick(result.Color, result.Released);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            if (HexField.Draw(FieldId, ref hexDigits, picker.ToRgb(), theme, scale))
            {
                Type();
            }
        }
    }

    private void Pick(HsvColor color, bool released)
    {
        picker = color;
        var packed = NameplatePalette.Pack(picker.ToRgb());
        if (packed != applied)
        {
            applied = packed;
            hexDigits = HexColor.ToDigits(picker.ToRgb());
            titles.Settings.SetColor(slot, packed);
        }

        if (released)
        {
            titles.Commit();
        }
    }

    private void Type()
    {
        if (!HexColor.TryParse(hexDigits, out var typed))
        {
            return;
        }

        picker = HsvColor.FromRgb(typed);
        applied = NameplatePalette.Pack(typed);
        titles.Settings.SetColor(slot, applied);
        titles.Commit();
    }
}
