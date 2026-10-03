using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float JamFeatureTile = 36f;
    private const float JamFeatureGlyphScale = 0.95f;
    private const float JamDraftHoldSeconds = 4f;
    private const int JamNoDraft = -1;

    private int jamPermissionsDraft = JamNoDraft;
    private float jamPermissionsDraftAt;
    private int jamApprovalDraft = JamNoDraft;
    private float jamApprovalDraftAt;
    private string jamRenameDraft = string.Empty;
    private string jamRenameSource = string.Empty;

    private void DrawJamSettings(float scale)
    {
        SectionHeader.Draw(ui, Loc.T(L.Music.Jam.SettingsHeader), false);
        DrawJamRename(scale);
        var permissions = ShownJamPermissions();
        var canAdd = JamPermission.Allows(permissions, JamPermission.AddToQueue);
        var canControl = JamPermission.Allows(permissions, JamPermission.ControlPlayback);
        var nextAdd = DrawJamFeature("music.jam.setting.add", FontAwesomeIcon.ListUl, AccentRing.Green,
            Loc.T(L.Music.Jam.GuestsAdd), Loc.T(L.Music.Jam.GuestsAddHint), canAdd, scale);
        var nextControl = DrawJamFeature("music.jam.setting.control", FontAwesomeIcon.Play, AccentRing.Indigo,
            Loc.T(L.Music.Jam.GuestsControl), Loc.T(L.Music.Jam.GuestsControlHint), canControl, scale);
        if (nextAdd != canAdd || nextControl != canControl)
        {
            var updated = (nextAdd ? JamPermission.AddToQueue : JamPermission.None)
                | (nextControl ? JamPermission.ControlPlayback : JamPermission.None);
            jamPermissionsDraft = updated;
            jamPermissionsDraftAt = clock;
            jam.SetGuestPermissions(updated);
        }

        var approval = ShownJamApproval();
        var nextApproval = DrawJamFeature("music.jam.setting.approval", FontAwesomeIcon.UserCheck, AccentRing.Orange,
            Loc.T(L.Music.Jam.Approval), Loc.T(L.Music.Jam.ApprovalHint), approval, scale);
        if (nextApproval != approval)
        {
            jamApprovalDraft = nextApproval ? 1 : 0;
            jamApprovalDraftAt = clock;
            jam.SetApprovalRequired(nextApproval);
        }

        DrawJamDiscoverableSetting(scale);
    }

    private int ShownJamPermissions()
    {
        if (jamPermissionsDraft == JamNoDraft)
        {
            return jam.GuestPermissions;
        }

        if (jamPermissionsDraft == jam.GuestPermissions || clock - jamPermissionsDraftAt > JamDraftHoldSeconds)
        {
            jamPermissionsDraft = JamNoDraft;
            return jam.GuestPermissions;
        }

        return jamPermissionsDraft;
    }

    private bool ShownJamApproval() => ShownJamFlag(ref jamApprovalDraft, jamApprovalDraftAt, jam.ApprovalRequired);

    private bool ShownJamFlag(ref int draft, float draftAt, bool confirmed)
    {
        if (draft == JamNoDraft)
        {
            return confirmed;
        }

        var drafted = draft == 1;
        if (drafted == confirmed || clock - draftAt > JamDraftHoldSeconds)
        {
            draft = JamNoDraft;
            return confirmed;
        }

        return drafted;
    }

    private bool DrawJamFeature(string id, FontAwesomeIcon icon, Vector4 tint, string title, string hint, bool value,
        float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var tile = JamFeatureTile * scale;
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var textWidth = ScrollLayout.StableContentWidth() - MusicUi.Inset * 2f * scale - pad * 3f - tile
            - toggleWidth - Metrics.Space.Md * scale;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, MathF.Max(1f, textWidth)).Y;
        var height = MathF.Max(tile, titleHeight + hintHeight) + pad * 2f;
        var card = BeginJamBlock(height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Md * scale);
        var tileMin = new Vector2(card.Min.X + pad, card.Center.Y - tile * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        AppSkin.Icon(drawList, tileMin + new Vector2(tile * 0.5f, tile * 0.5f), IconGlyph.Of(icon), AccentRing.Ink,
            JamFeatureGlyphScale);
        var textLeft = tileMin.X + tile + pad;
        var top = card.Center.Y - (titleHeight + hintHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(title, textWidth,
            TextStyles.BodyEmphasized), ui.TitleInk, TextStyles.BodyEmphasized);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + titleHeight), hint, ui.MutedInk, TextStyles.Footnote,
            MathF.Max(1f, textWidth));
        var toggle = new Rect(new Vector2(card.Max.X - pad - toggleWidth, card.Center.Y - toggleHeight * 0.5f),
            new Vector2(card.Max.X - pad, card.Center.Y + toggleHeight * 0.5f));
        var result = Toggle.Draw(id, toggle, value, theme);
        EndJamBlock();
        JamGap(Metrics.Space.Sm);
        return result;
    }

    private void DrawJamRename(float scale)
    {
        var title = jam.Title;
        if (!string.Equals(title, jamRenameSource, StringComparison.Ordinal))
        {
            jamRenameSource = title;
            jamRenameDraft = title;
        }

        var pad = Metrics.Space.Md * scale;
        var fieldHeight = JamFieldHeight * scale;
        var saveLabel = Loc.T(L.Music.Jam.Save);
        var saveWidth = Typography.Measure(saveLabel, TextStyles.SubheadlineEmphasized).X + fieldHeight;
        var card = BeginJamBlock(fieldHeight + pad * 2f);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Md * scale);
        var save = new Rect(new Vector2(card.Max.X - pad - saveWidth, card.Min.Y + pad),
            new Vector2(card.Max.X - pad, card.Max.Y - pad));
        var field = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + pad),
            new Vector2(save.Min.X - Metrics.Space.Sm * scale, card.Max.Y - pad));
        var submitted = SubmitField.Draw(field, "##musicJamRename", Loc.T(L.Music.Jam.NameHint), ref jamRenameDraft,
            theme, JamWire.MaxTitleLength, FontAwesomeIcon.Pen);
        var changed = !jamRenameDraft.AsSpan().Trim().Equals(title, StringComparison.Ordinal);
        if ((ui.AccentPill(save, saveLabel, changed, TextStyles.SubheadlineEmphasized) || submitted) && changed)
        {
            jam.Rename(jamRenameDraft);
            ShellToast.Show(Loc.T(L.Music.Jam.Renamed));
        }

        EndJamBlock();
        JamGap(Metrics.Space.Sm);
    }
}
