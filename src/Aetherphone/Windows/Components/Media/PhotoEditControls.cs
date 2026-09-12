using Aetherphone.Core.Media;

namespace Aetherphone.Windows.Components;

internal enum PhotoEditTool : byte
{
    Adjust,
    Looks,
    Crop,
}

internal sealed class PhotoEditControls
{
    public static readonly PhotoAdjustment[] Adjustments =
    {
        PhotoAdjustment.Brightness,
        PhotoAdjustment.Contrast,
        PhotoAdjustment.Saturation,
        PhotoAdjustment.Warmth,
        PhotoAdjustment.Vignette,
        PhotoAdjustment.Straighten,
    };

    public readonly ChipRail AdjustmentRail = new();
    public readonly ChipRail LookRail = new();
    public readonly string[] AdjustmentLabels = new string[Adjustments.Length];
    public readonly bool[] AdjustmentActive = new bool[Adjustments.Length];
    public readonly string[] LookLabels = new string[PhotoLooks.All.Length];
    public readonly bool[] LookActive = new bool[PhotoLooks.All.Length];

    private string valueLabel = string.Empty;
    private float valueLabelFor = float.NaN;
    private PhotoAdjustment valueLabelAdjustment;

    public PhotoEdit Edit { get; private set; } = PhotoEdit.None;

    public PhotoEditTool Tool { get; set; }

    public PhotoAdjustment Adjustment { get; set; }

    public void Load(in PhotoEdit edit)
    {
        Edit = edit;
        valueLabelFor = float.NaN;
    }

    public void Reset()
    {
        Load(PhotoEdit.None);
        Tool = PhotoEditTool.Adjust;
        Adjustment = PhotoAdjustment.Brightness;
    }

    public void Adjust(PhotoAdjustment adjustment, float value)
    {
        Edit = Edit.With(adjustment, value);
    }

    public void SetLook(PhotoLook look)
    {
        Edit = Edit.WithLook(look, Edit.LookStrength);
    }

    public void SetLookStrength(float strength)
    {
        Edit = Edit.WithLook(Edit.Look, strength);
    }

    public void Rotate()
    {
        Edit = Edit.RotatedClockwise();
    }

    public void Flip()
    {
        Edit = Edit.Flipped();
    }

    public string ValueLabel(PhotoAdjustment adjustment, float value, IFormatProvider culture)
    {
        if (adjustment == valueLabelAdjustment && value == valueLabelFor)
        {
            return valueLabel;
        }

        valueLabelAdjustment = adjustment;
        valueLabelFor = value;
        valueLabel = adjustment == PhotoAdjustment.Straighten
            ? value.ToString("+0.0;-0.0;0.0", culture) + "°"
            : MathF.Round(value * 100f).ToString("+0;-0;0", culture);
        return valueLabel;
    }
}
