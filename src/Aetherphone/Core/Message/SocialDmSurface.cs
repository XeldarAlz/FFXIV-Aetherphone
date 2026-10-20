using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Message;

internal sealed record SocialDmSurface(string Name, string AppId, string LogTag, string TypingSignalType,
    LocString SharedPost, Vector4 Accent)
{
    public static readonly SocialDmSurface Aethergram = new("gram", "aethergram", "AethergramDm",
        SignalType.GramTyping, L.Aethergram.SharedPost, AppPalettes.Aethergram.Accent);

    public static readonly SocialDmSurface Chirper = new("chirper", "chirper", "ChirperDm", SignalType.ChirperTyping,
        L.Chirper.SharedChirp, AppPalettes.Chirper.Accent);

    public string ImageUploadScope { get; } = Name + "-dm";

    public string VoiceUploadScope { get; } = Name + "-voice";

    public string ReportTargetType { get; } = Name + "_message";
}
