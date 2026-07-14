using System.Numerics;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private string discoverQuery = string.Empty;
    private string discoverApplied = string.Empty;
    private float discoverDebounce;
    private int discoverIntent;

    private void DrawDiscover(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var pad = Metrics.Space.Lg * scale;
        var searchTop = area.Min.Y + 8f * scale;
        var searchRect = new Rect(new Vector2(area.Min.X + pad, searchTop),
            new Vector2(area.Max.X - pad, searchTop + 36f * scale));
        DrawSearchField(searchRect, ref discoverQuery, "Search people by tag");
        TickDiscoverSearch();

        if (!store.DiscoverLoaded && !store.LoadingDiscover)
        {
            store.RefreshDiscover(discoverIntent, discoverApplied.Trim());
        }

        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y + 8f * scale), area.Max);
        using (AppSurface.Begin(listRect))
        {
            var width = ImGui.GetContentRegionAvail().X;
            DrawIntentFilter(width);
            ImGui.Dummy(new Vector2(0f, 8f * scale));

            var results = store.DiscoverResults;
            if (results.Length == 0)
            {
                var message = store.LoadingDiscover ? "Looking for people..." : "No one here yet.";
                var hint = store.LoadingDiscover ? string.Empty : "Try clearing filters or check back later.";
                Typography.DrawCentered(new Vector2(width * 0.5f + listRect.Min.X, listRect.Min.Y + 90f * scale),
                    message, VelvetTheme.TitleInk, TextStyles.Headline);
                if (hint.Length > 0)
                {
                    Typography.DrawCentered(new Vector2(width * 0.5f + listRect.Min.X, listRect.Min.Y + 116f * scale),
                        hint, VelvetTheme.MutedInk, TextStyles.Subheadline);
                }

                return;
            }

            if (!store.FeedLoaded && !store.LoadingFeed)
            {
                store.RefreshFeed();
            }

            var feed = store.Feed;
            VSectionHeader.Overline("People to meet", results.Length.ToString());
            Gap(8f);
            for (var index = 0; index < results.Length; index++)
            {
                DrawPersonCard(results[index], feed);
                Gap(26f);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawIntentFilter(float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var models = new VChipModel[VelvetIntent.All.Length + 1];
        var allSelected = discoverIntent == 0;
        models[0] = new VChipModel("All", allSelected ? VChipStyle.Solid : VChipStyle.Ghost,
            allSelected ? VelvetTheme.Rose : VelvetTheme.Moonlight);
        for (var index = 0; index < VelvetIntent.All.Length; index++)
        {
            var def = VelvetIntent.All[index];
            var selected = VelvetIntent.Has(discoverIntent, def.Flag);
            models[index + 1] = new VChipModel(def.Label, selected ? VChipStyle.Solid : VChipStyle.Ghost,
                selected ? def.Hue : VelvetTheme.Moonlight, def.Icon);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked == 0)
        {
            discoverIntent = 0;
            store.RefreshDiscover(discoverIntent, discoverApplied.Trim());
        }
        else if (clicked > 0)
        {
            discoverIntent = VelvetIntent.Toggle(discoverIntent, VelvetIntent.All[clicked - 1].Flag);
            store.RefreshDiscover(discoverIntent, discoverApplied.Trim());
        }
    }

    private void DrawPersonCard(VelvetProfileDto profile, VelvetPostDto[] feed)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var name = DisplayNameOf(profile.DisplayName, profile.Handle);
        var region = RegionOf(profile.World);
        var pad = 14f * scale;

        var coverUrl = string.Empty;
        var photoCount = 0;
        for (var index = 0; index < feed.Length; index++)
        {
            if (feed[index].OwnerId != profile.UserId)
            {
                continue;
            }

            if (coverUrl.Length == 0)
            {
                coverUrl = feed[index].MediaUrl;
            }

            photoCount++;
        }

        if (coverUrl.Length == 0)
        {
            coverUrl = profile.AvatarUrl ?? string.Empty;
        }

        var cardHeight = width * 0.82f;
        var card = Reserve(cardHeight / scale);
        var drawList = ImGui.GetWindowDrawList();
        var radius = Metrics.Radius.Card * scale;
        DrawCoverImage(drawList, card.Min, card.Max, coverUrl, radius, name);
        Squircle.Stroke(drawList, card.Min, card.Max, radius, VelvetTheme.CardStroke.Packed(), 1f * scale);
        Squircle.FillVerticalGradient(drawList, new Vector2(card.Min.X, card.Max.Y - card.Height * 0.52f), card.Max,
            radius, new Vector4(0.03f, 0.01f, 0.06f, 0f).Packed(), new Vector4(0.03f, 0.01f, 0.06f, 0.97f).Packed());

        var mask = VelvetIntent.Sanitize(profile.LookingFor);
        var chipX = card.Max.X - pad;
        var drawnChips = 0;
        for (var index = 0; index < VelvetIntent.All.Length && drawnChips < 2; index++)
        {
            var def = VelvetIntent.All[index];
            if ((mask & def.Flag) == 0)
            {
                continue;
            }

            var chipWidth = VChip.Width(def.Label, false, false, scale);
            chipX -= chipWidth;
            VChip.Draw(new Vector2(chipX, card.Min.Y + pad), 26f * scale,
                new VChipModel(def.Label, VChipStyle.Solid, def.Hue), scale);
            chipX -= 6f * scale;
            drawnChips++;
        }

        if (photoCount > 0)
        {
            var badgeText = photoCount + (photoCount == 1 ? " photo" : " photos");
            var badgeWidth = Typography.Measure(badgeText, TextStyles.Footnote).X + 32f * scale;
            var badgeMin = new Vector2(card.Min.X + pad, card.Min.Y + pad);
            var badgeMax = new Vector2(badgeMin.X + badgeWidth, badgeMin.Y + 24f * scale);
            Squircle.Fill(drawList, badgeMin, badgeMax, 12f * scale, new Vector4(0.03f, 0.01f, 0.06f, 0.55f).Packed());
            AppSkin.Icon(new Vector2(badgeMin.X + 13f * scale, (badgeMin.Y + badgeMax.Y) * 0.5f),
                FontAwesomeIcon.Lock.ToIconString(), VelvetTheme.RoseInk, 0.52f);
            Typography.Draw(new Vector2(badgeMin.X + 23f * scale, badgeMin.Y + 5f * scale), badgeText,
                VelvetTheme.OnAccent, TextStyles.Footnote);
        }

        var pillWidth = 104f * scale;
        var pillHeight = 40f * scale;
        var pillRect = new Rect(new Vector2(card.Max.X - pad - pillWidth, card.Max.Y - pad - pillHeight),
            new Vector2(card.Max.X - pad, card.Max.Y - pad));
        var cta = ConnectCta(profile.ConnectionState);
        var pillClicked = false;
        if (cta.Enabled)
        {
            pillClicked = ui.PillButton(pillRect, cta.Label, cta.Filled);
        }
        else
        {
            Squircle.Fill(drawList, pillRect.Min, pillRect.Max, pillHeight * 0.5f,
                new Vector4(0.03f, 0.01f, 0.06f, 0.7f).Packed());
            Typography.DrawCentered(pillRect.Center, cta.Label, VelvetTheme.MutedInk, 0.85f, FontWeight.SemiBold);
        }

        var textLeft = card.Min.X + pad;
        var textWidth = pillRect.Min.X - 10f * scale - textLeft;
        var fittedName = Typography.FitText(name, textWidth - 24f * scale, TextStyles.Title2);
        var nameSize = Typography.Measure(fittedName, TextStyles.Title2);
        var nameY = card.Max.Y - pad - 58f * scale;
        Typography.Draw(new Vector2(textLeft, nameY), fittedName, VelvetTheme.TitleInk, TextStyles.Title2);
        if (profile.Verified)
        {
            DrawVerifiedBadge(drawList, new Vector2(textLeft + nameSize.X + 12f * scale, nameY + nameSize.Y * 0.5f),
                scale);
        }

        Typography.Draw(new Vector2(textLeft, card.Max.Y - pad - 34f * scale),
            SocialIdentity.ProfileMeta(profile.Handle, region), VelvetTheme.BodyInk, TextStyles.Subheadline);
        var lookingFor = mask == 0 ? "Open to anything" : "Looking for " + VelvetIntent.Describe(mask);
        Typography.Draw(new Vector2(textLeft, card.Max.Y - pad - 15f * scale),
            Typography.FitText(lookingFor, textWidth, TextStyles.Footnote), VelvetTheme.RoseInk,
            TextStyles.SubheadlineEmphasized);

        if (!pillClicked && UiInteract.Hover(card.Min, card.Max) && !UiInteract.Hover(pillRect.Min, pillRect.Max) &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            OpenProfile(profile.UserId);
        }

        if (pillClicked && cta.Enabled)
        {
            switch (profile.ConnectionState)
            {
                case VelvetConnectionState.Connected:
                case VelvetConnectionState.IncomingRequest:
                    OpenThread(profile.UserId);
                    break;
                case VelvetConnectionState.None:
                    RequestIntro(profile.UserId, name);
                    break;
            }
        }
    }

    private void DrawCoverImage(ImDrawListPtr drawList, Vector2 min, Vector2 max, string url, float rounding,
        string fallbackName)
    {
        var texture = url.Length > 0 ? images.Get(url) : null;
        if (texture is null)
        {
            drawList.AddRectFilled(min, max, VelvetTheme.PlumWell.Packed(), rounding, ImDrawFlags.RoundCornersAll);
            Squircle.FillVerticalGradient(drawList, min, max, rounding,
                VelvetTheme.Alpha(VelvetTheme.CardHi, 0.6f).Packed(), VelvetTheme.Alpha(VelvetTheme.PlumWell, 0f).Packed());
            var monogram = fallbackName.Length > 0 ? fallbackName[..1].ToUpperInvariant() : "?";
            var monogramCenter = new Vector2((min.X + max.X) * 0.5f, min.Y + (max.Y - min.Y) * 0.40f);
            ProgressRing.Glow(monogramCenter, (max.X - min.X) * 0.22f, VelvetTheme.Alpha(VelvetTheme.Rose, 0.28f), 0.5f);
            Typography.DrawCentered(monogramCenter, monogram, VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.7f),
                TextStyles.LargeTitle);
            return;
        }

        var size = texture.Size;
        var targetAspect = (max.X - min.X) / (max.Y - min.Y);
        var imageAspect = size.Y > 0f ? size.X / size.Y : 1f;
        Vector2 uv0;
        Vector2 uv1;
        if (imageAspect > targetAspect)
        {
            var keep = targetAspect / imageAspect;
            var inset = (1f - keep) * 0.5f;
            uv0 = new Vector2(inset, 0f);
            uv1 = new Vector2(1f - inset, 1f);
        }
        else
        {
            var keep = imageAspect / targetAspect;
            var inset = (1f - keep) * 0.5f;
            uv0 = new Vector2(0f, inset);
            uv1 = new Vector2(1f, 1f - inset);
        }

        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
    }

    private static (string Label, bool Filled, bool Enabled) ConnectCta(int state) =>
        state switch
        {
            VelvetConnectionState.Connected => ("Message", false, true),
            VelvetConnectionState.OutgoingRequest => ("Requested", false, false),
            VelvetConnectionState.IncomingRequest => ("Reply", true, true),
            _ => ("Connect", true, true),
        };

    private void TickDiscoverSearch()
    {
        if (discoverQuery == discoverApplied)
        {
            return;
        }

        discoverDebounce += ImGui.GetIO().DeltaTime;
        if (discoverDebounce < 0.45f)
        {
            return;
        }

        discoverApplied = discoverQuery;
        discoverDebounce = 0f;
        store.RefreshDiscover(discoverIntent, discoverApplied.Trim());
    }

    private void DrawSearchField(Rect rect, ref string value, string hint)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Field * scale, VelvetTheme.PlumWell.Packed());
        AppSkin.Icon(new Vector2(rect.Min.X + 16f * scale, rect.Center.Y), FontAwesomeIcon.Search.ToIconString(),
            VelvetTheme.MutedInk, 0.8f);
        if (value.Length == 0)
        {
            Typography.Draw(new Vector2(rect.Min.X + 30f * scale, rect.Center.Y - 8f * scale), hint, VelvetTheme.Faint,
                TextStyles.Subheadline);
        }

        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + 30f * scale, rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Width - 56f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, VelvetTheme.TitleInk))
        {
            ImGui.InputText("##velvetSearch", ref value, 64, ImGuiInputTextFlags.None);
        }

        if (value.Length > 0 &&
            ui.IconButton(new Vector2(rect.Max.X - 16f * scale, rect.Center.Y), 12f * scale,
                FontAwesomeIcon.Times.ToIconString(), VelvetTheme.MutedInk, AppSkin.Transparent, 0.8f))
        {
            value = string.Empty;
        }
    }

    private string RegionOf(string world)
    {
        if (string.IsNullOrEmpty(world))
        {
            return string.Empty;
        }

        var region = gameData.RegionCodeForWorld(world);
        return region ?? string.Empty;
    }

    private static string DisplayNameOf(string displayName, string handle) =>
        string.IsNullOrWhiteSpace(displayName) ? "@" + handle : displayName;
}
