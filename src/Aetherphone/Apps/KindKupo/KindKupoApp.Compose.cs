using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.KindKupo;

internal sealed partial class KindKupoApp
{
    private const int MaxPostLength = 1000;

    private static readonly List<LocString> ExpiryTabOptions =
    [
        L.KindKupo.ExpiryNever,
        L.KindKupo.Expiry1d,
        L.KindKupo.Expiry3d,
        L.KindKupo.Expiry7d,
    ];

    private readonly List<string> expiryTabLabels = new(ExpiryTabOptions.Count);
    private string replyDraft = string.Empty;
    private string composeStatus = string.Empty;
    private int activeExpiryTab;
    private volatile bool composeBusy;
    private volatile int composeOutcome;
    private volatile int replyOutcome;

    private static long ExpiryFromTab(int tab) => tab switch
    {
        1 => DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds(),
        2 => DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds(),
        3 => DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds(),
        _ => 0L,
    };

    private void DrawWriteScreen(Rect area)
    {
        if (composeOutcome == 1)
        {
            composeOutcome = 0;
            draft = string.Empty;
            composeStatus = string.Empty;
            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
        }

        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var origin = ImGui.GetCursorScreenPos();

        var actionLabel = Loc.T(L.KindKupo.Post);
        var postWidth = AppSkin.HeaderActionWidth(actionLabel);
        var postLeft = area.Max.X - 12f * scale - postWidth;

        var stripWidth = 120f * scale;
        const float trackHeight = 25f;
        var stripHeight = trackHeight * scale;
        var stripGap = 8f * scale;
        var stripRight = postLeft - stripGap;
        var stripLeft = stripRight - stripWidth;
        var stripRect = new Rect(
            new Vector2(stripLeft, rowCenterY - stripHeight * 0.5f),
            new Vector2(stripRight, rowCenterY + stripHeight * 0.5f));

        var rightReserve = area.Max.X - stripLeft + 6f * scale;

        AppHeader.Draw(context, string.Empty, back);

        AppHeader.DrawTitleWithReserve(
            area,
            "kindkupo.new.confession",
            Loc.T(L.KindKupo.NewConfession),
            rightReserve,
            AppPalettes.KindKupo.TitleInk,
            scale,
            new TextStyle(1.05f, FontWeight.SemiBold));

        expiryTabLabels.Clear();
        for (var index = 0; index < ExpiryTabOptions.Count; index++)
        {
            expiryTabLabels.Add(Loc.T(ExpiryTabOptions[index]));
        }

        UiAnchors.Report("kindkupo.compose.expiry", stripRect);
        activeExpiryTab = SegmentStrip.Draw(
            "kindkupo.expiryTab",
            stripRect,
            expiryTabLabels,
            activeExpiryTab,
            AppPalettes.KindKupo,
            trackHeight: trackHeight,
            textScale: 0.70f);

        var canSubmit = !string.IsNullOrWhiteSpace(draft) && !composeBusy;
        if (ui.HeaderAction(area, Loc.T(L.KindKupo.Post), canSubmit))
        {
            composeBusy = true;
            composeStatus = string.Empty;
            store.ComposeConfession(draft, ExpiryFromTab(activeExpiryTab), success =>
            {
                composeBusy = false;
                composeOutcome = success ? 1 : 2;
            });
        }

        var fieldTop = ImGui.GetCursorScreenPos().Y;
        var fieldHeight = MathF.Max(120f, (area.Max.Y - fieldTop - 44f * scale) / scale);
        ui.Field(string.Empty, "##confessionText", ref draft, MaxPostLength, true, fieldHeight);

        if (draft.Length == 0)
        {
            var placeholderPos = new Vector2(
                origin.X + 16f * scale,
                origin.Y + 63f * scale);

            Typography.Draw(
                placeholderPos,
                Loc.T(L.KindKupo.Placeholder),
                AppPalettes.KindKupo.MutedInk,
                1.0f);
        }

        var remaining = MaxPostLength - draft.Length;
        var counter = string.Format(Loc.Culture, "{0} / {1}", draft.Length, MaxPostLength);
        var counterColor = remaining < 40
            ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f))
            : AppPalettes.KindKupo.MutedInk;
        var counterSize = Typography.Measure(counter, 1f, FontWeight.Medium);
        var counterY = (area.Max.Y - 44f * 0.5f * scale) - counterSize.Y * 0.5f;
        Typography.Draw(new Vector2(area.Max.X - 6f * scale - counterSize.X, counterY),
            counter, counterColor, 1f, FontWeight.Medium);

        if (composeStatus.Length > 0)
        {
            Typography.Draw(new Vector2(area.Min.X + 16f * scale, counterY), composeStatus, theme.Danger, 0.9f,
                FontWeight.Medium);
        }
    }

    private void DrawComposeResponse(Rect area, ConfessionDto confession)
    {
        if (replyOutcome == 1)
        {
            replyOutcome = 0;
            replyDraft = string.Empty;
            composeStatus = string.Empty;
            router.Pop();
            return;
        }

        if (replyOutcome == 2)
        {
            replyOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
        }

        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var padding = 16f * scale;

        AppHeader.Draw(context, Loc.T(L.KindKupo.Respond), back);

        var canSubmit = !string.IsNullOrWhiteSpace(replyDraft) && !composeBusy;
        if (ui.HeaderAction(area, Loc.T(L.KindKupo.Post), canSubmit))
        {
            composeBusy = true;
            composeStatus = string.Empty;
            store.SubmitResponse(confession.Id, replyDraft, success =>
            {
                composeBusy = false;
                replyOutcome = success ? 1 : 2;
            });
        }

        var top = area.Min.Y + AppHeader.Height * scale + 8f * scale;
        var body = new Rect(new Vector2(area.Min.X + padding, top), new Vector2(area.Max.X - padding, area.Max.Y));

        using (AppSurface.Begin(body))
        {
            DrawConfessionCard(confession, KindKupoScreen.ComposeResponse);

            ImGui.Dummy(new Vector2(0f, 8f * scale));

            if (composeStatus.Length > 0)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, theme.Danger))
                {
                    Typography.Plain(composeStatus);
                }

                ImGui.Dummy(new Vector2(0f, 4f * scale));
            }

            var availableHeight = MathF.Max(120f, (area.Max.Y - ImGui.GetCursorScreenPos().Y - 40f * scale) / scale);
            var fieldPos = ImGui.GetCursorScreenPos();
            ui.Field(string.Empty, "##replyDraft", ref replyDraft, MaxPostLength, true, availableHeight);

            if (replyDraft.Length == 0)
            {
                Typography.Draw(
                    new Vector2(fieldPos.X + 16f * scale, fieldPos.Y + 30f * scale),
                    Loc.T(L.KindKupo.ReplyPlaceholder),
                    AppPalettes.KindKupo.MutedInk,
                    1.0f);
            }
        }
    }

    private void DrawResponseFeed(Rect area)
    {
        var scale = UiScale.Current;
        var padding = 16f * scale;
        var feed = store.Confessions;

        AppHeader.Draw(new PhoneContext(area, theme, navigation), Loc.T(L.KindKupo.Respond), back);

        var top = area.Min.Y + AppHeader.Height * scale + 8f * scale;
        var body = new Rect(new Vector2(area.Min.X + padding, top), new Vector2(area.Max.X - padding, area.Max.Y));

        if (feed.Length == 0)
        {
            var empty = new Rect(new Vector2(area.Min.X, top), area.Max);
            if (store.Loading)
            {
                LoadingPulse.Spinner(empty.Center, 14f * scale, ui.Accent);
            }
            else
            {
                EmptyState.Draw(empty, ui, FontAwesomeIcon.Comments, Loc.T(L.KindKupo.NoConfessions),
                    string.Empty);
            }

            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < feed.Length; index++)
            {
                DrawConfessionCard(feed[index], KindKupoScreen.Respond);
            }

            if (store.HasMoreConfessions && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 8f * scale)
            {
                store.LoadMore();
            }
        }
    }
}
