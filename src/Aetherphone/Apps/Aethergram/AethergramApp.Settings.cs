using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private readonly CancellationTokenSource settingsCancellation = new();
    private volatile int messagePolicy;
    private volatile bool messagePolicyLoaded;
    private volatile bool messagePolicyLoading;

    private void DrawSettings(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.Settings), back);
        var scale = ImGuiHelpers.GlobalScale;
        EnsureMessagePolicyLoaded();
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(listRect))
        {
            var drawList = ImGui.GetWindowDrawList();
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            var width = ScrollLayout.StableContentWidth();
            var origin = ImGui.GetCursorScreenPos();
            Typography.Draw(drawList, new Vector2(origin.X + 4f * scale, origin.Y),
                Loc.T(L.Social.AllowMessages), AppPalettes.Aethergram.TitleInk, TextStyles.SubheadlineEmphasized);
            ImGui.Dummy(new Vector2(width, 28f * scale));
            if (!messagePolicyLoaded)
            {
                var loadingTop = ImGui.GetCursorScreenPos();
                Typography.Draw(drawList, new Vector2(loadingTop.X + 4f * scale, loadingTop.Y),
                    Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk, TextStyles.Subheadline);
                return;
            }

            DrawAudienceCard(drawList, width, scale);
            ImGui.Dummy(new Vector2(width, 10f * scale));
            var hintTop = ImGui.GetCursorScreenPos();
            var hintHeight = Typography.DrawWrappedLeft(new Vector2(hintTop.X + 4f * scale, hintTop.Y),
                Loc.T(L.Social.MessagesAudienceHint), AppPalettes.Aethergram.MutedInk, TextStyles.Footnote,
                width - 8f * scale);
            ImGui.Dummy(new Vector2(width, hintHeight + 24f * scale));
        }
    }

    private void DrawAudienceCard(ImDrawListPtr drawList, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = 46f * scale;
        var cardMax = new Vector2(origin.X + width, origin.Y + rowHeight * SocialAudience.Options.Length);
        ui.Card(drawList, origin, cardMax, 16f * scale);
        var dividerColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f));
        for (var index = 0; index < SocialAudience.Options.Length; index++)
        {
            var rowMin = new Vector2(origin.X, origin.Y + rowHeight * index);
            var rowMax = new Vector2(origin.X + width, rowMin.Y + rowHeight);
            var selected = messagePolicy == index;
            Typography.Draw(drawList, new Vector2(rowMin.X + 16f * scale, rowMin.Y + rowHeight * 0.5f - 9f * scale),
                Loc.T(SocialAudience.Options[index]),
                selected ? theme.TextStrong : AppPalettes.Aethergram.BodyInk, 1f,
                selected ? FontWeight.SemiBold : FontWeight.Regular);
            var radioCenter = new Vector2(rowMax.X - 22f * scale, rowMin.Y + rowHeight * 0.5f);
            if (selected)
            {
                drawList.AddCircleFilled(radioCenter, 8f * scale, ImGui.GetColorU32(Accent), 32);
                drawList.AddCircleFilled(radioCenter, 3.2f * scale,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)), 24);
            }
            else
            {
                drawList.AddCircle(radioCenter, 8f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.Aethergram.MutedInk, 0.55f)), 32, 1.6f * scale);
            }

            if (index > 0)
            {
                drawList.AddLine(new Vector2(rowMin.X + 16f * scale, rowMin.Y),
                    new Vector2(rowMax.X - 16f * scale, rowMin.Y), dividerColor, 1f);
            }

            if (!selected && UiInteract.HoverClick(rowMin, rowMax))
            {
                SetMessagePolicy(index);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight * SocialAudience.Options.Length));
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
                    messagePolicyLoaded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethergram message privacy load failed: {exception.Message}");
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
                AepLog.Warning($"Aethergram message privacy update failed: {exception.Message}");
            }
        });
    }
}
