using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private const float ImportRowHeight = 52f;
    private const float CopiedSeconds = 2f;

    private ShortcutEntry? importEntry;
    private float copiedClock;

    private void BeginImport()
    {
        if (WarnIfFull())
        {
            return;
        }

        if (!ShortcutCode.TryDecode(ImGui.GetClipboardText(), out var decoded, out var error))
        {
            Warn(ImportErrorText(error));
            return;
        }

        importEntry = decoded;
        router.Push(ShortcutsScreen.Import);
    }

    private static string ImportErrorText(ShortcutCodeError error) => error switch
    {
        ShortcutCodeError.UnsafeLink => Loc.T(L.Shortcuts.ImportUnsafeLink),
        ShortcutCodeError.Malformed => Loc.T(L.Shortcuts.ImportMalformed),
        _ => Loc.T(L.Shortcuts.ImportBadCode),
    };

    private void CopyDraftCode()
    {
        if (draft is null)
        {
            return;
        }

        ImGui.SetClipboardText(ShortcutCode.Encode(draft));
        copiedClock = CopiedSeconds;
    }

    private void DrawImport(Rect content, float scale)
    {
        if (importEntry is null)
        {
            router.Reset();
            return;
        }

        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Shortcuts.ImportShortcut), back);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawImportHero(scale);
            ui.SectionLabel(Loc.T(L.Shortcuts.ImportWillRun), TextStyles.FootnoteEmphasized, 6f);
            DrawImportSteps(scale);
            DrawImportAction(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }
    }

    private void DrawImportHero(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var tile = 66f * scale;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + tile * 0.5f);
        ShortcutArt.DrawSurface(ImGui.GetWindowDrawList(), center, tile, importEntry!, store.Icon(importEntry!), scale);
        Typography.DrawCentered(new Vector2(center.X, origin.Y + tile + 16f * scale),
            ShortcutRunText.Name(importEntry!.Name), ui.TitleInk, TextStyles.Title3);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tile + Metrics.Space.Xl * scale + 12f * scale));
    }

    private void DrawImportSteps(float scale)
    {
        var steps = importEntry!.Steps;
        var card = GroupCard.Begin(theme, steps.Count, ImportRowHeight);
        for (var index = 0; index < steps.Count; index++)
        {
            var row = card.NextRow();
            var step = steps[index];
            Typography.Draw(new Vector2(row.Min.X, row.Center.Y - 16f * scale), KindLabel(step.Kind), ui.HeaderInk,
                TextStyles.Caption1);
            Marquee.DrawLeftAuto("shortcuts.import.step." + index, StepDetail(step), row.Min.X,
                row.Center.Y + 2f * scale, row.Width, TextStyles.Footnote, ui.TitleInk);
        }

        card.End();
    }

    private string StepDetail(ShortcutStep step) => step.Kind switch
    {
        ShortcutStepKind.Wait => Loc.T(L.Shortcuts.WaitSeconds, Seconds(step.Seconds)),
        ShortcutStepKind.OpenPlugin => catalog.DisplayName(step.Text),
        _ => step.Text,
    };

    private void DrawImportAction(float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 38f * scale;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (ui.PillButton(rect, Loc.T(L.Shortcuts.ImportAdd), true))
        {
            CommitImport();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void CommitImport()
    {
        if (importEntry is null || WarnIfFull())
        {
            return;
        }

        store.Add(importEntry);
        importEntry = null;
        router.Pop();
    }
}
