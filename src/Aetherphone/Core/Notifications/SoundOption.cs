namespace Aetherphone.Core.Notifications;

internal enum SoundSource : byte
{
    Game,
    Bundled,
    User,
}

internal readonly record struct SoundOption(string Token, SoundSource Source);
