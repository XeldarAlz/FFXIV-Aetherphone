using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float FindFieldRowHeight = 44f;
    private const float FindResultRowHeight = 60f;
    private const float FindResultAvatarRadius = 20f;
    private const float FindPromptGlyph = 36f;
    private const float FindPromptRise = 26f;
    private const float FindPromptTitleDrop = 18f;
    private const float FindPromptHintDrop = 42f;
    private const float LookupHeroAvatarRadius = 48f;
    private const float LookupCrestSize = 72f;
    private const float PagerButtonRadius = 16f;
    private const float PagerGlyph = 18f;
    private const int LookupAvatarSegments = 48;
    private const float LookupMonogramScale = 1.4f;

    private readonly ChipRail findRail = new();
    private readonly string[] findSegmentLabels = new string[2];
    private readonly bool[] findSegmentActive = new bool[2];
    private LookupKind findKind = LookupKind.Character;
    private string findNameInput = string.Empty;
    private string findWorldInput = string.Empty;
    private string submittedName = string.Empty;
    private string submittedRegion = string.Empty;
    private bool submittedRegionIsDataCenter;
    private bool hasQuery;
    private bool forceSearch;
    private bool forceDetail;

    private void ResetFindState()
    {
        findKind = LookupKind.Character;
        findNameInput = string.Empty;
        findWorldInput = gameData.DataCenterName(gameData.LocalHomeWorldId);
        submittedName = string.Empty;
        submittedRegion = string.Empty;
        submittedRegionIsDataCenter = false;
        hasQuery = false;
        forceSearch = false;
        forceDetail = false;
    }

    private void SubmitSearch()
    {
        submittedName = findNameInput.Trim();
        submittedRegion = findWorldInput.Trim();
        submittedRegionIsDataCenter = gameData.IsDataCenterName(submittedRegion);
        hasQuery = submittedName.Length > 0;
        forceSearch = false;
    }

    private void DrawFindPrompt(Rect body, float scale)
    {
        var center = body.Center;
        PhoneIcon.Draw(ImGui.GetWindowDrawList(), new Vector2(center.X, center.Y - FindPromptRise * scale),
            PhoneIcons.Users, ink.MutedInk, FindPromptGlyph * scale);
        Typography.DrawCentered(new Vector2(center.X, center.Y + FindPromptTitleDrop * scale),
            Loc.T(L.FindPeople.Prompt), ink.TitleInk, TextStyles.Headline);
        Typography.DrawCentered(new Vector2(center.X, center.Y + FindPromptHintDrop * scale),
            Loc.T(L.FindPeople.PromptHint), ink.MutedInk, TextStyles.Subheadline);
    }

    private void DrawCharacterResults(Rect body, float scale)
    {
        var result = lookup.SearchCharacters(submittedName, submittedRegion, submittedRegionIsDataCenter, forceSearch);
        forceSearch = false;
        var matches = result.Matches;
        if (matches.Length == 0)
        {
            if (DrawLookupState(body, result.State, scale))
            {
                forceSearch = true;
            }

            return;
        }

        var hintWorld = submittedRegionIsDataCenter ? string.Empty : submittedRegion;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            for (var index = 0; index < matches.Length; index++)
            {
                var match = matches[index];
                var world = match.World.Length > 0 ? match.World : hintWorld;
                if (DrawLookupRow(match.Name, world, lodestone.Avatar(match.Name, world,
                        FindResultAvatarRadius * 2f * scale)))
                {
                    router.Push(LinkpearlRoute.Character(match.Id, match.Name, world));
                }
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawFreeCompanyResults(Rect body, float scale)
    {
        var result =
            lookup.SearchFreeCompanies(submittedName, submittedRegion, submittedRegionIsDataCenter, forceSearch);
        forceSearch = false;
        var matches = result.Matches;
        if (matches.Length == 0)
        {
            if (DrawLookupState(body, result.State, scale))
            {
                forceSearch = true;
            }

            return;
        }

        using (AppSurface.BeginEdgeToEdge(body))
        {
            for (var index = 0; index < matches.Length; index++)
            {
                var match = matches[index];
                if (DrawLookupRow(match.Name, match.Subtitle,
                        lodestone.Remote(match.CrestKey, match.Crest, FindResultAvatarRadius * 2f * scale)))
                {
                    router.Push(LinkpearlRoute.FreeCompany(match.Id, match.Name, match.World));
                }
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private bool DrawLookupRow(string title, string subtitle, AvatarHandle image)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var person = chrome.BeginPersonRow(drawList, FindResultRowHeight, FindResultAvatarRadius,
            ChatListChrome.ChevronSize * scale, true, out var avatarCenter);
        AvatarView.Draw(drawList, avatarCenter, FindResultAvatarRadius * scale, ink.FaintInk, Initials.Of(title),
            LookupMonogramScale, image, LookupAvatarSegments);
        chrome.DrawRowTitleAndSub(drawList, new MarqueeId("linkpearl.result.", title), title, subtitle,
            person.TextLeft, person.TextRight, person.Bounds.Center.Y, ink.TitleInk, ink.MutedInk);
        PhoneIcon.Draw(drawList,
            new Vector2(person.Bounds.Max.X - ChatListChrome.CellPadX * scale - ChatListChrome.ChevronSize * 0.5f * scale,
                person.Bounds.Center.Y), PhoneIcons.ChevronRight, ink.FaintInk, ChatListChrome.ChevronSize * scale);
        chrome.EndPersonRow(drawList, person);
        return person.Tapped;
    }

    private bool DrawRosterRow(Rect row, string title, string subtitle, AvatarHandle image)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var band = ChatListChrome.RowBand(row, scale);
        var hovered = UiInteract.Hover(band.Min, band.Max);
        if (hovered)
        {
            drawList.AddRectFilled(band.Min, band.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var radius = FindResultAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + radius, row.Center.Y);
        AvatarView.Draw(drawList, avatarCenter, radius, ink.FaintInk, Initials.Of(title), LookupMonogramScale, image,
            LookupAvatarSegments);
        var textLeft = avatarCenter.X + radius + ChatListChrome.RowAvatarGap * scale;
        var textRight = row.Max.X - ChatListChrome.ChevronSize * scale - ChatListChrome.RowTrailingGap * scale;
        chrome.DrawRowTitleAndSub(drawList, new MarqueeId("linkpearl.roster.", title), title, subtitle, textLeft,
            textRight, row.Center.Y, ink.TitleInk, ink.MutedInk);
        PhoneIcon.Draw(drawList, new Vector2(row.Max.X - ChatListChrome.ChevronSize * 0.5f * scale, row.Center.Y),
            PhoneIcons.ChevronRight, ink.FaintInk, ChatListChrome.ChevronSize * scale);
        return UiInteract.Click(band.Min, band.Max, hovered);
    }

    private void DrawCharacterDetail(Rect area, LinkpearlRoute route)
    {
        var scale = UiScale.Current;
        chrome.DrawScreenHeader(area, Loc.T(L.FindPeople.CharacterTitle), backToList);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var result = lookup.CharacterDetail(route.LookupId, route.LookupName, route.LookupWorld, forceDetail);
        forceDetail = false;
        var detail = result.Detail;
        if (detail is null)
        {
            if (DrawLookupState(body, result.State, scale))
            {
                forceDetail = true;
            }

            return;
        }

        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawCharacterHero(detail, width, scale);
            DrawCharacterActions(detail, width, scale);
            DrawInfoCard(detail);
            DrawJobsCard(detail.Jobs, JobCategory.Combat, Loc.T(L.FindPeople.Combat));
            DrawJobsCard(detail.Jobs, JobCategory.Crafter, Loc.T(L.FindPeople.Crafter));
            DrawJobsCard(detail.Jobs, JobCategory.Gatherer, Loc.T(L.FindPeople.Gatherer));
            DrawGearCard(detail.Gear);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawCharacterHero(CharacterDetail detail, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        var radius = LookupHeroAvatarRadius * scale;
        var avatarCenter = new Vector2(centerX, origin.Y + HeroTopPad * scale + radius);
        AvatarView.Draw(drawList, avatarCenter, radius, ink.FaintInk, Initials.Of(detail.Name), HeroMonogramScale,
            lodestone.Remote(detail.PortraitKey, detail.Portrait, radius * 2f), HeroAvatarSegments);
        var top = avatarCenter.Y + radius + HeroNameGap * scale;
        var maxWidth = width - Metrics.Space.Xl * scale;
        top += Typography.DrawWrappedCentered(new Vector2(centerX, top), detail.Name, ink.TitleInk, TextStyles.Title2,
            maxWidth) + HeroLineGap * scale;
        if (detail.Title.Length > 0)
        {
            top += Typography.DrawWrappedCentered(new Vector2(centerX, top), detail.Title, ink.AccentLink,
                TextStyles.Footnote, maxWidth) + HeroLineGap * scale;
        }

        top += Typography.DrawWrappedCentered(new Vector2(centerX, top), detail.World, ink.MutedInk,
            TextStyles.Subheadline, maxWidth) + HeroActionsGap * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, top - origin.Y));
    }

    private void DrawCharacterActions(CharacterDetail detail, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gap = HeroActionGap * scale;
        var buttonWidth = (width - gap * (HeroActionCount - 1)) / HeroActionCount;
        var height = HeroActionHeight * scale;
        var min = new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y);
        if (chrome.DrawHeroActionButton(drawList, new Rect(min, min + new Vector2(buttonWidth, height)),
                PhoneIcons.MessageCircle, Loc.T(L.FindPeople.Message), true))
        {
            OpenDirectThread(detail.Name, SendTarget(detail.Name, detail.World));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }

    private void DrawInfoCard(CharacterDetail detail)
    {
        var rows = 1;
        var hasGrandCompany = detail.GrandCompany.Length > 0;
        var hasFreeCompany = detail.FreeCompany.Length > 0;
        if (hasGrandCompany)
        {
            rows++;
        }

        if (hasFreeCompany)
        {
            rows++;
        }

        var drawList = ImGui.GetWindowDrawList();
        chrome.DrawInsetSectionLabel(Loc.T(L.FindPeople.CharacterTitle));
        var card = GroupCard.Begin(ui, rows, ChatListChrome.SettingRowHeight);
        chrome.DrawInfoRow(drawList, card.NextRow(), Loc.T(L.FindPeople.Character), detail.RaceClan);
        if (hasGrandCompany)
        {
            chrome.DrawInfoRow(drawList, card.NextRow(), Loc.T(L.FindPeople.GrandCompany), detail.GrandCompany);
        }

        if (hasFreeCompany)
        {
            chrome.DrawInfoRow(drawList, card.NextRow(), Loc.T(L.FindPeople.FreeCompany), detail.FreeCompany);
        }

        card.End();
        ChatListChrome.DrawCardGap();
    }

    private void DrawJobsCard(IReadOnlyList<ClassJobLevel> jobs, JobCategory category, string header)
    {
        var count = 0;
        for (var index = 0; index < jobs.Count; index++)
        {
            if (jobs[index].Category == category)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        chrome.DrawInsetSectionLabel(header);
        var card = GroupCard.Begin(ui, count, ChatListChrome.SettingRowHeight);
        for (var index = 0; index < jobs.Count; index++)
        {
            var job = jobs[index];
            if (job.Category != category)
            {
                continue;
            }

            chrome.DrawInfoRow(drawList, card.NextRow(), job.Name, job.LevelLabel);
        }

        card.End();
        ChatListChrome.DrawCardGap();
    }

    private void DrawGearCard(IReadOnlyList<GearPiece> gear)
    {
        if (gear.Count == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        chrome.DrawInsetSectionLabel(Loc.T(L.FindPeople.Gear));
        var card = GroupCard.Begin(ui, gear.Count, ChatListChrome.SettingRowHeight);
        for (var index = 0; index < gear.Count; index++)
        {
            var piece = gear[index];
            chrome.DrawInfoRow(drawList, card.NextRow(), piece.ItemName, piece.ItemLevelLabel);
        }

        card.End();
    }

    private void DrawFreeCompanyDetail(Rect area, LinkpearlRoute route)
    {
        var scale = UiScale.Current;
        chrome.DrawScreenHeader(area, Loc.T(L.FindPeople.FreeCompanyTitle), backToList);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var result = lookup.FreeCompanyDetail(route.LookupId, forceDetail);
        forceDetail = false;
        var detail = result.Detail;
        if (detail is null)
        {
            if (DrawLookupState(body, result.State, scale))
            {
                forceDetail = true;
            }

            return;
        }

        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawFreeCompanyHero(detail, width, scale);
            if (detail.Slogan.Length > 0)
            {
                chrome.DrawInsetSectionLabel(Loc.T(L.FindPeople.Slogan));
                var sloganCard = GroupCard.Begin(ui, 1, ChatListChrome.ActionRowHeight);
                DrawSloganRow(sloganCard.NextRow(), detail.Slogan);
                sloganCard.End();
                ChatListChrome.DrawCardGap();
            }

            DrawRoster(route.LookupId, result, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawFreeCompanyHero(FreeCompanyDetail detail, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        var crestSize = LookupCrestSize * scale;
        var crestCenter = new Vector2(centerX, origin.Y + HeroTopPad * scale + crestSize * 0.5f);
        DrawCrest(drawList, crestCenter, crestSize, detail);
        var top = crestCenter.Y + crestSize * 0.5f + HeroNameGap * scale;
        var maxWidth = width - Metrics.Space.Xl * scale;
        top += Typography.DrawWrappedCentered(new Vector2(centerX, top), detail.Heading, ink.TitleInk,
            TextStyles.Title2, maxWidth) + HeroLineGap * scale;
        top += Typography.DrawWrappedCentered(new Vector2(centerX, top), detail.World, ink.MutedInk,
            TextStyles.Subheadline, maxWidth) + HeroLineGap * scale;
        var recruit = detail.Recruiting ? Loc.T(L.FindPeople.Recruiting) : Loc.T(L.FindPeople.Closed);
        var membersSize = Typography.Measure(detail.MembersLabel, TextStyles.Footnote);
        var recruitSize = Typography.Measure(recruit, TextStyles.Footnote);
        var gap = Metrics.Space.Lg * scale;
        var lineWidth = membersSize.X + gap + recruitSize.X;
        var lineLeft = centerX - lineWidth * 0.5f;
        Typography.Draw(drawList, new Vector2(lineLeft, top), detail.MembersLabel, ink.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(lineLeft + membersSize.X + gap, top), recruit,
            detail.Recruiting ? ink.PresenceGreen : ink.MutedInk, TextStyles.Footnote);
        top += membersSize.Y + HeroActionsGap * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, top - origin.Y));
    }

    private void DrawCrest(ImDrawListPtr drawList, Vector2 center, float size, FreeCompanyDetail detail)
    {
        var handle = lodestone.Remote(detail.CrestKey, detail.Crest, size);
        var half = new Vector2(size * 0.5f, size * 0.5f);
        if (handle.Texture is { } texture)
        {
            drawList.AddImage(texture.Handle, center - half, center + half);
            return;
        }

        drawList.AddCircleFilled(center, size * 0.5f, ImGui.GetColorU32(ui.Palette.CardFill), LookupAvatarSegments);
        var initial = detail.Tag.Length > 0 ? detail.Tag.Substring(0, 1).ToUpperInvariant() : Initials.Of(detail.Name);
        Typography.DrawCentered(drawList, center, initial, ink.TitleInk, TextStyles.Title1);
    }

    private void DrawSloganRow(Rect row, string slogan)
    {
        var style = TextStyles.Subheadline;
        var hovering = UiInteract.Hover(row.Min, row.Max);
        Marquee.DrawLeft(ImGui.GetWindowDrawList(), new MarqueeId("linkpearl.slogan.", slogan), slogan, row.Min.X,
            row.Center.Y - Typography.LineHeight(style) * 0.5f, MathF.Max(1f, row.Width), style, ink.MutedInk,
            hovering);
    }

    private void DrawRoster(string companyId, FreeCompanyDetailResult result, float scale)
    {
        var roster = result.Roster;
        if (roster.Members.Length == 0)
        {
            return;
        }

        chrome.DrawInsetSectionLabel(Loc.T(L.FindPeople.Roster));
        var card = GroupCard.Begin(ui, roster.Members.Length, FindResultRowHeight);
        for (var index = 0; index < roster.Members.Length; index++)
        {
            var member = roster.Members[index];
            if (DrawRosterRow(card.NextRow(), member.Name, member.Subtitle,
                    lodestone.Remote(member.AvatarKey, member.Avatar, FindResultAvatarRadius * 2f * scale)))
            {
                router.Push(LinkpearlRoute.Character(member.Id, member.Name, member.World));
            }
        }

        card.End();
        if (roster.PageCount > 1)
        {
            DrawRosterPager(companyId, result, roster, scale);
        }
    }

    private void DrawRosterPager(string companyId, FreeCompanyDetailResult result, RosterSnapshot roster, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + PagerButtonRadius * scale);
        var loading = result.RosterLoading;
        if (DrawPagerButton(new Vector2(origin.X + PagerButtonRadius * 2.5f * scale, center.Y), PhoneIcons.ChevronLeft,
                roster.Page > 0 && !loading, scale))
        {
            lookup.RequestRosterPage(companyId, result, roster.Page - 1);
        }

        if (DrawPagerButton(new Vector2(origin.X + width - PagerButtonRadius * 2.5f * scale, center.Y),
                PhoneIcons.ChevronRight, roster.Page < roster.PageCount - 1 && !loading, scale))
        {
            lookup.RequestRosterPage(companyId, result, roster.Page + 1);
        }

        if (loading)
        {
            ProgressRing.Sweep(center, 9f * scale, 2.4f * scale, ink.MutedInk, 900.0, 1.8f, 0.95f);
        }
        else
        {
            Typography.DrawCentered(center, Loc.T(L.FindPeople.PageOf, roster.Page + 1, roster.PageCount),
                ink.MutedInk, TextStyles.FootnoteEmphasized);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, PagerButtonRadius * 2f * scale));
    }

    private bool DrawPagerButton(Vector2 center, string glyph, bool enabled, float scale)
    {
        var box = PagerButtonRadius * scale;
        var min = center - new Vector2(box, box);
        var max = center + new Vector2(box, box);
        var hovered = enabled && UiInteract.Hover(min, max);
        var color = enabled ? hovered ? ink.TitleInk : ink.AccentLink : ink.FaintInk;
        PhoneIcon.Draw(ImGui.GetWindowDrawList(), center, glyph, color, PagerGlyph * scale);
        if (!enabled)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private bool DrawLookupState(Rect body, LookupState state, float scale)
    {
        var center = body.Center;
        var drawList = ImGui.GetWindowDrawList();
        if (state == LookupState.Failed)
        {
            PhoneIcon.Draw(drawList, new Vector2(center.X, center.Y - FindPromptRise * scale), PhoneIcons.CloudDownload,
                ink.MutedInk, FindPromptGlyph * scale);
            Typography.DrawCentered(new Vector2(center.X, center.Y + FindPromptTitleDrop * scale),
                Loc.T(L.FindPeople.Failed), ink.MutedInk, TextStyles.Subheadline);
            return TextButton.Draw(new Vector2(center.X, center.Y + 48f * scale), Loc.T(L.FindPeople.TryAgain),
                ink.AccentLink, scale);
        }

        if (state == LookupState.Empty)
        {
            PhoneIcon.Draw(drawList, new Vector2(center.X, center.Y - FindPromptRise * scale), PhoneIcons.Search,
                ink.MutedInk, FindPromptGlyph * scale);
            Typography.DrawCentered(new Vector2(center.X, center.Y + FindPromptTitleDrop * scale),
                Loc.T(L.FindPeople.NoResults), ink.MutedInk, TextStyles.Subheadline);
            return false;
        }

        LoadingPulse.Draw(new Vector2(center.X, center.Y - 14f * scale), 13f * scale, ink.AccentLink, ink.MutedInk,
            Loc.T(L.Common.Searching));
        return false;
    }

    private static string SendTarget(string name, string world) =>
        world.Length > 0 ? string.Concat(name, "@", world) : name;
}
