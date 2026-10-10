namespace Aetherphone.Core.Notifications;

internal enum UiSound
{
    Sleep,
    AppOpen,
    AppClose,
    Shutter,
    MessageSent,
    Success,
    Payout,
    Caution,
    Blocked,
    Tap,
    ToggleOn,
    ToggleOff,
    Keystroke,
    CallConnect,
    CallEnd,
    RecordStart,
    RecordCancel,
    GameWin,
    Refresh,
    GameHitSoft,
    GameHitWood,
    GameBreak,
    GameExplosion,
    GamePop,
    GameCollect,
    GameMatch,
    GameClear,
    GamePowerUp,
    GameShoot,
    GameJump,
    GameCardPlace,
    GameCardFlip,
    GameShuffle,
    GamePiece,
    GameTick,
    GameWrong,
    SimonTone1,
    SimonTone2,
    SimonTone3,
    SimonTone4,
    IslandExpand,
    IslandCollapse,
    SheetPresent,
    SheetDismiss,
    KeystrokeDelete,
    KeystrokeSpace,
    MessageReceived,
    CasinoChips,
    CasinoDeal,
    HalloweenKnock,
    HalloweenThump,
    HalloweenChime,
    HalloweenClaw,
    HalloweenRustle,
    HalloweenRise,
    HalloweenHoot,
    HalloweenChorus,
    HalloweenHeartbeat,
    HalloweenCoffin,
    HalloweenFlutter,
    HalloweenIgnite,
    HalloweenOrgan,
    HalloweenSwarm,
    HalloweenSparkle,
    HalloweenWhisper,
    HalloweenFlare,
    HalloweenCrystal,
}

internal enum UiSoundChannel
{
    Event,
    Transition,
    Tap,
    Toggle,
    Keyboard,
    Game,
}

internal readonly struct UiSoundEntry
{
    public readonly string[] Files;
    public readonly float Gain;
    public readonly int MinimumIntervalMilliseconds;
    public readonly UiSoundChannel Channel;
    public readonly float PitchVariance;

    public UiSoundEntry(string[] files, float gain, int minimumIntervalMilliseconds, UiSoundChannel channel,
        float pitchVariance = 0f)
    {
        Files = files;
        Gain = gain;
        MinimumIntervalMilliseconds = minimumIntervalMilliseconds;
        Channel = channel;
        PitchVariance = pitchVariance;
    }
}

internal static class UiSoundCatalog
{
    private const float SubtleVariance = 0.03f;

    private static readonly string[] Lock = { "Ui/lock.wav" };
    private static readonly string[] AppOpen = { "Ui/app_open.wav" };
    private static readonly string[] AppClose = { "Ui/app_close.wav" };
    private static readonly string[] Shutter = { "Ui/shutter.wav" };
    private static readonly string[] Send = { "Ui/send.wav" };
    private static readonly string[] Receive = { "Ui/receive.wav" };
    private static readonly string[] Success = { "Ui/success.wav" };
    private static readonly string[] Coin = { "Ui/coin.wav" };
    private static readonly string[] Caution = { "Ui/caution.wav" };
    private static readonly string[] Blocked = { "Ui/blocked.wav" };
    private static readonly string[] ToggleOn = { "Ui/toggle_on.wav" };
    private static readonly string[] ToggleOff = { "Ui/toggle_off.wav" };
    private static readonly string[] CallConnect = { "Ui/call_connect.wav" };
    private static readonly string[] CallEnd = { "Ui/call_end.wav" };
    private static readonly string[] RecordStart = { "Ui/record_start.wav" };
    private static readonly string[] RecordCancel = { "Ui/record_cancel.wav" };
    private static readonly string[] Win = { "Ui/win.wav" };
    private static readonly string[] Refresh = { "Ui/refresh.wav" };
    private static readonly string[] IslandExpand = { "Ui/island_expand.wav" };
    private static readonly string[] IslandCollapse = { "Ui/island_collapse.wav" };
    private static readonly string[] SheetPresent = { "Ui/sheet_present.wav" };
    private static readonly string[] SheetDismiss = { "Ui/sheet_dismiss.wav" };
    private static readonly string[] KeyDelete = { "Ui/type_delete.wav" };
    private static readonly string[] KeySpace = { "Ui/type_space.wav" };

    private static readonly string[] Taps =
    {
        "Ui/tap_1.wav", "Ui/tap_2.wav", "Ui/tap_3.wav", "Ui/tap_4.wav", "Ui/tap_5.wav",
    };

    private static readonly string[] Keystrokes =
    {
        "Ui/type_1.wav", "Ui/type_2.wav", "Ui/type_3.wav", "Ui/type_4.wav", "Ui/type_5.wav",
    };

    private static readonly string[] HitSoft =
    {
        "Games/hit_soft_1.wav", "Games/hit_soft_2.wav", "Games/hit_soft_3.wav",
    };

    private static readonly string[] HitWood =
    {
        "Games/hit_wood_1.wav", "Games/hit_wood_2.wav", "Games/hit_wood_3.wav",
    };

    private static readonly string[] Break =
    {
        "Games/break_1.wav", "Games/break_2.wav", "Games/break_3.wav",
    };

    private static readonly string[] Explosion = { "Games/explosion_1.wav", "Games/explosion_2.wav" };

    private static readonly string[] Pop =
    {
        "Games/pop_1.wav", "Games/pop_2.wav", "Games/pop_3.wav",
    };

    private static readonly string[] Collect = { "Games/collect_1.wav", "Games/collect_2.wav" };
    private static readonly string[] Match = { "Games/match_1.wav", "Games/match_2.wav" };
    private static readonly string[] Clear = { "Games/clear_1.wav", "Games/clear_2.wav" };
    private static readonly string[] PowerUp = { "Games/powerup_1.wav", "Games/powerup_2.wav" };
    private static readonly string[] Shoot = { "Games/shoot_1.wav", "Games/shoot_2.wav" };

    private static readonly string[] Jump =
    {
        "Games/jump_1.wav", "Games/jump_2.wav", "Games/jump_3.wav",
    };

    private static readonly string[] CardPlace =
    {
        "Games/card_place_1.wav", "Games/card_place_2.wav", "Games/card_place_3.wav",
    };

    private static readonly string[] CardFlip =
    {
        "Games/card_flip_1.wav", "Games/card_flip_2.wav", "Games/card_flip_3.wav",
    };

    private static readonly string[] Shuffle = { "Games/shuffle.wav" };

    private static readonly string[] Piece =
    {
        "Games/piece_1.wav", "Games/piece_2.wav", "Games/piece_3.wav",
    };

    private static readonly string[] Tick = { "Games/tick_1.wav", "Games/tick_2.wav" };
    private static readonly string[] Wrong = { "Games/wrong_1.wav", "Games/wrong_2.wav" };
    private static readonly string[] Simon1 = { "Games/simon_1.wav" };
    private static readonly string[] Simon2 = { "Games/simon_2.wav" };
    private static readonly string[] Simon3 = { "Games/simon_3.wav" };
    private static readonly string[] Simon4 = { "Games/simon_4.wav" };

    private static readonly string[] Chips =
    {
        "Games/chips_1.wav", "Games/chips_2.wav", "Games/chips_3.wav",
    };

    private static readonly string[] Deal = { "Games/deal_1.wav", "Games/deal_2.wav" };

    private static readonly string[] HalloweenKnock =
    {
        "Ui/halloween_knock_1.wav", "Ui/halloween_knock_2.wav", "Ui/halloween_knock_3.wav",
    };

    private static readonly string[] HalloweenThump =
    {
        "Ui/halloween_thump_1.wav", "Ui/halloween_thump_2.wav", "Ui/halloween_thump_3.wav",
    };

    private static readonly string[] HalloweenChime =
    {
        "Ui/halloween_chime_1.wav", "Ui/halloween_chime_2.wav", "Ui/halloween_chime_3.wav",
        "Ui/halloween_chime_4.wav",
    };

    private static readonly string[] HalloweenClaw = { "Ui/halloween_claw.wav" };
    private static readonly string[] HalloweenRustle = { "Ui/halloween_rustle.wav" };
    private static readonly string[] HalloweenRise = { "Ui/halloween_rise.wav" };
    private static readonly string[] HalloweenHoot = { "Ui/halloween_hoot.wav" };
    private static readonly string[] HalloweenChorus = { "Ui/halloween_chorus.wav" };
    private static readonly string[] HalloweenHeartbeat = { "Ui/halloween_heartbeat.wav" };
    private static readonly string[] HalloweenCoffin = { "Ui/halloween_coffin.wav" };
    private static readonly string[] HalloweenFlutter = { "Ui/halloween_flutter.wav" };
    private static readonly string[] HalloweenIgnite = { "Ui/halloween_ignite.wav" };
    private static readonly string[] HalloweenOrgan = { "Ui/halloween_organ.wav" };
    private static readonly string[] HalloweenSwarm = { "Ui/halloween_swarm.wav" };
    private static readonly string[] HalloweenSparkle = { "Ui/halloween_sparkle.wav" };
    private static readonly string[] HalloweenWhisper = { "Ui/halloween_whisper.wav" };
    private static readonly string[] HalloweenFlare = { "Ui/halloween_flare.wav" };
    private static readonly string[] HalloweenCrystal = { "Ui/halloween_crystal.wav" };

    public static readonly UiSoundEntry[] Entries =
    {
        new(Lock, 0.8f, 120, UiSoundChannel.Event),
        new(AppOpen, 0.7f, 90, UiSoundChannel.Transition),
        new(AppClose, 0.7f, 90, UiSoundChannel.Transition),
        new(Shutter, 0.9f, 150, UiSoundChannel.Event),
        new(Send, 0.75f, 60, UiSoundChannel.Event),
        new(Success, 0.7f, 400, UiSoundChannel.Event),
        new(Coin, 0.65f, 120, UiSoundChannel.Event),
        new(Caution, 0.7f, 200, UiSoundChannel.Event),
        new(Blocked, 0.7f, 200, UiSoundChannel.Event),
        new(Taps, 0.55f, 35, UiSoundChannel.Tap, SubtleVariance),
        new(ToggleOn, 0.6f, 40, UiSoundChannel.Toggle),
        new(ToggleOff, 0.6f, 40, UiSoundChannel.Toggle),
        new(Keystrokes, 0.6f, 25, UiSoundChannel.Keyboard, SubtleVariance),
        new(CallConnect, 0.75f, 400, UiSoundChannel.Event),
        new(CallEnd, 0.75f, 400, UiSoundChannel.Event),
        new(RecordStart, 0.7f, 150, UiSoundChannel.Event),
        new(RecordCancel, 0.7f, 150, UiSoundChannel.Event),
        new(Win, 0.7f, 800, UiSoundChannel.Event),
        new(Refresh, 0.5f, 250, UiSoundChannel.Event),
        new(HitSoft, 0.65f, 40, UiSoundChannel.Game, SubtleVariance),
        new(HitWood, 0.6f, 40, UiSoundChannel.Game, SubtleVariance),
        new(Break, 0.6f, 45, UiSoundChannel.Game, SubtleVariance),
        new(Explosion, 0.65f, 90, UiSoundChannel.Game),
        new(Pop, 0.6f, 35, UiSoundChannel.Game, SubtleVariance),
        new(Collect, 0.55f, 50, UiSoundChannel.Game, SubtleVariance),
        new(Match, 0.6f, 60, UiSoundChannel.Game),
        new(Clear, 0.65f, 150, UiSoundChannel.Game),
        new(PowerUp, 0.6f, 200, UiSoundChannel.Game),
        new(Shoot, 0.5f, 60, UiSoundChannel.Game, SubtleVariance),
        new(Jump, 0.55f, 60, UiSoundChannel.Game, SubtleVariance),
        new(CardPlace, 0.6f, 50, UiSoundChannel.Game, SubtleVariance),
        new(CardFlip, 0.6f, 50, UiSoundChannel.Game, SubtleVariance),
        new(Shuffle, 0.6f, 300, UiSoundChannel.Game),
        new(Piece, 0.6f, 40, UiSoundChannel.Game, SubtleVariance),
        new(Tick, 0.5f, 35, UiSoundChannel.Game, SubtleVariance),
        new(Wrong, 0.6f, 150, UiSoundChannel.Game),
        new(Simon1, 0.65f, 1, UiSoundChannel.Game),
        new(Simon2, 0.65f, 1, UiSoundChannel.Game),
        new(Simon3, 0.65f, 1, UiSoundChannel.Game),
        new(Simon4, 0.65f, 1, UiSoundChannel.Game),
        new(IslandExpand, 0.45f, 90, UiSoundChannel.Transition),
        new(IslandCollapse, 0.45f, 90, UiSoundChannel.Transition),
        new(SheetPresent, 0.5f, 90, UiSoundChannel.Transition),
        new(SheetDismiss, 0.5f, 90, UiSoundChannel.Transition),
        new(KeyDelete, 0.6f, 25, UiSoundChannel.Keyboard, SubtleVariance),
        new(KeySpace, 0.6f, 25, UiSoundChannel.Keyboard, SubtleVariance),
        new(Receive, 0.7f, 120, UiSoundChannel.Event),
        new(Chips, 0.6f, 40, UiSoundChannel.Game, SubtleVariance),
        new(Deal, 0.6f, 50, UiSoundChannel.Game, SubtleVariance),
        new(HalloweenKnock, 0.55f, 35, UiSoundChannel.Tap, SubtleVariance),
        new(HalloweenThump, 0.55f, 35, UiSoundChannel.Tap, SubtleVariance),
        new(HalloweenChime, 0.5f, 60, UiSoundChannel.Tap),
        new(HalloweenClaw, 0.6f, 80, UiSoundChannel.Tap),
        new(HalloweenRustle, 0.6f, 80, UiSoundChannel.Tap),
        new(HalloweenRise, 0.5f, 300, UiSoundChannel.Tap),
        new(HalloweenHoot, 0.5f, 600, UiSoundChannel.Event),
        new(HalloweenChorus, 0.6f, 2500, UiSoundChannel.Event),
        new(HalloweenHeartbeat, 0.6f, 80, UiSoundChannel.Tap),
        new(HalloweenCoffin, 0.6f, 80, UiSoundChannel.Tap),
        new(HalloweenFlutter, 0.6f, 80, UiSoundChannel.Tap),
        new(HalloweenIgnite, 0.6f, 80, UiSoundChannel.Tap),
        new(HalloweenOrgan, 0.5f, 600, UiSoundChannel.Event),
        new(HalloweenSwarm, 0.6f, 2500, UiSoundChannel.Event),
        new(HalloweenSparkle, 0.5f, 80, UiSoundChannel.Tap, SubtleVariance),
        new(HalloweenWhisper, 0.55f, 120, UiSoundChannel.Event),
        new(HalloweenFlare, 0.6f, 2500, UiSoundChannel.Event),
        new(HalloweenCrystal, 0.5f, 600, UiSoundChannel.Event),
    };

    public static IReadOnlyList<string> Files()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var entryIndex = 0; entryIndex < Entries.Length; entryIndex++)
        {
            var files = Entries[entryIndex].Files;
            for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                names.Add(files[fileIndex]);
            }
        }

        var built = new string[names.Count];
        names.CopyTo(built);
        return built;
    }
}
