using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed partial class CasinoClient
{
    internal const string MinesStartPath = "/casino/mines/start";
    internal const string MinesRevealPath = "/casino/mines/reveal";
    internal const string MinesCashOutPath = "/casino/mines/cashout";
    internal const string DiceRollPath = "/casino/dice/roll";
    internal const string LimboPlayPath = "/casino/limbo/play";
    internal const string KenoDrawPath = "/casino/keno/draw";
    internal const string HiLoStartPath = "/casino/hilo/start";
    internal const string HiLoGuessPath = "/casino/hilo/guess";
    internal const string HiLoSkipPath = "/casino/hilo/skip";
    internal const string HiLoCashOutPath = "/casino/hilo/cashout";
    internal const string OriginalsOpenPath = "/casino/originals/open";

    public Task<CasinoMinesDto?> StartMinesAsync(string sittingId, string clientRoundId, long stake, int mines,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(MinesStartPath, new CasinoMinesStartRequest(sittingId, clientRoundId, stake, mines),
            AethernetJsonContext.Default.CasinoMinesStartRequest, AethernetJsonContext.Default.CasinoMinesDto, token,
            null, onFailure);
    }

    public Task<CasinoMinesDto?> RevealMineAsync(string roundId, int tile, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(MinesRevealPath, new CasinoMinesRevealRequest(roundId, tile),
            AethernetJsonContext.Default.CasinoMinesRevealRequest, AethernetJsonContext.Default.CasinoMinesDto, token,
            null, onFailure);
    }

    public Task<CasinoMinesDto?> CashOutMinesAsync(string roundId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(MinesCashOutPath, new CasinoOriginalsRoundRequest(roundId),
            AethernetJsonContext.Default.CasinoOriginalsRoundRequest, AethernetJsonContext.Default.CasinoMinesDto,
            token, null, onFailure);
    }

    public Task<CasinoDiceDto?> RollDiceAsync(string sittingId, string clientRoundId, long stake, int target,
        bool over, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(DiceRollPath, new CasinoDiceRollRequest(sittingId, clientRoundId, stake, target, over),
            AethernetJsonContext.Default.CasinoDiceRollRequest, AethernetJsonContext.Default.CasinoDiceDto, token,
            null, onFailure);
    }

    public Task<CasinoLimboDto?> PlayLimboAsync(string sittingId, string clientRoundId, long stake, int target,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(LimboPlayPath, new CasinoLimboPlayRequest(sittingId, clientRoundId, stake, target),
            AethernetJsonContext.Default.CasinoLimboPlayRequest, AethernetJsonContext.Default.CasinoLimboDto, token,
            null, onFailure);
    }

    public Task<CasinoKenoDto?> DrawKenoAsync(string sittingId, string clientRoundId, long stake, int risk,
        int[] picks, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(KenoDrawPath, new CasinoKenoDrawRequest(sittingId, clientRoundId, stake, risk, picks),
            AethernetJsonContext.Default.CasinoKenoDrawRequest, AethernetJsonContext.Default.CasinoKenoDto, token,
            null, onFailure);
    }

    public Task<CasinoHiLoDto?> StartHiLoAsync(string sittingId, string clientRoundId, long stake,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HiLoStartPath, new CasinoHiLoStartRequest(sittingId, clientRoundId, stake),
            AethernetJsonContext.Default.CasinoHiLoStartRequest, AethernetJsonContext.Default.CasinoHiLoDto, token,
            null, onFailure);
    }

    public Task<CasinoHiLoDto?> GuessHiLoAsync(string roundId, int step, string call, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HiLoGuessPath, new CasinoHiLoGuessRequest(roundId, step, call),
            AethernetJsonContext.Default.CasinoHiLoGuessRequest, AethernetJsonContext.Default.CasinoHiLoDto, token,
            null, onFailure);
    }

    public Task<CasinoHiLoDto?> SkipHiLoAsync(string roundId, int step, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HiLoSkipPath, new CasinoHiLoSkipRequest(roundId, step),
            AethernetJsonContext.Default.CasinoHiLoSkipRequest, AethernetJsonContext.Default.CasinoHiLoDto, token,
            null, onFailure);
    }

    public Task<CasinoHiLoDto?> CashOutHiLoAsync(string roundId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync(HiLoCashOutPath, new CasinoOriginalsRoundRequest(roundId),
            AethernetJsonContext.Default.CasinoOriginalsRoundRequest, AethernetJsonContext.Default.CasinoHiLoDto,
            token, null, onFailure);
    }

    public Task<CasinoOriginalsOpenDto?> OpenOriginalsAsync(CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(OriginalsOpenPath, AethernetJsonContext.Default.CasinoOriginalsOpenDto, token, null,
            onFailure);
    }
}
