using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed partial class SocialDms
{
    private const float SettingsRowHeight = 48f;
    private const float SettingsCheckSize = 20f;

    private static readonly TextStyle SettingsRowStyle = TextStyles.Body;
    private static readonly TextStyle SettingsSectionStyle = TextStyles.FootnoteEmphasized;

    private static readonly TextStyle SettingsHintStyle = TextStyles.Footnote;

    private readonly AccountClient account;
    private readonly CancellationTokenSource privacyCancellation = new();
    private volatile int messagePolicy;
    private volatile bool privacyLoaded;
    private volatile bool privacyLoading;
    private volatile bool privateAccount;

    public bool PrivateAccount
    {
        get => privateAccount;
        set => privateAccount = value;
    }

    public bool DrawMessagePrivacy()
    {
        EnsurePrivacyLoaded();
        SocialChrome.DrawSectionLabel(Loc.T(L.Social.AllowMessages), Ink, SettingsSectionStyle);
        if (!privacyLoaded)
        {
            SocialChrome.DrawHint(Loc.T(L.Common.Loading), Ink, SettingsHintStyle);
            return false;
        }

        for (var index = 0; index < SocialAudience.Options.Length; index++)
        {
            if (DrawChoiceRow(Loc.T(SocialAudience.Options[index]), messagePolicy == index))
            {
                SetMessagePolicy(index);
            }
        }

        SocialChrome.DrawHint(Loc.T(labels.PrivacyHint), Ink, SettingsHintStyle);
        return true;
    }

    private void DrawPrivacy(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(labels.PrivacyTitle));
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawMessagePrivacy();
        }
    }

    private bool DrawChoiceRow(string label, bool selected)
    {
        var ink = Ink;
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, SettingsRowHeight * scale, ink.HoverTint, !selected);
        var left = cell.Bounds.Min.X + CellPadX * scale;
        var checkCenter = new Vector2(cell.Bounds.Max.X - CellPadX * scale - SettingsCheckSize * 0.5f * scale,
            cell.Bounds.Center.Y);
        var labelRight = checkCenter.X - SettingsCheckSize * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, labelRight - left), SettingsRowStyle);
        var size = Typography.Measure(fitted, SettingsRowStyle);
        Typography.Draw(drawList, new Vector2(left, cell.Bounds.Center.Y - size.Y * 0.5f), fitted,
            selected ? ink.TitleInk : ink.BodyInk, SettingsRowStyle);
        if (selected)
        {
            PhoneIcon.Draw(drawList, checkCenter, PhoneIcons.Check, ink.AccentLink, SettingsCheckSize * scale);
        }

        FeedCell.End(drawList, cell, ink.Hairline);
        return cell.Tapped;
    }

    private void EnsurePrivacyLoaded()
    {
        if (privacyLoaded || privacyLoading || !store.IsSignedIn)
        {
            return;
        }

        privacyLoading = true;
        var token = privacyCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await account.MeAsync(token).ConfigureAwait(false);
                if (me is not null)
                {
                    messagePolicy = me.MessagePolicy;
                    privateAccount = me.IsPrivate;
                    privacyLoaded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"{labels.LogTag} message privacy load failed");
            }
            finally
            {
                privacyLoading = false;
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
        var token = privacyCancellation.Token;
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
                AepLog.Warning(exception, $"{labels.LogTag} message privacy update failed");
            }
        });
    }
}
