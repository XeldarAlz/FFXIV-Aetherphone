using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Settings.Pages;

internal readonly record struct AppSettingsEntry(string AppId, string Name, Vector4 Accent, bool HasChannel,
    bool HasBadge, IPhoneApp? App = null);
