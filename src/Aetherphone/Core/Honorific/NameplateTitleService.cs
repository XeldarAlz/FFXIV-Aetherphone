using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Honorific;

internal sealed class NameplateTitleService : IDisposable
{
    public const string ChirperAppId = "chirper";
    public const string AethergramAppId = "aethergram";
    private const float EvaluateSeconds = 1f;
    private const float VerifySeconds = 5f;
    private const float ProbeSeconds = 10f;
    private const string VelvetAppId = "velvet";
    private const string SampleCode = "4KX9QP";
    private const string SampleJamName = "Lofi Night";
    private const string SampleStation = "Gridania FM";
    private const string SampleHandle = "yourname";
    private const string SampleSong = "Tomorrow and Tomorrow";
    private const string SampleArtist = "Susan Calloway";
    private const string SampleGame = "Coil";
    private const long SampleChips = 1250;
    private const int MaxChildren = 4;
    private static readonly Vector4 CustomInk = new(1f, 1f, 1f, 1f);

    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly AethernetSession session;
    private readonly PlaybackHub playback;
    private readonly PcMediaSource pcMedia;
    private readonly JamSession jam;
    private readonly RadioRoomRouter radioRooms;
    private readonly MusterStore musters;
    private readonly CallHub calls;
    private readonly HonorificBridge bridge;
    private readonly NameplateTitleSettings signedOutSettings = new();
    private WatchAlongSession? watchAlong;
    private Func<IPhoneApp?> foregroundApp = static () => null;
    private IReadOnlyList<IPhoneApp> apps = Array.Empty<IPhoneApp>();
    private NameplateTitleSettings settings;
    private ulong settingsContentId;
    private float evaluateTimer = EvaluateSeconds;
    private float verifyTimer;
    private float probeTimer = ProbeSeconds;
    private bool reapplyRequested;
    private bool applied;
    private string appliedJson = string.Empty;
    private string appliedText = string.Empty;
    private TitleLook? ownLook;
    private string trackSong = string.Empty;
    private string trackArtist = string.Empty;
    private double trackSeconds;

    public NameplateTitleService(Configuration configuration, IFramework framework, IClientState clientState,
        IObjectTable objectTable, AethernetSession session, PlaybackHub playback, PcMediaSource pcMedia,
        JamSession jam, RadioRoomRouter radioRooms, MusterStore musters, CallHub calls, HonorificBridge bridge)
    {
        this.configuration = configuration;
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.session = session;
        this.playback = playback;
        this.pcMedia = pcMedia;
        this.jam = jam;
        this.radioRooms = radioRooms;
        this.musters = musters;
        this.calls = calls;
        this.bridge = bridge;
        signedOutSettings.Normalize();
        settings = signedOutSettings;
        framework.Update += OnUpdate;
        clientState.TerritoryChanged += OnTerritoryChanged;
        clientState.Logout += OnLogout;
    }

    public bool Available { get; private set; }
    public NameplateTitle Current { get; private set; } = NameplateTitle.None;
    public NameplateTitle Preview { get; private set; } = NameplateTitle.None;
    public string CharacterName { get; private set; } = string.Empty;
    public NameplateTitleSettings Settings => settings;
    public bool HasOwnLook => ownLook.HasValue;
    public long TurnTick => Environment.TickCount64 / (settings.TurnSeconds * 1000L);

    public void Bind(WatchAlongSession watchAlongSession, Func<IPhoneApp?> foreground, IReadOnlyList<IPhoneApp> phoneApps)
    {
        watchAlong = watchAlongSession;
        foregroundApp = foreground;
        apps = phoneApps;
    }

    public void Refresh() => evaluateTimer = EvaluateSeconds;

    public void Commit()
    {
        if (settingsContentId != 0)
        {
            configuration.NameplateTitleByCharacter[settingsContentId] = settings;
        }

        configuration.Save();
        Refresh();
        if (applied && settings.Style == NameplateTitleStyle.MatchMine && ownLook is null)
        {
            bridge.TryClearLocalTitle();
            applied = false;
            appliedJson = string.Empty;
            appliedText = string.Empty;
        }
    }

    public static string DefaultTemplate(NameplateStatus status)
    {
        ref readonly var info = ref NameplateStatusCatalog.For(status);
        return info.DefaultTemplate is { } template ? Loc.T(template) : string.Empty;
    }

    public string TemplateFor(NameplateStatus status)
    {
        var custom = settings.Template(status);
        return custom.Length > 0 || status == NameplateStatus.Custom ? custom : DefaultTemplate(status);
    }

    public NameplateTitle Sample(NameplateStatus status)
    {
        var handle = ResolveHandle(HandleAppId(status));
        var values = new NameplateValues(PartyCode.Display(SampleCode), SampleJamName, SampleStation,
            Loc.T(MusterCategories.Label(MusterCategories.Roleplay)), handle.Length > 0 ? handle : SampleHandle,
            SampleSong, SampleArtist, SampleGame, ChipCount(SampleChips));
        return status == NameplateStatus.NowPlaying
            ? ComposeNowPlaying(values, Environment.TickCount64 / 1000.0)
            : Compose(status, values);
    }

    private NameplateTitle Example()
    {
        var order = settings.Order;
        for (var index = 0; index < order.Length; index++)
        {
            if (settings.Shows(order[index]))
            {
                var sample = Sample(order[index]);
                if (!sample.IsNone)
                {
                    return sample;
                }
            }
        }

        return Sample(NameplateStatus.Jam);
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        clientState.Logout -= OnLogout;
        if (applied)
        {
            bridge.TryClearLocalTitle();
            applied = false;
        }

        bridge.Dispose();
    }

    private void OnTerritoryChanged(uint territory) => reapplyRequested = true;

    private void OnLogout(int type, int code)
    {
        applied = false;
        appliedJson = string.Empty;
        appliedText = string.Empty;
        ownLook = null;
    }

    private void OnUpdate(IFramework updatingFramework)
    {
        var delta = (float)updatingFramework.UpdateDelta.TotalSeconds;
        trackSeconds += delta;
        if (bridge.ConsumeDisposing())
        {
            Available = false;
            applied = false;
        }

        if (bridge.ConsumeReady())
        {
            probeTimer = ProbeSeconds;
            reapplyRequested = true;
        }

        probeTimer += delta;
        if (probeTimer >= ProbeSeconds)
        {
            probeTimer = 0f;
            Available = bridge.Probe();
        }

        evaluateTimer += delta;
        if (evaluateTimer < EvaluateSeconds && !reapplyRequested)
        {
            return;
        }

        evaluateTimer = 0f;
        ResolveSettings();
        var player = objectTable.LocalPlayer;
        if (player is null)
        {
            Current = NameplateTitle.None;
            Preview = Example();
            return;
        }

        if (CharacterName.Length == 0 || !player.Name.TextValue.Equals(CharacterName, StringComparison.Ordinal))
        {
            CharacterName = player.Name.TextValue;
        }

        if (Available && !applied)
        {
            CaptureOwnLook();
        }

        Current = settings.Enabled ? Resolve() : NameplateTitle.None;
        Preview = Current.IsNone ? Example() : Current;
        if (!Available)
        {
            return;
        }

        verifyTimer += EvaluateSeconds;
        Apply(Current);
    }

    private void ResolveSettings()
    {
        var contentId = session.PlayingContentId;
        if (contentId == settingsContentId)
        {
            return;
        }

        settingsContentId = contentId;
        if (contentId == 0)
        {
            settings = signedOutSettings;
            return;
        }

        if (!configuration.NameplateTitleByCharacter.TryGetValue(contentId, out var stored))
        {
            stored = new NameplateTitleSettings();
            configuration.NameplateTitleByCharacter[contentId] = stored;
        }

        stored.Normalize();
        settings = stored;
        ownLook = null;
    }

    private void Apply(in NameplateTitle title)
    {
        if (title.IsNone)
        {
            if (applied)
            {
                bridge.TryClearLocalTitle();
                applied = false;
                appliedJson = string.Empty;
                appliedText = string.Empty;
            }

            reapplyRequested = false;
            return;
        }

        var json = NameplateTitleText.ToJson(title);
        var changed = !string.Equals(json, appliedJson, StringComparison.Ordinal);
        if (!changed && !reapplyRequested && (!NeedsVerify() || StillShowing()))
        {
            return;
        }

        if (bridge.TrySetLocalTitle(json))
        {
            applied = true;
            appliedJson = json;
            appliedText = title.Text;
        }

        reapplyRequested = false;
    }

    private bool NeedsVerify()
    {
        if (verifyTimer < VerifySeconds)
        {
            return false;
        }

        verifyTimer = 0f;
        return true;
    }

    private bool StillShowing() =>
        bridge.TryGetLocalTitle(out var json) && NameplateTitleText.TryReadTitle(json, out var shown, out _) &&
        string.Equals(shown, appliedText, StringComparison.Ordinal);

    private void CaptureOwnLook()
    {
        if (applied || settings.Style != NameplateTitleStyle.MatchMine)
        {
            return;
        }

        if (!bridge.TryGetLocalTitle(out var json))
        {
            return;
        }

        ownLook = NameplateTitleText.TryReadTitle(json, out _, out var look) ? look : null;
    }

    private NameplateTitle Resolve()
    {
        var order = settings.Order;
        for (var index = 0; index < order.Length; index++)
        {
            var title = ResolveRanked(order[index]);
            if (!title.IsNone)
            {
                return title;
            }
        }

        return NameplateTitle.None;
    }

    private NameplateTitle ResolveRanked(NameplateStatus status)
    {
        if (!settings.Shows(status))
        {
            return NameplateTitle.None;
        }

        Span<NameplateStatus> children = stackalloc NameplateStatus[MaxChildren];
        var count = NameplateStatusCatalog.ChildrenOf(status, children);
        for (var index = 0; index < count; index++)
        {
            if (!settings.Shows(children[index]))
            {
                continue;
            }

            var title = ResolveStatus(children[index]);
            if (!title.IsNone)
            {
                return title;
            }
        }

        return ResolveStatus(status);
    }

    private NameplateTitle ResolveStatus(NameplateStatus status)
    {
        switch (status)
        {
            case NameplateStatus.MogCast when watchAlong is { IsHosting: true, IsPartyOpen: true } party &&
                                              party.RoomCode.Length > 0:
                return Compose(status, NameplateValues.Empty with { Code = PartyCode.Display(party.RoomCode) });
            case NameplateStatus.Jam when jam.IsHost && jam.DisplayCode.Length > 0:
                return Compose(status, NameplateValues.Empty with { Code = jam.DisplayCode, Name = jam.Title });
            case NameplateStatus.RadioOnAir when radioRooms.Room is { IsDj: true, IsLive: true }:
                return Compose(status, NameplateValues.Empty with
                {
                    Station = playback.RadioActive ? playback.Radio.CurrentStation : string.Empty,
                });
            case NameplateStatus.Muster when HostedMusterLive() is { } muster:
                return Compose(status, NameplateValues.Empty with
                {
                    Type = Loc.T(MusterCategories.Label(muster.Category)),
                });
            case NameplateStatus.Chirper or NameplateStatus.Aethergram or NameplateStatus.Velvet:
                return ResolveAppTag(status);
            case NameplateStatus.Games or NameplateStatus.Gamba or NameplateStatus.SlotsWin
                or NameplateStatus.SlotsLoss:
                return ResolveActivity(status);
            case NameplateStatus.InCall when calls.Snapshot().InCall:
            case NameplateStatus.DoNotDisturb when configuration.DoNotDisturb:
            case NameplateStatus.Custom:
                return Compose(status, NameplateValues.Empty);
            case NameplateStatus.NowPlaying:
                return ResolveNowPlaying();
            case NameplateStatus.Handle:
                return ResolveIdleHandle();
            default:
                return NameplateTitle.None;
        }
    }

    private MusterDto? HostedMusterLive()
    {
        var mine = musters.Mine;
        var userId = session.CurrentUser?.Id;
        if (mine is null || userId is null || !string.Equals(mine.HostId, userId, StringComparison.Ordinal))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return now >= mine.StartsAtUnix && now < mine.EndsAtUnix ? mine : null;
    }

    private NameplateTitle ResolveAppTag(NameplateStatus status)
    {
        if (foregroundApp() is not INameplateHandleSource source || source.TagStatus != status)
        {
            return NameplateTitle.None;
        }

        var handle = source.ResolveNameplateHandle();
        return handle.Length == 0
            ? NameplateTitle.None
            : Compose(status, NameplateValues.Empty with { Handle = handle });
    }

    private NameplateTitle ResolveActivity(NameplateStatus status) =>
        foregroundApp() is INameplateActivitySource source && source.TryNameplateActivity(status, out var values)
            ? Compose(status, values)
            : NameplateTitle.None;

    public static string ChipCount(long chips) => chips.ToString("N0", Loc.Culture);

    private NameplateTitle ResolveIdleHandle()
    {
        var handle = ResolveHandle(HandleAppId(NameplateStatus.Handle));
        return handle.Length == 0
            ? NameplateTitle.None
            : Compose(NameplateStatus.Handle, NameplateValues.Empty with { Handle = handle });
    }

    private NameplateTitle ResolveNowPlaying()
    {
        if (playback.IsPlaying)
        {
            if (playback.SongActive)
            {
                return ComposeTrack(playback.Title, playback.Subtitle);
            }

            var track = playback.RadioNowPlaying;
            return ComposeTrack(track.Length > 0 ? track : playback.Title, string.Empty);
        }

        if (!settings.IncludePcMedia)
        {
            return NameplateTitle.None;
        }

        ref readonly var media = ref pcMedia.Current;
        return media.IsPlaying && media.Title.Length > 0
            ? ComposeTrack(media.Title, media.Artist)
            : NameplateTitle.None;
    }

    private NameplateTitle ComposeTrack(string song, string artist)
    {
        if (!string.Equals(song, trackSong, StringComparison.Ordinal) ||
            !string.Equals(artist, trackArtist, StringComparison.Ordinal))
        {
            trackSong = song;
            trackArtist = artist;
            trackSeconds = 0;
        }

        return ComposeNowPlaying(NameplateValues.Empty with { Song = song, Artist = artist }, trackSeconds);
    }

    private NameplateTitle ComposeNowPlaying(in NameplateValues values, double seconds)
    {
        var turns = NameplateTitleText.Turns(TemplateFor(NameplateStatus.NowPlaying), values,
            settings.LongTitles == NameplateLongTitles.Shorten);
        if (turns.Length == 0)
        {
            return NameplateTitle.None;
        }

        var turn = (long)(seconds / settings.TurnSeconds) % turns.Length;
        return Compose(NameplateStatus.NowPlaying, turns[turn]);
    }

    private string HandleAppId(NameplateStatus status) => status switch
    {
        NameplateStatus.Chirper => ChirperAppId,
        NameplateStatus.Aethergram => AethergramAppId,
        NameplateStatus.Velvet => VelvetAppId,
        _ => settings.HandleApp switch
        {
            NameplateHandleApp.Aethergram => AethergramAppId,
            NameplateHandleApp.Velvet => VelvetAppId,
            _ => ChirperAppId,
        },
    };

    private string ResolveHandle(string appId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index] is INameplateHandleSource source &&
                string.Equals(apps[index].Id, appId, StringComparison.Ordinal))
            {
                var handle = source.ResolveNameplateHandle();
                if (handle.Length > 0)
                {
                    return handle;
                }
            }
        }

        return string.Equals(appId, VelvetAppId, StringComparison.Ordinal)
            ? string.Empty
            : session.CurrentUser?.Handle ?? string.Empty;
    }

    private NameplateTitle Compose(NameplateStatus status, in NameplateValues values) =>
        Compose(status, NameplateTitleText.RenderFitted(TemplateFor(status), values));

    private NameplateTitle Compose(NameplateStatus status, string text)
    {
        if (text.Length == 0)
        {
            return NameplateTitle.None;
        }

        var accent = status == NameplateStatus.Custom ? CustomInk : TintFor(status);
        return new NameplateTitle(status, text, LookFor(accent), settings.Prefix);
    }

    private Vector4 TintFor(NameplateStatus status) =>
        status == NameplateStatus.Handle
            ? AppAccents.For(HandleAppId(status))
            : NameplateStatusCatalog.For(status).Tint;

    private TitleLook LookFor(Vector4 accent)
    {
        switch (settings.Style)
        {
            case NameplateTitleStyle.MatchMine when ownLook is { } own:
                return own;
            case NameplateTitleStyle.Custom:
                return TitleLook.Solid(NameplatePalette.At(settings.CustomColor),
                    NameplatePalette.At(settings.CustomGlow));
            default:
                return TitleLook.Solid(new Vector3(accent.X, accent.Y, accent.Z), NameplatePalette.DarkGlow);
        }
    }
}
