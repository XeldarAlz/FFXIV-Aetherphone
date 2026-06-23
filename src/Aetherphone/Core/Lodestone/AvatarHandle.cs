using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Lodestone;

internal enum AvatarLoadState : byte
{
    Disabled,
    Loading,
    Ready,
    Failed,
}

internal readonly struct AvatarHandle
{
    public readonly IDalamudTextureWrap? Texture;
    public readonly AvatarLoadState State;
    public readonly string Key;

    public AvatarHandle(IDalamudTextureWrap? texture, AvatarLoadState state, string key)
    {
        Texture = texture;
        State = state;
        Key = key;
    }

    public static readonly AvatarHandle Disabled = new(null, AvatarLoadState.Disabled, string.Empty);
}
