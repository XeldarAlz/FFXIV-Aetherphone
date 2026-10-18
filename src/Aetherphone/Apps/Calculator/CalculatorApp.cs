using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Calculator;

internal sealed partial class CalculatorApp : IResumableApp
{
    private const float KeyGap = 12f;
    private const float KeypadBottomGap = 8f;
    private const float DisplayGap = 10f;
    private const float MaxFrameSeconds = 0.1f;
    private const int LocalizedCacheLimit = 256;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public string Id => "calculator";
    public string DisplayName => Loc.T(L.Apps.Calculator);
    public string Glyph => "=";
    public Vector4 Accent => AppAccents.For("calculator");
    public int BadgeCount => 0;

    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly CalculatorEngine engine;
    private readonly AppSkin ui = new(AppPalettes.Calculator);
    private readonly Dictionary<string, string> localized = new();
    private NumberMarks marks = NumberMarks.Invariant;
    private string decimalLabel = ".";
    private int savedHistoryVersion;
    private float delta;

    public CalculatorApp(Configuration configuration, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.confirm = confirm;
        engine = new CalculatorEngine(configuration.CalculatorHistory, UnixNow);
        savedHistoryVersion = engine.HistoryVersion;
    }

    public void OnOpened()
    {
        solvedAtOpen = engine.SolvedCount;
        solvedSeen = engine.SolvedCount;
        menuOpen = false;
        menuAppear.SnapTo(0f);
        gestureTracking = false;
        historySheet.CloseImmediately();
        displayScale.SnapTo(0f);
        answerAppear.SnapTo(1f);
        scrollTapeToBottom = true;
    }

    public void OnResumed() => gestureTracking = false;

    public void OnClosed()
    {
        historySheet.CloseImmediately();
        menuOpen = false;
        SaveHistoryIfChanged();
    }

    public void Draw(in PhoneContext context)
    {
        var scale = UiScale.Current;
        delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        ui.Theme = context.Theme;
        ui.Palette = AppPalettes.Calculator;
        RefreshMarks();
        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ui.Backdrop(screen);

        var keyGap = KeyGap * scale;
        var button = (content.Width - 3f * keyGap) / 4f;
        var keypadHeight = 5f * button + 4f * keyGap;
        var keypadBottom = content.Max.Y - KeypadBottomGap * scale;
        var keypad = new Rect(new Vector2(content.Min.X, keypadBottom - keypadHeight),
            new Vector2(content.Max.X, keypadBottom));
        var barBottom = content.Min.Y + NavBarMetrics.InlineHeight * scale;
        var display = new Rect(new Vector2(content.Min.X, barBottom),
            new Vector2(content.Max.X, keypad.Min.Y - DisplayGap * scale));

        using (InputShield.Engage(historySheet.CapturesPointer))
        {
            if (!historySheet.CapturesPointer)
            {
                HandleKeyboard(screen);
            }

            DrawDisplay(display, screen, scale);
            DrawKeypad(keypad, button, keyGap, scale);
            DrawHistoryButton(content, scale);
        }

        DrawHistorySheet(screen, context.Theme);
        SaveHistoryIfChanged();
    }

    private void DrawHistoryButton(Rect content, float scale)
    {
        var radius = Metrics.Size.GlassButton * scale * 0.5f;
        var center = new Vector2(NavBarMetrics.ButtonCenterX(content.Max.X, 0, 1, scale),
            content.Min.Y + NavBarMetrics.InlineHeight * scale * 0.5f);
        var extent = new Vector2(radius, radius);
        UiAnchors.Report("calculator.history", new Rect(center - extent, center + extent));
        if (AppHeader.NavButton(ImGui.GetWindowDrawList(), ImGui.GetID("calculator.history"), center,
                IconGlyph.Of(FontAwesomeIcon.History), Loc.T(L.Calculator.History), ui.TitleInk, ui.Theme, scale))
        {
            OpenHistory();
        }
    }

    private void RefreshMarks()
    {
        var current = NumberMarks.From(Loc.Culture.NumberFormat);
        if (current == marks)
        {
            return;
        }

        marks = current;
        decimalLabel = current.Decimal.ToString();
        localized.Clear();
        historyRowsVersion = -1;
    }

    private string Localize(string raw)
    {
        if (localized.TryGetValue(raw, out var cached))
        {
            return cached;
        }

        if (localized.Count >= LocalizedCacheLimit)
        {
            localized.Clear();
        }

        var text = CalculatorText.Localize(raw, marks);
        localized[raw] = text;
        return text;
    }

    private void SaveHistoryIfChanged()
    {
        if (engine.HistoryVersion == savedHistoryVersion)
        {
            return;
        }

        savedHistoryVersion = engine.HistoryVersion;
        configuration.Save();
    }

    private static void Play(UiSound sound) => UiFeedback.Play(sound);

    private static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void Dispose()
    {
    }
}
