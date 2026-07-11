using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class PrivacyPage : ISettingsPage, IDisposable
{
    public string Title => Loc.T(L.Settings.Privacy);
    public string Summary => configuration.AnalyticsEnabled ? Loc.T(L.Settings.PrivacyOn) : Loc.T(L.Settings.PrivacyOff);
    public FontAwesomeIcon Icon => FontAwesomeIcon.UserShield;
    public Vector4 Tint => new(0.42f, 0.56f, 0.86f, 1f);
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool chatPrivacyLoaded;
    private volatile bool chatPrivacyLoading;
    private volatile bool shareReadReceipts = true;
    private volatile bool sharePresence = true;

    public PrivacyPage(Configuration configuration, AethernetSession session, AethernetClient client)
    {
        this.configuration = configuration;
        this.session = session;
        this.client = client;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Privacy), theme);
            var card = GroupCard.Begin(theme, 1);
            var share = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.PrivacyAnalytics),
                configuration.AnalyticsEnabled, theme);
            card.End();
            if (share != configuration.AnalyticsEnabled)
            {
                configuration.AnalyticsEnabled = share;
                configuration.AnalyticsConsentPrompted = true;
                configuration.Save();
                if (share)
                {
                    Plugin.Analytics.Track(AnalyticsEvents.SettingChanged("analytics_enabled", "1"));
                }
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.PrivacyHint), theme);
            DrawChatPrivacy(theme, scale);
        }
    }

    private void DrawChatPrivacy(PhoneTheme theme, float scale)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        EnsureLoaded();
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        SettingsSection.Header(Loc.T(L.Apps.Message), theme);
        if (!chatPrivacyLoaded)
        {
            SettingsSection.Hint(Loc.T(L.Common.Loading), theme);
            return;
        }

        var card = GroupCard.Begin(theme, 2);
        var readReceipts = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ReadReceipts), shareReadReceipts, theme);
        var lastSeen = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.LastSeenOnline), sharePresence, theme);
        card.End();
        if (readReceipts != shareReadReceipts || lastSeen != sharePresence)
        {
            shareReadReceipts = readReceipts;
            sharePresence = lastSeen;
            Push(readReceipts, lastSeen);
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        SettingsSection.Hint(Loc.T(L.Settings.ChatPrivacyHint), theme);
    }

    private void EnsureLoaded()
    {
        if (chatPrivacyLoaded || chatPrivacyLoading)
        {
            return;
        }

        chatPrivacyLoading = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await client.MeAsync(token).ConfigureAwait(false);
                if (me is not null)
                {
                    shareReadReceipts = me.ShareReadReceipts;
                    sharePresence = me.SharePresence;
                    chatPrivacyLoaded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Chat privacy load failed: {exception.Message}");
            }
            finally
            {
                chatPrivacyLoading = false;
            }
        });
    }

    private void Push(bool readReceipts, bool lastSeen)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await client.UpdateChatPrivacyAsync(new UpdateChatPrivacyRequest(readReceipts, lastSeen),
                    token).ConfigureAwait(false);
                if (me is not null)
                {
                    shareReadReceipts = me.ShareReadReceipts;
                    sharePresence = me.SharePresence;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Chat privacy update failed: {exception.Message}");
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
