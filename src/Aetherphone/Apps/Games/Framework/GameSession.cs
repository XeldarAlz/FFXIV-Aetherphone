using Aetherphone.Core.Games;

namespace Aetherphone.Apps.Games.Framework;

internal enum StageFlow : byte
{
    Intro,
    Countdown,
    Playing,
    Paused,
    Result,
}

internal sealed class GameSession
{
    public const int CountdownSteps = 3;
    public const float CountdownStepSeconds = 0.6f;
    private const float CountdownTotalSeconds = CountdownStepSeconds * (CountdownSteps + 1);

    private readonly GameStatsStore stats;
    private readonly IScoreSink sink;
    private readonly IRankSource ranks;
    private float countdownElapsed;
    private int countdownStepSeen;

    public GameSession(GameStatsStore stats, IScoreSink sink, IRankSource ranks)
    {
        this.stats = stats;
        this.sink = sink;
        this.ranks = ranks;
        StatId = string.Empty;
    }

    public GameSpec Spec { get; private set; }

    public StageFlow State { get; private set; }

    public int Mode { get; private set; }

    public ulong Seed { get; private set; }

    public bool Daily { get; private set; }

    public string StatId { get; private set; }

    public int Best { get; private set; }

    public int Score { get; private set; }

    public bool Finished { get; private set; }

    public GameOutcome Outcome { get; private set; }

    public int ResultValue { get; private set; }

    public bool NewBest { get; private set; }

    public GameRank Rank { get; private set; }

    public float PlaySeconds { get; private set; }

    public int Runs { get; private set; }

    public bool CountdownStepChanged { get; private set; }

    public int Level { get; private set; }

    public int LevelStars { get; private set; } = GameOutcome.NoStars;

    public int Seats { get; private set; } = 1;

    public bool HandoffPending { get; private set; }

    public int HandoffSeat { get; private set; } = GameOutcome.NoSeat;

    public bool Unranked { get; private set; }

    public GameStatsStore Stats => stats;

    public ScoreKind Kind => Spec.KindFor(Mode);

    public string LeaderboardStatId => ScoreStatIds.LeaderboardId(StatId, Spec.Id, Kind);

    public GameStart Start => new(Mode, Seed, Daily, Level, Seats);

    public bool HotSeat => Seats > 1;

    public int LevelCount => Spec.LevelsFor(Mode) ? Spec.LevelCount : 0;

    public bool HasLevels => LevelCount > 0;

    public int TotalStars => HasLevels ? stats.TotalStars(Spec.Id) : 0;

    public bool CanAdvance => State == StageFlow.Result && HasLevels && LevelStars > 0 && Level < LevelCount;

    public bool BeatingBest =>
        (Kind == ScoreKind.Score || (Kind == ScoreKind.Level && !HasLevels)) && Score > 0 && Score > Best;

    public int CountdownStep
    {
        get
        {
            var step = CountdownSteps - (int)(countdownElapsed / CountdownStepSeconds);
            return Math.Clamp(step, 0, CountdownSteps);
        }
    }

    public float CountdownStepProgress
    {
        get
        {
            var within = countdownElapsed % CountdownStepSeconds;
            return Math.Clamp(within / CountdownStepSeconds, 0f, 1f);
        }
    }

    public void Begin(in GameSpec spec, in GameStart start)
    {
        Spec = spec;
        Mode = spec.ClampMode(start.Mode);
        Seed = start.Seed;
        Daily = start.Daily;
        State = StageFlow.Intro;
        Score = 0;
        Finished = false;
        NewBest = false;
        ResultValue = 0;
        PlaySeconds = 0f;
        Runs = 0;
        Rank = GameRank.Unknown;
        StatId = spec.StatIdFor(Mode);
        LevelStars = GameOutcome.NoStars;
        Level = ResolveLevel(start.Level);
        Seats = Math.Clamp(start.Seats, 1, Math.Max(1, spec.Seats));
        ClearHandoff();
        Unranked = false;
        LoadBest();
        RefreshRank();
    }

    public void SelectSeats(int seats)
    {
        if (State != StageFlow.Intro)
        {
            return;
        }

        Seats = Math.Clamp(seats, 1, Math.Max(1, Spec.Seats));
    }

    public bool Handoff(int seat)
    {
        if (Finished || State is StageFlow.Intro or StageFlow.Result)
        {
            return false;
        }

        HandoffSeat = Math.Clamp(seat, 0, GameSeats.Max - 1);
        HandoffPending = true;
        return true;
    }

    public void CompleteHandoff()
    {
        HandoffPending = false;
    }

    public void SelectMode(int mode)
    {
        if (State != StageFlow.Intro)
        {
            return;
        }

        var clamped = Spec.ClampMode(mode);
        if (clamped == Mode)
        {
            return;
        }

        Mode = clamped;
        stats.SetLastMode(Spec.Id, clamped);
        StatId = Spec.StatIdFor(Mode);
        Level = ResolveLevel(Level);
        LoadBest();
        RefreshRank();
    }

    public bool SelectLevel(int level)
    {
        if (State is not (StageFlow.Intro or StageFlow.Result) || !HasLevels || level < 1 || level > LevelCount ||
            !stats.IsUnlocked(Spec.Id, level))
        {
            return false;
        }

        Level = level;
        return true;
    }

    public bool AdvanceLevel()
    {
        if (!CanAdvance)
        {
            return false;
        }

        Level++;
        return true;
    }

    public void Play()
    {
        if (State is StageFlow.Playing or StageFlow.Countdown)
        {
            return;
        }

        if (Runs > 0 && !Daily)
        {
            Seed = GameSeed.Fresh();
        }

        Runs++;
        Score = 0;
        Finished = false;
        NewBest = false;
        LevelStars = GameOutcome.NoStars;
        Unranked = false;
        ClearHandoff();
        ResultValue = 0;
        PlaySeconds = 0f;
        countdownElapsed = 0f;
        countdownStepSeen = CountdownSteps;
        CountdownStepChanged = true;
        StatId = Spec.StatIdFor(Mode);
        LoadBest();
        State = Spec.CountdownFor(Mode) ? StageFlow.Countdown : StageFlow.Playing;
    }

    public void Tick(float deltaSeconds)
    {
        CountdownStepChanged = false;
        if (deltaSeconds <= 0f || HandoffPending)
        {
            return;
        }

        switch (State)
        {
            case StageFlow.Countdown:
                countdownElapsed += deltaSeconds;
                var step = CountdownStep;
                if (step != countdownStepSeen)
                {
                    countdownStepSeen = step;
                    CountdownStepChanged = true;
                }

                if (countdownElapsed >= CountdownTotalSeconds)
                {
                    State = StageFlow.Playing;
                }

                return;
            case StageFlow.Playing:
                PlaySeconds += deltaSeconds;
                return;
            default:
                return;
        }
    }

    public void Pause()
    {
        if (State != StageFlow.Playing)
        {
            return;
        }

        State = StageFlow.Paused;
    }

    public void Resume()
    {
        if (State != StageFlow.Paused)
        {
            return;
        }

        State = StageFlow.Playing;
    }

    public void Report(int score)
    {
        if (State is StageFlow.Result || Finished)
        {
            return;
        }

        Score = score;
    }

    public void Finish(in GameOutcome outcome)
    {
        if (Finished || State is StageFlow.Intro or StageFlow.Result)
        {
            return;
        }

        Finished = true;
        Outcome = outcome;
        HandoffPending = false;
        var statId = outcome.StatId.Length > 0 ? outcome.StatId : StatId;
        var value = outcome.Value;
        if (outcome.IsUnranked || HotSeat)
        {
            FinishUnranked(statId, value);
            return;
        }

        var submits = true;
        if (outcome.HasStars && HasLevels && Level > 0)
        {
            LevelStars = outcome.Stars;
            stats.SetStars(Spec.Id, Level, LevelStars);
            if (outcome.Kind == ScoreKind.Level)
            {
                value = stats.TotalStars(Spec.Id);
            }
        }

        switch (outcome.Kind)
        {
            case ScoreKind.Time when !outcome.Won:
                stats.CompleteDaily(statId);
                NewBest = false;
                submits = false;
                break;
            case ScoreKind.Time:
                NewBest = stats.SubmitTime(statId, value);
                break;
            case ScoreKind.Streak when outcome.IsDraw:
                stats.CompleteDaily(statId);
                value = stats.Get(statId).Streak;
                NewBest = false;
                submits = false;
                break;
            case ScoreKind.Streak:
                if (outcome.Won)
                {
                    value = stats.RecordWin(statId);
                }
                else
                {
                    stats.ResetStreak(statId);
                    value = 0;
                }

                NewBest = false;
                break;
            default:
                NewBest = stats.SubmitScore(statId, value);
                break;
        }

        if (outcome.HasSecondary)
        {
            if (outcome.SecondaryKind == ScoreKind.Time)
            {
                stats.SubmitTime(outcome.SecondaryStatId, outcome.SecondaryValue);
            }
            else
            {
                stats.SubmitScore(outcome.SecondaryStatId, outcome.SecondaryValue);
            }

            sink.Submit(new ScoreSubmission(outcome.SecondaryStatId, outcome.SecondaryValue, outcome.SecondaryKind,
                Seed, Daily, Spec.Id));
        }

        ResultValue = value;
        Score = outcome.Kind is ScoreKind.Score or ScoreKind.Level ? value : Score;
        StatId = statId;
        LoadBest();
        if (submits)
        {
            sink.Submit(new ScoreSubmission(statId, value, outcome.Kind, Seed, Daily, Spec.Id));
        }

        State = StageFlow.Result;
        RefreshRank();
    }

    public void RefreshRank()
    {
        var leaderboardId = LeaderboardStatId;
        Rank = leaderboardId.Length > 0 && ranks.TryGetRank(leaderboardId, out var rank) ? rank : GameRank.Unknown;
    }

    private void FinishUnranked(string statId, int value)
    {
        Unranked = true;
        NewBest = false;
        stats.CompleteDaily(statId);
        ResultValue = value;
        StatId = statId;
        LoadBest();
        State = StageFlow.Result;
        RefreshRank();
    }

    private void ClearHandoff()
    {
        HandoffPending = false;
        HandoffSeat = GameOutcome.NoSeat;
    }

    private int ResolveLevel(int requested)
    {
        var count = LevelCount;
        if (count == 0)
        {
            return 0;
        }

        return requested >= 1 && requested <= count && stats.IsUnlocked(Spec.Id, requested)
            ? requested
            : stats.NextLevel(Spec.Id, count);
    }

    private void LoadBest()
    {
        var record = stats.Get(StatId);
        Best = Kind switch
        {
            ScoreKind.Time => record.BestTimeSeconds,
            ScoreKind.Streak => record.Streak,
            _ => record.BestScore,
        };
    }
}
