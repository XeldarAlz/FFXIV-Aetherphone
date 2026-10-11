using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class WheelSpotCaption
{
    public const float Gap = 12f;

    public static readonly TextStyle HintStyle = TextStyles.FootnoteEmphasized;
    public static readonly TextStyle ReturnStyle = TextStyles.Footnote;

    public static float HintRoom(float rowWidth, float returnWidth, float scale) =>
        MathF.Max(0f, rowWidth - returnWidth - Gap * scale);

    public static void Draw(ImDrawListPtr drawList, Rect row, string hint, string returns, float scale)
    {
        if (row.Height <= 0f)
        {
            return;
        }

        var returnSize = Typography.Measure(returns, ReturnStyle);
        if (returns.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(row.Max.X - returnSize.X, row.Center.Y - returnSize.Y * 0.5f),
                returns, StageText.Body, ReturnStyle);
        }

        if (hint.Length == 0)
        {
            return;
        }

        var hintSize = Typography.Measure(hint, HintStyle);
        if (!StageText.Fits(hintSize.X, HintRoom(row.Width, returnSize.X, scale)))
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - hintSize.Y * 0.5f), hint, StageText.Strong,
            HintStyle);
    }
}
