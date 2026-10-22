using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private const float IdentityHeight = 84f;
    private const float IdentityTile = 56f;
    private const float IdentityPad = 14f;
    private const float NameFieldHeight = 40f;
    private const float EditBadge = 20f;
    private const float EditBadgeGlyph = 9f;
    private const float HomeCardHeight = 68f;
    private const float HomeTile = 34f;
    private const float HintGap = 8f;
    private const float BarGlyph = 16f;
    private const float BarLabelGap = 8f;
    private const float PlayGlyph = 15f;
    private const float BackHitWidth = 120f;

    private readonly NavBarButton[] editorButtons = new NavBarButton[1];
    private readonly Action leaveEditor;
    private ShortcutEntry? draft;
    private ShortcutEntry? draftBaseline;
    private Guid draftId = Guid.Empty;
    private bool draftPinned;
    private bool baselinePinned;
    private CachedText stepLimitLabel;

    private void StartNewShortcut()
    {
        if (WarnIfFull())
        {
            return;
        }

        var entry = new ShortcutEntry { Tint = HexColor.ToDigits(ShortcutPalette.Wheel[0]) };
        entry.Steps.Add(new ShortcutStep { Kind = ShortcutStepKind.Command });
        BeginDraft(entry, Guid.Empty, true);
    }

    private void StartEditShortcut(ShortcutEntry entry)
    {
        BeginDraft(entry.Copy(), entry.Id, store.IsPinned(entry.Id));
    }

    private void BeginDraft(ShortcutEntry entry, Guid id, bool pinned)
    {
        DiscardUnsavedIcon();
        draft = entry;
        draftId = id;
        draftPinned = pinned;
        draftBaseline = entry.Copy();
        baselinePinned = pinned;
        dragIndex = -1;
        focusStepIndex = -1;
        ResetBlockSprings();
        Push(ShortcutsRoute.Editor, RootTitle());
    }

    private string EditorTitle()
    {
        if (draft is not null && draft.Name.Length > 0)
        {
            return draft.Name;
        }

        return Loc.T(draftId == Guid.Empty ? L.Shortcuts.NewShortcut : L.Shortcuts.EditShortcut);
    }

    private bool HasUnsavedChanges =>
        draft is not null && draftBaseline is not null &&
        (draftPinned != baselinePinned || !ShortcutRules.SameContent(draft, draftBaseline));

    private void DrawEditor(in PhoneContext context, ShortcutsView view)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        var scale = UiScale.Current;
        if (draft is not null)
        {
            using (TabBar.ReserveContent(scale))
            using (var surface = AppSurface.Begin(navBar.Body))
            {
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var cursorY = DrawIdentity(drawList, origin, width, scale);
                cursorY += ShortcutsArt.SectionGap * scale * 0.5f;
                cursorY += CardSectionHeader.Draw(drawList, new Vector2(origin.X, cursorY), width,
                    Loc.T(L.Shortcuts.Steps), ui.TitleInk) + ShortcutsArt.HeaderGap * scale;
                var stepsTop = cursorY;
                cursorY = DrawEditableSteps(drawList, new Vector2(origin.X, cursorY), width, scale);
                if (dragIndex >= 0)
                {
                    surface.CancelDrag();
                }

                UiAnchors.Report("shortcuts.editor.steps",
                    new Rect(new Vector2(origin.X, stepsTop), new Vector2(origin.X + width, cursorY)));
                cursorY += HintGap * scale;
                cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Shortcuts.StepsHint),
                    ui.MutedInk, TextStyles.Footnote, width);
                cursorY = DrawHomeCard(drawList, new Vector2(origin.X, cursorY + ShortcutsArt.SectionGap * scale),
                    width, scale);
                if (draftId != Guid.Empty)
                {
                    cursorY = DrawEditorActions(drawList,
                        new Vector2(origin.X, cursorY + ShortcutsArt.TileGap * scale), width, scale);
                }

                ShortcutsArt.ReserveTo(origin, width, cursorY + ShortcutsArt.BottomPad * scale);
            }
        }

        editorButtons[0] = new NavBarButton(PhoneIcons.Check, Loc.T(L.Shortcuts.Save));
        UiAnchors.Report("shortcuts.editor.save", AppHeader.LargeTitleButtonRect(in navBar, 0, editorButtons.Length));
        UiAnchors.Report("shortcuts.editor.back", new Rect(context.Content.Min,
            new Vector2(context.Content.Min.X + BackHitWidth * scale,
                context.Content.Min.Y + NavBarMetrics.InlineHeight * scale)));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "shortcuts.nav.editor", EditorTitle(),
            NavBarStyle.From(ui), editorButtons, view.BackTitle, leaveEditor);
        if (pressed == 0)
        {
            TrySave();
        }

        if (draft is not null)
        {
            DrawEditorBar(context.Content, scale);
        }
    }

    private float DrawIdentity(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + IdentityHeight * scale));
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Grouped * scale);
        var pad = IdentityPad * scale;
        var tile = IdentityTile * scale;
        var tileCenter = new Vector2(rect.Min.X + pad + tile * 0.5f, rect.Center.Y);
        var tileHalf = new Vector2(tile * 0.5f);
        var tileHovered = UiInteract.Hover(tileCenter - tileHalf, tileCenter + tileHalf);
        var grow = PressFx.Scale(ImGui.GetID("##shortcutIdentityTile"),
            tileHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), PressFx.IconPressedScale);
        ShortcutArt.DrawSurface(drawList, tileCenter, tile * grow, draft!, store.Icon(draft!), scale);
        var badge = EditBadge * scale;
        var badgeCenter = tileCenter + new Vector2(tile * 0.5f - badge * 0.3f, tile * 0.5f - badge * 0.3f);
        drawList.AddCircleFilled(badgeCenter, badge * 0.5f + Metrics.Stroke.Ring * scale,
            ImGui.GetColorU32(theme.GroupedCard), 24);
        drawList.AddCircleFilled(badgeCenter, badge * 0.5f, ImGui.GetColorU32(ui.Accent), 24);
        ProgressRing.CenterIcon(drawList, badgeCenter, FontAwesomeIcon.Pen, new Vector4(1f, 1f, 1f, 1f),
            EditBadgeGlyph * scale);
        UiAnchors.Report("shortcuts.editor.icon", new Rect(tileCenter - tileHalf, tileCenter + tileHalf));
        if (tileHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(tileCenter - tileHalf, tileCenter + tileHalf), Loc.T(L.Shortcuts.Appearance),
                HoverLabelSide.Below);
        }

        if (UiInteract.Click(tileCenter - tileHalf, tileCenter + tileHalf, tileHovered))
        {
            OpenAppearance();
        }

        var fieldLeft = tileCenter.X + tile * 0.5f + pad;
        var fieldHeight = NameFieldHeight * scale;
        var field = new Rect(new Vector2(fieldLeft, rect.Center.Y - fieldHeight * 0.5f),
            new Vector2(rect.Max.X - pad, rect.Center.Y + fieldHeight * 0.5f));
        UiAnchors.Report("shortcuts.editor.name", field);
        SearchBar.Surface(drawList, field, ui.Ink);
        var inset = Metrics.Space.Md * scale;
        ImGui.SetCursorScreenPos(new Vector2(field.Min.X + inset, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(field.Width - inset * 2f);
        var name = draft!.Name;
        Plugin.Fonts.NoticeText(name);
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            if (ImGui.InputTextWithHint("##shortcutName", Loc.T(L.Shortcuts.NameHint), ref name,
                    ShortcutStore.NameMaxLength, ImGuiInputTextFlags.None))
            {
                draft.Name = name;
            }
        }

        return rect.Max.Y;
    }

    private float DrawHomeCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + HomeCardHeight * scale));
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var tile = HomeTile * scale;
        var tileMin = new Vector2(rect.Min.X + pad, rect.Center.Y - tile * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tile * 0.5f), FontAwesomeIcon.Home, AccentRing.Ink,
            tile * 0.46f);
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(rect.Max.X - pad - toggleWidth, rect.Center.Y - toggleHeight * 0.5f);
        var textLeft = tileMin.X + tile + ShortcutsArt.TextGap * scale;
        var textRight = toggleMin.X - ShortcutsArt.TextGap * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var title = Typography.FitText(Loc.T(L.Shortcuts.ShowOnHome), textWidth, TextStyles.BodyEmphasized);
        var body = Typography.FitText(Loc.T(L.Shortcuts.ShowOnHomeBody), textWidth, TextStyles.Footnote);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var bodyHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = rect.Center.Y - (titleHeight + bodyHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), title, ui.TitleInk, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight), body, ui.MutedInk, TextStyles.Footnote);
        var toggled = Toggle.Draw("shortcuts.pin", new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight)),
            draftPinned, theme);
        if (toggled != draftPinned)
        {
            draftPinned = toggled;
        }

        return rect.Max.Y;
    }

    private float DrawEditorActions(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var gap = ShortcutsArt.ActionTileGap * scale;
        var tileWidth = (width - gap * 2f) / 3f;
        var height = ShortcutsArt.ActionTileHeight * scale;
        var duplicate = new Rect(origin, new Vector2(origin.X + tileWidth, origin.Y + height));
        var share = new Rect(new Vector2(duplicate.Max.X + gap, origin.Y),
            new Vector2(duplicate.Max.X + gap + tileWidth, origin.Y + height));
        var delete = new Rect(new Vector2(share.Max.X + gap, origin.Y), new Vector2(origin.X + width, origin.Y + height));
        if (ShortcutsArt.ActionTile(drawList, ui, duplicate, "##shortcutDuplicate", FontAwesomeIcon.Clone,
                Loc.T(L.Shortcuts.Duplicate), ui.Accent, scale))
        {
            DuplicateDraft();
        }

        if (ShortcutsArt.ActionTile(drawList, ui, share, "##shortcutShare", FontAwesomeIcon.ShareSquare,
                Loc.T(L.Shortcuts.Share), ShortcutsArt.LinkTint, scale))
        {
            CopyCode(draft!);
        }

        if (ShortcutsArt.ActionTile(drawList, ui, delete, "##shortcutDelete", FontAwesomeIcon.TrashAlt,
                Loc.T(L.Shortcuts.Delete), theme.Danger, scale))
        {
            AskDelete(draftId, true);
        }

        return origin.Y + height;
    }

    private void DrawEditorBar(Rect area, float scale)
    {
        var capsule = TabBarLayout.FullCapsule(area, scale, true);
        var circle = TabBarLayout.ActionCircle(area, scale);
        var zone = TabBarLayout.Zone(area, scale);
        UiAnchors.Report("shortcuts.editor.add", capsule);
        UiAnchors.Report("shortcuts.editor.run", circle);
        using var layer = ScreenLayer.Begin("shortcuts.editorBar", zone, false);
        UiInteract.HoverOverlay(zone);
        var drawList = ImGui.GetWindowDrawList();
        var backdrop = ui.BackdropColor;
        var full = draft!.Steps.Count >= ShortcutStore.MaxSteps;
        var addHovered = !full && dragIndex < 0 && BarHover(capsule);
        var addGrow = PressFx.Scale(ImGui.GetID("##shortcutAddAction"),
            addHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), PressFx.ControlPressedScale);
        var addDrawn = Scaled(capsule, addGrow);
        Material.ThemedGlass(drawList, addDrawn.Min, addDrawn.Max, addDrawn.Height * 0.5f, scale, backdrop,
            TabBar.GlassOpacity);
        var label = !full ? Loc.T(L.Shortcuts.AddAction)
            : stepLimitLabel.IsCurrent(ShortcutStore.MaxSteps) ? stepLimitLabel.Value
            : stepLimitLabel.Store(ShortcutStore.MaxSteps,
                Loc.T(L.Shortcuts.StepLimitReached, ShortcutStore.MaxSteps));
        var ink = full ? ui.MutedInk : ui.Accent;
        var glyph = BarGlyph * scale;
        var labelWidth = MathF.Max(1f, addDrawn.Width - glyph - BarLabelGap * scale - addDrawn.Height);
        var fitted = Typography.FitText(label, labelWidth, TextStyles.BodyEmphasized);
        var labelSize = Typography.Measure(fitted, TextStyles.BodyEmphasized);
        var contentWidth = (full ? 0f : glyph + BarLabelGap * scale) + labelSize.X;
        var left = addDrawn.Center.X - contentWidth * 0.5f;
        if (!full)
        {
            ProgressRing.CenterIcon(drawList, new Vector2(left + glyph * 0.5f, addDrawn.Center.Y), FontAwesomeIcon.Plus,
                ink, glyph * 0.8f);
            left += glyph + BarLabelGap * scale;
        }

        Typography.Draw(drawList, new Vector2(left, addDrawn.Center.Y - labelSize.Y * 0.5f), fitted, ink,
            TextStyles.BodyEmphasized);
        if (addHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(capsule.Min, capsule.Max, addHovered))
        {
            OpenActionSheet();
        }

        DrawRunCircle(drawList, circle, backdrop, scale);
    }

    private void DrawRunCircle(ImDrawListPtr drawList, Rect circle, Vector4 backdrop, float scale)
    {
        var run = runner.Snapshot();
        var running = run.IsRunning && run.Id == draft!.Id;
        var hovered = dragIndex < 0 && BarHover(circle);
        var grow = PressFx.Scale(ImGui.GetID("##shortcutRunDraft"), hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left),
            PressFx.ControlPressedScale);
        var drawn = Scaled(circle, grow);
        Material.ThemedGlass(drawList, drawn.Min, drawn.Max, drawn.Height * 0.5f, scale, backdrop, TabBar.GlassOpacity);
        if (running)
        {
            ShortcutsArt.RunRing(drawList, drawn.Center, drawn.Height * 0.32f, run.Progress, ui.Accent, scale);
        }
        else
        {
            ProgressRing.CenterIcon(drawList, drawn.Center, FontAwesomeIcon.Play, ui.Accent, PlayGlyph * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(circle, Loc.T(running ? L.Shortcuts.Stop : L.Shortcuts.TestRun), HoverLabelSide.Above);
        if (!UiInteract.Click(circle.Min, circle.Max, hovered, false))
        {
            return;
        }

        if (running)
        {
            UiFeedback.Play(UiSound.Tap);
            runner.Cancel();
            return;
        }

        if (!ShortcutRules.HasRunnableStep(draft!))
        {
            Refuse(ShortcutRules.HasBadLink(draft!) ? ShortcutDraftIssue.BadLink : ShortcutDraftIssue.NoSteps);
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        runner.Run(draft!);
    }

    private static bool BarHover(Rect rect) =>
        !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(rect.Min, rect.Max);

    private static Rect Scaled(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }

    private void TrySave()
    {
        if (draft is null)
        {
            return;
        }

        var issue = ShortcutRules.Check(draft);
        if (issue != ShortcutDraftIssue.None)
        {
            Refuse(issue);
            return;
        }

        draft.Name = draft.Name.Trim();
        ShortcutRules.PruneEmptySteps(draft);
        var target = draftId == Guid.Empty ? null : store.Find(draftId);
        if (target is null)
        {
            if (WarnIfFull())
            {
                return;
            }

            store.Add(draft);
            store.SetPinned(draft.Id, draftPinned);
            draftId = draft.Id;
        }
        else
        {
            var previousIcon = target.IconImage;
            target.CopyFrom(draft);
            store.Save();
            store.SetPinned(target.Id, draftPinned);
            if (previousIcon.Length > 0 && previousIcon != target.IconImage)
            {
                store.ReleaseIcon(previousIcon);
            }
        }

        draftBaseline = null;
        UiFeedback.Play(UiSound.Success);
        router.Pop();
    }

    private void Refuse(ShortcutDraftIssue issue)
    {
        UiFeedback.Play(UiSound.Blocked);
        ShellToast.Show(issue switch
        {
            ShortcutDraftIssue.MissingName => Loc.T(L.Shortcuts.NeedName),
            ShortcutDraftIssue.BadLink => Loc.T(L.Shortcuts.FixLink),
            _ => Loc.T(L.Shortcuts.NeedStep),
        });
    }

    private void LeaveEditor()
    {
        if (!HasUnsavedChanges)
        {
            DiscardDraft();
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Shortcuts.DiscardTitle),
            Message = Loc.T(L.Shortcuts.DiscardMessage),
            ConfirmLabel = Loc.T(L.Shortcuts.Discard),
            CancelLabel = Loc.T(L.Shortcuts.KeepEditing),
            Sheet = true,
            Confirm = DiscardDraft,
        });
    }

    private void DiscardDraft()
    {
        DiscardUnsavedIcon();
        draftBaseline = null;
        dragIndex = -1;
        router.Pop();
    }

    private void DuplicateDraft()
    {
        if (draft is null)
        {
            return;
        }

        var issue = ShortcutRules.Check(draft);
        if (issue != ShortcutDraftIssue.None)
        {
            Refuse(issue);
            return;
        }

        var previousUnsavedIcon = UnsavedIconOf(draft);
        var copy = DuplicateEntry(draft);
        if (copy is null)
        {
            return;
        }

        if (previousUnsavedIcon.Length > 0)
        {
            store.ReleaseIcon(previousUnsavedIcon);
        }

        draft = copy.Copy();
        draftId = copy.Id;
        draftPinned = false;
        draftBaseline = draft.Copy();
        baselinePinned = false;
        ShellToast.Show(Loc.T(L.Shortcuts.AddedToLibrary));
    }
}
