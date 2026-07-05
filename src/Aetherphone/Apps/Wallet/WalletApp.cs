using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Wallet;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Wallet;

internal sealed class WalletApp : IPhoneApp
{
    private const float RefreshIntervalSeconds = 1.5f;
    public string Id => "wallet";
    public string DisplayName => Loc.T(L.Apps.Wallet);
    public string Glyph => "G";
    public Vector4 Accent => new(0.26f, 0.78f, 0.52f, 1f);
    public int BadgeCount => 0;
    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private WalletEntry? gil;
    private WalletSection[] sections = Array.Empty<WalletSection>();
    private float sinceRefresh;

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
        AppHeader.Draw(context, DisplayName);
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        if (gil is null)
        {
            Rebuild();
        }

        if (gil is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Wallet.LogInToView), theme.TextMuted);
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
            CurrencyRow.Hero(gil, textures, theme);
            for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
            {
                var section = sections[sectionIndex];
                if (section.Entries.Length == 0)
                {
                    continue;
                }

                SettingsSection.Header(section.Title, theme);
                var card = GroupCard.Begin(theme, section.Entries.Length, CurrencyRow.Height);
                for (var entryIndex = 0; entryIndex < section.Entries.Length; entryIndex++)
                {
                    CurrencyRow.Draw(card.NextRow(), section.Entries[entryIndex], textures, theme);
                }

                card.End();
            }
        }
    }

    public void Dispose()
    {
    }
}
