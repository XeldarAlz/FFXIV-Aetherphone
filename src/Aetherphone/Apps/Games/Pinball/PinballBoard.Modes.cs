namespace Aetherphone.Apps.Games.Pinball;

internal sealed partial class PinballBoard
{
    public const float ComboWindowSeconds = 4f;
    public const int CombosPerMultiplier = 3;
    public const int ComboPoints = 10000;
    public const int MaxPlayfieldMultiplier = 5;
    public const float PlayfieldSeconds = 30f;
    public const float FeverSeconds = 20f;
    public const int FeverFactor = 2;
    public const float FeverChargeFalloff = 0.5f;
    public const float BumperCharge = 0.012f;
    public const float SlingCharge = 0.005f;
    public const float SpinnerCharge = 0.003f;
    public const float SwitchCharge = 0.02f;
    public const float ShotCharge = 0.07f;
    public const float ComboCharge = 0.04f;
    public const int OrbitPoints = 20000;
    public const int BonusPerOrbit = 2000;
    public const float OrbitWindowSeconds = 2.5f;
    public const int KickbackPoints = 5000;
    public const float KickbackSpeed = 12.5f;
    public const float KickbackTurnY = 7.9f;
    public const int MysteryBumpers = 20;
    public const int MysteryPoints = 75000;
    public const float MysteryBallSaveSeconds = 10f;
    public const float MysteryFeverCharge = 0.4f;
    public const int SuperJackpotFactor = 3;
    public const float LightShowSeconds = 1.4f;
    private const int MysteryAwardCount = 5;
    private const int BothInlanes = 3;
    private static readonly Vector2 KickbackRedirect = new(5.2f, -8f);
    private static readonly Vector2 TableCenter = new(PinballTable.Width * 0.5f, PinballTable.Height * 0.5f);

    private float comboSeconds;
    private int mysteryHits;
    private float kickbackFlash;
    private float orbitFlash;

    public int Combo { get; private set; }

    public int BestCombo { get; private set; }

    public bool ComboRunning => comboSeconds > 0f;

    public int PlayfieldMultiplier { get; private set; } = 1;

    public float PlayfieldLeft { get; private set; }

    public float FeverCharge { get; private set; }

    public float FeverLeft { get; private set; }

    public int FeverTotal { get; private set; }

    public int Fevers { get; private set; }

    public int Orbits { get; private set; }

    public bool KickbackLit { get; private set; }

    public int InlanesLit { get; private set; }

    public bool MysteryLit { get; private set; }

    public float MysteryProgress => MysteryLit ? 1f : mysteryHits / (float)MysteryBumpers;

    public bool SuperJackpotLit { get; private set; }

    public float LightShow { get; private set; }

    public bool FeverActive => FeverLeft > 0f;

    public int ScoreFactor => PlayfieldMultiplier * (FeverActive ? FeverFactor : 1);

    public float KickbackFlash => kickbackFlash;

    public float OrbitFlash => orbitFlash;

    public bool InlaneLit(int rollover) => (InlanesLit & InlaneBit(rollover)) != 0;

    private static int InlaneBit(int rollover) =>
        rollover == PinballTable.LeftInlane ? 1 : rollover == PinballTable.RightInlane ? 2 : 0;

    private void ResetModes()
    {
        ClearBallModes();
        BestCombo = 0;
        FeverCharge = 0f;
        FeverTotal = 0;
        Fevers = 0;
        Orbits = 0;
        KickbackLit = true;
        MysteryLit = false;
        mysteryHits = 0;
        LightShow = 0f;
        kickbackFlash = 0f;
        orbitFlash = 0f;
    }

    private void ClearBallModes()
    {
        Combo = 0;
        comboSeconds = 0f;
        PlayfieldMultiplier = 1;
        PlayfieldLeft = 0f;
        FeverLeft = 0f;
        InlanesLit = 0;
        SuperJackpotLit = false;
    }

    private void AdvanceModes(float seconds)
    {
        kickbackFlash = MathF.Max(0f, kickbackFlash - seconds * 2f);
        orbitFlash = MathF.Max(0f, orbitFlash - seconds * 2f);
        LightShow = MathF.Max(0f, LightShow - seconds);
        if (comboSeconds > 0f)
        {
            comboSeconds -= seconds;
            if (comboSeconds <= 0f)
            {
                Combo = 0;
            }
        }

        if (PlayfieldLeft > 0f)
        {
            PlayfieldLeft -= seconds;
            if (PlayfieldLeft <= 0f)
            {
                PlayfieldLeft = 0f;
                PlayfieldMultiplier = 1;
            }
        }

        if (FeverLeft <= 0f)
        {
            return;
        }

        FeverLeft -= seconds;
        if (FeverLeft <= 0f)
        {
            FinishFever();
        }
    }

    private int AddScore(int points)
    {
        if (points <= 0 || Tilted)
        {
            return 0;
        }

        var awarded = (int)Math.Min(int.MaxValue, (long)points * ScoreFactor);
        Credit(awarded);
        if (FeverActive)
        {
            FeverTotal = (int)Math.Min(int.MaxValue, (long)FeverTotal + awarded);
        }

        return awarded;
    }

    private void Credit(int points)
    {
        if (points <= 0)
        {
            return;
        }

        Score = (int)Math.Min(int.MaxValue, (long)Score + points);
        if (Attract || ExtraBallAwarded || Score < ExtraBallScore)
        {
            return;
        }

        ExtraBallAwarded = true;
        ExtraBalls++;
        Emit(PinballEventKind.ExtraBall, PinballTable.LanePlunger, 0, 0);
    }

    private void MajorShot(Vector2 position)
    {
        Charge(ShotCharge);
        Combo = comboSeconds > 0f ? Combo + 1 : 1;
        comboSeconds = ComboWindowSeconds;
        BestCombo = Math.Max(BestCombo, Combo);
        if (Combo < 2)
        {
            return;
        }

        var awarded = AddScore(ComboPoints * (Combo - 1));
        Emit(PinballEventKind.Combo, position, awarded, Combo);
        Charge(ComboCharge);
        if (Combo % CombosPerMultiplier == 0)
        {
            RaisePlayfield(position);
        }
    }

    private void RaisePlayfield(Vector2 position)
    {
        PlayfieldLeft = PlayfieldSeconds;
        if (PlayfieldMultiplier >= MaxPlayfieldMultiplier)
        {
            return;
        }

        PlayfieldMultiplier++;
        LightShow = MathF.Max(LightShow, LightShowSeconds * 0.5f);
        Emit(PinballEventKind.PlayfieldRaised, position, PlayfieldMultiplier, 0);
    }

    private void Charge(float amount)
    {
        if (FeverActive || Tilted || amount <= 0f)
        {
            return;
        }

        FeverCharge += amount / (1f + Fevers * FeverChargeFalloff);
        if (FeverCharge < 1f)
        {
            return;
        }

        FeverCharge = 0f;
        FeverLeft = FeverSeconds;
        FeverTotal = 0;
        Fevers++;
        LightShow = LightShowSeconds;
        Emit(PinballEventKind.FeverStart, TableCenter, Fevers, 0);
    }

    private void FinishFever()
    {
        FeverLeft = 0f;
        Emit(PinballEventKind.FeverEnd, TableCenter, FeverTotal, Fevers);
    }

    private void PassOrbit(int slot, int side)
    {
        ref var ball = ref balls[slot];
        var tag = (byte)(side + 1);
        if (ball.Velocity.Y < 0f)
        {
            ball.OrbitSide = tag;
            ball.OrbitSeconds = OrbitWindowSeconds;
            return;
        }

        var completed = ball.OrbitSide != 0 && ball.OrbitSide != tag && ball.OrbitSeconds > 0f;
        ball.OrbitSide = 0;
        if (completed)
        {
            CompleteOrbit(PinballTable.OrbitSensors[side]);
        }
    }

    public void CompleteOrbit(Vector2 position)
    {
        orbitFlash = 1f;
        if (Tilted)
        {
            return;
        }

        DisarmSkill();
        Orbits++;
        Bonus += BonusPerOrbit;
        Emit(PinballEventKind.Orbit, position, AddScore(OrbitPoints), Orbits);
        MajorShot(position);
    }

    private void LightInlane(int rollover)
    {
        InlanesLit |= InlaneBit(rollover);
        if (InlanesLit != BothInlanes)
        {
            return;
        }

        InlanesLit = 0;
        if (KickbackLit)
        {
            return;
        }

        KickbackLit = true;
        Emit(PinballEventKind.KickbackLit, PinballTable.Rollovers[PinballTable.LeftOutlane], 0, 0);
    }

    private void FireKickback(int slot)
    {
        ref var ball = ref balls[slot];
        KickbackLit = false;
        kickbackFlash = 1f;
        ball.Kicked = true;
        ball.Velocity = new Vector2(0f, -KickbackSpeed);
        world.SetVelocity(ball.Body, ball.Velocity);
        Emit(PinballEventKind.Kickback, PinballTable.Rollovers[PinballTable.LeftOutlane], AddScore(KickbackPoints),
            slot);
    }

    private void GuideKick(ref PinballBall ball)
    {
        if (!ball.Kicked)
        {
            return;
        }

        if (ball.Velocity.Y >= 0f)
        {
            ball.Kicked = false;
            return;
        }

        if (ball.Position.Y > KickbackTurnY)
        {
            return;
        }

        ball.Kicked = false;
        ball.Velocity = KickbackRedirect;
        world.SetVelocity(ball.Body, ball.Velocity);
    }

    private void CountMystery()
    {
        if (MysteryLit)
        {
            return;
        }

        mysteryHits++;
        if (mysteryHits < MysteryBumpers)
        {
            return;
        }

        mysteryHits = 0;
        MysteryLit = true;
        Emit(PinballEventKind.MysteryLit, PinballTable.Saucer, 0, 0);
    }

    private void AwardMystery()
    {
        MysteryLit = false;
        var award = (MysteryAward)random.Next(MysteryAwardCount);
        if ((award == MysteryAward.Kickback && KickbackLit) || (award == MysteryAward.Fever && FeverActive))
        {
            award = MysteryAward.BigPoints;
        }

        var value = 0;
        switch (award)
        {
            case MysteryAward.BigPoints:
                value = AddScore(MysteryPoints);
                break;
            case MysteryAward.Kickback:
                KickbackLit = true;
                break;
            case MysteryAward.Playfield:
                RaisePlayfield(PinballTable.Saucer);
                value = PlayfieldMultiplier;
                break;
            case MysteryAward.Fever:
                Charge(MysteryFeverCharge * (1f + Fevers * FeverChargeFalloff));
                break;
            case MysteryAward.BallSave:
                BallSaveLeft = MathF.Max(BallSaveLeft, MysteryBallSaveSeconds);
                break;
        }

        Emit(PinballEventKind.Mystery, PinballTable.Saucer, value, (int)award);
    }

    private void CollectSuperJackpot()
    {
        SuperJackpotLit = false;
        JackpotLit = true;
        Jackpots++;
        LightShow = LightShowSeconds;
        Emit(PinballEventKind.SuperJackpot, PinballTable.Saucer, AddScore(JackpotValue * SuperJackpotFactor),
            Jackpots);
    }
}
