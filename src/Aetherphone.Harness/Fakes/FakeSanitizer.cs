using Dalamud.Game;
using Dalamud.Game.Text.Sanitizer;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeSanitizer : ISanitizer
{
    public string Sanitize(string unsanitizedString) => unsanitizedString;

    public string Sanitize(string unsanitizedString, ClientLanguage clientLanguage) => unsanitizedString;

    public IEnumerable<string> Sanitize(IEnumerable<string> unsanitizedStrings) => unsanitizedStrings;

    public IEnumerable<string> Sanitize(IEnumerable<string> unsanitizedStrings, ClientLanguage clientLanguage) => unsanitizedStrings;
}
