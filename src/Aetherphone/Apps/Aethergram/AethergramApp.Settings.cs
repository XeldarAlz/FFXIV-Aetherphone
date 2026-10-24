using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float SettingsRowHeight = 48f;
    private const float SettingsToggleWidth = 48f;
    private const float SettingsToggleHeight = 28f;
    private const float SettingsCheckSize = 20f;

    private static readonly TextStyle SettingsRowStyle = TextStyles.Body;
    private static readonly TextStyle SettingsHintStyle = TextStyles.Footnote;

    private void DrawSettings(Rect area)
    {
        DrawScreenHeader(area, Loc.T(L.Aethergram.Settings));
        var scale = UiScale.Current;
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            if (!dms.DrawMessagePrivacy())
            {
                return;
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var toggled = DrawSettingsToggleRow(Loc.T(L.Aethergram.PrivateAccount), dms.PrivateAccount);
            if (toggled != dms.PrivateAccount)
            {
                SetAccountPrivacy(toggled);
            }

            SocialChrome.DrawHint(Loc.T(L.Aethergram.PrivateAccountHint), Ink, SettingsHintStyle);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            if (DrawSettingsLinkRow(Loc.T(L.Social.FeedHowItWorks)))
            {
                feedExplainer.Open();
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private bool DrawSettingsLinkRow(string label)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, SettingsRowHeight * scale, Ink.HoverTint);
        var left = cell.Bounds.Min.X + CellPadX * scale;
        var chevronCenter = new Vector2(cell.Bounds.Max.X - CellPadX * scale - SettingsCheckSize * 0.5f * scale,
            cell.Bounds.Center.Y);
        var fitted = Typography.FitText(label, MathF.Max(1f, chevronCenter.X - SettingsCheckSize * scale - left), SettingsRowStyle);
        var size = Typography.Measure(fitted, SettingsRowStyle);
        Typography.Draw(drawList, new Vector2(left, cell.Bounds.Center.Y - size.Y * 0.5f), fitted, Ink.TitleInk,
            SettingsRowStyle);
        PhoneIcon.Draw(drawList, chevronCenter, PhoneIcons.ChevronRight, Ink.MutedInk, SettingsCheckSize * scale);
        FeedCell.End(drawList, cell, Ink.Hairline);
        return cell.Tapped;
    }

    private bool DrawSettingsToggleRow(string label, bool value)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, SettingsRowHeight * scale, Ink.HoverTint, false);
        var left = cell.Bounds.Min.X + CellPadX * scale;
        var toggleMax = new Vector2(cell.Bounds.Max.X - CellPadX * scale,
            cell.Bounds.Center.Y + SettingsToggleHeight * 0.5f * scale);
        var toggleMin = new Vector2(toggleMax.X - SettingsToggleWidth * scale,
            cell.Bounds.Center.Y - SettingsToggleHeight * 0.5f * scale);
        var fitted = Typography.FitText(label, MathF.Max(1f, toggleMin.X - 12f * scale - left), SettingsRowStyle);
        var size = Typography.Measure(fitted, SettingsRowStyle);
        Typography.Draw(drawList, new Vector2(left, cell.Bounds.Center.Y - size.Y * 0.5f), fitted, Ink.TitleInk,
            SettingsRowStyle);
        var result = Toggle.Draw("aethergram.settings.private", new Rect(toggleMin, toggleMax), value, theme);
        FeedCell.End(drawList, cell, Ink.Hairline);
        return result;
    }

    private void SetAccountPrivacy(bool isPrivate)
    {
        if (dms.PrivateAccount == isPrivate)
        {
            return;
        }

        var previous = dms.PrivateAccount;
        dms.PrivateAccount = isPrivate;
        store.UpdateAccountPrivacy(isPrivate, succeeded =>
        {
            if (!succeeded)
            {
                dms.PrivateAccount = previous;
            }
        });
    }
}
