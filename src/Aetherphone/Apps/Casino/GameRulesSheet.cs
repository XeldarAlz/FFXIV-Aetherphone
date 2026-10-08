using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed class GameRulesSheet
{
    private const float PanelHeightShare = 0.78f;
    private const float StepGap = 12f;
    private const float FactRowHeight = 34f;
    private const float FactValueGap = 12f;
    private const float FactStackPadY = 8f;
    private const float FactStackGap = 2f;
    private const float BulletRadius = 11f;
    private const float PlayPillHeight = Button.LargeHeight;
    private const float PlayPillGap = 10f;
    private const float BodyMinHeight = 24f;

    private readonly SheetSurface sheet = new("casino.rules");
    private readonly Action<Rect> drawSheetBody;

    private AppSkin skin = null!;
    private bool playRequested;
    private string gameId = string.Empty;

    public GameRulesSheet()
    {
        drawSheetBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public string GameId => gameId;

    public void Open(string game)
    {
        gameId = game;
        if (sheet.IsOpen)
        {
            return;
        }

        sheet.Open();
        playRequested = false;
    }

    public void Close()
    {
        sheet.Close();
    }

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public bool TakePlayRequest()
    {
        if (!playRequested)
        {
            return false;
        }

        playRequested = false;
        return true;
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        skin = ui;
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(CasinoRules.TitleOf(gameId)), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        var scale = UiScale.Current;
        var pillTop = content.Max.Y - PlayPillHeight * scale;
        var bodyHeight = MathF.Max(BodyMinHeight * scale, pillTop - PlayPillGap * scale - content.Min.Y);
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##casinoRulesBody", new Vector2(content.Width, bodyHeight), false,
                   ImGuiWindowFlags.NoBackground))
        {
            DrawBody(skin, scale);
        }

        var pillRect = new Rect(new Vector2(content.Min.X, pillTop), new Vector2(content.Max.X, content.Max.Y));
        if (!Button.Draw(pillRect, Loc.T(L.Casino.RulesPlay), skin.Ink, ButtonStyle.Prominent, enabled: sheet.IsOpen,
                overlay: true, id: "casino.rules.play"))
        {
            return;
        }

        playRequested = true;
        Close();
    }

    private void DrawBody(AppSkin ui, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();

        var pitch = Loc.T(CasinoRules.PitchOf(gameId));
        var pitchOrigin = ImGui.GetCursorScreenPos();
        var pitchBlock = Typography.MeasureWrappedBlock(pitch, TextStyles.Subheadline, width);
        Typography.DrawWrappedLeft(pitchOrigin, pitch, ui.BodyInk, TextStyles.Subheadline, width);
        ImGui.Dummy(new Vector2(width, pitchBlock.Y + Metrics.Space.Md * scale));

        var headingOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, headingOrigin, Loc.T(L.Casino.RulesHowToPlay), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        ImGui.Dummy(new Vector2(width, 24f * scale));

        var steps = CasinoRules.StepsOf(gameId);
        var textLeft = BulletRadius * 2f + 12f * scale;
        for (var index = 0; index < steps.Length; index++)
        {
            var stepText = Loc.T(steps[index]);
            var stepOrigin = ImGui.GetCursorScreenPos();
            var stepWidth = width - textLeft;
            var block = Typography.MeasureWrappedBlock(stepText, TextStyles.Footnote, stepWidth);
            var bulletCenter = new Vector2(stepOrigin.X + BulletRadius * scale,
                stepOrigin.Y + BulletRadius * scale - 1f * scale);
            drawList.AddCircleFilled(bulletCenter, BulletRadius * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.18f)), 24);
            Typography.DrawCentered(drawList, bulletCenter, Games.Framework.GameNumber.Label(index + 1), ui.Accent,
                TextStyles.Footnote);
            Typography.DrawWrappedLeft(new Vector2(stepOrigin.X + textLeft, stepOrigin.Y), stepText, ui.BodyInk,
                TextStyles.Footnote, stepWidth);
            ImGui.Dummy(new Vector2(width, MathF.Max(block.Y, BulletRadius * 2f * scale) + StepGap * scale));
        }

        ImGui.Dummy(new Vector2(width, Metrics.Space.Sm * scale));
        var factsOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, factsOrigin, Loc.T(L.Casino.RulesNumbers), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        ImGui.Dummy(new Vector2(width, 24f * scale));

        for (var index = 0; CasinoRules.FactsOf(gameId, index, out var label, out var value); index++)
        {
            var rowOrigin = ImGui.GetCursorScreenPos();
            var labelText = Loc.T(label);
            var labelSize = Typography.Measure(labelText, TextStyles.Footnote);
            var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
            var rowHeight = FactRowHeight * scale;
            if (labelSize.X + FactValueGap * scale + valueSize.X <= width)
            {
                var rowCenterY = rowOrigin.Y + rowHeight * 0.5f;
                Typography.Draw(drawList, new Vector2(rowOrigin.X, rowCenterY - labelSize.Y * 0.5f), labelText,
                    ui.BodyInk, TextStyles.Footnote);
                Typography.Draw(drawList,
                    new Vector2(rowOrigin.X + width - valueSize.X, rowCenterY - valueSize.Y * 0.5f),
                    value, ui.TitleInk, TextStyles.SubheadlineEmphasized);
            }
            else
            {
                var labelFitted = Typography.FitText(labelText, width, TextStyles.Footnote);
                var valueLines = Typography.WrapText(value, TextStyles.SubheadlineEmphasized, width);
                var valueLineHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
                var labelTop = rowOrigin.Y + FactStackPadY * scale;
                Typography.Draw(drawList, new Vector2(rowOrigin.X, labelTop), labelFitted, ui.MutedInk,
                    TextStyles.Footnote);
                var valueTop = labelTop + labelSize.Y + FactStackGap * scale;
                for (var lineIndex = 0; lineIndex < valueLines.Length; lineIndex++)
                {
                    var lineSize = Typography.Measure(valueLines[lineIndex], TextStyles.SubheadlineEmphasized);
                    Typography.Draw(drawList,
                        new Vector2(rowOrigin.X + width - lineSize.X, valueTop + lineIndex * valueLineHeight),
                        valueLines[lineIndex], ui.TitleInk, TextStyles.SubheadlineEmphasized);
                }

                rowHeight = valueTop + valueLines.Length * valueLineHeight + FactStackPadY * scale - rowOrigin.Y;
            }

            drawList.AddLine(new Vector2(rowOrigin.X, rowOrigin.Y + rowHeight),
                new Vector2(rowOrigin.X + width, rowOrigin.Y + rowHeight),
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), 1f);
            ImGui.Dummy(new Vector2(width, rowHeight));
        }

        var fairness = Loc.T(L.Casino.RulesFairness);
        ImGui.Dummy(new Vector2(width, Metrics.Space.Md * scale));
        var fairnessOrigin = ImGui.GetCursorScreenPos();
        var fairnessBlock = Typography.MeasureWrappedBlock(fairness, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(fairnessOrigin, fairness, ui.BodyInk,
            TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, fairnessBlock.Y + Metrics.Space.Lg * scale));
    }
}
