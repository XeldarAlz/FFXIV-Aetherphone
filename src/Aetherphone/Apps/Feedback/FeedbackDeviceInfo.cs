using System.Globalization;
using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Feedback;

internal sealed class FeedbackDeviceInfo
{
    public const int MaxContextLength = 300;
    public const int EntryCount = 5;

    private static readonly LocString[] Labels =
    {
        L.Feedback.InfoVersion,
        L.Feedback.InfoLanguage,
        L.Feedback.InfoGameClient,
        L.Feedback.InfoRegion,
        L.Feedback.InfoPhoneSize,
    };

    private static readonly string[] WireKeys = { "v", "lang", "client", "region", "phone" };

    private readonly string[] values = new string[EntryCount];
    private readonly StringBuilder builder = new(MaxContextLength);

    public FeedbackDeviceInfo()
    {
        for (var index = 0; index < EntryCount; index++)
        {
            values[index] = string.Empty;
        }
    }

    public string Context { get; private set; } = string.Empty;

    public static LocString Label(int index) => Labels[index];

    public string Value(int index) => values[index];

    public void Capture(GameData gameData, Configuration configuration)
    {
        var region = gameData.LocalRegionCode();
        values[0] = AepConstants.Version;
        values[1] = Loc.Current.Code;
        values[2] = string.Concat(gameData.IsChineseGameClient() ? "CN" : "Global", " (",
            Plugin.ClientState.ClientLanguage.ToString(), ")");
        values[3] = region.Length == 0 ? "-" : region;
        values[4] = ((int)configuration.PhoneWidth).ToString(CultureInfo.InvariantCulture);

        builder.Clear();
        for (var index = 0; index < EntryCount; index++)
        {
            if (index > 0)
            {
                builder.Append("; ");
            }

            builder.Append(WireKeys[index]).Append('=').Append(values[index]);
        }

        Context = builder.Length <= MaxContextLength ? builder.ToString() : builder.ToString(0, MaxContextLength);
    }
}
