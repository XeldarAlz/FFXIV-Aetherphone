using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Telephony;

namespace Aetherphone.Core.Message;

internal static class ConversationTitle
{
    public static string Of(ConversationDto item, ContactBook contacts)
    {
        if (item.IsGroup)
        {
            return item.Title.Length > 0 ? item.Title : Loc.T(L.DirectMessages.GroupFallback);
        }

        return contacts.NameFor(item.OtherUserId, SocialIdentity.Name(item.OtherDisplayName, item.OtherHandle));
    }

    public static bool Matches(ConversationDto item, ContactBook contacts, string query)
    {
        if (query.Length == 0 || Of(item, contacts).Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !item.IsGroup
            && (item.OtherDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.OtherHandle.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
