namespace Aetherphone.Core.Aethernet;

[Serializable]
internal sealed class CharacterSession
{
    public string Token { get; set; } = string.Empty;
    public string EncryptionKeyCache { get; set; } = string.Empty;
    public string EncryptionKeyCacheUserId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
}
