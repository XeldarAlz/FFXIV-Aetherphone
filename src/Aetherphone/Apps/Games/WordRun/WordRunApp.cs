using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.WordRun;

internal sealed class WordRunApp : IMiniGame
{
    private const string GameId = "wordrun";
    private const string WordsFolder = "Words";
    private const string AnswersKind = "answers";
    private const string ValidKind = "valid";
    private const string FileSuffix = ".txt";
    private const string DefaultBank = "en";
    private const float KeyboardFraction = 0.3f;
    private const float MessageSeconds = 1.4f;
    private const float SolvedPauseSeconds = 2.5f;
    private const float FallbackSeconds = 2.4f;
    private const float ConfirmSeconds = 2.5f;
    private const float ComboWindowSeconds = 3600f;
    private const float RowGap = 10f;
    private const float BannerFraction = 0.42f;
    private const float CapsulePadX = 10f;
    private const float CapsuleIconSize = 11f;
    private const float CapsuleIconGap = 5f;
    private const int PunchMultiplier = 3;
    private const int SweepGuesses = 2;
    private static readonly string[] BankCodes = { DefaultBank, "de", "es", "fr", "pt" };
    private static readonly LocString[] Modes =
    {
        L.WordRun.BankEnglish, L.WordRun.BankGerman, L.WordRun.BankSpanish, L.WordRun.BankFrench, L.WordRun.BankPortuguese,
    };
    private static readonly string[] ModeStatIds = { GameId, GameId, GameId, GameId, GameId };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.WordRun, GameGenre.Brain, L.WordRun.Hook,
        Backdrop.Paper, HudStyle.Standard, ScoreKind.Score, Modes, ModeStatIds, clocked: true, keyboard: true);
    private static readonly string?[] GainLabels = new string?[WordRunBoard.MaxPointsPerWord + 1];
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.33f, 0.70f, 0.42f, 1f), new(0.80f, 0.65f, 0.26f, 1f), new(0.98f, 0.98f, 0.9f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
    };
    private static readonly Vector4 Danger = new(0.92f, 0.28f, 0.32f, 1f);
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;

    private sealed class Bank
    {
        public string[] Answers = Array.Empty<string>();
        public HashSet<string> Valid = new();
    }

    private readonly GameData gameData;
    private readonly WordRunBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Dictionary<string, Bank> banks = new();
    private readonly HashSet<string> missingBanks = new();
    private readonly List<string> supplement = new();
    private ComboMeter combo = new(ComboWindowSeconds);
    private GameGrid grid;
    private Rect keyboardArea;
    private Rect speedRow;
    private string messageText = string.Empty;
    private string solvesValue = string.Empty;
    private int solvesShown = -1;
    private int lastSeenMode = -1;
    private int mode;
    private int bestStreak;
    private int revealRow = -1;
    private float revealSeconds;
    private float shakeRemaining;
    private float messageProgress = 1f;
    private float messageLifetime = MessageSeconds;
    private float solvedRemaining;
    private float confirmRemaining;
    private float entrance = 1f;
    private bool supplementBuilt;
    private bool bankEmpty;
    private bool finished;

    public WordRunApp(GameData gameData)
    {
        this.gameData = gameData;
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        mode = StageSpec.ClampMode(start.Mode);
        var code = BankCodes[mode];
        var bank = LoadBank(code);
        board.Load(bank.Answers, bank.Valid);
        board.StartRun(start.Random);
        bankEmpty = bank.Answers.Length == 0;
        particles.Clear();
        fx.Clear();
        combo.Reset();
        bestStreak = 0;
        revealRow = -1;
        revealSeconds = 0f;
        shakeRemaining = 0f;
        messageProgress = 1f;
        solvedRemaining = 0f;
        confirmRemaining = 0f;
        entrance = 0f;
        solvesShown = -1;
        finished = false;
        if (!bankEmpty && missingBanks.Contains(code))
        {
            ShowMessage(Loc.T(L.WordRun.EnglishFallback), FallbackSeconds);
        }
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        lastSeenMode = -1;
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        SyncBankChoice(context.Session);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Layout(context.Safe, scale);
        WordRunRenderer.DrawBoard(drawList, board, grid, -1, 0f, 0f, Accent, context.Backdrop.Ink, 1f, scale);
        WordRunRenderer.DrawKeyboard(drawList, board, keyboardArea, Accent, 0f, false, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var ink = context.Backdrop.Ink;
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds);
        combo.Update(simDelta);
        Layout(Grow(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale)), scale);
        var playing = context.Session.State == StageFlow.Playing && !finished;
        AdvanceTimers(simDelta, scale, context);
        if (!finished)
        {
            board.Tick(simDelta);
        }

        var press = WordRunRenderer.DrawKeyboard(drawList, board, keyboardArea, Accent, combo.Heat, playing, scale);
        if (playing)
        {
            HandleInput(press, scale, context);
        }

        WordRunRenderer.DrawBoard(drawList, board, grid, revealRow, revealSeconds, shakeRemaining, Accent, ink, entrance,
            scale);
        var speedBonus = board.Outcome == WordOutcome.Playing ? WordRunBoard.SpeedBonus(board.WordSeconds) : 0;
        WordRunRenderer.DrawSpeedBar(drawList, speedRow, speedBonus / (float)WordRunBoard.SpeedBonusMax,
            GainLabel(speedBonus), Accent, ink, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, new Vector2(grid.Center.X, grid.Origin.Y + grid.Height * BannerFraction), messageText,
            Accent, context.Theme, messageProgress, TextStyles.Headline);
        if (bankEmpty)
        {
            Typography.DrawCentered(drawList, grid.Center, Loc.T(L.WordRun.NoWordList),
                ink == StageInk.Dark ? GamePalette.InkDark : GamePalette.InkLight, TextStyles.Subheadline);
        }

        FillHud(context, drawList, playing, scale);
        context.Session.Report(board.Score);
    }

    private void Layout(Rect area, float scale)
    {
        var gap = RowGap * scale;
        var keyboardHeight = area.Height * KeyboardFraction;
        keyboardArea = new Rect(new Vector2(area.Min.X, area.Max.Y - keyboardHeight), area.Max);
        var inset = BoardPlate.Padding * scale;
        var boardArea = new Rect(area.Min + new Vector2(inset, inset),
            new Vector2(area.Max.X - inset, keyboardArea.Min.Y - gap - WordRunRenderer.SpeedRowHeight * scale - gap - inset));
        var centered = WordRunRenderer.Grid(boardArea);
        grid = WordRunRenderer.Grid(boardArea, boardArea.Min.Y - centered.Origin.Y);
        var plate = WordRunRenderer.PlateRect(grid, scale);
        speedRow = new Rect(new Vector2(plate.Min.X + inset, plate.Max.Y + gap),
            new Vector2(plate.Max.X - inset, plate.Max.Y + gap + WordRunRenderer.SpeedRowHeight * scale));
    }

    private void SyncBankChoice(GameSession session)
    {
        if (lastSeenMode < 0)
        {
            var stored = IndexOfCode(session.Stats.WordBank);
            if (stored >= 0 && stored != session.Mode)
            {
                session.SelectMode(stored);
            }

            lastSeenMode = session.Mode;
            return;
        }

        if (session.Mode == lastSeenMode)
        {
            return;
        }

        lastSeenMode = session.Mode;
        session.Stats.WordBank = BankCodes[StageSpec.ClampMode(lastSeenMode)];
    }

    private static int IndexOfCode(string code)
    {
        for (var index = 0; index < BankCodes.Length; index++)
        {
            if (string.Equals(BankCodes[index], code, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string WordsPath(string code, string kind) =>
        Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, WordsFolder,
            string.Concat(code, ".", kind, FileSuffix));

    private void EnsureSupplement()
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
            AepLog.Warning(string.Concat("[WordRun] Could not read game names for the word bank: ", exception.Message));
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

        EnsureSupplement();
        if (!File.Exists(WordsPath(code, AnswersKind)))
        {
            missingBanks.Add(code);
            var fallback = string.Equals(code, DefaultBank, StringComparison.Ordinal) ? new Bank() : LoadBank(DefaultBank);
            banks[code] = fallback;
            return fallback;
        }

        var bank = new Bank();
        try
        {
            var answerLines = File.ReadAllLines(WordsPath(code, AnswersKind));
            var validLines = File.ReadAllLines(WordsPath(code, ValidKind));
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
            AepLog.Error(string.Concat("[WordRun] Could not load the ", code, " word bank: ", exception.Message));
        }

        banks[code] = bank;
        return bank;
    }

    private void AdvanceTimers(float deltaSeconds, float scale, in GameContext context)
    {
        if (revealRow >= 0)
        {
            revealSeconds += deltaSeconds;
            if (revealSeconds >= WordRunRenderer.RevealSeconds)
            {
                OnRevealFinished(scale, context);
            }
        }

        shakeRemaining = MathF.Max(0f, shakeRemaining - deltaSeconds);
        confirmRemaining = MathF.Max(0f, confirmRemaining - deltaSeconds);
        messageProgress = GameBanner.Advance(messageProgress, deltaSeconds, messageLifetime);
        if (solvedRemaining <= 0f)
        {
            return;
        }

        solvedRemaining -= deltaSeconds;
        if (solvedRemaining > 0f)
        {
            return;
        }

        solvedRemaining = 0f;
        board.NextWord();
        revealRow = -1;
    }

    private void OnRevealFinished(float scale, in GameContext context)
    {
        var row = revealRow;
        revealRow = -1;
        revealSeconds = 0f;
        if (board.Outcome == WordOutcome.Solved)
        {
            OnSolved(row, scale, context);
            return;
        }

        if (board.Outcome == WordOutcome.Failed)
        {
            FinishRun(context);
        }
    }

    private void OnSolved(int row, float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        var center = grid.CellCenter(WordRunBoard.WordLength / 2, row);
        particles.Confetti(center, 50, CelebrationPalette, 240f * scale, 3.5f, 1.2f);
        fx.Shockwave(center, grid.Pitch * 3f, WordRunRenderer.CorrectColor with { W = 0.7f }, 0.45f, 2.5f);
        fx.AddText(GainLabel(board.LastWordPoints), center - new Vector2(0f, grid.Pitch), WordRunRenderer.CorrectColor,
            1.2f);
        ShowMessage(Loc.T(L.Games.SolvedWord), SolvedPauseSeconds);
        var multiplierBefore = combo.Multiplier;
        var multiplier = combo.Hit();
        bestStreak = Math.Max(bestStreak, combo.Count);
        if (multiplier > multiplierBefore)
        {
            GameSfx.ComboTierUp();
        }

        if (multiplier >= PunchMultiplier)
        {
            context.Fx.Punch(0.04f);
        }

        if (board.LastWordGuesses <= SweepGuesses)
        {
            context.Fx.Sweep();
            context.Fx.Flash(WordRunRenderer.CorrectColor, 0.18f);
        }

        solvedRemaining = SolvedPauseSeconds;
    }

    private void FinishRun(in GameContext context)
    {
        finished = true;
        confirmRemaining = 0f;
        UiFeedback.Play(UiSound.GameWrong);
        fx.AddTrauma(0.3f);
        context.Fx.Flash(Danger, 0.25f);
        context.Fx.Vignette(Danger, 0.35f, 0.8f);
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Words, GameNumber.Label(board.WordsSolved))
            .WithStat(L.WordRun.Answer, board.Answer)
            .WithStat(L.Games.Combo, GameNumber.Label(bestStreak))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)board.RunSeconds)));
    }

    private void ShowMessage(string text, float seconds)
    {
        messageText = text;
        messageProgress = 0f;
        messageLifetime = seconds;
    }

    private void HandleInput(in KeyboardPress press, float scale, in GameContext context)
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

        Submit(scale, context);
    }

    private void Submit(float scale, in GameContext context)
    {
        switch (board.Submit())
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
                context.Fx.Vignette(Danger, 0.2f, 0.3f);
                break;
            default:
                revealRow = board.RowCount - 1;
                revealSeconds = 0f;
                UiFeedback.Play(UiSound.GameCardFlip);
                fx.AddTrauma(0.04f);
                particles.Sparkle(grid.CellCenter(WordRunBoard.WordLength / 2, revealRow), 6,
                    GamePalette.Lighten(Accent, 0.3f), 90f * scale, 1.8f, 0.4f);
                break;
        }
    }

    private void FillHud(in GameContext context, ImDrawListPtr drawList, bool playing, float scale)
    {
        var hud = context.Hud;
        hud.Score(board.Score);
        hud.Combo(combo);
        hud.Best(context.Session.Best);
        if (solvesShown != board.WordsSolved)
        {
            solvesShown = board.WordsSolved;
            solvesValue = GameNumber.Label(board.WordsSolved);
        }

        var armed = confirmRemaining > 0f;
        var label = armed ? Loc.T(L.Games.EndRun) : solvesValue;
        var width = CapsulePadX * 2f + CapsuleIconSize + CapsuleIconGap + Typography.Measure(label, CapsuleStyle).X / scale;
        hud.Custom(width);
        var rect = hud.CustomRect;
        if (rect.Width <= 0f)
        {
            return;
        }

        DrawRunCapsule(drawList, rect, label, armed, context.Theme, scale);
        if (!playing)
        {
            return;
        }

        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!UiInteract.Click(rect.Min, rect.Max, true))
        {
            return;
        }

        if (!armed)
        {
            confirmRemaining = ConfirmSeconds;
            return;
        }

        board.EndRun();
        FinishRun(context);
    }

    private void DrawRunCapsule(ImDrawListPtr drawList, Rect rect, string label, bool armed, PhoneTheme theme, float scale)
    {
        var iconSize = CapsuleIconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        var centerY = rect.Center.Y;
        var textOrigin = new Vector2(left + iconSize + CapsuleIconGap * scale, centerY - Typography.LineHeight(CapsuleStyle) * 0.5f);
        if (!armed)
        {
            StageHud.Capsule(drawList, rect, scale);
            ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Flag, Accent,
                iconSize);
            Typography.Draw(drawList, textOrigin, label, theme.TextStrong, CapsuleStyle);
            return;
        }

        var pulse = 0.5f + 0.5f * Pulse.Wave(Pulse.Fast);
        var fill = Vector4.Lerp(Danger, GamePalette.Lighten(Danger, 0.2f), pulse);
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        Material.Sheen(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.3f)),
            1f * scale, 1f * scale);
        var ink = GamePalette.InkOn(fill);
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Stop, ink, iconSize);
        Typography.Draw(drawList, textOrigin, label, ink, CapsuleStyle);
    }

    private static string GainLabel(int points)
    {
        var index = Math.Clamp(points, 0, WordRunBoard.MaxPointsPerWord);
        return GainLabels[index] ??= string.Concat("+", GameNumber.Label(index));
    }

    private static Rect Grow(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }
}
