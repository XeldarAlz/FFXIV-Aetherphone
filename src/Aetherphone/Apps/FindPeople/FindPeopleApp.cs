using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.FindPeople;

internal sealed class FindPeopleApp : IPhoneApp
{
    private const float SegmentRowHeight = 36f;
    private const float FieldRowHeight = 44f;
    private const float ResultRowHeight = 60f;

    private enum ViewKind : byte
    {
        Search,
        CharacterDetail,
        FreeCompanyDetail,
    }

    private readonly struct View
    {
        public readonly ViewKind Kind;
        public readonly string Id;
        public readonly string Name;
        public readonly string World;

        public View(ViewKind kind, string id, string name, string world)
        {
            Kind = kind;
            Id = id;
            Name = name;
            World = world;
        }

        public static readonly View SearchRoot = new(ViewKind.Search, string.Empty, string.Empty, string.Empty);
    }

    public string Id => "findpeople";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.FindPeople);
    public string Glyph => "Fp";
    public int BadgeCount => 0;
    private readonly LookupService lookup;
    private readonly LodestoneService lodestone;
    private readonly MessageLauncher launcher;
    private readonly GameData gameData;
    private readonly ViewRouter<View> router;
    private readonly RouterDraw<View> drawView;
    private readonly Action back;
    private readonly string[] segmentLabels = new string[2];
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private LookupKind kind = LookupKind.Character;
    private string nameInput = string.Empty;
    private string worldInput = string.Empty;
    private string submittedName = string.Empty;
    private string submittedRegion = string.Empty;
    private bool submittedRegionIsDataCenter;
    private bool hasQuery;
    private bool forceSearch;
    private bool forceDetail;

    public FindPeopleApp(LookupService lookup, LodestoneService lodestone, MessageLauncher launcher, GameData gameData)
    {
        this.lookup = lookup;
        this.lodestone = lodestone;
        this.launcher = launcher;
        this.gameData = gameData;
        router = new ViewRouter<View>(View.SearchRoot, Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        nameInput = string.Empty;
        worldInput = gameData.DataCenterName(gameData.LocalHomeWorldId);
        submittedName = string.Empty;
        submittedRegion = string.Empty;
        submittedRegionIsDataCenter = false;
        hasQuery = false;
        forceSearch = false;
        forceDetail = false;
    }

    public void OnClosed() => router.Reset();

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(View view, Rect area, int depth)
    {
        switch (view.Kind)
        {
            case ViewKind.CharacterDetail:
                DrawCharacterDetail(area, view);
                break;
            case ViewKind.FreeCompanyDetail:
                DrawFreeCompanyDetail(area, view);
                break;
            default:
                DrawSearch(area);
                break;
        }
    }

    private void DrawSearch(Rect area)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);
        var scale = ImGuiHelpers.GlobalScale;
        var theme = frameTheme;
        var pad = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var segmentRow = new Rect(new Vector2(area.Min.X + pad, top),
            new Vector2(area.Max.X - pad, top + SegmentRowHeight * scale));
        segmentLabels[0] = Loc.T(L.FindPeople.Character);
        segmentLabels[1] = Loc.T(L.FindPeople.FreeCompany);
        var selected = SegmentStrip.Draw("findpeople.kind", segmentRow, segmentLabels, (int)kind, theme);
        if (selected != (int)kind)
        {
            kind = (LookupKind)selected;
            if (hasQuery)
            {
                SubmitSearch();
            }
        }

        var nameTop = segmentRow.Max.Y + 8f * scale;
        var nameBar = new Rect(new Vector2(area.Min.X + pad, nameTop),
            new Vector2(area.Max.X - pad, nameTop + FieldRowHeight * scale));
        var nameChanged =
            SubmitField.Draw(nameBar, "##findNameField", Loc.T(L.FindPeople.NameHint), ref nameInput, theme);
        var worldTop = nameBar.Max.Y + 8f * scale;
        var worldBar = new Rect(new Vector2(area.Min.X + pad, worldTop),
            new Vector2(area.Max.X - pad, worldTop + FieldRowHeight * scale));
        var worldChanged = SubmitField.Draw(worldBar, "##findWorldField", Loc.T(L.FindPeople.WorldHint), ref worldInput,
            theme);
        if (nameChanged || worldChanged)
        {
            SubmitSearch();
        }

        var body = new Rect(new Vector2(area.Min.X, worldBar.Max.Y + 4f * scale), area.Max);
        if (!hasQuery)
        {
            DrawPrompt(body, theme, scale);
            return;
        }

        if (kind == LookupKind.Character)
        {
            DrawCharacterResults(body, theme, scale);
        }
        else
        {
            DrawFreeCompanyResults(body, theme, scale);
        }
    }

    private void SubmitSearch()
    {
        submittedName = nameInput.Trim();
        submittedRegion = worldInput.Trim();
        submittedRegionIsDataCenter = gameData.IsDataCenterName(submittedRegion);
        hasQuery = submittedName.Length > 0;
        forceSearch = false;
    }

    private void DrawPrompt(Rect body, PhoneTheme theme, float scale)
    {
        var center = body.Center;
        ProgressRing.CenterIcon(new Vector2(center.X, center.Y - 26f * scale), FontAwesomeIcon.Users, theme.TextMuted,
            36f * scale);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 18f * scale), Loc.T(L.FindPeople.Prompt),
            theme.TextStrong, 1.0f, FontWeight.SemiBold);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 42f * scale), Loc.T(L.FindPeople.PromptHint),
            theme.TextMuted, 0.85f, FontWeight.Regular);
    }

    private void DrawCharacterResults(Rect body, PhoneTheme theme, float scale)
    {
        var result = lookup.SearchCharacters(submittedName, submittedRegion, submittedRegionIsDataCenter, forceSearch);
        forceSearch = false;
        var matches = result.Matches;
        if (matches.Length == 0)
        {
            if (DrawState(body, result.State, theme, scale))
            {
                forceSearch = true;
            }

            return;
        }

        var hintWorld = submittedRegionIsDataCenter ? string.Empty : submittedRegion;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var card = GroupCard.Begin(theme, matches.Length, ResultRowHeight);
            for (var index = 0; index < matches.Length; index++)
            {
                var match = matches[index];
                var world = match.World.Length > 0 ? match.World : hintWorld;
                if (DrawResultRow(card.NextRow(), theme, scale, match.Name, world, lodestone.Avatar(match.Name, world)))
                {
                    router.Push(new View(ViewKind.CharacterDetail, match.Id, match.Name, world));
                }
            }

            card.End();
        }
    }

    private void DrawFreeCompanyResults(Rect body, PhoneTheme theme, float scale)
    {
        var result =
            lookup.SearchFreeCompanies(submittedName, submittedRegion, submittedRegionIsDataCenter, forceSearch);
        forceSearch = false;
        var matches = result.Matches;
        if (matches.Length == 0)
        {
            if (DrawState(body, result.State, theme, scale))
            {
                forceSearch = true;
            }

            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var card = GroupCard.Begin(theme, matches.Length, ResultRowHeight);
            for (var index = 0; index < matches.Length; index++)
            {
                var match = matches[index];
                if (DrawResultRow(card.NextRow(), theme, scale, match.Name, match.Subtitle,
                        lodestone.Remote(match.CrestKey, match.Crest)))
                {
                    router.Push(new View(ViewKind.FreeCompanyDetail, match.Id, match.Name, match.World));
                }
            }

            card.End();
        }
    }

    private static bool DrawResultRow(Rect row, PhoneTheme theme, float scale, string title, string subtitle,
        AvatarHandle image)
    {
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        var drawList = ImGui.GetWindowDrawList();
        var avatarRadius = 20f * scale;
        var avatarCenter = new Vector2(row.Min.X + avatarRadius, row.Center.Y);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, theme.SurfaceMuted, Initials.Of(title), 1.4f, image, 48);
        var textX = avatarCenter.X + avatarRadius + 12f * scale;
        Typography.Draw(new Vector2(textX, row.Center.Y - 16f * scale), title, theme.TextStrong, 0.95f,
            FontWeight.Medium);
        if (subtitle.Length > 0)
        {
            Typography.Draw(new Vector2(textX, row.Center.Y + 3f * scale), subtitle, theme.TextMuted, 0.8f,
                FontWeight.Regular);
        }

        DrawChevronRight(new Vector2(row.Max.X, row.Center.Y), 6f * scale, 2.2f * scale,
            hovered ? theme.TextStrong : theme.TextMuted);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawCharacterDetail(Rect area, View view)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, Loc.T(L.FindPeople.CharacterTitle), back);
        var scale = ImGuiHelpers.GlobalScale;
        var theme = frameTheme;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var result = lookup.CharacterDetail(view.Id, view.Name, view.World, forceDetail);
        forceDetail = false;
        var detail = result.Detail;
        if (detail is null)
        {
            if (DrawState(body, result.State, theme, scale))
            {
                forceDetail = true;
            }

            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawCharacterHero(detail, theme, scale);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            DrawCharacterActions(detail, theme, scale);
            DrawInfoCard(detail, theme);
            DrawJobsCard(detail.Jobs, JobCategory.Combat, Loc.T(L.FindPeople.Combat), theme);
            DrawJobsCard(detail.Jobs, JobCategory.Crafter, Loc.T(L.FindPeople.Crafter), theme);
            DrawJobsCard(detail.Jobs, JobCategory.Gatherer, Loc.T(L.FindPeople.Gatherer), theme);
            DrawGearCard(detail.Gear, theme);
            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
    }

    private void DrawCharacterHero(CharacterDetail detail, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var hasTitle = detail.Title.Length > 0;
        var heroHeight = (hasTitle ? 196f : 178f) * scale;
        var heroMax = new Vector2(origin.X + width, origin.Y + heroHeight);
        var rounding = 22f * scale;
        Elevation.Card(drawList, origin, heroMax, rounding, scale);
        Squircle.Fill(drawList, origin, heroMax, rounding, ImGui.GetColorU32(theme.GroupedCard));
        Material.TopGlow(drawList, origin, heroMax, rounding, theme.Accent, 0.82f, 0.16f);
        Material.EdgeSquircle(drawList, origin, heroMax, rounding, scale);
        var centerX = origin.X + width * 0.5f;
        var avatarRadius = 40f * scale;
        var avatarCenter = new Vector2(centerX, origin.Y + 22f * scale + avatarRadius);
        ProgressRing.Glow(avatarCenter, avatarRadius, theme.Accent, 0.4f);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, theme.SurfaceMuted, Initials.Of(detail.Name), 2.0f,
            lodestone.Remote(detail.PortraitKey, detail.Portrait), 72);
        var cursorY = avatarCenter.Y + avatarRadius + 16f * scale;
        Typography.DrawCentered(new Vector2(centerX, cursorY), detail.Name, theme.TextStrong, TextStyles.Title2);
        cursorY += 24f * scale;
        if (hasTitle)
        {
            Typography.DrawCentered(new Vector2(centerX, cursorY), detail.Title, theme.Accent, TextStyles.Footnote);
            cursorY += 18f * scale;
        }

        Typography.DrawCentered(new Vector2(centerX, cursorY), detail.World, theme.TextMuted, TextStyles.Subheadline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, heroHeight));
    }

    private void DrawCharacterActions(CharacterDetail detail, PhoneTheme theme, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 26f * scale;
        var rowHeight = radius * 2f + 30f * scale;
        if (QuickAction.Draw("findpeople.message", new Vector2(origin.X + width * 0.5f, origin.Y + radius + 2f * scale),
                radius, FontAwesomeIcon.CommentDots, new Vector4(0.30f, 0.78f, 0.42f, 1f), Loc.T(L.FindPeople.Message),
                theme))
        {
            launcher.Request(detail.Name, SendTarget(detail.Name, detail.World));
            frameNavigation.Open("messages");
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawInfoCard(CharacterDetail detail, PhoneTheme theme)
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

        SettingsSection.Header(Loc.T(L.FindPeople.CharacterTitle), theme);
        var card = GroupCard.Begin(theme, rows);
        SettingsRow.Info(card.NextRow(), Loc.T(L.FindPeople.Character), detail.RaceClan, theme);
        if (hasGrandCompany)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.FindPeople.GrandCompany), detail.GrandCompany, theme);
        }

        if (hasFreeCompany)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.FindPeople.FreeCompany), detail.FreeCompany, theme);
        }

        card.End();
    }

    private void DrawJobsCard(IReadOnlyList<ClassJobLevel> jobs, JobCategory category, string header, PhoneTheme theme)
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

        SettingsSection.Header(header, theme);
        var card = GroupCard.Begin(theme, count);
        for (var index = 0; index < jobs.Count; index++)
        {
            var job = jobs[index];
            if (job.Category != category)
            {
                continue;
            }

            SettingsRow.Info(card.NextRow(), job.Name, job.LevelLabel, theme);
        }

        card.End();
    }

    private void DrawGearCard(IReadOnlyList<GearPiece> gear, PhoneTheme theme)
    {
        if (gear.Count == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.FindPeople.Gear), theme);
        var card = GroupCard.Begin(theme, gear.Count);
        for (var index = 0; index < gear.Count; index++)
        {
            var piece = gear[index];
            SettingsRow.Info(card.NextRow(), piece.ItemName, piece.ItemLevelLabel, theme);
        }

        card.End();
    }

    private void DrawFreeCompanyDetail(Rect area, View view)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, Loc.T(L.FindPeople.FreeCompanyTitle), back);
        var scale = ImGuiHelpers.GlobalScale;
        var theme = frameTheme;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var result = lookup.FreeCompanyDetail(view.Id, forceDetail);
        forceDetail = false;
        var detail = result.Detail;
        if (detail is null)
        {
            if (DrawState(body, result.State, theme, scale))
            {
                forceDetail = true;
            }

            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawFreeCompanyHero(detail, theme, scale);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            if (detail.Slogan.Length > 0)
            {
                SettingsSection.Header(Loc.T(L.FindPeople.Slogan), theme);
                var sloganCard = GroupCard.Begin(theme, 1, 56f);
                DrawSloganRow(sloganCard.NextRow(), detail.Slogan, theme, scale);
                sloganCard.End();
            }

            DrawRoster(view.Id, result, theme, scale);
            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
    }

    private void DrawFreeCompanyHero(FreeCompanyDetail detail, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var heroHeight = 192f * scale;
        var heroMax = new Vector2(origin.X + width, origin.Y + heroHeight);
        var rounding = 22f * scale;
        Elevation.Card(drawList, origin, heroMax, rounding, scale);
        Squircle.Fill(drawList, origin, heroMax, rounding, ImGui.GetColorU32(theme.GroupedCard));
        Material.TopGlow(drawList, origin, heroMax, rounding, theme.Accent, 0.82f, 0.16f);
        Material.EdgeSquircle(drawList, origin, heroMax, rounding, scale);
        var centerX = origin.X + width * 0.5f;
        var crestSize = 64f * scale;
        var crestCenter = new Vector2(centerX, origin.Y + 22f * scale + crestSize * 0.5f);
        DrawCrest(drawList, crestCenter, crestSize, detail, theme, scale);
        var cursorY = crestCenter.Y + crestSize * 0.5f + 14f * scale;
        Typography.DrawCentered(new Vector2(centerX, cursorY), detail.Heading, theme.TextStrong, TextStyles.Title3);
        cursorY += 22f * scale;
        Typography.DrawCentered(new Vector2(centerX, cursorY), detail.World, theme.TextMuted, TextStyles.Subheadline);
        cursorY += 22f * scale;
        var recruitColor = detail.Recruiting ? new Vector4(0.30f, 0.78f, 0.46f, 1f) : theme.TextMuted;
        var recruit = detail.Recruiting ? Loc.T(L.FindPeople.Recruiting) : Loc.T(L.FindPeople.Closed);
        Typography.DrawCentered(new Vector2(centerX - 44f * scale, cursorY), detail.MembersLabel, theme.TextMuted,
            TextStyles.Footnote);
        Typography.DrawCentered(new Vector2(centerX + 52f * scale, cursorY), recruit, recruitColor,
            TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, heroHeight));
    }

    private void DrawCrest(ImDrawListPtr drawList, Vector2 center, float size, FreeCompanyDetail detail,
        PhoneTheme theme, float scale)
    {
        var handle = lodestone.Remote(detail.CrestKey, detail.Crest);
        var half = new Vector2(size * 0.5f, size * 0.5f);
        if (handle.Texture is { } texture)
        {
            drawList.AddImage(texture.Handle, center - half, center + half);
            return;
        }

        drawList.AddCircleFilled(center, size * 0.5f, ImGui.GetColorU32(theme.SurfaceMuted), 32);
        var initial = detail.Tag.Length > 0 ? detail.Tag.Substring(0, 1).ToUpperInvariant() : Initials.Of(detail.Name);
        Typography.DrawCentered(center, initial, theme.TextStrong, 1.8f, FontWeight.SemiBold);
    }

    private static void DrawSloganRow(Rect row, string slogan, PhoneTheme theme, float scale)
    {
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - Typography.Measure(slogan, 0.86f).Y * 0.5f), slogan,
            theme.TextMuted, 0.86f, FontWeight.Regular);
    }

    private void DrawRoster(string companyId, FreeCompanyDetailResult result, PhoneTheme theme, float scale)
    {
        var roster = result.Roster;
        if (roster.Members.Length == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.FindPeople.Roster), theme);
        var card = GroupCard.Begin(theme, roster.Members.Length, ResultRowHeight);
        for (var index = 0; index < roster.Members.Length; index++)
        {
            var member = roster.Members[index];
            if (DrawResultRow(card.NextRow(), theme, scale, member.Name, member.Subtitle,
                    lodestone.Remote(member.AvatarKey, member.Avatar)))
            {
                router.Push(new View(ViewKind.CharacterDetail, member.Id, member.Name, member.World));
            }
        }

        card.End();
        if (roster.PageCount > 1)
        {
            DrawRosterPager(companyId, result, roster, theme, scale);
        }
    }

    private void DrawRosterPager(string companyId, FreeCompanyDetailResult result, RosterSnapshot roster,
        PhoneTheme theme, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + 16f * scale);
        var loading = result.RosterLoading;
        if (DrawPagerButton(new Vector2(origin.X + 40f * scale, center.Y), FontAwesomeIcon.ChevronLeft,
                roster.Page > 0 && !loading, theme, scale))
        {
            lookup.RequestRosterPage(companyId, result, roster.Page - 1);
        }

        if (DrawPagerButton(new Vector2(origin.X + width - 40f * scale, center.Y), FontAwesomeIcon.ChevronRight,
                roster.Page < roster.PageCount - 1 && !loading, theme, scale))
        {
            lookup.RequestRosterPage(companyId, result, roster.Page + 1);
        }

        if (loading)
        {
            ProgressRing.Sweep(center, 9f * scale, 2.4f * scale, theme.TextMuted, 900.0, 1.8f, 0.95f);
        }
        else
        {
            Typography.DrawCentered(center, Loc.T(L.FindPeople.PageOf, roster.Page + 1, roster.PageCount),
                theme.TextMuted, 0.82f, FontWeight.Medium);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 32f * scale));
    }

    private static bool DrawPagerButton(Vector2 center, FontAwesomeIcon icon, bool enabled, PhoneTheme theme,
        float scale)
    {
        var box = 16f * scale;
        var hovered = enabled &&
                      ImGui.IsMouseHoveringRect(center - new Vector2(box, box), center + new Vector2(box, box));
        var color = enabled ? (hovered ? theme.TextStrong : theme.Accent) : Palette.WithAlpha(theme.TextMuted, 0.35f);
        ProgressRing.CenterIcon(center, icon, color, 16f * scale);
        if (!enabled)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private bool DrawState(Rect body, LookupState state, PhoneTheme theme, float scale)
    {
        var center = body.Center;
        if (state == LookupState.Failed)
        {
            ProgressRing.CenterIcon(new Vector2(center.X, center.Y - 26f * scale), FontAwesomeIcon.CloudDownloadAlt,
                theme.TextMuted, 34f * scale);
            Typography.DrawCentered(new Vector2(center.X, center.Y + 18f * scale), Loc.T(L.FindPeople.Failed),
                theme.TextMuted, 0.95f, FontWeight.Medium);
            return DrawTextButton(new Vector2(center.X, center.Y + 48f * scale), Loc.T(L.FindPeople.TryAgain), Accent,
                scale);
        }

        if (state == LookupState.Empty)
        {
            ProgressRing.CenterIcon(new Vector2(center.X, center.Y - 24f * scale), FontAwesomeIcon.UserSlash,
                theme.TextMuted, 34f * scale);
            Typography.DrawCentered(new Vector2(center.X, center.Y + 20f * scale), Loc.T(L.FindPeople.NoResults),
                theme.TextMuted, 0.95f, FontWeight.Medium);
            return false;
        }

        LoadingPulse.Draw(new Vector2(center.X, center.Y - 14f * scale), 13f * scale, Accent, theme.TextMuted,
            Loc.T(L.Common.Searching));
        return false;
    }

    private static bool DrawTextButton(Vector2 center, string label, Vector4 color, float scale)
    {
        var size = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        var hitMin = new Vector2(center.X - size.X * 0.5f - 12f * scale, center.Y - size.Y * 0.5f - 6f * scale);
        var hitMax = new Vector2(center.X + size.X * 0.5f + 12f * scale, center.Y + size.Y * 0.5f + 6f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(hitMin, hitMax, ImGui.GetColorU32(Palette.WithAlpha(color, hovered ? 0.22f : 0.14f)),
            (hitMax.Y - hitMin.Y) * 0.5f);
        Typography.DrawCentered(center, label, color, 0.9f, FontWeight.SemiBold);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawChevronRight(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var drawList = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }

    private static string SendTarget(string name, string world) =>
        world.Length > 0 ? string.Concat(name, "@", world) : name;

    public void Dispose()
    {
    }
}
