using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Settings;

internal interface ISettingsPage
{
    string Title { get; }
    string Summary { get; }
    string Glyph { get; }
    Vector4 Tint { get; }
    void Draw(in PhoneContext context, Rect body);
}
