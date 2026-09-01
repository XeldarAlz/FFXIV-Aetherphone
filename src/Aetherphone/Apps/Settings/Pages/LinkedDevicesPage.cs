using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Net.Codecrete.QrCodeGenerator;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class LinkedDevicesPage : ISettingsPage, IDisposable
{
    private enum LinkStage
    {
        Idle,
        Loading,
        Showing,
        Claimed,
        Approving,
        Linked,
        Failed,
    }

    private const float QrCardSize = 190f;
    private const float QrCardPadding = 14f;
    private const float QrCardRounding = 16f;
    private const float RowHeight = 54f;
    private static readonly TimeSpan StatusPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(5);
    private static readonly Vector4 QrCardFill = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 QrModuleInk = new(0.043f, 0.039f, 0.086f, 1f);

    private readonly AethernetSession session;
    private readonly AuthClient client;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object gate = new();

    private volatile LinkStage stage = LinkStage.Idle;
    private volatile string code = string.Empty;
    private volatile string claimedDeviceName = string.Empty;
    private volatile bool[][] qrModules = Array.Empty<bool[]>();
    private DateTime codeExpiresAt = DateTime.MinValue;
    private DateTime nextStatusPollAt = DateTime.MinValue;
    private volatile bool requestInFlight;
    private volatile bool statusInFlight;

    public LinkedDevicesPage(AethernetSession session, AuthClient client)
    {
        this.session = session;
        this.client = client;
    }

    public string Title => Loc.T(L.Settings.LinkedDevices);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Qrcode;
    public Vector4 Tint => new(0.545f, 0.486f, 0.973f, 1f);

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        if (!session.IsSignedIn)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Settings.LinkedDevicesSignIn), theme.TextMuted, 0.86f);
            return;
        }

        Advance();
        using (AppSurface.Begin(body))
        {
            switch (stage)
            {
                case LinkStage.Showing:
                    DrawShowing(theme);
                    break;
                case LinkStage.Claimed:
                    DrawClaimed(theme);
                    break;
                case LinkStage.Approving:
                    DrawSpinnerBlock(theme, Loc.T(L.Settings.LinkedDevicesApprove));
                    break;
                case LinkStage.Linked:
                    DrawLinked(theme);
                    break;
                case LinkStage.Failed:
                    DrawFailed(theme);
                    break;
                default:
                    DrawSpinnerBlock(theme, string.Empty);
                    break;
            }
        }
    }

    private void Advance()
    {
        var now = DateTime.UtcNow;
        if (stage == LinkStage.Idle)
        {
            BeginRequestCode();
            return;
        }

        if (stage == LinkStage.Showing && now >= codeExpiresAt)
        {
            BeginRequestCode();
            return;
        }

        if (stage is LinkStage.Showing or LinkStage.Claimed && now >= nextStatusPollAt && !statusInFlight)
        {
            statusInFlight = true;
            nextStatusPollAt = now + StatusPollInterval;
            _ = PollStatusAsync(code);
        }
    }

    private void DrawShowing(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Settings.LinkedDevices), theme, Loc.T(L.Settings.LinkedDevicesScan));
        DrawQrCard();
        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(1f, 10f * scale));
        var width = ImGui.GetContentRegionAvail().X;
        var codeSize = Typography.Measure(code, 1.05f, FontWeight.SemiBold);
        var codeLeft = ImGui.GetCursorScreenPos().X + (width - codeSize.X) * 0.5f;
        Typography.Draw(new Vector2(codeLeft, ImGui.GetCursorScreenPos().Y), code, theme.Accent, 1.05f,
            FontWeight.SemiBold);
        ImGui.Dummy(new Vector2(1f, codeSize.Y + 8f * scale));
        SettingsSection.Hint(Loc.T(L.Settings.LinkedDevicesRefresh), theme);
    }

    private void DrawQrCard()
    {
        var modules = qrModules;
        if (modules.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cardSize = QrCardSize * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var cardMin = new Vector2(origin.X + (width - cardSize) * 0.5f, origin.Y);
        var cardMax = cardMin + new Vector2(cardSize, cardSize);
        drawList.AddRectFilled(cardMin, cardMax, ImGui.GetColorU32(QrCardFill), QrCardRounding * scale);
        var inner = cardSize - QrCardPadding * 2f * scale;
        var moduleSize = inner / modules.Length;
        var innerMin = cardMin + new Vector2(QrCardPadding * scale, QrCardPadding * scale);
        var moduleInk = ImGui.GetColorU32(QrModuleInk);
        for (var rowIndex = 0; rowIndex < modules.Length; rowIndex++)
        {
            var row = modules[rowIndex];
            for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
            {
                if (!row[columnIndex])
                {
                    continue;
                }

                var moduleMin = innerMin + new Vector2(columnIndex * moduleSize, rowIndex * moduleSize);
                drawList.AddRectFilled(moduleMin, moduleMin + new Vector2(moduleSize + 0.5f, moduleSize + 0.5f),
                    moduleInk);
            }
        }

        ImGui.Dummy(new Vector2(width, cardSize));
    }

    private void DrawClaimed(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Settings.LinkedDevicesClaimTitle), theme,
            Loc.T(L.Settings.LinkedDevicesClaimBody, claimedDeviceName));
        var card = GroupCard.Begin(theme, 2, RowHeight);
        var approve = SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.LinkedDevicesApprove), string.Empty,
            theme);
        var deny = SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.LinkedDevicesDeny), string.Empty, theme);
        card.End();
        if (approve)
        {
            stage = LinkStage.Approving;
            _ = ApproveAsync(code);
        }
        else if (deny)
        {
            BeginRequestCode();
        }
    }

    private void DrawLinked(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Settings.LinkedDevicesLinkedTitle), theme,
            Loc.T(L.Settings.LinkedDevicesLinkedBody, claimedDeviceName));
        var card = GroupCard.Begin(theme, 1, RowHeight);
        var again = SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.LinkedDevicesAnother), string.Empty,
            theme);
        card.End();
        if (again)
        {
            BeginRequestCode();
        }
    }

    private void DrawFailed(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Settings.LinkedDevices), theme, Loc.T(L.Settings.LinkedDevicesFailed));
        var card = GroupCard.Begin(theme, 1, RowHeight);
        var retry = SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.LinkedDevicesRetry), string.Empty, theme);
        card.End();
        if (retry)
        {
            BeginRequestCode();
        }
    }

    private static void DrawSpinnerBlock(PhoneTheme theme, string label)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var center = ImGui.GetCursorScreenPos() + new Vector2(width * 0.5f, 70f * scale);
        LoadingPulse.Spinner(center, 10f * scale, theme.Accent);
        if (label.Length > 0)
        {
            Typography.DrawCentered(center + new Vector2(0f, 26f * scale), label, theme.TextMuted, 0.82f);
        }

        ImGui.Dummy(new Vector2(width, 120f * scale));
    }

    private void BeginRequestCode()
    {
        lock (gate)
        {
            if (requestInFlight)
            {
                return;
            }

            requestInFlight = true;
        }

        stage = LinkStage.Loading;
        claimedDeviceName = string.Empty;
        _ = RequestCodeAsync();
    }

    private async Task RequestCodeAsync()
    {
        try
        {
            var response = await client.NewDeviceLinkAsync(cancellation.Token).ConfigureAwait(false);
            if (response is null || response.Code.Length == 0)
            {
                stage = LinkStage.Failed;
                return;
            }

            qrModules = BuildModules(response.Code);
            code = response.Code;
            codeExpiresAt = DateTime.UtcNow.AddSeconds(response.ExpiresInSeconds) - ExpiryMargin;
            nextStatusPollAt = DateTime.UtcNow + StatusPollInterval;
            stage = LinkStage.Showing;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            stage = LinkStage.Failed;
        }
        finally
        {
            requestInFlight = false;
        }
    }

    private async Task PollStatusAsync(string polledCode)
    {
        try
        {
            var response = await client.DeviceLinkStatusAsync(polledCode, cancellation.Token).ConfigureAwait(false);
            if (response is null || !string.Equals(polledCode, code, StringComparison.Ordinal))
            {
                return;
            }

            if (!response.Ok)
            {
                if (stage == LinkStage.Claimed)
                {
                    BeginRequestCode();
                }

                return;
            }

            if (response.DeviceName is { Length: > 0 } deviceName && stage == LinkStage.Showing)
            {
                claimedDeviceName = deviceName;
                stage = LinkStage.Claimed;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            statusInFlight = false;
        }
    }

    private async Task ApproveAsync(string approvedCode)
    {
        try
        {
            var response = await client.ApproveDeviceLinkAsync(approvedCode, cancellation.Token).ConfigureAwait(false);
            stage = response is { Ok: true } ? LinkStage.Linked : LinkStage.Failed;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            stage = LinkStage.Failed;
        }
    }

    private static bool[][] BuildModules(string content)
    {
        var qr = QrCode.EncodeText(content, QrCode.Ecc.Medium);
        var modules = new bool[qr.Size][];
        for (var rowIndex = 0; rowIndex < qr.Size; rowIndex++)
        {
            var row = new bool[qr.Size];
            for (var columnIndex = 0; columnIndex < qr.Size; columnIndex++)
            {
                row[columnIndex] = qr.GetModule(columnIndex, rowIndex);
            }

            modules[rowIndex] = row;
        }

        return modules;
    }
}
