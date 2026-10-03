using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallet;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Wallet;

internal sealed partial class WalletApp : IPhoneApp
{
    private const float BottomPad = 28f;

    public string Id => WalletService.AppId;
    public string DisplayName => Loc.T(L.Apps.Wallet);
    public string Glyph => "G";
    public int BadgeCount => wallet.FullCount;
    public bool HasBadge => true;

    private readonly WalletService wallet;
    private readonly ITextureProvider textures;
    private readonly WalletText text;
    private readonly AppSkin ui = new(AppPalettes.Wallet);
    private readonly ViewRouter<WalletView> router;
    private readonly RouterDraw<WalletView> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    public WalletApp(WalletService wallet, GameData gameData, ITextureProvider textures)
    {
        this.wallet = wallet;
        this.textures = textures;
        text = new WalletText(gameData);
        router = new ViewRouter<WalletView>(WalletView.Root());
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        PrimeMotion();
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        if (wallet.Ready)
        {
            TourHolds.Release(Id);
            text.Sync(wallet);
        }
        else
        {
            TourHolds.Hold(Id);
            if (router.Depth > 1)
            {
                router.Reset();
            }
        }

        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, theme, scale));
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(WalletView view, Rect area, int depth)
    {
        ui.Body(area);
        switch (view.Kind)
        {
            case WalletViewKind.Currency:
                DrawDetail(area, view.ItemId, depth);
                break;
            case WalletViewKind.Activity:
                DrawActivity(area);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void Open(uint itemId)
    {
        UiFeedback.Play(UiSound.Tap);
        PrimeDetail(itemId);
        router.Push(WalletView.ForCurrency(itemId));
    }

    private void OpenActivity()
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(WalletView.ForActivity());
    }

    private static float Step(ref Spring spring, float target)
    {
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        return spring.Step(target, Motion.Sheet, deltaSeconds);
    }

    private static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    private float DrawRowsCard(ImDrawListPtr drawList, Vector2 origin, float width, ReadOnlySpan<WalletEntry> entries,
        string? anchor, float scale)
    {
        var rowHeight = WalletArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + entries.Length * rowHeight);
        if (!ImGui.IsRectVisible(origin, max))
        {
            return max.Y;
        }

        WalletArt.Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        for (var index = 0; index < entries.Length; index++)
        {
            var top = origin.Y + index * rowHeight;
            var row = new Rect(new Vector2(origin.X, top), new Vector2(max.X, top + rowHeight));
            if (index > 0)
            {
                WalletArt.Hairline(drawList, ui, origin.X + pad + (WalletArt.DialSize + WalletArt.TextGap) * scale,
                    max.X - pad, top);
            }

            if (index == 0 && anchor is not null)
            {
                UiAnchors.Report(anchor, row);
            }

            if (DrawEntryRow(drawList, row, entries[index], true, scale))
            {
                Open(entries[index].ItemId);
            }
        }

        return max.Y;
    }

    private bool DrawEntryRow(ImDrawListPtr drawList, Rect row, WalletEntry entry, bool interactive, float scale)
    {
        var hovered = interactive && WalletArt.RowInteraction(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var dialSize = WalletArt.DialSize * scale;
        var center = new Vector2(row.Min.X + pad + dialSize * 0.5f, row.Center.Y);
        var level = entry.HasWeeklyCap ? WalletMath.Level(entry.WeeklyAmount, entry.WeeklyCap) : entry.Level;
        var fraction = entry.HasWeeklyCap ? WalletMath.Fraction(entry.WeeklyAmount, entry.WeeklyCap) : entry.Fraction;
        var tint = WalletArt.LevelInk(level, ui.Accent);
        WalletArt.Dial(drawList, textures, entry.IconId, center, dialSize, fraction, tint, level != CapLevel.None,
            WalletArt.Backing(ui.TitleInk), scale);
        var valueWidth = WalletArt.Value(drawList, row.Max.X - pad, row.Center.Y, NumberText.Group(entry.Amount),
            ui.TitleInk);
        var textLeft = center.X + dialSize * 0.5f + WalletArt.TextGap * scale;
        var textRight = row.Max.X - pad - valueWidth - WalletArt.ValueGap * scale;
        var subtitleInk = !entry.HasWeeklyCap && WalletMath.NeedsAttention(level) ? WalletArt.GoldInk : ui.MutedInk;
        WalletArt.Labels(drawList, textLeft, textRight, row.Center.Y, entry.Name, text.Subtitle(entry), ui.TitleInk,
            subtitleInk, scale);
        return hovered && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private float DrawLinesCard(ImDrawListPtr drawList, Vector2 origin, float width, int first, int count,
        uint onlyItem, bool stampSubtitle, bool interactive, float scale)
    {
        var rowHeight = WalletArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + count * rowHeight);
        if (!ImGui.IsRectVisible(origin, max))
        {
            return max.Y;
        }

        WalletArt.Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var drawn = 0;
        for (var index = first; index < text.LineCount && drawn < count; index++)
        {
            var line = text.Line(index);
            if (onlyItem != 0 && line.ItemId != onlyItem)
            {
                continue;
            }

            var top = origin.Y + drawn * rowHeight;
            var row = new Rect(new Vector2(origin.X, top), new Vector2(max.X, top + rowHeight));
            if (drawn > 0)
            {
                WalletArt.Hairline(drawList, ui, origin.X + pad + (WalletArt.DialSize + WalletArt.TextGap) * scale,
                    max.X - pad, top);
            }

            drawn++;
            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            if (DrawLineRow(drawList, row, line, stampSubtitle, interactive, scale))
            {
                Open(line.ItemId);
            }
        }

        return max.Y;
    }

    private bool DrawLineRow(ImDrawListPtr drawList, Rect row, WalletLine line, bool stampSubtitle, bool interactive,
        float scale)
    {
        if (!wallet.TryGetEntry(line.ItemId, out var entry))
        {
            return false;
        }

        var hovered = interactive && WalletArt.RowInteraction(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var dialSize = WalletArt.DialSize * scale;
        var center = new Vector2(row.Min.X + pad + dialSize * 0.5f, row.Center.Y);
        WalletArt.Dial(drawList, textures, entry.IconId, center, dialSize, 0f, ui.Accent, false,
            WalletArt.Backing(ui.TitleInk), scale);
        var valueInk = line.Delta > 0 ? WalletArt.GainInk : ui.TitleInk;
        var valueWidth = WalletArt.Value(drawList, row.Max.X - pad, row.Center.Y, line.DeltaText, valueInk);
        var textLeft = center.X + dialSize * 0.5f + WalletArt.TextGap * scale;
        var textRight = row.Max.X - pad - valueWidth - WalletArt.ValueGap * scale;
        WalletArt.Labels(drawList, textLeft, textRight, row.Center.Y, entry.Name,
            stampSubtitle ? line.StampSubtitle : line.ClockSubtitle, ui.TitleInk, ui.MutedInk, scale);
        return hovered && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private float DrawSectionTitle(ImDrawListPtr drawList, Vector2 origin, float width, string title,
        string trailing, out bool trailingClicked, float scale)
    {
        trailingClicked = false;
        var reserve = trailing.Length > 0
            ? Typography.Measure(trailing, TextStyles.Body).X + WalletArt.ValueGap * scale
            : 0f;
        var height = WalletArt.SectionHeader(drawList, origin, width, title, ui.TitleInk, reserve, scale);
        if (trailing.Length == 0)
        {
            return height;
        }

        var size = Typography.Measure(trailing, TextStyles.Body);
        var tapHeight = MathF.Max(height, Metrics.Size.TapTarget * scale);
        var hitMin = new Vector2(origin.X + width - size.X - WalletArt.ValueGap * scale,
            origin.Y + (height - tapHeight) * 0.5f);
        var hitMax = new Vector2(origin.X + width, hitMin.Y + tapHeight);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var ink = hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent;
        Typography.Draw(drawList, new Vector2(origin.X + width - size.X, origin.Y + (height - size.Y) * 0.5f),
            trailing, ink, TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        trailingClicked = UiInteract.Click(hitMin, hitMax, hovered);
        return height;
    }

    private void DrawSignedOut(Rect body)
    {
        WalletArt.StateScreen(ImGui.GetWindowDrawList(), ui, body, FontAwesomeIcon.Coins,
            Loc.T(L.Wallet.SignedOutTitle), Loc.T(L.Wallet.SignedOutBody), UiScale.Current);
    }

    public void Dispose()
    {
    }
}
