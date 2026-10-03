using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallet;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Wallet;

internal sealed partial class WalletApp
{
    private const int RecentCount = 4;
    private const float CardAspect = 0.56f;
    private const float CardPad = 20f;
    private const float CardGlassOpacity = 0.92f;
    private const float CardIconSize = 30f;
    private const float CardIconGap = 10f;
    private const float CardLineGap = 4f;
    private const float CardSubAlpha = 0.82f;
    private const float CardIconWashAlpha = 0.22f;
    private const float WeeklyRingRadius = 30f;
    private const float WeeklyRingThickness = 6.5f;
    private const float WeeklyTrackAlpha = 0.16f;
    private const float WeeklyTextGap = 16f;
    private const float HintGap = 2f;

    private static readonly Vector4 CardInk = new(1f, 1f, 1f, 1f);

    private readonly List<WalletEntry> attention = new();
    private RollingValue gilRoll;
    private Spring weeklyFill;
    private CachedText weeklyStatus;

    private void PrimeMotion()
    {
        gilRoll = default;
        weeklyFill.SnapTo(0f);
    }

    private void DrawRoot(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        if (!wallet.Ready)
        {
            DrawSignedOut(navBar.Body);
        }
        else
        {
            using (AppSurface.Begin(navBar.Body))
            {
                var scale = UiScale.Current;
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var cursorY = DrawGilCard(drawList, origin, width, scale);
                var limited = Limited();
                cursorY = DrawWeekly(drawList, new Vector2(origin.X, cursorY), width, limited, scale);
                cursorY = DrawAttention(drawList, new Vector2(origin.X, cursorY), width, limited is null, scale);
                cursorY = DrawRecent(drawList, new Vector2(origin.X, cursorY), width, scale);
                cursorY = DrawSections(drawList, new Vector2(origin.X, cursorY), width, scale);
                ReserveTo(origin, width, cursorY + BottomPad * scale);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "wallet.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private float DrawGilCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var gil = wallet.Gil!;
        var pad = CardPad * scale;
        var iconSize = CardIconSize * scale;
        var amountStyle = TextStyles.WidgetDisplay;
        var amountHeight = Typography.LineHeight(amountStyle);
        var summaryHeight = Typography.LineHeight(TextStyles.Subheadline);
        var natural = pad * 2f + iconSize + amountHeight + CardLineGap * scale + summaryHeight +
                      Metrics.Space.Lg * scale;
        var height = MathF.Max(width * CardAspect, natural);
        var restMax = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("wallet.gil", new Rect(origin, restMax));
        var hovered = UiInteract.Hover(origin, restMax);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale("wallet.card", pressed, Motion.PressScaleCard);
        var center = (origin + restMax) * 0.5f;
        var half = (restMax - origin) * 0.5f * press;
        var min = center - half;
        var max = center + half;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Material.AccentGlass(drawList, min, max, Metrics.Radius.Widget * scale, scale, ui.Accent, CardGlassOpacity);
        var subInk = Palette.WithAlpha(CardInk, CardSubAlpha);
        var left = min.X + pad;
        var right = max.X - pad;
        var iconCenter = new Vector2(left + iconSize * 0.5f, min.Y + pad + iconSize * 0.5f);
        drawList.AddCircleFilled(iconCenter, iconSize * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(CardInk, CardIconWashAlpha)), 32);
        WalletArt.Icon(drawList, textures, gil.IconId, iconCenter, iconSize * 0.78f);
        var nameLeft = left + iconSize + CardIconGap * scale;
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var ownerWidth = 0f;
        if (wallet.CharacterName.Length > 0)
        {
            var owner = Typography.FitText(wallet.CharacterName, (right - nameLeft) * 0.5f, TextStyles.Footnote);
            var ownerSize = Typography.Measure(owner, TextStyles.Footnote);
            ownerWidth = ownerSize.X + CardIconGap * scale;
            Typography.Draw(drawList, new Vector2(right - ownerSize.X, iconCenter.Y - ownerSize.Y * 0.5f), owner, subInk,
                TextStyles.Footnote);
        }

        Typography.Draw(drawList, new Vector2(nameLeft, iconCenter.Y - nameHeight * 0.5f),
            Typography.FitText(gil.Name, MathF.Max(1f, right - ownerWidth - nameLeft), TextStyles.Headline), CardInk,
            TextStyles.Headline);

        var summaryTop = max.Y - pad - summaryHeight;
        Typography.Draw(drawList, new Vector2(left, summaryTop),
            Typography.FitText(text.Summary(wallet, gil.ItemId), right - left, TextStyles.Subheadline), subInk,
            TextStyles.Subheadline);
        var target = (int)Math.Clamp(gil.Amount, 0, int.MaxValue);
        gilRoll.Update(target, MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        var amount = NumberText.Group(gilRoll.Display);
        var fitted = WidgetText.FitStyle(amount, amountStyle, right - left, true);
        var fittedHeight = Typography.LineHeight(fitted);
        WidgetText.Tabular(drawList, new Vector2(left, summaryTop - CardLineGap * scale - fittedHeight), amount,
            CardInk, fitted);
        if (UiInteract.Click(origin, restMax, hovered))
        {
            Open(gil.ItemId);
        }

        return restMax.Y;
    }

    private WalletEntry? Limited()
    {
        var entries = wallet.Entries;
        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index].HasWeeklyCap)
            {
                return entries[index];
            }
        }

        return null;
    }

    private float DrawWeekly(ImDrawListPtr drawList, Vector2 origin, float width, WalletEntry? limited, float scale)
    {
        if (limited is null)
        {
            return origin.Y;
        }

        var top = origin.Y + Metrics.Space.Md * scale;
        var pad = Metrics.Space.Lg * scale;
        var ringRadius = WeeklyRingRadius * scale;
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var statusHeight = Typography.LineHeight(TextStyles.Subheadline);
        var textHeight = eyebrowHeight + titleHeight + statusHeight + WalletArt.LineGap * scale * 2f;
        var height = MathF.Max(ringRadius * 2f, textHeight) + pad * 2f;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + height);
        UiAnchors.Report("wallet.capped", new Rect(min, max));
        if (!ImGui.IsRectVisible(min, max))
        {
            return max.Y;
        }

        var row = new Rect(min, max);
        WalletArt.Card(drawList, ui, min, max, scale);
        var hovered = WalletArt.RowInteraction(drawList, ui, row, scale);
        var done = limited.WeeklyAmount >= limited.WeeklyCap;
        var tint = done ? WalletArt.GoldInk : ui.Accent;
        var fraction = WalletMath.Fraction(limited.WeeklyAmount, limited.WeeklyCap);
        var shown = Math.Clamp(Step(ref weeklyFill, fraction), 0f, 1f);
        var ringCenter = new Vector2(min.X + pad + ringRadius, min.Y + height * 0.5f);
        var thickness = WeeklyRingThickness * scale;
        ProgressRing.Track(drawList, ringCenter, ringRadius - thickness * 0.5f, thickness,
            Palette.WithAlpha(tint, WeeklyTrackAlpha));
        ProgressRing.Fill(drawList, ringCenter, ringRadius - thickness * 0.5f, thickness, shown, tint);
        var weeklyText = NumberText.Group(limited.WeeklyAmount);
        var weeklySize = new Vector2(WidgetText.TabularWidth(weeklyText, TextStyles.SubheadlineEmphasized),
            Typography.LineHeight(TextStyles.SubheadlineEmphasized));
        WidgetText.Tabular(drawList, ringCenter - weeklySize * 0.5f, weeklyText, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);

        var textLeft = ringCenter.X + ringRadius + WeeklyTextGap * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var lineTop = min.Y + (height - textHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, lineTop),
            Typography.FitText(Loc.T(L.Wallet.WeeklyLimit), textWidth, TextStyles.FootnoteEmphasized), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        lineTop += eyebrowHeight + WalletArt.LineGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, lineTop),
            Typography.FitText(limited.Name, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        lineTop += titleHeight + WalletArt.LineGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, lineTop),
            Typography.FitText(WeeklyStatus(limited, done), textWidth, TextStyles.Subheadline),
            done ? WalletArt.GoldInk : ui.MutedInk, TextStyles.Subheadline);
        if (UiInteract.Click(min, max, hovered))
        {
            Open(limited.ItemId);
        }

        return max.Y;
    }

    private string WeeklyStatus(WalletEntry limited, bool done)
    {
        var reset = GameSchedule.NextWeeklyReset(DateTime.UtcNow);
        var remaining = reset - DateTime.UtcNow;
        var left = WalletMath.Remaining(limited.WeeklyAmount, limited.WeeklyCap);
        var key = ((long)remaining.TotalMinutes << 24) ^ left ^ (done ? 1L << 62 : 0L);
        if (weeklyStatus.IsCurrent(key))
        {
            return weeklyStatus.Value;
        }

        var until = TimeText.Until(remaining);
        return weeklyStatus.Store(key, done
            ? Loc.T(L.Wallet.WeeklyDone, until)
            : Loc.T(L.Wallet.WeeklyLeft, NumberText.Group(left), until));
    }

    private float DrawAttention(ImDrawListPtr drawList, Vector2 origin, float width, bool anchorRows, float scale)
    {
        attention.Clear();
        var entries = wallet.Entries;
        for (var index = 0; index < entries.Length; index++)
        {
            if (WalletMath.NeedsAttention(entries[index].Level))
            {
                attention.Add(entries[index]);
            }
        }

        if (attention.Count == 0)
        {
            return origin.Y;
        }

        var cursorY = origin.Y + WalletArt.SectionGap * scale;
        cursorY += DrawSectionTitle(drawList, new Vector2(origin.X, cursorY), width, Loc.T(L.Wallet.NearCapTitle),
            string.Empty, out _, scale);
        cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Wallet.NearCapHint),
            ui.MutedInk, TextStyles.Footnote, width);
        cursorY += (WalletArt.HeaderGap + HintGap) * scale;
        return DrawRowsCard(drawList, new Vector2(origin.X, cursorY), width,
            CollectionsMarshal.AsSpan(attention), anchorRows ? "wallet.capped" : null,
            scale);
    }

    private float DrawRecent(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var headerTop = origin.Y + WalletArt.SectionGap * scale;
        var trailing = text.LineCount > RecentCount ? Loc.T(L.Wallet.SeeAll) : string.Empty;
        var cursorY = headerTop + DrawSectionTitle(drawList, new Vector2(origin.X, headerTop), width,
            Loc.T(L.Wallet.RecentTitle), trailing, out var seeAll, scale);
        cursorY += WalletArt.HeaderGap * scale;
        if (seeAll)
        {
            OpenActivity();
        }

        float bottom;
        if (text.LineCount == 0)
        {
            var title = Loc.T(L.Wallet.RecentEmptyTitle);
            var body = Loc.T(L.Wallet.RecentEmptyBody);
            var height = WalletArt.PanelHeight(title, body, width, scale);
            WalletArt.Panel(drawList, ui, new Vector2(origin.X, cursorY), width, height, FontAwesomeIcon.Receipt, title,
                body, scale);
            bottom = cursorY + height;
        }
        else
        {
            bottom = DrawLinesCard(drawList, new Vector2(origin.X, cursorY), width, 0,
                Math.Min(RecentCount, text.LineCount), 0, true, true, scale);
        }

        UiAnchors.Report("wallet.recent", new Rect(new Vector2(origin.X, headerTop), new Vector2(origin.X + width,
            bottom)));
        return bottom;
    }

    private float DrawSections(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var cursorY = origin.Y;
        var sections = wallet.Sections;
        for (var index = 0; index < sections.Length; index++)
        {
            var section = sections[index];
            cursorY += WalletArt.SectionGap * scale;
            cursorY += DrawSectionTitle(drawList, new Vector2(origin.X, cursorY), width, Loc.T(Title(section.Group)),
                string.Empty, out _, scale);
            cursorY += WalletArt.HeaderGap * scale;
            cursorY = DrawRowsCard(drawList, new Vector2(origin.X, cursorY), width, section.Entries, null, scale);
        }

        return cursorY;
    }

    private static LocString Title(WalletGroup group) => group switch
    {
        WalletGroup.Tomestones => L.Wallet.SectionTomestones,
        WalletGroup.Scrips => L.Wallet.SectionCrafting,
        WalletGroup.Hunt => L.Wallet.SectionHunt,
        WalletGroup.GrandCompany => L.Wallet.SectionGrandCompany,
        WalletGroup.Pvp => L.Wallet.SectionPvp,
        _ => L.Wallet.SectionOther,
    };
}
