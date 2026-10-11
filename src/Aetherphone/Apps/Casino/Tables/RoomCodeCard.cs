using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class RoomCodeCard
{
    private const long CopiedMilliseconds = 2_000;
    private const float RimAlpha = 0.45f;
    private const float GlowAlpha = 0.10f;

    private readonly string id;
    private readonly string[] letters = new string[CasinoRoomCodes.Length];
    private string lettersOf = string.Empty;
    private string hint = string.Empty;
    private LanguageInfo? hintLanguage;
    private long copiedAtTick;

    public RoomCodeCard(string id)
    {
        this.id = id;
        Array.Fill(letters, string.Empty);
    }

    public void Reset()
    {
        copiedAtTick = 0;
    }

    public void Draw(AppSkin ui, string code, string fallbackToken, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var bottom = Draw(ImGui.GetWindowDrawList(), ui, origin, code, fallbackToken, width, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y));
    }

    public float Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, string code, string fallbackToken,
        float width, float scale)
    {
        var hasCode = code.Length == CasinoRoomCodes.Length;
        var copied = Environment.TickCount64 - copiedAtTick < CopiedMilliseconds;
        var copyLabel = hasCode
            ? Loc.T(copied ? L.Tables.CodeCopied : L.Tables.CodeCopy)
            : Loc.T(copied ? L.Tables.CodeCopied : L.Casino.DoorCopyInvite);
        var copyWidth = Button.WidthFor(copyLabel, ButtonSize.Large);
        var message = hasCode || fallbackToken.Length > 0 ? Hint() : Loc.T(L.Tables.CodePending);
        var labelHeight = Typography.LineHeight(TextStyles.Headline);
        var hintHeight = Typography.MeasureWrappedBlock(message, TextStyles.Subheadline,
            RoomCodeLayout.HintWidthFor(width, scale)).Y;
        var layout = RoomCodeLayout.Compute(origin, width, scale, labelHeight, hintHeight, copyWidth);
        var card = layout.Card;
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, card.Min, card.Max, radius);
        Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.Accent with { W = GlowAlpha }));
        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.Accent with { W = RimAlpha }),
            1.4f * scale);
        Typography.Draw(drawList, layout.Label,
            Typography.FitText(Loc.T(L.Tables.CodeHeading), MathF.Max(1f, layout.Copy.Min.X - layout.Label.X
                - Metrics.Space.Sm * scale), TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        DrawCells(drawList, ui, layout, hasCode ? code : string.Empty, scale);
        Typography.DrawWrappedLeft(layout.Hint, message, ui.BodyInk, TextStyles.Subheadline, layout.HintWidth);
        var copyText = hasCode ? code : fallbackToken;
        if (Button.Draw(drawList, layout.Copy, copyLabel, ui.Ink, copied ? ButtonStyle.Gray : ButtonStyle.Prominent,
                enabled: copyText.Length > 0, id: id))
        {
            ImGui.SetClipboardText(copyText);
            copiedAtTick = Environment.TickCount64;
        }

        return card.Max.Y;
    }

    private void DrawCells(ImDrawListPtr drawList, AppSkin ui, in RoomCodeLayout layout, string code, float scale)
    {
        Refresh(code);
        var fill = ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Secondary));
        var rounding = Metrics.Radius.Md * scale;
        for (var index = 0; index < CasinoRoomCodes.Length; index++)
        {
            var cell = layout.Cell(index);
            Squircle.Fill(drawList, cell.Min, cell.Max, rounding, fill);
            var letter = code.Length == 0 ? "·" : letters[index];
            Typography.DrawCentered(drawList, cell.Center, letter, ui.TitleInk, TextStyles.Title1);
        }
    }

    private void Refresh(string code)
    {
        if (ReferenceEquals(code, lettersOf) || code.Length != CasinoRoomCodes.Length)
        {
            return;
        }

        lettersOf = code;
        for (var index = 0; index < letters.Length; index++)
        {
            letters[index] = code.Substring(index, 1);
        }
    }

    private string Hint()
    {
        if (ReferenceEquals(hintLanguage, Loc.Current) && hint.Length > 0)
        {
            return hint;
        }

        hintLanguage = Loc.Current;
        hint = Loc.T(L.Tables.CodeHint, Loc.T(L.Apps.Casino), Loc.T(L.Casino.TablesTitle),
            Loc.T(L.Tables.JoinAction));
        return hint;
    }
}
