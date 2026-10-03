using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float JamHeroTile = 64f;
    private const float JamHeroGlyphScale = 1.7f;
    private const float JamButtonHeight = 46f;
    private const float JamFieldHeight = 40f;
    private const float JamCodeFieldHeight = 54f;
    private const float JamCodeFieldMaxWidth = 340f;
    private const float JamWaitingHeight = 150f;
    private const float JamSpinnerRadius = 16f;
    private const float JamGlowCoverage = 0.55f;
    private const float JamGlowStrength = 0.22f;
    private const int JamStackMax = 4;

    private static readonly TextStyle JamButtonStyle = TextStyles.Headline;

    private string jamTitleDraft = string.Empty;
    private Vector2 jamBlockOrigin;
    private Vector2 jamBlockSize;

    private void DrawJamLobby(in PhoneContext context)
    {
        var scale = UiScale.Current;
        EnsureJamMembers();
        jam.WantNearby();
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            JamGap(Metrics.Space.Xs);
            switch (jam.Mode)
            {
                case JamMode.Idle:
                    DrawJamIdle(scale);
                    break;
                case JamMode.Starting:
                case JamMode.Joining:
                    DrawJamConnecting(scale);
                    break;
                case JamMode.Pending:
                    DrawJamPending(scale);
                    break;
                default:
                    DrawJamRoom(scale);
                    break;
            }

            JamGap(Metrics.Space.Xxl);
        }

        EndPage(in frame, context, Loc.T(L.Music.Jam.Title));
    }

    private void DrawJamIdle(float scale)
    {
        var signedIn = jamAccount.IsSignedIn;
        var title = Loc.T(L.Music.Jam.HeroTitle);
        var body = Loc.T(signedIn ? L.Music.Jam.HeroBody : L.Music.Jam.SignedOut);
        var pad = Metrics.Space.Xl * scale;
        var innerWidth = ScrollLayout.StableContentWidth() - MusicUi.Inset * 2f * scale - pad * 2f;
        var tile = JamHeroTile * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, innerWidth).Y;
        var fieldHeight = signedIn ? JamFieldHeight * scale + Metrics.Space.Md * scale : 0f;
        var buttonHeight = JamButtonHeight * scale;
        var card = BeginJamBlock(pad + tile + Metrics.Space.Lg * scale + titleHeight + Metrics.Space.Xs * scale
            + bodyHeight + Metrics.Space.Xl * scale + fieldHeight + buttonHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, true);
        Material.TopGlow(drawList, card.Min, card.Max, rounding, ui.Accent, JamGlowCoverage, JamGlowStrength);
        var top = card.Min.Y + pad;
        DrawJamHeroTile(drawList, new Vector2(card.Center.X, top + tile * 0.5f), tile, FontAwesomeIcon.UserFriends);
        top += tile + Metrics.Space.Lg * scale;
        Typography.DrawCentered(drawList, new Vector2(card.Center.X, top + titleHeight * 0.5f),
            Typography.FitText(title, innerWidth, TextStyles.Title2), ui.TitleInk, TextStyles.Title2);
        top += titleHeight + Metrics.Space.Xs * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(card.Center.X, top + bodyHeight * 0.5f), body,
            ui.MutedInk, TextStyles.Subheadline, innerWidth);
        top += bodyHeight + Metrics.Space.Xl * scale;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var submitted = false;
        if (signedIn)
        {
            var field = new Rect(new Vector2(left, top), new Vector2(right, top + JamFieldHeight * scale));
            submitted = SubmitField.Draw(field, "##musicJamTitle", Loc.T(L.Music.Jam.TitleHint), ref jamTitleDraft,
                theme, JamWire.MaxTitleLength, FontAwesomeIcon.Pen);
            top += fieldHeight;
        }

        var button = new Rect(new Vector2(left, top), new Vector2(right, top + buttonHeight));
        if ((ui.AccentPill(button, Loc.T(L.Music.Jam.Start), signedIn, JamButtonStyle) || submitted) && signedIn)
        {
            StartJam();
        }

        EndJamBlock();
        if (!signedIn)
        {
            return;
        }

        SectionHeader.Draw(ui, Loc.T(L.Music.Jam.JoinHeader), false);
        DrawJamCodeJoin(scale);
        DrawJamNearby(scale);
    }

    private void DrawJamCodeJoin(float scale)
    {
        var fieldHeight = JamCodeFieldHeight * scale;
        var buttonHeight = JamButtonHeight * scale;
        var hint = Loc.T(L.Music.Jam.CodeHint);
        var hintWidth = ScrollLayout.StableContentWidth() - MusicUi.Inset * 2f * scale;
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, hintWidth).Y;
        var block = BeginJamBlock(fieldHeight + Metrics.Space.Md * scale + buttonHeight + Metrics.Space.Sm * scale
            + hintHeight);
        var fieldWidth = MathF.Min(block.Width, JamCodeFieldMaxWidth * scale);
        var fieldLeft = block.Center.X - fieldWidth * 0.5f;
        var field = new Rect(new Vector2(fieldLeft, block.Min.Y), new Vector2(fieldLeft + fieldWidth,
            block.Min.Y + fieldHeight));
        var submitted = jamCodeField.Draw("##musicJamCode", field, ui, clock);
        var top = field.Max.Y + Metrics.Space.Md * scale;
        var half = (block.Width - Metrics.Space.Sm * scale) * 0.5f;
        var paste = new Rect(new Vector2(block.Min.X, top), new Vector2(block.Min.X + half, top + buttonHeight));
        var join = new Rect(new Vector2(block.Max.X - half, top), new Vector2(block.Max.X, top + buttonHeight));
        if (ui.GhostButton(paste, Loc.T(L.Music.Jam.Paste)))
        {
            jamCodeField.Set(ImGui.GetClipboardText() ?? string.Empty);
        }

        var complete = jamCodeField.Complete;
        if (ui.AccentPill(join, Loc.T(L.Music.Jam.Join), complete, JamButtonStyle) || (submitted && complete))
        {
            JoinJam(jamCodeField.Code);
        }
        else if (submitted)
        {
            ShellToast.Show(Loc.T(L.Music.Jam.CodeIncomplete));
        }

        var drawList = ImGui.GetWindowDrawList();
        top += buttonHeight + Metrics.Space.Sm * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(block.Center.X, top + hintHeight * 0.5f), hint,
            ui.MutedInk, TextStyles.Footnote, hintWidth);
        EndJamBlock();
    }

    private void DrawJamConnecting(float scale)
    {
        var label = Loc.T(jam.Mode == JamMode.Starting ? L.Music.Jam.Starting : L.Music.Jam.Joining);
        var block = BeginJamBlock(JamWaitingHeight * scale);
        LoadingPulse.Draw(new Vector2(block.Center.X, block.Center.Y - JamSpinnerRadius * scale),
            JamSpinnerRadius * scale, ui.Accent, ui.MutedInk, label);
        EndJamBlock();
        var button = BeginJamBlock(JamButtonHeight * scale);
        if (ui.GhostButton(button, Loc.T(L.Common.Cancel)))
        {
            jam.Leave();
        }

        EndJamBlock();
    }

    private void DrawJamPending(float scale)
    {
        var title = Loc.T(L.Music.Jam.PendingTitle);
        var body = Loc.T(L.Music.Jam.PendingBody);
        var pad = Metrics.Space.Xl * scale;
        var innerWidth = ScrollLayout.StableContentWidth() - MusicUi.Inset * 2f * scale - pad * 2f;
        var spinner = JamSpinnerRadius * 2f * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, innerWidth).Y;
        var buttonHeight = JamButtonHeight * scale;
        var card = BeginJamBlock(pad + spinner + Metrics.Space.Lg * scale + titleHeight + Metrics.Space.Xs * scale
            + bodyHeight + Metrics.Space.Xl * scale + buttonHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale, true);
        var top = card.Min.Y + pad;
        LoadingPulse.Spinner(new Vector2(card.Center.X, top + spinner * 0.5f), JamSpinnerRadius * scale, ui.Accent);
        top += spinner + Metrics.Space.Lg * scale;
        Typography.DrawCentered(drawList, new Vector2(card.Center.X, top + titleHeight * 0.5f),
            Typography.FitText(title, innerWidth, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
        top += titleHeight + Metrics.Space.Xs * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(card.Center.X, top + bodyHeight * 0.5f), body,
            ui.MutedInk, TextStyles.Subheadline, innerWidth);
        top += bodyHeight + Metrics.Space.Xl * scale;
        var button = new Rect(new Vector2(card.Min.X + pad, top), new Vector2(card.Max.X - pad, top + buttonHeight));
        if (ui.DangerGhostButton(button, Loc.T(L.Music.Jam.CancelRequest)))
        {
            jam.Leave();
        }

        EndJamBlock();
    }

    private void DrawJamHeroTile(ImDrawListPtr drawList, Vector2 center, float tile, FontAwesomeIcon icon)
    {
        var half = new Vector2(tile * 0.5f, tile * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        AppSkin.Icon(drawList, center, IconGlyph.Of(icon), AccentRing.Ink, JamHeroGlyphScale);
    }

    private static void JamGap(float pixels) => ImGui.Dummy(new Vector2(0f, pixels * UiScale.Current));

    private Rect BeginJamBlock(float height)
    {
        var pad = MusicUi.Inset * UiScale.Current;
        jamBlockOrigin = ImGui.GetCursorScreenPos();
        jamBlockSize = new Vector2(ScrollLayout.StableContentWidth(), height);
        return new Rect(new Vector2(jamBlockOrigin.X + pad, jamBlockOrigin.Y),
            new Vector2(jamBlockOrigin.X + jamBlockSize.X - pad, jamBlockOrigin.Y + height));
    }

    private void EndJamBlock()
    {
        ImGui.SetCursorScreenPos(jamBlockOrigin);
        ImGui.Dummy(jamBlockSize);
    }
}
