using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp
{
    private static readonly string[] SalesTransactionTypes = { "SALE", "TIP", "COVER_CHARGE", "OTHER" };

    private string? salesServicesLoadedForVenueId;
    private List<VenueSyncService> salesServices = new();
    private string? salesSelectedServiceId;
    private bool salesServicePickerExpanded;
    private int salesSelectedTypeIndex;
    private bool salesTypePickerExpanded;
    private string salesCustomerName = string.Empty;
    private string salesAmountText = string.Empty;
    private string? salesError;
    private volatile bool salesSubmitBusy;

    private void DrawSales(Rect area)
    {
        var scale = UiScale.Current;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), Loc.T(L.VenueSync.LogASale), back);

        if (salesServicesLoadedForVenueId != configuration.VenueSyncSelectedVenueId)
        {
            salesServicesLoadedForVenueId = configuration.VenueSyncSelectedVenueId;
            salesSelectedServiceId = null;
            salesServicePickerExpanded = false;
            if (string.IsNullOrEmpty(configuration.VenueSyncSelectedVenueId))
            {
                salesServices = new();
            }
            else
            {
                _ = LoadSalesServicesAsync();
            }
        }

        var services = salesServices;

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var serviceExpanded = salesServicePickerExpanded;
            var typeExpanded = salesTypePickerExpanded;
            var rowCount = (salesError is not null ? 6 : 5)
                + (serviceExpanded ? services.Count : 0)
                + (typeExpanded ? SalesTransactionTypes.Length : 0);
            var card = GroupCard.Begin(theme, rowCount, Metrics.Size.Row, fillOverride: ui.Palette.CardFill);

            var serviceLabel = Loc.T(L.VenueSync.SelectAService);
            for (var serviceIndex = 0; serviceIndex < services.Count; serviceIndex++)
            {
                if (string.Equals(services[serviceIndex].Id, salesSelectedServiceId, StringComparison.Ordinal))
                {
                    serviceLabel = services[serviceIndex].Name;
                    break;
                }
            }

            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.VenueSync.Service), serviceLabel, theme))
            {
                if (services.Count == 0)
                {
                    _ = LoadSalesServicesAsync();
                }

                salesServicePickerExpanded = !serviceExpanded;
            }

            if (serviceExpanded)
            {
                for (var serviceIndex = 0; serviceIndex < services.Count; serviceIndex++)
                {
                    var service = services[serviceIndex];
                    var selected = string.Equals(service.Id, salesSelectedServiceId, StringComparison.Ordinal);
                    if (SettingsRow.Selectable(card.NextRow(), service.Name, selected, theme,
                            $"venuesync.sales.service-{service.Id}"))
                    {
                        salesSelectedServiceId = service.Id;
                        salesServicePickerExpanded = false;
                    }
                }
            }

            var typeLabel = TransactionTypeLabel(SalesTransactionTypes[salesSelectedTypeIndex]);
            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.VenueSync.Type), typeLabel, theme))
            {
                salesTypePickerExpanded = !typeExpanded;
            }

            if (typeExpanded)
            {
                for (var typeIndex = 0; typeIndex < SalesTransactionTypes.Length; typeIndex++)
                {
                    var typeValue = SalesTransactionTypes[typeIndex];
                    var selected = typeIndex == salesSelectedTypeIndex;
                    if (SettingsRow.Selectable(card.NextRow(), TransactionTypeLabel(typeValue), selected, theme,
                            $"venuesync.sales.type-{typeValue}"))
                    {
                        salesSelectedTypeIndex = typeIndex;
                        salesTypePickerExpanded = false;
                    }
                }
            }

            var customerRow = card.NextRow();
            var targetButtonWidth = 36f * scale;
            var customerFieldWidth = MathF.Max(1f,
                customerRow.Width - targetButtonWidth - Metrics.Space.Sm * scale);
            var customerFieldMin = new Vector2(customerRow.Min.X,
                customerRow.Center.Y - Metrics.Size.FieldHeight * scale * 0.5f);
            var customerFieldMax = new Vector2(customerRow.Min.X + customerFieldWidth,
                customerRow.Center.Y + Metrics.Size.FieldHeight * scale * 0.5f);
            Squircle.Fill(ImGui.GetWindowDrawList(), customerFieldMin, customerFieldMax, Metrics.Radius.Field * scale,
                ImGui.GetColorU32(ui.Palette.FieldSurface));
            ImGui.SetCursorScreenPos(customerFieldMin);
            ImGui.SetNextItemWidth(customerFieldWidth);
            using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
            {
                ImGui.InputTextWithHint("##sales-customer", Loc.T(L.VenueSync.CustomerOptional),
                    ref salesCustomerName, 64);
            }

            var targetButtonCenter = new Vector2(customerRow.Max.X - targetButtonWidth * 0.5f, customerRow.Center.Y);
            if (AppSkin.IconButton(targetButtonCenter, targetButtonWidth * 0.5f,
                    FontAwesomeIcon.Crosshairs.ToIconString(), ui.MutedInk, ui.Palette.FieldSurface, 0.6f, theme,
                    Loc.T(L.VenueSync.UseCurrentTarget)))
            {
                var name = gameData.Target?.Name.TextValue;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    salesCustomerName = name;
                }
            }

            var amountRow = card.NextRow();
            var amountFieldMin = new Vector2(amountRow.Min.X,
                amountRow.Center.Y - Metrics.Size.FieldHeight * scale * 0.5f);
            var amountFieldMax = new Vector2(amountRow.Max.X,
                amountRow.Center.Y + Metrics.Size.FieldHeight * scale * 0.5f);
            Squircle.Fill(ImGui.GetWindowDrawList(), amountFieldMin, amountFieldMax, Metrics.Radius.Field * scale,
                ImGui.GetColorU32(ui.Palette.FieldSurface));
            ImGui.SetCursorScreenPos(amountFieldMin);
            ImGui.SetNextItemWidth(amountRow.Width);
            using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
            {
                ImGui.InputTextWithHint("##sales-amount", Loc.T(L.VenueSync.AmountGil), ref salesAmountText, 12,
                    ImGuiInputTextFlags.CharsDecimal);
            }

            if (salesError is not null)
            {
                var errorRow = card.NextRow();
                Marquee.DrawLeftAuto("venuesync.sales.error", Loc.T(L.VenueSync.ErrorTapLogSaleToRetry, salesError),
                    errorRow.Min.X, errorRow.Center.Y - 10f * scale, errorRow.Width, TextStyles.Caption1,
                    theme.Danger);
            }

            var submitRow = card.NextRow();
            var submitButtonHeight = Metrics.Size.FieldHeight * scale;
            var submitButtonRect = new Rect(new Vector2(submitRow.Min.X, submitRow.Center.Y - submitButtonHeight * 0.5f),
                new Vector2(submitRow.Max.X, submitRow.Center.Y + submitButtonHeight * 0.5f));
            var submitEnabled = !salesSubmitBusy;
            if (AppSkin.PillButton(submitButtonRect, Loc.T(L.VenueSync.LogSale), true, submitEnabled, theme) &&
                submitEnabled)
            {
                salesSubmitBusy = true;
                _ = SubmitSaleAsync();
            }

            card.End();

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

            var summaryOrigin = ImGui.GetCursorScreenPos();
            var summaryWidth = ImGui.GetContentRegionAvail().X;
            var summaryRect = new Rect(summaryOrigin,
                new Vector2(summaryOrigin.X + summaryWidth, summaryOrigin.Y + SaleSummaryRow.Height * scale));
            SaleSummaryRow.Draw(summaryRect, state.SessionSalesCount, state.SessionSalesTotal, theme, ui.Accent);
        }
    }

    private static string TransactionTypeLabel(string type) => type switch
    {
        "TIP" => Loc.T(L.VenueSync.TransactionTypeTip),
        "COVER_CHARGE" => Loc.T(L.VenueSync.TransactionTypeCoverCharge),
        "OTHER" => Loc.T(L.VenueSync.TransactionTypeOther),
        _ => Loc.T(L.VenueSync.TransactionTypeSale),
    };

    private async Task LoadSalesServicesAsync()
    {
        var response = await client.GetServicesAsync(configuration.VenueSyncSelectedVenueId, CancellationToken.None)
            .ConfigureAwait(false);
        if (response is not null)
        {
            salesServices = response.Services;
        }
    }

    private async Task SubmitSaleAsync()
    {
        try
        {
            if (!decimal.TryParse(salesAmountText, NumberStyles.Number, CultureInfo.InvariantCulture,
                    out var amount) || amount <= 0)
            {
                salesError = Loc.T(L.VenueSync.EnterAValidAmount);
                return;
            }

            var request = new VenueSyncTransactionRequest
            {
                VenueId = configuration.VenueSyncSelectedVenueId,
                ServiceId = salesSelectedServiceId,
                Amount = amount,
                Type = SalesTransactionTypes[salesSelectedTypeIndex],
                CustomerName = string.IsNullOrWhiteSpace(salesCustomerName) ? null : salesCustomerName,
            };

            var result = await client.LogTransactionAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (result is { Success: true })
            {
                salesError = null;
                state.RecordSale(amount);
                salesAmountText = string.Empty;
            }
            else
            {
                salesError = result?.Error ?? Loc.T(L.VenueSync.FailedToLogSale);
            }
        }
        catch (Exception exception)
        {
            salesError = exception.Message;
        }
        finally
        {
            salesSubmitBusy = false;
        }
    }
}
