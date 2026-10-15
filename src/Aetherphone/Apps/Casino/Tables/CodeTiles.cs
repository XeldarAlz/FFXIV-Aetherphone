using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class CodeTiles
{
    private const string PendingLetter = "·";
    private const float FillAlpha = 0.12f;
    private const float RimAlpha = 0.10f;
    private const float RoundingShare = 0.3f;

    private readonly string[] letters = new string[CasinoRoomCodes.Length];
    private string lettersOf = string.Empty;

    public CodeTiles()
    {
        Array.Fill(letters, PendingLetter);
    }

    public void Draw(ImDrawListPtr drawList, Vector2 origin, float tileWidth, float tileHeight, float gap,
        string code, in TextStyle style, float scale)
    {
        Refresh(code);
        var fill = ImGui.GetColorU32(CasinoArt.White with { W = FillAlpha });
        var rim = ImGui.GetColorU32(CasinoArt.White with { W = RimAlpha });
        var rounding = tileWidth * RoundingShare;
        for (var index = 0; index < letters.Length; index++)
        {
            var min = new Vector2(origin.X + index * (tileWidth + gap), origin.Y);
            var max = new Vector2(min.X + tileWidth, min.Y + tileHeight);
            Squircle.Fill(drawList, min, max, rounding, fill);
            Squircle.Stroke(drawList, min, max, rounding, rim, 1f * scale);
            Typography.DrawCentered(drawList, (min + max) * 0.5f, letters[index], CasinoArt.White, style);
        }
    }

    private void Refresh(string code)
    {
        if (ReferenceEquals(code, lettersOf))
        {
            return;
        }

        lettersOf = code;
        var hasCode = code.Length == CasinoRoomCodes.Length;
        for (var index = 0; index < letters.Length; index++)
        {
            letters[index] = hasCode ? code.Substring(index, 1) : PendingLetter;
        }
    }
}
