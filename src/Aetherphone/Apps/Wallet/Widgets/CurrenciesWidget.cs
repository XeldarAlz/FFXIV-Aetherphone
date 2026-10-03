using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Wallet;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using CurrencyKind = Aetherphone.Core.Wallet.CurrencyKind;

namespace Aetherphone.Apps.Wallet.Widgets;

internal sealed class CurrenciesWidget : IHomeWidget
{
    private const int TomestoneSlots = 2;
    private const int GridColumns = 2;
    private const float IconUnits = 22f;
    private const float GridIconUnits = 20f;
    private const float BarUnits = 5f;
    private const float GridBarUnits = 3f;
    private const float GridRowMinimumUnits = 26f;
    private const float GridRowMaximumUnits = 36f;
    private const float LeftColumnFraction = 0.44f;
    private const float RelevanceHours = 24f;
    private const float WeeklyRelevance = 0.5f;

    private static readonly Vector4 WalletAccent = AppAccents.For("wallet");

    private readonly WalletService wallet;
    private readonly ActivityTracker activity;
    private readonly WalletEntry?[] tomestones = new WalletEntry?[TomestoneSlots];
    private readonly WidgetStates<Purse> purses = new();
    private readonly CachedText[] tomestoneAmounts = new CachedText[TomestoneSlots];
    private readonly CachedText[] tomestoneCaptions = new CachedText[TomestoneSlots];
    private WalletEntry[] entries = Array.Empty<WalletEntry>();
    private CachedText[] entryAmounts = Array.Empty<CachedText>();
    private CachedText earnedText;

    public CurrenciesWidget(WalletService wallet, ActivityTracker activity)
    {
        this.wallet = wallet;
        this.activity = activity;
    }

    private WalletEntry? Gil => wallet.Gil;

    public string Id => "wallet.currencies";
    public string DisplayName => Loc.T(L.WidgetsAdventure.CurrenciesName);
    public string Description => Loc.T(L.WidgetsAdventure.CurrenciesDescription);
    public string AppId => "wallet";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config)
    {
        if (!AdventureWidgetArt.IsLoggedIn)
        {
            return 0f;
        }

        EnsureBuilt();
        var limited = tomestones[0];
        if (Gil is null || limited is null || !limited.HasWeeklyCap || limited.WeeklyAmount >= limited.WeeklyCap)
        {
            return 0f;
        }

        var utcNow = DateTime.UtcNow;
        return (GameSchedule.NextWeeklyReset(utcNow) - utcNow).TotalHours <= RelevanceHours ? WeeklyRelevance : 0f;
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var loggedIn = AdventureWidgetArt.IsLoggedIn;
        var sample = !loggedIn && context.Preview;
        if (!loggedIn && !sample)
        {
            var top = WidgetChrome.Header(context, ink, AppId, L.WidgetsAdventure.CurrenciesName, WalletAccent);
            WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, top), FontAwesomeIcon.UserCircle, default,
                Loc.T(L.WidgetsAdventure.LogIn), string.Empty);
            return;
        }

        EnsureBuilt();

        switch (context.Size)
        {
            case WidgetSize.Small:
                DrawSmall(context, ink, sample);
                return;
            case WidgetSize.Large:
                DrawLarge(context, ink, sample);
                return;
            default:
                DrawMedium(context, ink, sample);
                return;
        }
    }

    private void EnsureBuilt()
    {
        var current = wallet.Entries;
        if (ReferenceEquals(current, entries))
        {
            return;
        }

        entries = current;
        entryAmounts = new CachedText[current.Length];
        tomestones[0] = null;
        tomestones[1] = null;
        for (var index = 0; index < current.Length; index++)
        {
            PlaceTomestone(current[index]);
        }
    }

    private void PlaceTomestone(WalletEntry entry)
    {
        if (entry.Kind == CurrencyKind.LimitedTomestone)
        {
            tomestones[1] = tomestones[0] ?? tomestones[1];
            tomestones[0] = entry;
            return;
        }

        if (entry.Kind != CurrencyKind.Tomestone)
        {
            return;
        }

        if (tomestones[0] is null)
        {
            tomestones[0] = entry;
            return;
        }

        tomestones[1] ??= entry;
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, bool sample)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var headerBottom = WidgetChrome.Header(context, ink, AppId, GilName(), WalletAccent);
        DrawGilHero(context, ink, sample, new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * scale * 0.5f),
            content.Width, WidgetType.DisplayCompact);
        DrawEarned(context, ink, sample, content);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, bool sample)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var leftWidth = content.Width * LeftColumnFraction;
        var left = new Rect(content.Min, new Vector2(content.Min.X + leftWidth, content.Max.Y));
        var headerBottom = WidgetChrome.Header(context, ink, AppId, GilName(), WalletAccent);
        DrawGilHero(context, ink, sample, new Vector2(left.Min.X, headerBottom + gutter * 0.5f), left.Width,
            WidgetType.DisplayCompact);
        DrawEarned(context, ink, sample, left);

        var right = new Rect(new Vector2(left.Max.X + gutter * 2f, content.Min.Y), content.Max);
        var rowHeight = right.Height / TomestoneSlots;
        for (var slot = 0; slot < TomestoneSlots; slot++)
        {
            var top = right.Min.Y + slot * rowHeight;
            DrawTomestone(context, ink, sample, slot,
                new Rect(new Vector2(right.Min.X, top), new Vector2(right.Max.X, top + rowHeight)));
        }
    }

    private void DrawLarge(in WidgetContext context, in WidgetInk ink, bool sample)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var drawList = context.DrawList;
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.WidgetsAdventure.CurrenciesName, WalletAccent);
        var heroTop = headerBottom + gutter * 0.5f;
        var heroHeight = DrawGilHero(context, ink, sample, new Vector2(content.Min.X, heroTop), content.Width * 0.62f,
            WidgetType.DisplayCompact);
        var earned = EarnedText(sample);
        if (earned.Length > 0)
        {
            var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
            WidgetText.DrawRight(drawList, content.Max.X, heroTop + heroHeight - captionHeight * 1.2f, earned,
                ink.Secondary, WidgetType.Caption);
        }

        var separatorY = heroTop + heroHeight + gutter;
        WidgetChrome.Separator(context, ink, content.Min.X, content.Max.X, separatorY);
        var grid = new Rect(new Vector2(content.Min.X, separatorY + gutter), content.Max);
        DrawGrid(context, ink, sample, grid);
    }

    private float DrawGilHero(in WidgetContext context, in WidgetInk ink, bool sample, Vector2 topLeft,
        float maxWidth, in TextStyle style)
    {
        var target = sample ? WidgetSamples.Gil : Gil?.Amount ?? 0;
        var clamped = (int)Math.Clamp(target, 0, int.MaxValue);
        var purse = purses.For(context);
        purse.Gil.Update(clamped, purse.GilFrame.Delta(context.Delta));
        var text = WidgetText.Number(ref purse.GilText, purse.Gil.Display);
        var fitted = WidgetText.FitStyle(text, style, maxWidth, true);
        WidgetText.Tabular(context.DrawList, topLeft, text, ink.Primary, fitted);
        return Typography.Measure(text, fitted).Y;
    }

    private void DrawEarned(in WidgetContext context, in WidgetInk ink, bool sample, Rect column)
    {
        var scale = context.Scale;
        var icon = IconUnits * scale;
        var bottom = column.Max.Y;
        var drawn = AdventureWidgetArt.GameIcon(context.DrawList, Gil?.IconId ?? 0,
            new Vector2(column.Min.X, bottom - icon), new Vector2(column.Min.X + icon, bottom), 0f, ink);
        var earned = EarnedText(sample);
        if (earned.Length == 0)
        {
            return;
        }

        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var left = drawn ? column.Min.X + icon + WidgetMetrics.RowGap * 2f * scale : column.Min.X;
        WidgetText.Draw(context.DrawList, new Vector2(left, bottom - icon * 0.5f - captionHeight * 0.5f), earned,
            ink.Secondary, WidgetType.Caption, MathF.Max(1f, column.Max.X - left));
    }

    private void DrawTomestone(in WidgetContext context, in WidgetInk ink, bool sample, int slot, Rect row)
    {
        var entry = tomestones[slot];
        if (entry is null)
        {
            return;
        }

        var drawList = context.DrawList;
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var icon = IconUnits * scale;
        var headlineHeight = WidgetText.LineHeight(WidgetType.Headline);
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var bar = BarUnits * scale;
        var blockHeight = headlineHeight + WidgetMetrics.RowGap * 2f * scale + bar + WidgetMetrics.RowGap * scale +
                          captionHeight;
        var top = row.Center.Y - blockHeight * 0.5f;
        AdventureWidgetArt.GameIcon(drawList, entry.IconId,
            new Vector2(row.Min.X, top + headlineHeight * 0.5f - icon * 0.5f),
            new Vector2(row.Min.X + icon, top + headlineHeight * 0.5f + icon * 0.5f), 0f, ink);
        var amount = sample ? WidgetSamples.CurrencyAmount(slot) : entry.Amount;
        var amountText = WidgetText.Number(ref tomestoneAmounts[slot], amount);
        var amountWidth = WidgetText.TabularRight(drawList, row.Max.X, top, amountText, ink.Primary,
            WidgetType.Headline);
        var nameLeft = row.Min.X + icon + gutter;
        WidgetText.Draw(drawList, new Vector2(nameLeft, top), entry.Name, ink.Primary, WidgetType.Headline,
            MathF.Max(1f, row.Max.X - amountWidth - gutter - nameLeft));

        var weekly = entry.Kind == CurrencyKind.LimitedTomestone;
        var weeklyAmount = sample ? WidgetSamples.TomestoneWeekly : entry.WeeklyAmount;
        var weeklyCap = sample ? WidgetSamples.TomestoneWeeklyCap : entry.WeeklyCap;
        var fraction = weekly
            ? weeklyCap > 0 ? weeklyAmount / (float)weeklyCap : 0f
            : entry.Cap > 0 ? amount / (float)entry.Cap : 0f;
        var barTop = top + headlineHeight + WidgetMetrics.RowGap * 2f * scale;
        WidgetChrome.Bar(drawList, new Rect(new Vector2(row.Min.X, barTop), new Vector2(row.Max.X, barTop + bar)),
            purses.For(context).TomestoneBars[slot].Fraction(fraction, context.Delta, !context.Preview), ink.Fill,
            ink.Accent(BarTint(fraction)));
        var caption = Caption(slot, weekly, weeklyAmount, weeklyCap, amount, entry.Cap);
        WidgetText.Draw(drawList, new Vector2(row.Min.X, barTop + bar + WidgetMetrics.RowGap * scale), caption,
            ink.Secondary, WidgetType.Caption, row.Width);
    }

    private void DrawGrid(in WidgetContext context, in WidgetInk ink, bool sample, Rect grid)
    {
        if (entries.Length == 0 || grid.Height <= 0f)
        {
            return;
        }

        var drawList = context.DrawList;
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var rowsNeeded = (entries.Length + GridColumns - 1) / GridColumns;
        var rowsFit = Math.Max(1, (int)(grid.Height / (GridRowMinimumUnits * scale)));
        var rows = Math.Min(rowsNeeded, rowsFit);
        var rowHeight = MathF.Min(GridRowMaximumUnits * scale, grid.Height / rows);
        var columnWidth = (grid.Width - gutter * 2f) / GridColumns;
        var icon = GridIconUnits * scale;
        var bar = GridBarUnits * scale;
        var bodyHeight = WidgetText.LineHeight(WidgetType.Body);
        var visible = Math.Min(entries.Length, rows * GridColumns);
        var bars = purses.For(context).EntryBars(entries.Length);
        for (var index = 0; index < visible; index++)
        {
            var entry = entries[index];
            var column = index % GridColumns;
            var rowIndex = index / GridColumns;
            var left = grid.Min.X + column * (columnWidth + gutter * 2f);
            var centerY = grid.Min.Y + rowIndex * rowHeight + rowHeight * 0.5f;
            AdventureWidgetArt.GameIcon(drawList, entry.IconId, new Vector2(left, centerY - icon * 0.5f),
                new Vector2(left + icon, centerY + icon * 0.5f), 0f, ink);
            var textLeft = left + icon + gutter;
            var textRight = left + columnWidth;
            var amount = sample ? WidgetSamples.CurrencyAmount(index) : entry.Amount;
            var hasBar = entry.Cap > 0 || entry.HasWeeklyCap;
            var textTop = hasBar ? centerY - (bodyHeight + bar + WidgetMetrics.RowGap * scale) * 0.5f
                : centerY - bodyHeight * 0.5f;
            var text = WidgetText.Number(ref entryAmounts[index], amount);
            WidgetText.Tabular(drawList, new Vector2(textLeft, textTop), text, ink.Primary, WidgetType.Body);
            if (!hasBar)
            {
                continue;
            }

            var fraction = entry.HasWeeklyCap
                ? entry.WeeklyAmount / (float)entry.WeeklyCap
                : amount / (float)entry.Cap;
            var barTop = textTop + bodyHeight + WidgetMetrics.RowGap * scale;
            WidgetChrome.Bar(drawList,
                new Rect(new Vector2(textLeft, barTop), new Vector2(textRight, barTop + bar)),
                bars[index].Fraction(fraction, context.Delta, !context.Preview), ink.Fill, ink.Accent(BarTint(fraction)));
        }
    }

    private string Caption(int slot, bool weekly, long weeklyAmount, long weeklyCap, long amount, long cap)
    {
        if (weekly)
        {
            var weeklyKey = weeklyAmount * 100000L + weeklyCap;
            return tomestoneCaptions[slot].IsCurrent(weeklyKey)
                ? tomestoneCaptions[slot].Value
                : tomestoneCaptions[slot].Store(weeklyKey,
                    Loc.T(L.Wallet.WeeklyCap, NumberText.Group(weeklyAmount), NumberText.Group(weeklyCap)));
        }

        var key = -1 - (amount * 100000L + cap);
        return tomestoneCaptions[slot].IsCurrent(key)
            ? tomestoneCaptions[slot].Value
            : tomestoneCaptions[slot].Store(key,
                string.Concat(NumberText.Group(amount), " / ", NumberText.Group(cap)));
    }

    private string EarnedText(bool sample)
    {
        var earned = sample || !activity.IsTracking ? 0L : activity.Today.GilEarned;
        if (earned <= 0)
        {
            return string.Empty;
        }

        return earnedText.IsCurrent(earned)
            ? earnedText.Value
            : earnedText.Store(earned, Loc.T(L.WidgetsAdventure.Today, NumberText.Group(earned)));
    }

    private static Vector4 BarTint(float fraction) =>
        fraction >= WalletMath.NearFraction ? WalletArt.GoldInk : WalletAccent;

    private string GilName() => Gil?.Name ?? Loc.T(L.WidgetsAdventure.CurrenciesName);

    public void Dispose()
    {
    }

    private sealed class Purse
    {
        public readonly WidgetEase[] TomestoneBars = new WidgetEase[TomestoneSlots];
        public RollingValue Gil;
        public WidgetFrame GilFrame;
        public CachedText GilText;
        private WidgetEase[] entryBars = Array.Empty<WidgetEase>();

        public WidgetEase[] EntryBars(int count)
        {
            if (entryBars.Length != count)
            {
                entryBars = new WidgetEase[count];
            }

            return entryBars;
        }
    }
}
