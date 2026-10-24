using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Message;

internal sealed record SocialDmSurface(string Name, string AppId, string LogTag, string TypingSignalType,
    LocString SharedPost, Vector4 Accent, string? LaunchFlag = null)
{
    public static readonly SocialDmSurface Aethergram = new(PairSurface.Gram, "aethergram", "AethergramDm",
        SignalType.GramTyping, L.Aethergram.SharedPost, AppPalettes.Aethergram.Accent);

    public static readonly SocialDmSurface Chirper = new(PairSurface.Chirper, "chirper", "ChirperDm",
        SignalType.ChirperTyping, L.Chirper.SharedChirp, AppPalettes.Chirper.Accent, AppAvailability.ChirperDms);

    public string ImageUploadScope { get; } = Name + "-dm";

    public string VoiceUploadScope { get; } = Name + "-voice";

    public string ReportTargetType { get; } = Name + "_message";

    public bool Live => LaunchFlag is null || AppAvailability.IsEnabled(LaunchFlag);
}
