using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal enum LoveIslandSheet : byte
{
    None,
    About,
    Member,
}

internal sealed partial class LoveIslandApp
{
    private const float SheetPadX = 20f;
    private const float SheetGap = 12f;
    private const float SheetMaxShare = 0.86f;
    private const float MemberPortrait = 30f;
    private const int MaxMemberLinks = 4;
    private const string SheetCloseId = "loveisland.sheet.close";
    private const int AboutTitleLines = 2;
    private const float SheetTintTop = 0.62f;
    private const float SheetTintBottom = 0.45f;
    private const float SheetGlowAlpha = 0.30f;
    private const string MemberLinkPrefix = "loveisland.member.link.";

    private static readonly string[] MemberLinkIds = BuildMemberLinkIds();

    private readonly Sheet sheet = new();
    private readonly ClampedLines aboutLines = new();
    private readonly ClampedLines aboutTitleLines = new();
    private readonly ChipRail aboutRail = new();
    private readonly Dictionary<string, string> monograms = new(StringComparer.Ordinal);
    private LoveIslandSheet sheetKind;
    private string sheetMemberId = string.Empty;
    private CampaignMemberDto? memberLineSource;
    private string memberLine = string.Empty;
    private string[] memberLinkLabels = Array.Empty<string>();
    private string[] aboutBodyLines = Array.Empty<string>();

    private static string[] BuildMemberLinkIds()
    {
        var ids = new string[MaxMemberLinks];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = MemberLinkPrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ids;
    }

    private void OpenAbout()
    {
        UiFeedback.Play(UiSound.Tap);
        sheetKind = LoveIslandSheet.About;
        sheet.Open();
    }

    private void OpenMember(string memberId)
    {
        if (Ledger.FindMember(memberId) is null)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        sheetMemberId = memberId;
        sheetKind = LoveIslandSheet.Member;
        sheet.Open();
    }

    private string MonogramFor(string key, string name)
    {
        if (!monograms.TryGetValue(key, out var monogram))
        {
            monogram = Initials.Of(name);
            monograms[key] = monogram;
        }

        return monogram;
    }

    private void DrawSheet(Rect screen)
    {
        if (!sheet.CapturesPointer || sheetKind == LoveIslandSheet.None)
        {
            return;
        }

        var height = sheetKind == LoveIslandSheet.About ? AboutSheetHeight(screen) : MemberSheetHeight(screen);
        using var layer = ScreenLayer.Begin("loveisland.sheet", screen, false);
        var frame = sheet.Begin(ImGui.GetWindowDrawList(), screen, theme, SheetDetents.Fitted(height),
            SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        TintSheet(in frame);
        if (sheetKind == LoveIslandSheet.About)
        {
            DrawAboutSheet(in frame, screen);
        }
        else
        {
            DrawMemberSheet(in frame);
        }

        sheet.End(in frame);
    }

    private void TintSheet(in SheetFrame frame)
    {
        var scale = UiScale.Current;
        var drawList = frame.DrawList;
        var panel = frame.Panel;
        var rounding = theme.ScreenRounding * scale;
        var opacity = frame.Opacity;
        Squircle.FillVerticalGradient(drawList, panel.Min, panel.Max, rounding,
            ImGui.GetColorU32(Palette.Mix(ui.Accent, LoveIslandKit.Night, 0.72f) with { W = SheetTintTop * opacity }),
            ImGui.GetColorU32(LoveIslandKit.Night with { W = SheetTintBottom * opacity }));
        drawList.PushClipRect(panel.Min, panel.Max, true);
        SoftGlow.Draw(drawList, new Vector2(panel.Center.X, panel.Min.Y), panel.Width * 0.62f,
            ui.Accent with { W = SheetGlowAlpha * opacity * (0.85f + 0.15f * EclipseFx.Breath(3400.0)) });
        drawList.PopClipRect();
        Squircle.StrokeDirectional(drawList, panel.Min, panel.Max, rounding,
            ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.5f) with { W = 0.55f * opacity }), 1.2f * scale,
            new Vector2(0f, -1f), 3f);
    }

    private float SheetTextWidth(Rect screen) => MathF.Max(1f, screen.Width - SheetPadX * 2f * UiScale.Current);

    private float SheetChrome(float scale) =>
        (SheetMetrics.GrabberZone + SheetGap * 2f + Metrics.Size.HomeIndicatorInset) * scale
        + Button.LargeHeight * scale;

    private float AboutSheetHeight(Rect screen)
    {
        var scale = UiScale.Current;
        var width = SheetTextWidth(screen);
        var titleHeight = aboutTitleLines.Get(labels.AboutTitle, TextStyles.Title3, width, AboutTitleLines, fontKey)
            .Length * Typography.LineHeight(TextStyles.Title3);
        var footerHeight = Typography.MeasureWrappedBlock(Texts.T(CampaignTextKeys.AboutFooter), TextStyles.Footnote,
            width).Y;
        var railHeight = labels.LinkLabels.Length > 0 ? (ChipRail.RowHeight + SheetGap) * scale : 0f;
        var fixedHeight = SheetChrome(scale) + titleHeight + footerHeight + railHeight + SheetGap * 2f * scale;
        aboutBodyLines = AboutBody(width, screen.Height * SheetMaxShare - fixedHeight);
        return fixedHeight + aboutBodyLines.Length * Typography.LineHeight(TextStyles.Body);
    }

    private string[] AboutBody(float width, float available)
    {
        var about = frameCampaign?.About ?? string.Empty;
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        var maxLines = Math.Max(1, (int)(available / MathF.Max(1f, lineHeight)));
        return aboutLines.Get(about, TextStyles.Body, width, maxLines, fontKey);
    }

    private void DrawAboutSheet(in SheetFrame frame, Rect screen)
    {
        var scale = UiScale.Current;
        var drawList = frame.DrawList;
        var content = frame.Content;
        var ink = frame.Ink with { W = frame.Ink.W * frame.Opacity };
        var muted = ink with { W = ink.W * 0.72f };
        var left = content.Min.X + SheetPadX * scale;
        var width = SheetTextWidth(screen);
        var cursorY = content.Min.Y + SheetGap * scale;
        var titleLines = aboutTitleLines.Get(labels.AboutTitle, TextStyles.Title3, width, AboutTitleLines, fontKey);
        var titleEffect = ChromeEffect();
        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(left, cursorY), titleLines[lineIndex], ink, TextStyles.Title3,
                titleEffect);
            cursorY += Typography.LineHeight(TextStyles.Title3);
        }

        cursorY += SheetGap * scale;
        var footer = Texts.T(CampaignTextKeys.AboutFooter);
        var bodyLines = aboutBodyLines;
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        for (var lineIndex = 0; lineIndex < bodyLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(left, cursorY), bodyLines[lineIndex], ink, TextStyles.Body);
            cursorY += lineHeight;
        }

        cursorY += SheetGap * scale;
        var campaign = frameCampaign;
        if (campaign is not null && labels.LinkLabels.Length > 0)
        {
            var count = Math.Min(labels.LinkLabels.Length, linkActive.Length);
            var row = new Rect(new Vector2(content.Min.X, cursorY),
                new Vector2(content.Max.X, cursorY + ChipRail.RowHeight * scale));
            var tapped = aboutRail.Draw(row, ui, labels.LinkLabels.AsSpan(0, count), linkActive.AsSpan(0, count), true);
            if (tapped >= 0 && tapped < campaign.Links.Length && frame.Interactive)
            {
                var url = campaign.Links[tapped].Url;
                sheet.Close();
                UrlActions.AskThenOpen(url);
            }

            cursorY = row.Max.Y + SheetGap * scale;
        }

        cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), footer, muted, TextStyles.Footnote, width);
        cursorY += SheetGap * scale;
        DrawSheetClose(drawList, left, cursorY, width, in frame);
    }

    private void DrawSheetClose(ImDrawListPtr drawList, float left, float top, float width, in SheetFrame frame)
    {
        var scale = UiScale.Current;
        var close = new Rect(new Vector2(left, top), new Vector2(left + width, top + Button.LargeHeight * scale));
        if (frame.Interactive && Button.Draw(drawList, close, Loc.T(L.Common.Close), ui.Ink, ButtonStyle.Gray,
                id: SheetCloseId))
        {
            sheet.Close();
        }
    }

    private void SyncMemberLine(CampaignMemberDto member)
    {
        if (ReferenceEquals(member, memberLineSource))
        {
            return;
        }

        memberLineSource = member;
        memberLine = member.World.Length > 0 && member.Role.Length > 0
            ? string.Concat(member.Role, LoveIslandKit.Separator, member.World)
            : member.Role.Length > 0 ? member.Role : member.World;
        var links = member.Links ?? Array.Empty<CampaignLinkDto>();
        var count = Math.Min(links.Length, MaxMemberLinks);
        var linkLabels = new string[count];
        for (var index = 0; index < count; index++)
        {
            linkLabels[index] = LoveIslandKit.LinkLabel(links[index].Kind);
        }

        memberLinkLabels = linkLabels;
    }

    private float MemberSheetHeight(Rect screen)
    {
        var scale = UiScale.Current;
        var member = Ledger.FindMember(sheetMemberId);
        if (member is null)
        {
            return SheetChrome(scale);
        }

        SyncMemberLine(member);
        var linkRows = memberLinkLabels.Length;
        var linksHeight = linkRows > 0
            ? linkRows * (Button.LargeHeight + Metrics.Space.Sm) * scale
            : Typography.MeasureWrappedBlock(Loc.T(L.LoveIsland.NoLinks), TextStyles.Subheadline,
                SheetTextWidth(screen)).Y + SheetGap * scale;
        return SheetChrome(scale) + MemberPortrait * 2f * scale + SheetGap * scale + linksHeight;
    }

    private void DrawMemberSheet(in SheetFrame frame)
    {
        var member = Ledger.FindMember(sheetMemberId);
        if (member is null)
        {
            sheet.Close();
            return;
        }

        SyncMemberLine(member);
        var scale = UiScale.Current;
        var drawList = frame.DrawList;
        var content = frame.Content;
        var ink = frame.Ink with { W = frame.Ink.W * frame.Opacity };
        var muted = ink with { W = ink.W * 0.72f };
        var left = content.Min.X + SheetPadX * scale;
        var width = MathF.Max(1f, content.Width - SheetPadX * 2f * scale);
        var cursorY = content.Min.Y + SheetGap * scale;
        var radius = MemberPortrait * scale;
        var center = new Vector2(left + radius, cursorY + radius);
        LoveIslandKit.Portrait(drawList, images, member.PortraitUrl, center, radius, ui.Accent,
            MonogramFor(member.Id, member.Name), ink);
        var textLeft = center.X + radius + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, left + width - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var textTop = center.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), Typography.FitText(member.Name, textWidth,
            TextStyles.Headline), ink, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
            Typography.FitText(memberLine, textWidth, TextStyles.Subheadline), muted, TextStyles.Subheadline);
        cursorY += radius * 2f + SheetGap * scale;
        var links = member.Links ?? Array.Empty<CampaignLinkDto>();
        if (memberLinkLabels.Length == 0)
        {
            cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), Loc.T(L.LoveIsland.NoLinks), muted,
                TextStyles.Subheadline, width) + SheetGap * scale;
        }

        for (var index = 0; index < memberLinkLabels.Length && index < links.Length; index++)
        {
            var rect = new Rect(new Vector2(left, cursorY), new Vector2(left + width, cursorY + Button.LargeHeight * scale));
            if (frame.Interactive && Button.Draw(drawList, rect, memberLinkLabels[index], ui.Ink, ButtonStyle.Tinted,
                    id: MemberLinkIds[index]))
            {
                var url = links[index].Url;
                sheet.Close();
                UrlActions.AskThenOpen(url);
            }

            cursorY = rect.Max.Y + Metrics.Space.Sm * scale;
        }

        DrawSheetClose(drawList, left, cursorY, width, in frame);
    }
}
