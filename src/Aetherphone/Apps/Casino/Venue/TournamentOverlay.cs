using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class TournamentOverlay
{
    private const float StripHeight = 44f;
    private const float RowHeight = 44f;
    private const float Pad = 14f;
    private const int MaxRows = 8;

    private readonly TournamentPlayback playback = new();
    private readonly CasinoTextCache texts = new();
    private readonly RollLabels amounts = new();
    private readonly VenueCheer cheer = new();

    private bool expanded;

    public TournamentPlayback Playback => playback;

    public void Reset()
    {
        playback.Reset();
        cheer.Clear();
        expanded = false;
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, CasinoBlackjackRoomStateDto? board, string me)
    {
        var tournament = board?.Tournament;
        playback.Update(tournament, frame.DeltaSeconds, frame.SnapToTruth);
        if (tournament is null || (!tournament.Live && tournament.WinnerUserId.Length == 0))
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var safe = frame.Safe;
        React(stage, frame, me, safe);
        var strip = new Rect(safe.Min, new Vector2(safe.Max.X, safe.Min.Y + StripHeight * scale));
        DrawStrip(drawList, tournament, strip, scale);
        if (UiInteract.Click(strip.Min, strip.Max, UiInteract.Hover(strip.Min, strip.Max)))
        {
            expanded = !expanded;
            CasinoSfx.Play(UiSound.CardSnap);
        }

        if (expanded || (!tournament.Live && tournament.WinnerUserId.Length > 0))
        {
            DrawBoard(drawList, tournament, new Rect(new Vector2(safe.Min.X, strip.Max.Y + 6f * scale), safe.Max), me,
                scale);
        }

        if (playback.NoticeShowing)
        {
            var text = texts.Named(L.Venue.TournamentOut, playback.NoticeName);
            GameBanner.Draw(drawList, new Vector2(safe.Center.X, safe.Min.Y + safe.Height * 0.45f), text,
                CasinoColors.LightB, PhoneTheme.Default, MathF.Max(0.001f, playback.NoticeProgress),
                TextStyles.Title2);
        }

        cheer.Draw(drawList, safe, frame.DeltaSeconds);
    }

    private void React(CasinoStage stage, in CasinoStageFrame frame, string me, Rect safe)
    {
        switch (playback.TakeEvent())
        {
            case TournamentEvent.Eliminated:
                CasinoSfx.Play(UiSound.Bust);
                break;
            case TournamentEvent.Won:
                var winner = texts.Named(L.Venue.TournamentWinner, playback.NoticeName);
                if (me.Length > 0 && string.Equals(playback.WinnerUserId, me, StringComparison.Ordinal))
                {
                    cheer.Show(stage, winner, safe.Center, frame.Instant);
                }
                else
                {
                    CasinoSfx.Play(UiSound.Fanfare);
                    cheer.Show(stage, winner, safe.Center, true);
                }

                break;
        }
    }

    private void DrawStrip(ImDrawListPtr drawList, CasinoBlackjackTournamentDto tournament, Rect strip, float scale)
    {
        var radius = strip.Height * 0.5f;
        Material.LiquidGlass(drawList, strip.Min, strip.Max, radius, scale, GlassTone.Dark, 0f);
        var pad = Pad * scale;
        var left = tournament.Live
            ? texts.Count(L.Venue.HandsLeft, Math.Max(0, tournament.Hands - tournament.HandsPlayed))
            : Loc.T(L.Venue.TournamentOver);
        var leader = Leader(tournament);
        var right = leader is null ? string.Empty : texts.NamedNumber(L.Venue.TournamentLeader, leader.DisplayName,
            leader.Chips);
        var rightWidth = Typography.Measure(right, TextStyles.SubheadlineEmphasized).X;
        var available = strip.Width - pad * 2f;
        rightWidth = MathF.Min(rightWidth, available * 0.55f);
        var lineTop = strip.Center.Y - Typography.LineHeight(TextStyles.SubheadlineEmphasized) * 0.5f;
        Typography.Draw(drawList, new Vector2(strip.Min.X + pad, lineTop),
            Typography.FitText(left, available - rightWidth - pad, TextStyles.SubheadlineEmphasized),
            CasinoColors.InkTitle, TextStyles.SubheadlineEmphasized);
        if (right.Length > 0)
        {
            var fitted = Typography.FitText(right, rightWidth, TextStyles.SubheadlineEmphasized);
            var width = Typography.Measure(fitted, TextStyles.SubheadlineEmphasized).X;
            Typography.Draw(drawList, new Vector2(strip.Max.X - pad - width, lineTop), fitted, CasinoColors.Money,
                TextStyles.SubheadlineEmphasized);
        }
    }

    private void DrawBoard(ImDrawListPtr drawList, CasinoBlackjackTournamentDto tournament, Rect area, string me,
        float scale)
    {
        var standings = tournament.Standings ?? Array.Empty<CasinoBlackjackStandingDto>();
        var rowHeight = RowHeight * scale;
        var count = Math.Min(Math.Min(standings.Length, MaxRows), (int)((area.Height - Pad * scale) / rowHeight));
        if (count <= 0)
        {
            return;
        }

        var pad = Pad * scale;
        var panel = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + rowHeight * count + pad));
        Material.LiquidGlass(drawList, panel.Min, panel.Max, Metrics.Radius.Grouped * scale, scale, GlassTone.Dark,
            0f);
        for (var index = 0; index < count; index++)
        {
            var standing = standings[index];
            var top = panel.Min.Y + pad * 0.5f + index * rowHeight;
            var center = top + rowHeight * 0.5f;
            var mine = me.Length > 0 && string.Equals(standing.UserId, me, StringComparison.Ordinal);
            var ink = standing.Eliminated ? CasinoColors.InkBody : mine ? CasinoColors.MoneyHighlight
                : CasinoColors.InkTitle;
            var place = standing.Eliminated ? Loc.T(L.Venue.TournamentEliminated) : GameNumber.Label(standing.Place);
            var placeWidth = 40f * scale;
            var lineTop = center - Typography.LineHeight(TextStyles.Headline) * 0.5f;
            Typography.Draw(drawList, new Vector2(panel.Min.X + pad, lineTop),
                Typography.FitText(place, placeWidth, TextStyles.Headline), ink, TextStyles.Headline);
            var chips = amounts.Value(standing.Chips);
            var chipsWidth = Typography.Measure(chips, TextStyles.Headline).X;
            Typography.Draw(drawList, new Vector2(panel.Max.X - pad - chipsWidth, lineTop), chips,
                standing.Eliminated ? CasinoColors.InkBody : CasinoColors.Money, TextStyles.Headline);
            var nameLeft = panel.Min.X + pad + placeWidth + 8f * scale;
            VenueArt.LeftLine(drawList, standing.DisplayName, new Vector2(nameLeft, lineTop),
                MathF.Max(1f, panel.Max.X - pad - chipsWidth - 8f * scale - nameLeft), ink, TextStyles.Headline);
        }
    }

    private static CasinoBlackjackStandingDto? Leader(CasinoBlackjackTournamentDto tournament)
    {
        var standings = tournament.Standings;
        if (standings is null)
        {
            return null;
        }

        CasinoBlackjackStandingDto? best = null;
        for (var index = 0; index < standings.Length; index++)
        {
            if (!standings[index].Eliminated && (best is null || standings[index].Chips > best.Chips))
            {
                best = standings[index];
            }
        }

        return best;
    }
}
