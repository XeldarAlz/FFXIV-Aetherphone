using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
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
        var rowHeight = 36f * scale;
        var buttonSize = 36f * scale;
        var buttonGap = 8f * scale;
        var searchRect = new Rect(new Vector2(area.Min.X + pad, searchTop),
            new Vector2(area.Max.X - pad - buttonSize - buttonGap, searchTop + rowHeight));
        DrawSearchField(searchRect, ref discoverQuery, Loc.T(L.Velvet.SearchPeopleHint));
        var filterRect = new Rect(new Vector2(area.Max.X - pad - buttonSize, searchTop),
            new Vector2(area.Max.X - pad, searchTop + rowHeight));
        DrawFilterButton(filterRect);
        UiAnchors.Report("velvet.discover.filter", filterRect);
        TickDiscoverSearch();

        if (!store.DiscoverLoaded && !store.LoadingDiscover)
        {
            store.RefreshDiscover(discoverIntent, discoverApplied.Trim());
        }

        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y + 8f * scale), area.Max);
        using (AppSurface.Begin(listRect))
        {
            var width = ImGui.GetContentRegionAvail().X;
            DrawActiveFilters(width);

            var results = store.DiscoverResults;
            if (results.Length == 0)
            {
                var message = store.LoadingDiscover ? Loc.T(L.Velvet.DiscoverLoading) : Loc.T(L.Velvet.DiscoverNone);
                var hint = store.LoadingDiscover ? string.Empty : Loc.T(L.Velvet.DiscoverNoneHint);
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
            VSectionHeader.Overline(Loc.T(L.Velvet.PeopleToMeet), results.Length.ToString(Loc.Culture));
            Gap(8f);
            for (var index = 0; index < results.Length; index++)
            {
                var cardTop = ImGui.GetCursorScreenPos();
                DrawPersonCard(results[index], feed);
                if (index == 0)
                {
                    UiAnchors.Report("velvet.discover.card",
                        new Rect(cardTop, new Vector2(cardTop.X + width, ImGui.GetCursorScreenPos().Y)));
                }

                Gap(26f);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawFilterButton(Rect rect)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var active = VelvetIntent.Sanitize(discoverIntent) != 0;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = Metrics.Radius.Field * scale;
        var fill = active
            ? VelvetTheme.Alpha(VelvetTheme.Rose, hovered ? 0.34f : 0.26f)
            : hovered ? VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.10f) : VelvetTheme.PlumWell;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, fill.Packed());
        if (active)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, VelvetTheme.Alpha(VelvetTheme.Rose, 0.55f).Packed(),
                1f * scale);
        }

        AppSkin.Icon(rect.Center, FontAwesomeIcon.SlidersH.ToIconString(),
            active ? VelvetTheme.RoseInk : VelvetTheme.MutedInk, 0.86f);

        if (active)
        {
            var dotCenter = new Vector2(rect.Max.X - 6f * scale, rect.Min.Y + 6f * scale);
            drawList.AddCircleFilled(dotCenter, 4f * scale, VelvetTheme.RoseBright.Packed(), 16);
            drawList.AddCircle(dotCenter, 4f * scale, VelvetTheme.CardHi.Packed(), 16, 1.4f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                filterSheet.Toggle(rect);
            }
        }
    }

    private void DrawActiveFilters(float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var mask = VelvetIntent.Sanitize(discoverIntent);
        if (mask == 0)
        {
            Gap(6f);
            return;
        }

        var defs = VelvetIntent.All;
        var count = 0;
        for (var index = 0; index < defs.Length; index++)
        {
            if ((mask & defs[index].Flag) != 0)
            {
                count++;
            }
        }

        var models = new VChipModel[count];
        var flags = new int[count];
        var cursor = 0;
        for (var index = 0; index < defs.Length; index++)
        {
            var def = defs[index];
            if ((mask & def.Flag) == 0)
            {
                continue;
            }

            models[cursor] = new VChipModel(Loc.T(def.Label), VChipStyle.Tint, def.Hue, def.Icon, true);
            flags[cursor] = def.Flag;
            cursor++;
        }

        Gap(4f);
        var removed = VChipFlow.Draw(models, width, scale);
        if (removed >= 0)
        {
            discoverIntent = VelvetIntent.Toggle(discoverIntent, flags[removed]);
            store.RefreshDiscover(discoverIntent, discoverApplied.Trim());
        }

        Gap(10f);
    }

    private void DrawDiscoverFilterSheet(Rect screen)
    {
        if (!filterSheet.Open)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var newMask = filterSheet.Draw(screen, scale, discoverIntent);
        if (newMask != discoverIntent)
        {
            discoverIntent = newMask;
            store.RefreshDiscover(discoverIntent, discoverApplied.Trim());
        }
    }

    private void DrawPersonCard(VelvetProfileDto profile, VelvetPostDto[] feed)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ScrollLayout.StableContentWidth();
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

            var chipLabel = Loc.T(def.Label);
            var chipWidth = VChip.Width(chipLabel, false, false, scale);
            chipX -= chipWidth;
            VChip.Draw(new Vector2(chipX, card.Min.Y + pad), 26f * scale,
                new VChipModel(chipLabel, VChipStyle.Solid, def.Hue), scale);
            chipX -= 6f * scale;
            drawnChips++;
        }

        if (photoCount > 0)
        {
            var badgeText = Loc.Plural(L.Velvet.PhotoBadge, photoCount);
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
        Typography.Draw(new Vector2(textLeft, card.Max.Y - pad - 15f * scale),
            Typography.FitText(VelvetIntent.Summary(mask), textWidth, TextStyles.Footnote), VelvetTheme.RoseInk,
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
            VelvetConnectionState.Connected => (Loc.T(L.Velvet.Message), false, true),
            VelvetConnectionState.OutgoingRequest => (Loc.T(L.Velvet.Requested), false, false),
            VelvetConnectionState.IncomingRequest => (Loc.T(L.Velvet.Reply), true, true),
            _ => (Loc.T(L.Velvet.Connect), true, true),
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
