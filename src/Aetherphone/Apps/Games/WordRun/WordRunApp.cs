using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.WordRun;

internal sealed class WordRunApp : ILegacyMiniGame
{
    private const string GameId = "wordrun";
    private const string WordsFolder = "Words";
    private const string DefaultBank = "en";
    private const float KeyboardFraction = 0.3f;
    private const float MessageSeconds = 1.4f;
    private const float SolvedPauseSeconds = 2.5f;
    private const float ConfirmSeconds = 2.5f;
    private static readonly string[] BankCodes = { "en", "de", "es", "fr", "pt" };
    private static readonly string[] BankLabels = { "EN", "DE", "ES", "FR", "PT" };
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.33f, 0.70f, 0.42f, 1f), new(0.80f, 0.65f, 0.26f, 1f), new(0.98f, 0.98f, 0.9f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
    };

    private sealed class Bank
    {
        public string[] Answers = Array.Empty<string>();
        public HashSet<string> Valid = new();
    }

    private readonly GameData gameData;
    private readonly WordRunBoard board = new();
    private readonly WordRunRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Dictionary<string, Bank> banks = new();
    private readonly List<string> supplement = new();
    private readonly List<string> availableCodes = new();
    private readonly List<string> availableLabels = new();
    private RollingValue scoreRoll;
    private bool supplementBuilt;
    private bool started;
    private bool finished;
    private bool pendingSubmit;
    private bool newBest;
    private int loadedBest;
    private int selectedBank;
    private float resultAppear;
    private int revealRow = -1;
    private float revealSeconds;
    private float shakeRemaining;
    private float messageProgress = 1f;
    private string messageText = string.Empty;
    private float solvedRemaining;
    private float confirmRemaining;
    private string resultLine = string.Empty;
    private string wordsText = string.Empty;
    private int wordsTextCount = -1;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.WordRun);
    public GameGenre Genre => GameGenre.Brain;
    public bool RunsOnAClock => true;

    public WordRunApp(GameData gameData)
    {
        this.gameData = gameData;
    }

    public void Open()
    {
        loadedBest = 0;
        started = false;
        BuildSupplement();
        DiscoverBanks();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private static string WordsPath(string code, string kind) =>
        Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, WordsFolder, code + "." + kind + ".txt");

    private void DiscoverBanks()
    {
        availableCodes.Clear();
        availableLabels.Clear();
        for (var index = 0; index < BankCodes.Length; index++)
        {
            if (!File.Exists(WordsPath(BankCodes[index], "answers")))
            {
                continue;
            }

            availableCodes.Add(BankCodes[index]);
            availableLabels.Add(BankLabels[index]);
        }
    }

    private void BuildSupplement()
    {
        if (supplementBuilt)
        {
            return;
        }

        supplementBuilt = true;
        try
        {
            AddNames(gameData.CollectableMountIds(), gameData.MountEntry);
            AddNames(gameData.CollectableMinionIds(), gameData.MinionEntry);
            AddNames(gameData.TriviaActionIds(), gameData.ActionEntry);
            AddNames(gameData.TriviaEmoteIds(), gameData.EmoteEntry);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[WordRun] Could not read game names for the word bank: {exception.Message}");
        }
    }

    private void AddNames(uint[] ids, Func<uint, NamedIcon> lookup)
    {
        for (var index = 0; index < ids.Length; index++)
        {
            var name = lookup(ids[index]).Name;
            if (name is null || name.Length != WordRunBoard.WordLength)
            {
                continue;
            }

            var upper = name.ToUpperInvariant();
            var letters = true;
            for (var letter = 0; letter < upper.Length; letter++)
            {
                if (upper[letter] < 'A' || upper[letter] > 'Z')
                {
                    letters = false;
                    break;
                }
            }

            if (letters && !supplement.Contains(upper))
            {
                supplement.Add(upper);
            }
        }
    }

    private Bank LoadBank(string code)
    {
        if (banks.TryGetValue(code, out var cached))
        {
            return cached;
        }

        var bank = new Bank();
        try
        {
            var answerLines = File.ReadAllLines(WordsPath(code, "answers"));
            var validLines = File.ReadAllLines(WordsPath(code, "valid"));
            var answers = new List<string>(answerLines.Length + supplement.Count);
            for (var index = 0; index < answerLines.Length; index++)
            {
                var word = answerLines[index].Trim().ToUpperInvariant();
                if (word.Length == WordRunBoard.WordLength)
                {
                    answers.Add(word);
                    bank.Valid.Add(word);
                }
            }

            for (var index = 0; index < validLines.Length; index++)
            {
                var word = validLines[index].Trim().ToUpperInvariant();
                if (word.Length == WordRunBoard.WordLength)
                {
                    bank.Valid.Add(word);
                }
            }

            for (var index = 0; index < supplement.Count; index++)
            {
                if (bank.Valid.Add(supplement[index]))
                {
                    answers.Add(supplement[index]);
                }
            }

            bank.Answers = answers.ToArray();
        }
        catch (Exception exception)
        {
            AepLog.Error($"[WordRun] Could not load the {code} word bank: {exception.Message}");
        }

        banks[code] = bank;
        return bank;
    }

    private int InitialBank(GameContext context)
    {
        var stored = context.Stats.WordBank;
        var storedIndex = availableCodes.IndexOf(stored);
        if (storedIndex >= 0)
        {
            return storedIndex;
        }

        var cultureIndex = availableCodes.IndexOf(Loc.Culture.TwoLetterISOLanguageName);
        if (cultureIndex >= 0)
        {
            return cultureIndex;
        }

        var fallback = availableCodes.IndexOf(DefaultBank);
        return fallback >= 0 ? fallback : 0;
    }

    private void StartNewGame()
    {
        var bank = LoadBank(availableCodes[selectedBank]);
        board.Load(bank.Answers, bank.Valid);
        board.StartRun();
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        finished = false;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        revealRow = -1;
        revealSeconds = 0f;
        shakeRemaining = 0f;
        messageProgress = 1f;
        solvedRemaining = 0f;
        confirmRemaining = 0f;
        wordsTextCount = -1;
        started = true;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        if (availableCodes.Count == 0)
        {
            Typography.DrawCentered(drawList, body.Center, Loc.T(L.Games.NotInWordList), theme.TextMuted, TextStyles.Subheadline);
            return;
        }

        if (loadedBest == 0)
        {
            loadedBest = context.Stats.Get(GameId).BestScore;
        }

        if (!started)
        {
            selectedBank = InitialBank(context);
            StartNewGame();
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, board.Score);
            if (newBest)
            {
                loadedBest = board.Score;
            }

            pendingSubmit = false;
        }

        var rowY = body.Min.Y + 30f * scale;
        var stripHeight = availableCodes.Count > 1 ? 34f * scale : 0f;
        var keyboardHeight = body.Height * KeyboardFraction;
        var keyboardArea = new Rect(new Vector2(body.Min.X + 4f * scale, body.Max.Y - keyboardHeight), body.Max - new Vector2(4f * scale, 6f * scale));
        var boardArea = new Rect(new Vector2(body.Min.X + 16f * scale, rowY + 30f * scale + stripHeight),
            new Vector2(body.Max.X - 16f * scale, keyboardArea.Min.Y - 10f * scale));
        AdvanceTimers(deltaSeconds, boardArea, scale);
        if (!finished)
        {
            board.Tick(fx.ScaleDelta(deltaSeconds));
            var press = renderer.DrawKeyboard(board, keyboardArea, Accent, theme, scale);
            HandleInput(press, boardArea, scale);
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        DrawHud(body, rowY, theme, deltaSeconds, scale);
        if (stripHeight > 0f)
        {
            DrawBankStrip(body, rowY + 30f * scale, stripHeight, theme, scale);
        }

        var shake = fx.ShakeOffset(scale);
        var shakenBoard = new Rect(boardArea.Min + shake, boardArea.Max + shake);
        renderer.DrawBoard(board, shakenBoard, revealRow, revealSeconds, shakeRemaining, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, new Vector2(boardArea.Center.X, boardArea.Min.Y + boardArea.Height * 0.42f), messageText, Accent,
            theme, messageProgress, TextStyles.Headline);
        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void AdvanceTimers(float deltaSeconds, Rect boardArea, float scale)
    {
        if (revealRow >= 0)
        {
            revealSeconds += deltaSeconds;
            if (revealSeconds >= WordRunRenderer.RevealSeconds)
            {
                OnRevealFinished(boardArea, scale);
            }
        }

        shakeRemaining = MathF.Max(0f, shakeRemaining - deltaSeconds);
        confirmRemaining = MathF.Max(0f, confirmRemaining - deltaSeconds);
        messageProgress = GameBanner.Advance(messageProgress, deltaSeconds, MessageSeconds);
        if (solvedRemaining <= 0f)
        {
            return;
        }

        solvedRemaining -= deltaSeconds;
        if (solvedRemaining <= 0f)
        {
            solvedRemaining = 0f;
            board.NextWord();
            revealRow = -1;
        }
    }

    private void OnRevealFinished(Rect boardArea, float scale)
    {
        var row = revealRow;
        revealRow = -1;
        revealSeconds = 0f;
        if (board.Outcome == WordOutcome.Solved)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            var grid = WordRunRenderer.Grid(boardArea);
            var center = grid.CellCenter(2, row);
            particles.Confetti(center, 50, CelebrationPalette, 240f * scale, 3.5f, 1.2f);
            fx.Shockwave(center, grid.Pitch * 3f, WordRunRenderer.CorrectColor with { W = 0.7f }, 0.45f, 2.5f);
            ShowMessage($"{Loc.T(L.Games.SolvedWord)}  +{GameNumber.Label(board.LastWordPoints)}", SolvedPauseSeconds);
            solvedRemaining = SolvedPauseSeconds;
            return;
        }

        if (board.Outcome == WordOutcome.Failed)
        {
            Finish();
        }
    }

    private void Finish()
    {
        finished = true;
        resultAppear = 0f;
        pendingSubmit = true;
        resultLine = $"{Loc.T(L.Games.WordWas, board.Answer)}  ·  {Loc.T(L.Games.Words)} {GameNumber.Label(board.WordsSolved)}";
    }

    private void ShowMessage(string text, float seconds)
    {
        messageText = text;
        messageProgress = 0f;
        messageLifetime = seconds;
    }

    private float messageLifetime = MessageSeconds;

    private void HandleInput(in KeyboardPress press, Rect boardArea, float scale)
    {
        if (board.Outcome != WordOutcome.Playing || revealRow >= 0)
        {
            return;
        }

        var keyboard = GameInput.Claim();
        var letter = press.Letter;
        if (keyboard)
        {
            for (var index = 0; index < WordRunBoard.LetterCount && letter == '\0'; index++)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.A + index, false))
                {
                    letter = (char)('A' + index);
                }
            }
        }

        if (letter != '\0')
        {
            board.TypeLetter(letter);
            return;
        }

        if (press.Backspace || (keyboard && ImGui.IsKeyPressed(ImGuiKey.Backspace)))
        {
            board.Backspace();
            return;
        }

        if (!press.Enter && !(keyboard && (ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false))))
        {
            return;
        }

        var result = board.Submit();
        switch (result)
        {
            case WordSubmit.TooShort:
                shakeRemaining = WordRunRenderer.ShakeSeconds;
                ShowMessage(Loc.T(L.Games.NotEnoughLetters), MessageSeconds);
                fx.AddTrauma(0.08f);
                break;
            case WordSubmit.NotAWord:
                shakeRemaining = WordRunRenderer.ShakeSeconds;
                ShowMessage(Loc.T(L.Games.NotInWordList), MessageSeconds);
                UiFeedback.Play(UiSound.GameWrong);
                fx.AddTrauma(0.08f);
                break;
            default:
                revealRow = board.RowCount - 1;
                revealSeconds = 0f;
                UiFeedback.Play(UiSound.GameCardFlip);
                fx.AddTrauma(0.04f);
                break;
        }
    }

    private void DrawHud(Rect body, float rowY, PhoneTheme theme, float deltaSeconds, float scale)
    {
        if (wordsTextCount != board.WordsSolved)
        {
            wordsTextCount = board.WordsSolved;
            wordsText = GameNumber.Label(board.WordsSolved);
        }

        var scoreLabel = Loc.T(L.Games.Score);
        var scoreText = GameNumber.Label(board.Score);
        var wordsLabel = Loc.T(L.Games.Words);
        var scoreWidth = GameHud.PillWidth(scoreLabel, scoreText);
        var wordsWidth = GameHud.PillWidth(wordsLabel, wordsText);
        var gap = 12f * scale;
        var scoreX = body.Center.X - gap * 0.5f - scoreWidth * 0.5f;
        var wordsX = body.Center.X + gap * 0.5f + wordsWidth * 0.5f;
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(scoreX, rowY), scoreLabel, ref scoreRoll, board.Score, Accent, theme, deltaSeconds, beatingBest);
        GameHud.Pill(new Vector2(wordsX, rowY), wordsLabel, wordsText, Accent, theme);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 22f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame();
            return;
        }

        if (finished)
        {
            return;
        }

        var endLabel = confirmRemaining > 0f ? Loc.T(L.Games.Sure) : Loc.T(L.Games.EndRun);
        var endWidth = MathF.Max(64f * scale, Typography.Measure(endLabel, TextStyles.Caption1).X + 20f * scale);
        if (!GameHud.Button(new Vector2(body.Min.X + 8f * scale + endWidth * 0.5f, rowY), new Vector2(endWidth, 28f * scale), endLabel,
                Accent, theme))
        {
            return;
        }

        if (confirmRemaining > 0f)
        {
            confirmRemaining = 0f;
            board.EndRun();
            Finish();
            return;
        }

        confirmRemaining = ConfirmSeconds;
    }

    private void DrawBankStrip(Rect body, float top, float height, PhoneTheme theme, float scale)
    {
        var row = new Rect(new Vector2(body.Min.X + 24f * scale, top + 4f * scale), new Vector2(body.Max.X - 24f * scale, top + height - 4f * scale));
        var selection = SegmentStrip.Draw("wordrun.bank", row, availableLabels, selectedBank, theme);
        if (selection == selectedBank)
        {
            return;
        }

        selectedBank = selection;
        StartNewGame();
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score), GameNumber.Label(board.Score), resultLine,
            newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame();
        }
    }
}
