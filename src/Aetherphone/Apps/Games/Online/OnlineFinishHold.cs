using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

internal sealed class OnlineFinishHold
{
    private const long HoldMilliseconds = 5_000;
    private const float CardPaddingX = 18f;
    private const float CardPaddingY = 12f;
    private const float LineGap = 4f;

    private string headline = string.Empty;
    private long countdownStartedAtTick;
    private int cachedSeconds = -1;
    private string cachedCountdown = string.Empty;
    private LanguageInfo? cachedLanguage;
    private bool active;
    private bool skipped;

    public bool Holding => active && !Elapsed;

    private bool Elapsed => skipped
        || (countdownStartedAtTick != 0 && Environment.TickCount64 - countdownStartedAtTick >= HoldMilliseconds);

    public void Begin(string result)
    {
        active = true;
        skipped = false;
        countdownStartedAtTick = 0;
        cachedSeconds = -1;
        headline = result;
    }

    public void Clear()
    {
        active = false;
        skipped = false;
        countdownStartedAtTick = 0;
    }

    public void Draw(ImDrawListPtr drawList, Vector2 center, float maxWidth, PhoneTheme theme, float scale,
        bool settled)
    {
        if (!Holding)
        {
            return;
        }

        if (countdownStartedAtTick == 0)
        {
            if (!settled)
            {
                return;
            }

            countdownStartedAtTick = Environment.TickCount64;
        }

        var countdown = CountdownText(HoldMilliseconds - (Environment.TickCount64 - countdownStartedAtTick));
        var paddingX = CardPaddingX * scale;
        var paddingY = CardPaddingY * scale;
        var textWidth = MathF.Max(1f, maxWidth - paddingX * 2f);
        var headlineBlock = Typography.MeasureWrappedBlock(headline, TextStyles.SubheadlineEmphasized, textWidth);
        var countdownBlock = Typography.MeasureWrappedBlock(countdown, TextStyles.Footnote, textWidth);
        var width = MathF.Max(headlineBlock.X, countdownBlock.X) + paddingX * 2f;
        var height = headlineBlock.Y + LineGap * scale + countdownBlock.Y + paddingY * 2f;
        var half = new Vector2(width, height) * 0.5f;
        var min = center - half;
        var max = center + half;
        var radius = Metrics.Radius.Card * scale;
        var accent = Core.Apps.AppAccents.For("games");
        var hovered = UiInteract.Hover(min, max);
        Elevation.Floating(drawList, min, max, radius, scale);
        Material.Frosted(drawList, min, max, radius, scale);
        Squircle.Stroke(drawList, min, max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(accent, hovered ? 0.75f : 0.4f)), 1f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawWrappedCentered(drawList, new Vector2(center.X, min.Y + paddingY + headlineBlock.Y * 0.5f),
            headline, theme.TextStrong, TextStyles.SubheadlineEmphasized, textWidth);
        Typography.DrawWrappedCentered(drawList, new Vector2(center.X, max.Y - paddingY - countdownBlock.Y * 0.5f),
            countdown, theme.TextMuted, TextStyles.Footnote, textWidth);
        if (UiInteract.Click(min, max, hovered))
        {
            skipped = true;
        }
    }

    private string CountdownText(long remainingMilliseconds)
    {
        var seconds = (int)((remainingMilliseconds + 999) / 1000);
        if (seconds < 1)
        {
            seconds = 1;
        }

        if (seconds == cachedSeconds && ReferenceEquals(Loc.Current, cachedLanguage))
        {
            return cachedCountdown;
        }

        cachedSeconds = seconds;
        cachedLanguage = Loc.Current;
        cachedCountdown = string.Concat(Loc.Plural(L.Games.OnlineBackToLobby, seconds), " · ",
            Loc.T(L.Games.OnlineTapToSkip));
        return cachedCountdown;
    }
}
