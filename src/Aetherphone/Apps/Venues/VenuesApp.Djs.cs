using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float DjRowHeight = 60f;
    private const float DjAvatar = 40f;
    private const float DjRing = 2f;
    private const float DjRingGap = 2.5f;

    private static readonly Vector4 DjInitialInk = new(1f, 1f, 1f, 0.92f);

    private readonly List<VenueDj> detailDjs = new();
    private readonly List<string> detailDjMeta = new();
    private readonly List<string> detailDjInitials = new();

    private void CollectDetailDjs(string venueId)
    {
        detailDjs.Clear();
        detailDjMeta.Clear();
        detailDjInitials.Clear();
        var djs = venues.Djs;
        for (var index = 0; index < djs.Count; index++)
        {
            var dj = djs[index];
            if (!string.Equals(dj.VenueId, venueId, StringComparison.Ordinal))
            {
                continue;
            }

            var viewers = DjViewers(dj);
            detailDjs.Add(dj);
            detailDjMeta.Add(dj.Genres.Count > 0 ? $"{dj.Genres[0]} · {viewers}" : viewers);
            detailDjInitials.Add(VenueLabelCache.InitialOf(dj.Name));
        }

        if (detailDjs.Count == 1)
        {
            detailDjMeta[0] = DjViewers(detailDjs[0]);
        }
    }

    private static string DjViewers(VenueDj dj) =>
        dj.Viewers > 0 ? VenueFormat.Viewers(dj.Viewers) : Loc.T(L.Venues.LiveNowLabel);

    private void DrawDetailDjRow(ImDrawListPtr drawList, Vector2 rowMin, Vector2 rowMax, int index, bool linkable,
        float inset, float scale)
    {
        var dj = detailDjs[index];
        var hasTwitch = linkable && !string.IsNullOrEmpty(dj.TwitchUrl);
        var hovered = hasTwitch && UiInteract.Hover(rowMin, rowMax);
        if (hovered)
        {
            drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(Ink.HoverTint), 8f * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var centerY = rowMin.Y + (rowMax.Y - rowMin.Y) * 0.5f;
        var radius = DjAvatar * scale * 0.5f;
        var ringRadius = radius + DjRingGap * scale;
        var center = new Vector2(rowMin.X + inset + ringRadius, centerY);
        var pulse = 0.6f + 0.4f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircle(center, ringRadius, ImGui.GetColorU32(Palette.WithAlpha(MediaOverlay.LiveGreen, pulse)),
            32, DjRing * scale);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var avatar = images.Get(dj.AvatarUrl);
        if (avatar is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(avatar.Size);
            Squircle.FillImage(drawList, min, max, radius, avatar.Handle, 0xFFFFFFFFu, uv0, uv1);
        }
        else
        {
            Squircle.FillImage(drawList, min, max, radius, artwork.HandleForName(dj.Name), 0xFFFFFFFFu);
            Typography.DrawCentered(drawList, center, detailDjInitials[index], DjInitialInk, radius / (22f * scale),
                FontWeight.Bold);
        }

        var textLeft = center.X + ringRadius + 12f * scale;
        var textRight = rowMax.X - inset - (hasTwitch ? 26f * scale : 0f);
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var nameHeight = Typography.LineHeight(InfoValueStyle);
        var metaHeight = Typography.LineHeight(CaptionStyle);
        var textTop = centerY - (nameHeight + 2f * scale + metaHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(dj.Name, textWidth, InfoValueStyle), Ink.TitleInk, InfoValueStyle);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + 2f * scale),
            Typography.FitText(detailDjMeta[index], textWidth, CaptionStyle), Ink.MutedInk, CaptionStyle);
        if (hasTwitch)
        {
            PhoneIcon.Draw(drawList, new Vector2(rowMax.X - inset - 8f * scale, centerY), PhoneIcons.ExternalLink,
                Ink.AccentLink, 16f * scale);
        }

        if (index < detailDjs.Count - 1)
        {
            FeedCell.Hairline(drawList, textLeft, rowMax.X - inset, rowMax.Y, Ink.Hairline);
        }

        if (hasTwitch && UiInteract.Click(rowMin, rowMax, hovered))
        {
            UrlActions.AskThenOpen(dj.TwitchUrl!);
        }
    }
}
