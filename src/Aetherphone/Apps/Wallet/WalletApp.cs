using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Wallet;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Wallet;

internal sealed class WalletApp : IPhoneApp
{
    private const float RefreshIntervalSeconds = 1.5f;
    private const float SectionGap = 12f;
    private const float BadgeRefreshMillis = 1500f;

    public string Id => "wallet";
    public string DisplayName => Loc.T(L.Apps.Wallet);
    public string Glyph => "G";

    public int BadgeCount
    {
        get
        {
            var now = Environment.TickCount64;
            if (now >= nextBadgeTick)
            {
                nextBadgeTick = now + (long)BadgeRefreshMillis;
                cappedBadge = gameData.LocalPlayer is null ? 0 : WalletReader.CountCapped(gameData);
            }

            return cappedBadge;
        }
    }

    public bool HasBadge => true;

    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly AppSkin ui = new(AppPalettes.Wallet);
    private WalletEntry? gil;
    private WalletSection[] sections = Array.Empty<WalletSection>();
    private float sinceRefresh;
    private int cappedBadge;
    private long nextBadgeTick;

    public WalletApp(GameData gameData, ITextureProvider textures)
    {
        this.gameData = gameData;
        this.textures = textures;
    }

    public void OnOpened() => Rebuild();

    public void OnClosed()
    {
        gil = null;
        sections = Array.Empty<WalletSection>();
    }

    private void Rebuild()
    {
        if (gameData.LocalPlayer is null)
        {
            gil = null;
            sections = Array.Empty<WalletSection>();
            return;
        }

        gil = WalletReader.BuildGil(gameData);
        sections = WalletReader.BuildSections(gameData);
        WalletReader.RefreshAmounts(gil, sections);
        sinceRefresh = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        var content = context.Content;
        ui.Theme = theme;
        ui.Backdrop(SceneChrome.ScreenFrom(content, theme, scale));
        DrawHeader(content, scale);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        if (gil is null)
        {
            Rebuild();
        }

        if (gil is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Wallet.LogInToView), AppPalettes.Wallet.MutedInk);
            return;
        }

        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh >= RefreshIntervalSeconds)
        {
            WalletReader.RefreshAmounts(gil, sections);
            sinceRefresh = 0f;
        }

        using (AppSurface.Begin(body))
        {
            UiAnchors.Report("wallet.gil", CurrencyRow.Hero(gil, textures, AppPalettes.Wallet));
            ImGui.Dummy(new Vector2(0f, 6f * scale));

            var currenciesAnchored = false;
            for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
            {
                var section = sections[sectionIndex];
                if (section.Entries.Length == 0)
                {
                    continue;
                }

                ui.SectionHeading(Loc.T(section.Title), currenciesAnchored ? 4f : 8f);
                var cardRect = DrawSectionCard(section, scale);
                if (!currenciesAnchored)
                {
                    UiAnchors.Report("wallet.currencies", cardRect);
                    currenciesAnchored = true;
                }

                ImGui.Dummy(new Vector2(0f, SectionGap * scale));
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
        }
    }

    private void DrawHeader(Rect content, float scale)
    {
        var rowCenterY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, rowCenterY), DisplayName, AppPalettes.Wallet.TitleInk,
            1.15f, FontWeight.SemiBold);
    }

    private Rect DrawSectionCard(WalletSection section, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var rowCount = section.Entries.Length;
        var origin = ImGui.GetCursorScreenPos();
        var card = GroupCard.Begin(ui, rowCount, CurrencyRow.Height);
        for (var entryIndex = 0; entryIndex < rowCount; entryIndex++)
        {
            var contentRect = card.NextRow();
            var band = new Rect(new Vector2(origin.X, contentRect.Min.Y),
                new Vector2(origin.X + width, contentRect.Max.Y));
            CurrencyRow.Draw(band, contentRect, section.Entries[entryIndex], textures, ui.Palette,
                Metrics.Radius.Md * scale, entryIndex == 0, entryIndex == rowCount - 1);
        }

        card.End();
        return new Rect(origin, origin + new Vector2(width, rowCount * CurrencyRow.Height * scale));
    }

    public void Dispose()
    {
    }
}
