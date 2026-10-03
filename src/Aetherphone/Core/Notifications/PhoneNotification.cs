using System.Text;

namespace Aetherphone.Core.Notifications;

internal sealed record PhoneNotification(
    string AppId,
    string Title,
    string Body,
    DateTime ReceivedAt,
    Vector4 Accent,
    string? GroupKey = null)
{
    public string SingleLineBody { get; } = FlattenLineBreaks(Body);

    public long Id { get; init; }

    public string? ActorId { get; init; }

    public string? PostId { get; init; }

    public int SocialType { get; init; } = -1;

    public long CreatedAtUnix { get; init; }

    public string? ChannelId { get; init; }

    public bool Read { get; set; }

    public bool Muted { get; init; }

    public string StackKey => string.IsNullOrEmpty(GroupKey) ? AppId : GroupKey;

    public string SettingsKey => string.IsNullOrEmpty(ChannelId) ? AppId : ChannelId;

    private static string FlattenLineBreaks(string body)
    {
        if (body.IndexOf('\n') < 0 && body.IndexOf('\r') < 0)
        {
            return body;
        }

        var builder = new StringBuilder(body.Length);
        var previousWasSpace = true;
        for (var index = 0; index < body.Length; index++)
        {
            var character = body[index];
            if (character is '\n' or '\r')
            {
                character = ' ';
            }

            if (character == ' ')
            {
                if (previousWasSpace)
                {
                    continue;
                }

                previousWasSpace = true;
            }
            else
            {
                previousWasSpace = false;
            }

            builder.Append(character);
        }

        while (builder.Length > 0 && builder[builder.Length - 1] == ' ')
        {
            builder.Length--;
        }

        return builder.ToString();
    }
}
