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
    private static readonly TextStyle SettingsSectionStyle = TextStyles.FootnoteEmphasized;

    private volatile int messagePolicy;
    private volatile bool messagePolicyLoaded;
    private volatile bool messagePolicyLoading;
    private volatile bool privateAccount;

    private void DrawSettings(Rect area)
    {
        DrawScreenHeader(area, Loc.T(L.Aethergram.Settings));
        var scale = UiScale.Current;
        EnsureMessagePolicyLoaded();
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            SocialChrome.DrawSectionLabel(Loc.T(L.Social.AllowMessages), Ink, SettingsSectionStyle);
            if (!messagePolicyLoaded)
            {
                DrawSettingsHint(Loc.T(L.Common.Loading));
                return;
            }

            for (var index = 0; index < SocialAudience.Options.Length; index++)
            {
                if (DrawSettingsChoiceRow(Loc.T(SocialAudience.Options[index]), messagePolicy == index))
                {
                    SetMessagePolicy(index);
                }
            }

            DrawSettingsHint(Loc.T(L.Social.MessagesAudienceHint));
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var toggled = DrawSettingsToggleRow(Loc.T(L.Aethergram.PrivateAccount), privateAccount);
            if (toggled != privateAccount)
            {
                SetAccountPrivacy(toggled);
            }

            DrawSettingsHint(Loc.T(L.Aethergram.PrivateAccountHint));
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private bool DrawSettingsChoiceRow(string label, bool selected)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, SettingsRowHeight * scale, Ink.HoverTint, !selected);
        var left = cell.Bounds.Min.X + CellPadX * scale;
        var checkCenter = new Vector2(cell.Bounds.Max.X - CellPadX * scale - SettingsCheckSize * 0.5f * scale,
            cell.Bounds.Center.Y);
        var labelRight = checkCenter.X - SettingsCheckSize * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, labelRight - left), SettingsRowStyle);
        var size = Typography.Measure(fitted, SettingsRowStyle);
        Typography.Draw(drawList, new Vector2(left, cell.Bounds.Center.Y - size.Y * 0.5f), fitted,
            selected ? Ink.TitleInk : Ink.BodyInk, SettingsRowStyle);
        if (selected)
        {
            PhoneIcon.Draw(drawList, checkCenter, PhoneIcons.Check, Ink.AccentLink, SettingsCheckSize * scale);
        }

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

    private static void DrawSettingsHint(string text)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var padX = CellPadX * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + padX, origin.Y + 10f * scale), text,
            Ink.MutedInk, SettingsHintStyle, width - padX * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 16f * scale));
    }

    private void EnsureMessagePolicyLoaded()
    {
        if (messagePolicyLoaded || messagePolicyLoading || !store.IsSignedIn)
        {
            return;
        }

        messagePolicyLoading = true;
        var token = settingsCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await account.MeAsync(token).ConfigureAwait(false);
                if (me is not null)
                {
                    messagePolicy = me.MessagePolicy;
                    privateAccount = me.IsPrivate;
                    messagePolicyLoaded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Aethergram message privacy load failed");
            }
            finally
            {
                messagePolicyLoading = false;
            }
        });
    }

    private void SetMessagePolicy(int policy)
    {
        if (!SocialAudience.IsDefined(policy) || messagePolicy == policy)
        {
            return;
        }

        messagePolicy = policy;
        var token = settingsCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await account.UpdateMessagePrivacyAsync(policy, token).ConfigureAwait(false);
                if (me is not null)
                {
                    messagePolicy = me.MessagePolicy;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Aethergram message privacy update failed");
            }
        });
    }

    private void SetAccountPrivacy(bool isPrivate)
    {
        if (privateAccount == isPrivate)
        {
            return;
        }

        var previous = privateAccount;
        privateAccount = isPrivate;
        store.UpdateAccountPrivacy(isPrivate, succeeded =>
        {
            if (!succeeded)
            {
                privateAccount = previous;
            }
        });
    }
}
