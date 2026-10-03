using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Jam;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float JamBannerHeight = 40f;
    private const float JamBannerSpinner = 7f;
    private const float JamCopyRadius = 18f;
    private const float JamCopyGlyphScale = 0.8f;
    private const float JamCardStackRadius = 14f;
    private const float JamNowPlayingHeight = 64f;
    private const float JamEyebrowGlyphScale = 0.7f;
    private const float JamBannerFillAlpha = 0.16f;
    private const float JamCatchUpWidth = 92f;
    private const float JamCatchUpHeight = 28f;

    private JamTextCache jamListeningText;
    private JamTextCache jamHostedByText;
    private JamTextCache jamCodeLabelText;
    private JamTextCache jamNowPlayingLabelText;

    private void DrawJamRoom(float scale)
    {
        DrawJamStatusBanner(scale);
        DrawJamCodeCard(scale);
        DrawJamNowPlayingRow(scale);
        DrawJamReactionBlock(scale);
        if (jam.IsHost && jam.JoinRequests.Length > 0)
        {
            DrawJamRequests(scale);
        }

        DrawJamChat(scale);
        DrawJamMembers(scale);
        if (jam.IsHost)
        {
            DrawJamSettings(scale);
        }

        DrawJamExitButtons(scale);
    }

    private void DrawJamStatusBanner(float scale)
    {
        string label;
        var spinning = false;
        var catchUp = false;
        if (jam.Reconnecting)
        {
            label = Loc.T(L.Music.Reconnecting);
            spinning = true;
        }
        else if (jam.Stale && !jam.IsHost)
        {
            label = Loc.T(L.Music.Jam.Stale);
            spinning = true;
        }
        else if (jam.LocalHold)
        {
            label = Loc.T(L.Music.Jam.LocalHold);
            catchUp = true;
        }
        else
        {
            return;
        }

        var block = BeginJamBlock(JamBannerHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var radius = block.Height * 0.5f;
        Squircle.Fill(drawList, block.Min, block.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, JamBannerFillAlpha)));
        var left = block.Min.X + Metrics.Space.Lg * scale;
        var right = block.Max.X - Metrics.Space.Md * scale;
        if (spinning)
        {
            LoadingPulse.Spinner(new Vector2(left + JamBannerSpinner * scale, block.Center.Y),
                JamBannerSpinner * scale, ui.Accent, 1f, drawList);
            left += (JamBannerSpinner * 2f + Metrics.Space.Sm) * scale;
        }

        if (catchUp)
        {
            var button = new Rect(new Vector2(right - JamCatchUpWidth * scale, block.Center.Y - JamCatchUpHeight * 0.5f * scale),
                new Vector2(right, block.Center.Y + JamCatchUpHeight * 0.5f * scale));
            if (ui.AccentPill(button, Loc.T(L.Music.Jam.CatchUp), true, TextStyles.FootnoteEmphasized))
            {
                playback.TogglePlayPause();
            }

            right = button.Min.X - Metrics.Space.Sm * scale;
        }

        var textHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, block.Center.Y - textHeight * 0.5f),
            Typography.FitText(label, right - left, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        EndJamBlock();
        JamGap(Metrics.Space.Md);
    }

    private void DrawJamCodeCard(float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var codeHeight = Typography.LineHeight(TextStyles.Hero);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var buttonHeight = JamButtonHeight * scale;
        var card = BeginJamBlock(pad + eyebrowHeight + Metrics.Space.Xs * scale + codeHeight + Metrics.Space.Lg * scale
            + titleHeight + subtitleHeight + Metrics.Space.Lg * scale + buttonHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, true);
        Material.TopGlow(drawList, card.Min, card.Max, rounding, ui.Accent, JamGlowCoverage, JamGlowStrength);
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var top = card.Min.Y + pad;

        var glyphCenter = new Vector2(left + Metrics.Space.Sm * scale, top + eyebrowHeight * 0.5f);
        DrawJamGlyph(drawList, glyphCenter, ui.Accent, JamEyebrowGlyphScale);
        var eyebrowLeft = glyphCenter.X + Metrics.Space.Lg * scale;
        var listening = jamListeningText.Format(Loc.T(L.Music.ListeningCount), Math.Max(1, jamMembers.Length));
        var listeningWidth = Typography.Measure(listening, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(right - listeningWidth, top), listening, ui.MutedInk,
            TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(eyebrowLeft, top),
            Typography.FitText(jamCodeLabelText.Upper(Loc.T(L.Music.Jam.CodeLabel)),
                right - listeningWidth - Metrics.Space.Md * scale - eyebrowLeft, TextStyles.FootnoteEmphasized),
            ui.Accent, TextStyles.FootnoteEmphasized);
        top += eyebrowHeight + Metrics.Space.Xs * scale;

        var copyRadius = JamCopyRadius * scale;
        var copyCenter = new Vector2(right - copyRadius, top + codeHeight * 0.5f);
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(jam.DisplayCode, copyCenter.X - copyRadius - Metrics.Space.Md * scale - left,
                TextStyles.Hero), ui.TitleInk, TextStyles.Hero);
        if (jam.DisplayCode.Length > 0 && ui.IconButton(copyCenter, copyRadius, IconGlyph.Of(FontAwesomeIcon.Copy),
                ui.TitleInk, ui.FieldSurface, JamCopyGlyphScale, Loc.T(L.Music.Jam.CopyCode)))
        {
            ImGui.SetClipboardText(jam.DisplayCode);
            ShellToast.Show(Loc.T(L.Music.Jam.CodeCopied));
        }

        top += codeHeight + Metrics.Space.Lg * scale;
        var stackRadius = JamCardStackRadius * scale;
        var stackWidth = JamAvatarStack.Width(jamMembers.Length, JamStackMax, stackRadius);
        var textRight = right - (stackWidth > 0f ? stackWidth + Metrics.Space.Md * scale : 0f);
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(JamTitle, textRight - left,
            TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
        var subtitle = jam.IsHost
            ? Loc.T(L.Music.Jam.HostingYou)
            : jamHostedByText.Format(Loc.T(L.Music.Jam.HostedBy),
                jamHostName.Length > 0 ? jamHostName : Loc.T(L.Music.Jam.SomeoneName));
        Typography.Draw(drawList, new Vector2(left, top + titleHeight),
            Typography.FitText(subtitle, textRight - left, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        if (stackWidth > 0f)
        {
            JamAvatarStack.Draw(drawList, new Vector2(right - stackWidth, top + (titleHeight + subtitleHeight) * 0.5f),
                stackRadius, jamMembers, jamMemberNames, JamStackMax, jamOverflowLabel, ui.Palette.CardFill, theme,
                images, lodestone, scale);
        }

        top += titleHeight + subtitleHeight + Metrics.Space.Lg * scale;
        var gap = Metrics.Space.Sm * scale;
        var shareWidth = MathF.Min((right - left - gap) * 0.4f,
            Typography.Measure(Loc.T(L.Music.Jam.ShareCode), JamButtonStyle).X + buttonHeight);
        var invite = new Rect(new Vector2(left, top), new Vector2(right - shareWidth - gap, top + buttonHeight));
        var share = new Rect(new Vector2(right - shareWidth, top), new Vector2(right, top + buttonHeight));
        if (ui.AccentPill(invite, Loc.T(L.Music.Jam.InviteFriends), true, JamButtonStyle))
        {
            OpenJamInviteSheet();
        }

        if (ui.GhostButton(share, Loc.T(L.Music.Jam.ShareCode)))
        {
            ImGui.SetClipboardText(string.Format(Loc.Culture, Loc.T(L.Music.Jam.ShareText), jam.DisplayCode));
            ShellToast.Show(Loc.T(L.Music.Jam.ShareCopied));
        }

        EndJamBlock();
    }

    private void DrawJamNowPlayingRow(float scale)
    {
        JamGap(Metrics.Space.Md);
        var block = BeginJamBlock(JamNowPlayingHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Md * scale;
        ui.Card(drawList, block.Min, block.Max, rounding);
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var pad = (block.Height - side) * 0.5f;
        var artMin = new Vector2(block.Min.X + pad, block.Min.Y + pad);
        var song = JamNowPlaying;
        var textLeft = artMin.X + side + Metrics.Space.Md * scale;
        var textWidth = block.Max.X - Metrics.Space.Md * scale - textLeft;
        var eyebrowHeight = Typography.LineHeight(TextStyles.Caption2);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        if (song.IsEmpty)
        {
            Squircle.Fill(drawList, artMin, artMin + new Vector2(side, side), side * ArtworkTile.TileRadiusFraction,
                ImGui.GetColorU32(ui.FieldSurface));
            AppSkin.Icon(drawList, artMin + new Vector2(side * 0.5f, side * 0.5f), IconGlyph.Of(FontAwesomeIcon.Music),
                ui.MutedInk, 1f);
            var empty = Loc.T(jam.IsHost ? L.Music.Jam.NothingPlayingHost : L.Music.Jam.NothingPlayingGuest);
            var emptyHeight = Typography.LineHeight(TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, block.Center.Y - emptyHeight * 0.5f),
                Typography.FitText(empty, textWidth, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
            EndJamBlock();
            return;
        }

        ArtworkTile.Draw(drawList, images, artMin, side, song.ThumbnailUrl, song.Title);
        var authorHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = block.Center.Y - (eyebrowHeight + titleHeight + authorHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(jamNowPlayingLabelText.Upper(Loc.T(L.Music.NowPlayingState)), textWidth,
                TextStyles.Caption2), ui.Accent, TextStyles.Caption2);
        top += eyebrowHeight;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(song.Title, textWidth,
            TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        top += titleHeight;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(song.Author, textWidth,
            TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        EndJamBlock();
    }

    private void DrawJamReactionBlock(float scale)
    {
        JamGap(Metrics.Space.Md);
        var block = BeginJamBlock(JamReactionBar.Height * scale);
        var drawList = ImGui.GetWindowDrawList();
        var kind = JamReactionBar.Draw(drawList, block, Material.ToneFor(theme), ui.TitleInk, scale);
        if (kind >= 0)
        {
            jam.React(kind);
        }

        JamReactionBar.DrawRising(drawList, block, jam.Reactions, scale);
        EndJamBlock();
    }

    private void DrawJamExitButtons(float scale)
    {
        JamGap(MusicUi.SectionGap);
        var hosting = jam.IsHost;
        var company = JamOthersCount > 0;
        var buttonHeight = JamButtonHeight * scale;
        var rows = hosting && company ? 2 : 1;
        var block = BeginJamBlock(buttonHeight * rows + Metrics.Space.Sm * scale * (rows - 1));
        var first = new Rect(block.Min, new Vector2(block.Max.X, block.Min.Y + buttonHeight));
        if (!hosting)
        {
            if (ui.DangerGhostButton(first, Loc.T(L.Music.Jam.Leave)))
            {
                jam.Leave();
            }

            EndJamBlock();
            return;
        }

        if (ui.DangerPillButton(first, Loc.T(L.Music.Jam.End)))
        {
            ConfirmEndJam(company);
        }

        if (company)
        {
            var second = new Rect(new Vector2(block.Min.X, first.Max.Y + Metrics.Space.Sm * scale), block.Max);
            if (ui.GhostButton(second, Loc.T(L.Music.Jam.Leave)))
            {
                confirm.Ask(new ConfirmRequest
                {
                    Title = Loc.T(L.Music.Jam.LeaveHostTitle),
                    Message = Loc.T(L.Music.Jam.LeaveHostBody),
                    ConfirmLabel = Loc.T(L.Music.Jam.Leave),
                    CancelLabel = Loc.T(L.Common.Cancel),
                    Sheet = true,
                    Confirm = jam.Leave,
                });
            }
        }

        EndJamBlock();
    }

    private void ConfirmEndJam(bool company)
    {
        if (!company)
        {
            jam.End();
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Music.Jam.EndTitle),
            Message = Loc.T(L.Music.Jam.EndBody),
            ConfirmLabel = Loc.T(L.Music.Jam.End),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = jam.End,
        });
    }
}
