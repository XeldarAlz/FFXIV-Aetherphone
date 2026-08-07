using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private const float StepCardHeight = 78f;
    private const float SwatchColumns = 6f;

    private ShortcutEntry? draft;
    private Guid draftId = Guid.Empty;
    private bool draftPinned;
    private string hexBuffer = string.Empty;
    private int pendingStepRemoval = -1;

    private void Warn(string message) => confirm.Alert(null, message, Loc.T(L.Shortcuts.Ok));

    private bool WarnIfFull()
    {
        if (!store.AtCapacity)
        {
            return false;
        }

        Warn(Loc.T(L.Shortcuts.LimitReached, ShortcutStore.MaxShortcuts));
        return true;
    }

    private void StartNewShortcut()
    {
        if (WarnIfFull())
        {
            return;
        }

        draft = new ShortcutEntry { Tint = HexColor.ToDigits(ShortcutPalette.Wheel[0]) };
        draft.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command });
        draftId = Guid.Empty;
        draftPinned = true;
        router.Push(ShortcutsScreen.Editor);
    }

    private void StartEditShortcut(ShortcutEntry entry)
    {
        draft = entry.Copy();
        draftId = entry.Id;
        draftPinned = store.IsPinned(entry.Id);
        router.Push(ShortcutsScreen.Editor);
    }

    private void DrawEditor(Rect content, float scale)
    {
        if (draft is null)
        {
            router.Reset();
            return;
        }

        var context = new PhoneContext(content, theme, navigation);
        var title = draftId == Guid.Empty ? Loc.T(L.Shortcuts.NewShortcut) : Loc.T(L.Shortcuts.EditShortcut);
        AppHeader.Draw(context, title, back);
        var canSave = draft.Name.Trim().Length > 0 && HasRunnableStep(draft);
        if (ui.HeaderAction(content, Loc.T(L.Shortcuts.Save), canSave))
        {
            CommitDraft();
            return;
        }

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawEditorIdentity(scale);
            DrawEditorSteps(scale);
            DrawEditorOptions(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }
    }

    private static bool HasRunnableStep(ShortcutEntry entry)
    {
        var runnable = false;
        for (var index = 0; index < entry.Steps.Count; index++)
        {
            var step = entry.Steps[index];
            if (step.Kind == ShortcutStepKind.OpenUrl)
            {
                if (step.Text.Trim().Length == 0)
                {
                    continue;
                }

                if (!ShortcutCommandText.IsWebUrl(step.Text))
                {
                    return false;
                }

                runnable = true;
                continue;
            }

            if (step.Kind != ShortcutStepKind.Command || step.Text.Trim().Length > 0)
            {
                runnable = true;
            }
        }

        return runnable;
    }

    private void DrawEditorIdentity(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var fieldHeight = Metrics.Size.FieldHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var height = fieldHeight * 2f + gap;
        var tile = height;
        var tileCenter = new Vector2(origin.X + tile * 0.5f, origin.Y + tile * 0.5f);
        ShortcutArt.DrawSurface(ImGui.GetWindowDrawList(), tileCenter, tile, draft!, store.Icon(draft!), scale);
        if (UiInteract.HoverClick(tileCenter - new Vector2(tile * 0.5f), tileCenter + new Vector2(tile * 0.5f)))
        {
            OpenAppearance();
        }

        var fieldLeft = origin.X + tile + Metrics.Space.Md * scale;
        var fieldWidth = origin.X + width - fieldLeft;
        var fieldMin = new Vector2(fieldLeft, origin.Y);
        Squircle.Fill(ImGui.GetWindowDrawList(), fieldMin, new Vector2(fieldLeft + fieldWidth, fieldMin.Y + fieldHeight),
            Metrics.Radius.Field * scale, ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + Metrics.Space.Md * scale,
            fieldMin.Y + fieldHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldWidth - Metrics.Space.Md * 2f * scale);
        var name = draft!.Name;
        Plugin.Fonts.NoticeText(name);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            if (ImGui.InputTextWithHint("##shortcut.name", Loc.T(L.Shortcuts.NameHint), ref name,
                    ShortcutStore.NameMaxLength, ImGuiInputTextFlags.None))
            {
                draft.Name = name;
            }
        }

        var appearanceMin = new Vector2(fieldLeft, origin.Y + fieldHeight + gap);
        var appearanceRect = new Rect(appearanceMin, new Vector2(fieldLeft + fieldWidth, appearanceMin.Y + fieldHeight));
        if (ui.PillButton(appearanceRect, Loc.T(L.Shortcuts.Appearance), false))
        {
            OpenAppearance();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }

    private void DrawEditorSteps(float scale)
    {
        ui.SectionLabel(Loc.T(L.Shortcuts.Steps), TextStyles.FootnoteEmphasized, 6f);
        var steps = draft!.Steps;
        pendingStepRemoval = -1;
        for (var index = 0; index < steps.Count; index++)
        {
            DrawStepCard(steps, index, scale);
        }

        if (pendingStepRemoval >= 0 && pendingStepRemoval < steps.Count)
        {
            steps.RemoveAt(pendingStepRemoval);
            pendingStepRemoval = -1;
        }

        DrawAddStepRow(scale);
    }

    private void DrawStepCard(List<ShortcutStep> steps, int index, float scale)
    {
        var step = steps[index];
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = StepCardHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Md * scale);

        var padding = Metrics.Space.Md * scale;
        var headerY = card.Min.Y + 16f * scale;
        Typography.Draw(drawList, new Vector2(card.Min.X + padding, headerY - 7f * scale), KindLabel(step.Kind),
            ui.HeaderInk, TextStyles.Caption1.Scale, TextStyles.Caption1.Weight);

        var buttonRadius = 12f * scale;
        var deleteCenter = new Vector2(card.Max.X - padding - buttonRadius, headerY);
        var downCenter = new Vector2(deleteCenter.X - buttonRadius * 2.4f, headerY);
        var upCenter = new Vector2(downCenter.X - buttonRadius * 2.4f, headerY);
        if (index > 0 && ui.IconButton(upCenter, buttonRadius, FontAwesomeIcon.ChevronUp.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 0.5f, Loc.T(L.Shortcuts.MoveUp)))
        {
            Swap(steps, index, index - 1);
        }

        if (index < steps.Count - 1 && ui.IconButton(downCenter, buttonRadius,
                FontAwesomeIcon.ChevronDown.ToIconString(), ui.MutedInk, AppSkin.Transparent, 0.5f,
                Loc.T(L.Shortcuts.MoveDown)))
        {
            Swap(steps, index, index + 1);
        }

        if (ui.IconButton(deleteCenter, buttonRadius, FontAwesomeIcon.TrashAlt.ToIconString(), theme.Danger,
                AppSkin.Transparent, 0.5f, Loc.T(L.Shortcuts.RemoveStep)))
        {
            pendingStepRemoval = index;
        }

        var valueTop = card.Min.Y + 34f * scale;
        var valueRect = new Rect(new Vector2(card.Min.X + padding, valueTop),
            new Vector2(card.Max.X - padding, valueTop + Metrics.Size.FieldHeight * scale));
        DrawStepValue(step, index, valueRect, scale);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawStepValue(ShortcutStep step, int index, Rect rect, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (step.Kind == ShortcutStepKind.OpenPlugin)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Field * scale,
                ImGui.GetColorU32(ui.FieldSurface));
            var loaded = catalog.IsLoaded(step.Text);
            var label = catalog.DisplayName(step.Text);
            Marquee.DrawLeftAuto("shortcuts.step.plugin." + index, label, rect.Min.X + Metrics.Space.Md * scale,
                rect.Center.Y - 9f * scale, rect.Width - Metrics.Space.Md * 2f * scale, TextStyles.Body,
                loaded ? ui.TitleInk : theme.Danger);
            return;
        }

        if (step.Kind == ShortcutStepKind.Wait)
        {
            StepperField.Draw(ui, rect, Loc.T(L.Shortcuts.WaitSeconds, Seconds(step.Seconds)), scale,
                () => step.Seconds = MathF.Max(ShortcutRunner.MinWaitSeconds,
                    MathF.Round((step.Seconds - 0.5f) * 10f) / 10f),
                () => step.Seconds = MathF.Min(ShortcutRunner.MaxWaitSeconds,
                    MathF.Round((step.Seconds + 0.5f) * 10f) / 10f));
            return;
        }

        var isLink = step.Kind == ShortcutStepKind.OpenUrl;
        var malformed = isLink && step.Text.Trim().Length > 0 && !ShortcutCommandText.IsWebUrl(step.Text);
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Field * scale, ImGui.GetColorU32(ui.FieldSurface));
        if (malformed)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, Metrics.Radius.Field * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.75f)), 1.4f * scale);
        }

        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + Metrics.Space.Md * scale,
            rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Width - Metrics.Space.Md * 2f * scale);
        var text = step.Text;
        Plugin.Fonts.NoticeText(text);
        var hint = isLink ? Loc.T(L.Shortcuts.UrlHint) : Loc.T(L.Shortcuts.CommandHint);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, malformed ? theme.Danger : ui.TitleInk))
        {
            if (ImGui.InputTextWithHint("##shortcut.step" + index, hint, ref text,
                    ShortcutStore.CommandMaxLength, ImGuiInputTextFlags.None))
            {
                step.Text = text;
            }
        }
    }

    private void DrawAddStepRow(float scale)
    {
        if (draft!.Steps.Count >= ShortcutStore.MaxSteps)
        {
            ui.HelpText(Loc.T(L.Shortcuts.StepLimitReached, ShortcutStore.MaxSteps));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var gap = Metrics.Space.Sm * scale;
        var buttonWidth = (width - gap) * 0.5f;
        var secondRowY = origin.Y + height + gap;

        var commandRect = new Rect(origin, new Vector2(origin.X + buttonWidth, origin.Y + height));
        if (ui.PillButton(commandRect, Loc.T(L.Shortcuts.AddCommand), false))
        {
            draft.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command });
        }

        var waitMin = new Vector2(origin.X + buttonWidth + gap, origin.Y);
        var waitRect = new Rect(waitMin, new Vector2(waitMin.X + buttonWidth, origin.Y + height));
        if (ui.PillButton(waitRect, Loc.T(L.Shortcuts.AddWait), false))
        {
            draft.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Wait, Seconds = 1f });
        }

        var openMin = new Vector2(origin.X, secondRowY);
        var openRect = new Rect(openMin, new Vector2(openMin.X + buttonWidth, secondRowY + height));
        if (ui.PillButton(openRect, Loc.T(L.Shortcuts.AddOpen), false))
        {
            OpenPluginPicker(false);
        }

        var linkMin = new Vector2(origin.X + buttonWidth + gap, secondRowY);
        var linkRect = new Rect(linkMin, new Vector2(linkMin.X + buttonWidth, secondRowY + height));
        if (ui.PillButton(linkRect, Loc.T(L.Shortcuts.AddLink), false))
        {
            draft.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.OpenUrl });
        }

        var pasteMin = new Vector2(origin.X, secondRowY + height + gap);
        var pasteRect = new Rect(pasteMin, new Vector2(origin.X + width, pasteMin.Y + height));
        if (ui.PillButton(pasteRect, Loc.T(L.Shortcuts.PasteMacro), false))
        {
            PasteMacro();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height * 3f + gap * 2f + Metrics.Space.Lg * scale));
        ui.HelpText(Loc.T(L.Shortcuts.StepsHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void PasteMacro()
    {
        var added = ShortcutMacro.Append(ImGui.GetClipboardText(), draft!.Steps, ShortcutStore.MaxSteps,
            out var truncated);
        if (truncated)
        {
            Warn(Loc.T(L.Shortcuts.StepLimitReached, ShortcutStore.MaxSteps));
            return;
        }

        if (added == 0)
        {
            Warn(Loc.T(L.Shortcuts.PasteEmpty));
        }
    }

    private void DrawEditorOptions(float scale)
    {
        ui.SectionLabel(Loc.T(L.Shortcuts.Options), TextStyles.FootnoteEmphasized, 6f);
        var card = GroupCard.Begin(theme, 1, Metrics.Size.Row);
        var row = card.NextRow();
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(row.Max.X - toggleWidth, row.Center.Y - toggleHeight * 0.5f);
        var label = Loc.T(L.Shortcuts.ShowOnHome);
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, ui.TitleInk, TextStyles.Body);
        draftPinned = Toggle.Draw("shortcuts.pin", new Rect(toggleMin,
            toggleMin + new Vector2(toggleWidth, toggleHeight)), draftPinned, theme);
        card.End();

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 38f * scale;
        var testRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (ui.PillButton(testRect, Loc.T(L.Shortcuts.TestRun), false) && HasRunnableStep(draft!))
        {
            runner.Run(draft!);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));

        if (draftId == Guid.Empty)
        {
            return;
        }

        var pairOrigin = ImGui.GetCursorScreenPos();
        var gap = Metrics.Space.Sm * scale;
        var halfWidth = (width - gap) * 0.5f;
        var duplicateRect = new Rect(pairOrigin, new Vector2(pairOrigin.X + halfWidth, pairOrigin.Y + height));
        if (ui.PillButton(duplicateRect, Loc.T(L.Shortcuts.Duplicate), false))
        {
            DuplicateDraft();
            return;
        }

        var shareMin = new Vector2(pairOrigin.X + halfWidth + gap, pairOrigin.Y);
        var shareRect = new Rect(shareMin, new Vector2(shareMin.X + halfWidth, pairOrigin.Y + height));
        if (ui.PillButton(shareRect, Loc.T(copiedClock > 0f ? L.Shortcuts.Copied : L.Shortcuts.Share), false))
        {
            CopyDraftCode();
        }

        ImGui.SetCursorScreenPos(pairOrigin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));

        var deleteOrigin = ImGui.GetCursorScreenPos();
        var deleteRect = new Rect(deleteOrigin, new Vector2(deleteOrigin.X + width, deleteOrigin.Y + height));
        if (ui.DangerGhostButton(deleteRect, Loc.T(L.Shortcuts.DeleteShortcut)))
        {
            AskDeleteDraft();
        }

        ImGui.SetCursorScreenPos(deleteOrigin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void CommitDraft()
    {
        if (draft is null)
        {
            return;
        }

        draft.Name = draft.Name.Trim();
        PruneEmptySteps(draft);
        var target = draftId == Guid.Empty ? null : store.Find(draftId);
        if (target is null)
        {
            store.Add(draft);
            store.SetPinned(draft.Id, draftPinned);
        }
        else
        {
            target.CopyFrom(draft);
            store.Save();
            store.SetPinned(target.Id, draftPinned);
        }

        draft = null;
        draftId = Guid.Empty;
        router.Pop();
    }

    private void DuplicateDraft()
    {
        if (draft is null || WarnIfFull())
        {
            return;
        }

        var copy = draft.Copy();
        copy.Name = CopyName(draft.Name);
        PruneEmptySteps(copy);
        store.Add(copy);
        draft = copy.Copy();
        draftId = copy.Id;
        draftPinned = false;
    }

    private static string CopyName(string name)
    {
        var candidate = Loc.T(L.Shortcuts.CopyName, ShortcutRunText.Name(name).Trim());
        return candidate.Length <= ShortcutStore.NameMaxLength
            ? candidate
            : candidate.Substring(0, ShortcutStore.NameMaxLength);
    }

    private static void PruneEmptySteps(ShortcutEntry entry)
    {
        for (var index = entry.Steps.Count - 1; index >= 0; index--)
        {
            var step = entry.Steps[index];
            if (step.Kind != ShortcutStepKind.Command && step.Kind != ShortcutStepKind.OpenUrl)
            {
                continue;
            }

            if (step.Text.Trim().Length == 0)
            {
                entry.Steps.RemoveAt(index);
                continue;
            }

            step.Text = step.Text.Trim();
        }
    }

    private void AskDeleteDraft()
    {
        var id = draftId;
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Shortcuts.DeleteConfirm),
            ConfirmLabel = Loc.T(L.Shortcuts.Delete),
            CancelLabel = Loc.T(L.Shortcuts.KeepIt),
            Confirm = () => DeleteShortcut(id),
        });
    }

    private void DeleteShortcut(Guid id)
    {
        store.Remove(id);
        draft = null;
        draftId = Guid.Empty;
        router.Reset();
    }

    private static void Swap(List<ShortcutStep> steps, int first, int second)
    {
        (steps[first], steps[second]) = (steps[second], steps[first]);
    }

    private static string KindLabel(ShortcutStepKind kind) => kind switch
    {
        ShortcutStepKind.Wait => Loc.T(L.Shortcuts.KindWait),
        ShortcutStepKind.OpenPlugin => Loc.T(L.Shortcuts.KindOpenPlugin),
        ShortcutStepKind.OpenUrl => Loc.T(L.Shortcuts.KindOpenUrl),
        _ => Loc.T(L.Shortcuts.KindCommand),
    };

    private void OpenAppearance()
    {
        hexBuffer = draft!.Tint;
        router.Push(ShortcutsScreen.Appearance);
    }

    private void DrawAppearance(Rect content, float scale)
    {
        if (draft is null)
        {
            router.Reset();
            return;
        }

        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Shortcuts.Appearance), back);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawAppearancePreview(scale);
            DrawColorSection(scale);
            DrawGlyphSection(draft, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }
    }

    private void DrawAppearancePreview(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var tile = 76f * scale;
        ShortcutArt.DrawSurface(ImGui.GetWindowDrawList(), new Vector2(origin.X + width * 0.5f, origin.Y + tile * 0.5f),
            tile, draft!, store.Icon(draft!), scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tile + Metrics.Space.Lg * scale));
    }

    private void DrawColorSection(float scale)
    {
        ui.SectionLabel(Loc.T(L.Shortcuts.Color), TextStyles.FootnoteEmphasized, 6f);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cell = width / SwatchColumns;
        var rows = (int)MathF.Ceiling(ShortcutPalette.Wheel.Length / SwatchColumns);
        var height = rows * cell;
        var radius = cell * 0.34f;
        var selected = ShortcutTint.Resolve(draft!.Tint);
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < ShortcutPalette.Wheel.Length; index++)
        {
            var color = ShortcutPalette.Wheel[index];
            var column = index % (int)SwatchColumns;
            var row = index / (int)SwatchColumns;
            var center = new Vector2(origin.X + (column + 0.5f) * cell, origin.Y + (row + 0.5f) * cell);
            var active = SameColor(color, selected);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 28);
            if (active)
            {
                drawList.AddCircle(center, radius + 4f * scale, ImGui.GetColorU32(ui.TitleInk), 32, 1.8f * scale);
            }

            var hit = new Vector2(radius + 3f * scale, radius + 3f * scale);
            if (UiInteract.HoverClick(center - hit, center + hit))
            {
                draft.Tint = HexColor.ToDigits(color);
                hexBuffer = draft.Tint;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));

        ui.Field(Loc.T(L.Shortcuts.CustomColor), "##shortcut.hex", ref hexBuffer, 7, false);
        if (HexColor.TryParse(hexBuffer, out var custom))
        {
            draft.Tint = HexColor.ToDigits(custom);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private static bool SameColor(Vector4 first, Vector4 second) =>
        MathF.Abs(first.X - second.X) < 0.004f && MathF.Abs(first.Y - second.Y) < 0.004f &&
        MathF.Abs(first.Z - second.Z) < 0.004f;

    private void DrawGlyphSection(ShortcutEntry entry, float scale)
    {
        ui.SectionLabel(Loc.T(L.Shortcuts.Symbol), TextStyles.FootnoteEmphasized, 6f);
        var usesPluginIcon = entry.IconPlugin.Length > 0;
        DrawIconPluginRow(entry, usesPluginIcon, scale);

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cell = width / SwatchColumns;
        var total = ShortcutPalette.Glyphs.Length + 1;
        var rows = (int)MathF.Ceiling(total / SwatchColumns);
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < total; index++)
        {
            var column = index % (int)SwatchColumns;
            var row = index / (int)SwatchColumns;
            var center = new Vector2(origin.X + (column + 0.5f) * cell, origin.Y + (row + 0.5f) * cell);
            var tile = cell * 0.78f;
            var min = center - new Vector2(tile * 0.5f);
            var max = center + new Vector2(tile * 0.5f);
            var glyph = index == 0 ? 0 : (int)ShortcutPalette.Glyphs[index - 1];
            var active = !usesPluginIcon && entry.Glyph == glyph;
            Squircle.Fill(drawList, min, max, tile * 0.28f,
                ImGui.GetColorU32(active ? Palette.WithAlpha(ui.Accent, 0.32f) : ui.FieldSurface));
            if (active)
            {
                Squircle.Stroke(drawList, min, max, tile * 0.28f, ImGui.GetColorU32(ui.Accent), 1.6f * scale);
            }

            if (index == 0)
            {
                Typography.DrawCentered(drawList, center, ShortcutStore.Monogram(entry.Name), ui.TitleInk,
                    TextStyles.Headline.Scale, TextStyles.Headline.Weight);
            }
            else
            {
                ProgressRing.CenterIcon(drawList, center, ShortcutPalette.Glyphs[index - 1], ui.TitleInk, tile * 0.42f);
            }

            if (UiInteract.HoverClick(min, max))
            {
                entry.Glyph = glyph;
                entry.IconPlugin = string.Empty;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * cell));
    }

    private void DrawIconPluginRow(ShortcutEntry entry, bool usesPluginIcon, float scale)
    {
        var card = GroupCard.Begin(theme, 1, Metrics.Size.Row);
        var value = usesPluginIcon
            ? catalog.DisplayName(entry.IconPlugin)
            : Loc.T(L.Shortcuts.PluginIconNone);
        var picked = SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Shortcuts.PluginIcon), value, theme,
            "shortcuts.iconPlugin");
        card.End();
        if (picked)
        {
            OpenPluginPicker(true);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }
}
