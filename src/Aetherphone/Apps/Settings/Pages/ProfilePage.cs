using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ProfilePage : ISettingsPage, IDisposable
{
    public string Title => Loc.T(L.Profile.Title);
    public string Summary => SocialRegion.EffectiveCode(configuration, gameData);
    public FontAwesomeIcon Icon => FontAwesomeIcon.IdCard;
    public Vector4 Tint => new(0.42f, 0.58f, 0.86f, 1f);
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly AccountClient client;
    private readonly GameData gameData;
    private readonly CancellationTokenSource cancellation = new();
    private bool initialSynced;

    public ProfilePage(Configuration configuration, AethernetSession session, AccountClient client, GameData gameData)
    {
        this.configuration = configuration;
        this.session = session;
        this.client = client;
        this.gameData = gameData;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            if (session.IsSignedIn && session.CurrentUser is not null && !initialSynced)
            {
                initialSynced = true;
                PushTimeZone(null);
            }

            DrawRegionSection(theme, scale);
            ImGui.Dummy(new Vector2(0f, 18f * scale));
            SettingsSection.Header(Loc.T(L.Profile.TimeZoneSection), theme);
            Hint(Loc.T(L.Profile.TimeZoneHelp), theme);
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            var share = session.CurrentUser?.ShareTimeZone ?? true;
            var shareCard = GroupCard.Begin(theme, 1);
            var nextShare = SettingsRow.Bool(shareCard.NextRow(), Loc.T(L.Profile.ShareTimeZoneLabel), share, theme);
            shareCard.End();
            if (nextShare != share && session.IsSignedIn)
            {
                PushTimeZone(nextShare);
            }

            if (!session.IsSignedIn)
            {
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                Hint(Loc.T(L.Profile.SignInToShare), theme);
            }

            if (nextShare)
            {
                ImGui.Dummy(new Vector2(0f, 12f * scale));
                var rowCount = configuration.TimeZoneManual ? 3 : 2;
                var card = GroupCard.Begin(theme, rowCount);
                var nextManual = SettingsRow.Bool(card.NextRow(), Loc.T(L.Profile.TimeZoneManualLabel),
                    configuration.TimeZoneManual, theme);
                if (configuration.TimeZoneManual)
                {
                    DrawOffsetStepper(card.NextRow(), theme);
                }

                SettingsRow.Info(card.NextRow(), Loc.T(L.Profile.YourTimeLabel),
                    SocialTimeZone.Describe(SocialTimeZone.EffectiveOffsetMinutes(configuration)), theme);
                card.End();
                if (nextManual != configuration.TimeZoneManual)
                {
                    configuration.TimeZoneManual = nextManual;
                    if (nextManual)
                    {
                        configuration.ManualUtcOffsetMinutes = SocialTimeZone.DeviceOffsetMinutes();
                    }

                    configuration.Save();
                    PushTimeZone(null);
                }
            }
        }
    }

    private void DrawRegionSection(PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Profile.RegionSection), theme);
        Hint(Loc.T(L.Profile.RegionHelp), theme);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var card = GroupCard.Begin(theme, SocialRegion.Codes.Length + 1);
        var autoLabel = $"{Loc.T(L.Profile.RegionAutomatic)}  ({SocialRegion.AutoCode(gameData)})";
        if (SettingsRow.Selectable(card.NextRow(), autoLabel, !configuration.RegionManual, theme) &&
            configuration.RegionManual)
        {
            configuration.RegionManual = false;
            configuration.Save();
        }

        for (var index = 0; index < SocialRegion.Codes.Length; index++)
        {
            var code = SocialRegion.Codes[index];
            var selected = configuration.RegionManual &&
                           string.Equals(configuration.ManualRegion, code, StringComparison.Ordinal);
            if (SettingsRow.Selectable(card.NextRow(), code, selected, theme) && !selected)
            {
                configuration.RegionManual = true;
                configuration.ManualRegion = code;
                configuration.Save();
            }
        }

        card.End();
    }

    private void DrawOffsetStepper(Rect row, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var buttonSize = 26f * scale;
        var plusMin = new Vector2(row.Max.X - buttonSize, row.Center.Y - buttonSize * 0.5f);
        var minusMin = new Vector2(plusMin.X - 96f * scale, row.Center.Y - buttonSize * 0.5f);
        var label = Typography.FitText(Loc.T(L.Profile.UtcOffsetLabel), minusMin.X - 12f * scale - row.Min.X, 1f,
            FontWeight.Regular);
        var labelSize = Typography.Measure(label);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, theme.TextStrong);
        if (StepperButton(drawList, minusMin, buttonSize, "-", theme))
        {
            AdjustOffset(-SocialTimeZone.StepMinutes);
        }

        if (StepperButton(drawList, plusMin, buttonSize, "+", theme))
        {
            AdjustOffset(SocialTimeZone.StepMinutes);
        }

        var value = SocialTimeZone.FormatOffset(SocialTimeZone.EffectiveOffsetMinutes(configuration));
        Typography.DrawCentered(new Vector2((minusMin.X + buttonSize + plusMin.X) * 0.5f, row.Center.Y), value,
            theme.TextStrong, 1f, FontWeight.SemiBold);
    }

    private static bool StepperButton(ImDrawListPtr drawList, Vector2 min, float size, string glyph, PhoneTheme theme)
    {
        var max = min + new Vector2(size, size);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var fill = hovered
            ? Palette.Mix(theme.GroupedCard, theme.Accent, 0.35f)
            : Palette.WithAlpha(theme.TextStrong, 0.10f);
        Squircle.Fill(drawList, min, max, size * 0.32f, ImGui.GetColorU32(fill));
        Typography.DrawCentered(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), glyph, theme.TextStrong,
            1.1f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void AdjustOffset(int deltaMinutes)
    {
        var next = Math.Clamp(configuration.ManualUtcOffsetMinutes + deltaMinutes, SocialTimeZone.MinOffsetMinutes,
            SocialTimeZone.MaxOffsetMinutes);
        if (next == configuration.ManualUtcOffsetMinutes)
        {
            return;
        }

        configuration.ManualUtcOffsetMinutes = next;
        configuration.Save();
        PushTimeZone(null);
    }

    private void PushTimeZone(bool? share)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var request = new UpdateTimeZoneRequest(share, SocialTimeZone.EffectiveOffsetMinutes(configuration));
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var updated = await client.UpdateTimeZoneAsync(request, token).ConfigureAwait(false);
                if (updated is not null)
                {
                    session.SetUser(updated);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Time zone update failed: {exception.Message}");
            }
        });
    }

    private static void Hint(string text, PhoneTheme theme) => SettingsSection.Hint(text, theme);

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
