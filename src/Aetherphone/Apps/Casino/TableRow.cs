using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal readonly record struct TableRowView(
    string Name,
    string Stakes,
    string Seats,
    string Spectators,
    bool Full,
    bool InviteOnly,
    bool Mine,
    bool Draining,
    int Currency,
    string CurrencyLabel,
    string Reputation,
    bool ReputationWarns,
    bool Paused);

internal static class TableRow
{
    public const float Height = 74f;

    public const float ReputationHeight = 18f;

    private static readonly Vector4 GilTint = new(1f, 0.788f, 0.290f, 1f);

    public static float HeightOf(in TableRowView view) =>
        view.Reputation.Length > 0 ? Height + ReputationHeight : Height;

    public static Vector4 CurrencyTint(int currency, Vector4 accent) => currency switch
    {
        CasinoCurrencies.Practice => CasinoColors.Practice,
        CasinoCurrencies.Gil => GilTint,
        _ => accent,
    };

    public static bool Draw(ImDrawListPtr drawList, in Rect row, AppSkin ui, in TableRowView view, float scale)
    {
        var rounding = Metrics.Radius.Grouped * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        ui.Card(drawList, row.Min, row.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (view.Mine)
        {
            Squircle.Stroke(drawList, row.Min, row.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.40f)), 1f * scale);
        }

        var pad = 14f * scale;
        var badgeWidth = DrawBadge(drawList, row, ui, view, scale);
        var currencyWidth = DrawCurrency(drawList, row, ui, view, badgeWidth, scale);
        var textLeft = row.Min.X + pad;
        var textWidth = row.Width - pad * 2f - badgeWidth - currencyWidth;
        var name = Typography.FitText(view.Name, MathF.Max(1f, textWidth), TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 12f * scale), name,
            view.Draining ? ui.MutedInk : ui.TitleInk, TextStyles.SubheadlineEmphasized);

        var fullWidth = row.Width - pad * 2f;
        var stakes = Typography.FitText(view.Stakes, fullWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 32f * scale), stakes, ui.MutedInk,
            TextStyles.Footnote);

        var seatsInk = view.Full ? ui.MutedInk : ui.Accent;
        var seats = Typography.FitText(view.Seats, fullWidth * 0.5f, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 50f * scale), seats, seatsInk,
            TextStyles.Footnote);
        if (view.Spectators.Length > 0)
        {
            var seatsWidth = Typography.Measure(seats, TextStyles.Footnote).X;
            Typography.Draw(drawList, new Vector2(textLeft + seatsWidth + 10f * scale, row.Min.Y + 50f * scale),
                Typography.FitText(view.Spectators, fullWidth - seatsWidth - 10f * scale, TextStyles.Footnote),
                ui.MutedInk, TextStyles.Footnote);
        }

        if (view.Reputation.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + (Height - 4f) * scale),
                Typography.FitText(view.Reputation, fullWidth, TextStyles.Footnote),
                view.ReputationWarns ? ui.Accent : GilTint, TextStyles.Footnote);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static float DrawCurrency(ImDrawListPtr drawList, in Rect row, AppSkin ui, in TableRowView view,
        float badgeWidth, float scale)
    {
        if (view.CurrencyLabel.Length == 0)
        {
            return 0f;
        }

        var tint = CurrencyTint(view.Currency, ui.Accent);
        var labelSize = Typography.Measure(view.CurrencyLabel, TextStyles.Footnote);
        var chipHeight = labelSize.Y + 6f * scale;
        var right = row.Max.X - 14f * scale - badgeWidth;
        var chipMin = new Vector2(right - labelSize.X - 16f * scale, row.Min.Y + 12f * scale);
        var chipMax = new Vector2(right, chipMin.Y + chipHeight);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.16f)));
        if (view.Currency == CasinoCurrencies.Practice)
        {
            Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(tint, 0.55f)), 1f * scale);
        }

        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, view.CurrencyLabel, tint, TextStyles.Footnote);
        return chipMax.X - chipMin.X + 8f * scale;
    }

    private static float DrawBadge(ImDrawListPtr drawList, in Rect row, AppSkin ui, in TableRowView view, float scale)
    {
        var label = BadgeLabel(view);
        if (label.Length == 0)
        {
            return 0f;
        }

        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        var chipHeight = labelSize.Y + 6f * scale;
        var chipMax = new Vector2(row.Max.X - 14f * scale, row.Min.Y + 12f * scale + chipHeight);
        var chipMin = new Vector2(chipMax.X - labelSize.X - 16f * scale, row.Min.Y + 12f * scale);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f * scale);
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label,
            view.Full || view.Draining || view.Paused ? ui.MutedInk : ui.Accent, TextStyles.Footnote);
        return chipMax.X - chipMin.X + 8f * scale;
    }

    private static string BadgeLabel(in TableRowView view)
    {
        if (view.Draining)
        {
            return Loc.T(L.Casino.TableClosingBadge);
        }

        if (view.Paused)
        {
            return Loc.T(L.Tables.PausedBadge);
        }

        if (view.Mine)
        {
            return Loc.T(L.Casino.TableYoursBadge);
        }

        if (view.InviteOnly)
        {
            return Loc.T(L.Casino.TablePrivateBadge);
        }

        return view.Full ? Loc.T(L.Casino.TableFullBadge) : string.Empty;
    }
}
