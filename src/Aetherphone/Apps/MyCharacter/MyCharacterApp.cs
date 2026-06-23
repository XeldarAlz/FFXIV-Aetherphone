using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Character;
using Aetherphone.Core.Game;
using Aetherphone.Core.Lodestone;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.MyCharacter;

internal sealed class MyCharacterApp : IPhoneApp
{
    private const float RefreshIntervalSeconds = 3f;

    public string Id => "character";

    public string DisplayName => "Character";

    public string Glyph => "Me";

    public Vector4 Accent => new(0.36f, 0.72f, 0.62f, 1f);

    public int BadgeCount => 0;

    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly LodestoneService lodestone;

    private LocalCharacter? character;
    private float sinceRefresh;

    public MyCharacterApp(GameData gameData, ITextureProvider textures, LodestoneService lodestone)
    {
        this.gameData = gameData;
        this.textures = textures;
        this.lodestone = lodestone;
    }

    public void OnOpened() => Refresh();

    public void OnClosed()
    {
    }

    private void Refresh()
    {
        character = LocalCharacterReader.Read(gameData);
        sinceRefresh = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh >= RefreshIntervalSeconds)
        {
            Refresh();
        }

        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);

        if (character is not { } snapshot)
        {
            Typography.DrawCentered(body.Center, "Log in to view your character", theme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            CharacterHeader.Draw(snapshot, theme, lodestone);

            SettingsSection.Header("Profile", theme);
            var card = GroupCard.Begin(theme, 7, ProfileRow.RowHeight);
            ProfileRow.Stacked(card.NextRow(), "Race", snapshot.Race, theme);
            ProfileRow.Stacked(card.NextRow(), "Clan", snapshot.Clan, theme);
            ProfileRow.Stacked(card.NextRow(), "Gender", snapshot.Gender, theme);
            ProfileRow.Stacked(card.NextRow(), "Nameday", snapshot.Nameday, theme);
            ProfileRow.Stacked(card.NextRow(), "Guardian", snapshot.Guardian, theme);
            ProfileRow.Stacked(card.NextRow(), "City-state", snapshot.CityState, theme);
            ProfileRow.Stacked(card.NextRow(), "Grand Company", snapshot.GrandCompany, theme);
            card.End();

            if (snapshot.Gear.Count > 0)
            {
                SettingsSection.Header("Equipment", theme);
                GearGrid.Draw(snapshot.Gear, textures, theme);
            }
        }
    }

    public void Dispose()
    {
    }
}
