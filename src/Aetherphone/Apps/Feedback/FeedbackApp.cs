using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Feedback;

internal sealed class FeedbackApp : IPhoneApp
{
    private const int MaxFeedbackLength = 1000;
    private const long CooldownSeconds = 60;

    public string Id => "feedback";
    public string DisplayName => Loc.T(L.Apps.Feedback);
    public string Glyph => "Fb";
    public int BadgeCount => 0;

    private readonly FeedbackStore store;
    private readonly AppSkin ui = new(AppPalettes.Feedback);

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private string draft = string.Empty;
    private volatile int composeOutcome;
    private bool sent;

    public FeedbackApp(AethernetSession session, AethernetClient client)
    {
        store = new FeedbackStore(session, client);
    }

    public void OnOpened()
    {
        composeOutcome = 0;
        sent = false;
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        ui.Body(content);
        DrawScreen(content);
    }

    private void DrawScreen(Rect area)
    {
        if (composeOutcome == 1)
        {
            composeOutcome = 0;
            draft = string.Empty;
            sent = true;
            Plugin.Cfg.LastFeedbackSentUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Plugin.Cfg.Save();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var headerContext = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(headerContext, Loc.T(L.Feedback.SendFeedback), navigation.Back);

        if (!sent)
        {
            var canSend = !string.IsNullOrWhiteSpace(draft) && !store.Posting && CooldownRemaining() == 0;
            if (ui.HeaderAction(area, store.Posting ? Loc.T(L.Feedback.Sending) : Loc.T(L.Feedback.Send), canSend))
            {
                AskSend();
            }
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            if (sent)
            {
                DrawThankYou(area);
            }
            else
            {
                DrawFeedbackCard(area);
            }
        }
    }

    private void DrawFeedbackCard(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var footerHeight = 40f * scale;

        var pad = 14f * scale;
        var cardMin = new Vector2(origin.X + 4f * scale, origin.Y + 4f * scale);
        var cardMax = new Vector2(origin.X + width - 4f * scale, area.Max.Y - footerHeight);
        ui.Card(drawList, cardMin, cardMax, 18f * scale);

        var inputX = cardMin.X + pad;
        var inputTop = cardMin.Y + pad;
        var inputWidth = width - pad * 2f;
        var inputHeight = cardMax.Y - inputTop - pad;
        ImGui.SetCursorScreenPos(new Vector2(inputX, inputTop));
        ImGui.SetNextItemWidth(inputWidth);

        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Feedback.TitleInk))
        using (Plugin.Fonts.Push(1.15f))
        {
            ImGui.InputTextMultiline("##feedbackBody", ref draft, MaxFeedbackLength,
                new Vector2(inputWidth, inputHeight), ImGuiInputTextFlags.None);
        }

        if (draft.Length == 0)
        {
            var placeholderPos = new Vector2(inputX + 4f * scale, inputTop + 2f * scale);
            var wrapRight = inputX + inputWidth - 4f * scale - ImGui.GetWindowPos().X;
            using (Plugin.Fonts.Push(1.15f))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Feedback.MutedInk))
            {
                ImGui.SetCursorScreenPos(placeholderPos);
                ImGui.PushTextWrapPos(wrapRight);
                ImGui.TextUnformatted(Loc.T(L.Feedback.Placeholder));
                ImGui.PopTextWrapPos();
            }
        }

        var remaining = MaxFeedbackLength - draft.Length;
        var counterColor = remaining < 40
            ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f))
            : AppPalettes.Feedback.MutedInk;
        var counter = remaining.ToString(Loc.Culture);
        var counterSize = Typography.Measure(counter, 0.9f, FontWeight.Medium);
        Typography.Draw(new Vector2(area.Max.X - 4f * scale - counterSize.X,
            area.Max.Y - footerHeight * 0.5f - counterSize.Y * 0.5f), counter, counterColor, 0.9f, FontWeight.Medium);

        var cooldown = CooldownRemaining();
        if (cooldown > 0)
        {
            var notice = Loc.T(L.Feedback.Cooldown, FormatCooldown(cooldown));
            Typography.Draw(new Vector2(origin.X + 2f * scale,
                area.Max.Y - footerHeight * 0.5f - Typography.Measure(notice, 0.85f).Y * 0.5f), notice,
                AppPalettes.Feedback.MutedInk, 0.85f);
        }
    }

    private void DrawThankYou(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cardMin = new Vector2(origin.X + 4f * scale, origin.Y + 4f * scale);
        var cardMax = new Vector2(origin.X + width - 4f * scale, area.Max.Y - 8f * scale);
        ui.Card(drawList, cardMin, cardMax, 18f * scale);

        var centerX = (cardMin.X + cardMax.X) * 0.5f;
        var badgeCenter = new Vector2(centerX, cardMin.Y + 78f * scale);
        var badgeRadius = 34f * scale;
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(AppPalettes.Feedback.Accent), 48);
        var check = badgeRadius;
        var checkColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        drawList.AddLine(badgeCenter + new Vector2(-0.42f * check, 0.02f * check),
            badgeCenter + new Vector2(-0.10f * check, 0.34f * check), checkColor, 4f * scale);
        drawList.AddLine(badgeCenter + new Vector2(-0.10f * check, 0.34f * check),
            badgeCenter + new Vector2(0.44f * check, -0.30f * check), checkColor, 4f * scale);

        var titleY = badgeCenter.Y + badgeRadius + 26f * scale;
        Typography.DrawCentered(new Vector2(centerX, titleY), Loc.T(L.Feedback.Sent), theme.TextStrong, 1.35f,
            FontWeight.SemiBold);
        Typography.DrawCentered(new Vector2(centerX, titleY + 34f * scale), Loc.T(L.Feedback.ThankYou),
            AppPalettes.Feedback.BodyInk, 1.05f, FontWeight.Medium);
        Typography.DrawCentered(new Vector2(centerX, titleY + 60f * scale), Loc.T(L.Feedback.SentMessage),
            AppPalettes.Feedback.MutedInk, 0.9f);

        var cooldown = CooldownRemaining();
        var actionY = cardMax.Y - 36f * scale;
        if (cooldown > 0)
        {
            var notice = Loc.T(L.Feedback.Cooldown, FormatCooldown(cooldown));
            Typography.DrawCentered(new Vector2(centerX, actionY), notice, AppPalettes.Feedback.MutedInk, 0.95f,
                FontWeight.Medium);
            return;
        }

        var label = Loc.T(L.Feedback.SendMore);
        var buttonWidth = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 44f * scale;
        var buttonHeight = 34f * scale;
        var rect = new Rect(new Vector2(centerX - buttonWidth * 0.5f, actionY - buttonHeight * 0.5f),
            new Vector2(centerX + buttonWidth * 0.5f, actionY + buttonHeight * 0.5f));
        if (ui.PillButton(rect, label, true))
        {
            sent = false;
        }
    }

    private void AskSend()
    {
        if (string.IsNullOrWhiteSpace(draft) || store.Posting || CooldownRemaining() > 0)
        {
            return;
        }

        var pending = draft;
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Feedback.ConfirmMessage),
            ConfirmLabel = Loc.T(L.Feedback.Send),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Feedback.Sending),
            FailedMessage = Loc.T(L.Feedback.ErrorMessage),
            Danger = false,
            ConfirmAsync = done => store.Compose(pending, ok =>
            {
                if (ok)
                {
                    composeOutcome = 1;
                }

                done(ok);
            }),
        });
    }

    private static int CooldownRemaining()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remaining = CooldownSeconds - (now - Plugin.Cfg.LastFeedbackSentUnix);
        return remaining > 0 ? (int)remaining : 0;
    }

    private static string FormatCooldown(int seconds)
    {
        if (seconds >= 60)
        {
            return string.Format(Loc.Culture, "{0}:{1:D2}", seconds / 60, seconds % 60);
        }

        return string.Format(Loc.Culture, "{0}s", seconds);
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
