namespace Aetherphone.Core.Theme;

internal enum PhoneCaseKind : byte
{
    Color,
    Art,
}

internal sealed record PhoneCase(string Id, PhoneCaseKind Kind, Vector4 Tint, string TextureId)
{
    public static PhoneCase Color(string id, Vector4 tint) => new(id, PhoneCaseKind.Color, tint, string.Empty);

    public static PhoneCase Art(string id, Vector4 tint) => new(id, PhoneCaseKind.Art, tint, id);
}
