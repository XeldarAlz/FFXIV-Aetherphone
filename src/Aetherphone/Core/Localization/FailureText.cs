using Aetherphone.Core.Net;

namespace Aetherphone.Core.Localization;

internal static class FailureCodes
{
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string RateLimited = "rate_limited";
    public const string ServerError = "server_error";
    public const string Suspended = "suspended";
    public const string PostEmpty = "post_empty";
    public const string PostTooLong = "post_too_long";
    public const string PostTooManyImages = "post_too_many_images";
    public const string PostQuoteMissing = "post_quote_missing";
    public const string PostQuoteNotChirp = "post_quote_not_chirp";
    public const string PostQuoteBlocked = "post_quote_blocked";
    public const string PostCooldown = "post_cooldown";
    public const string MediaInvalidReference = "media_invalid_reference";
    public const string MediaInvalidImage = "media_invalid_image";
    public const string MediaInvalidAudio = "media_invalid_audio";
    public const string TokenExpired = "token_expired";
    public const string SessionRevoked = "session_revoked";
    public const string SocialDisabled = "social_disabled";
    public const string AppDisabled = "app_disabled";
    public const string ValidationFailed = "validation_failed";
    public const string Conflict = "conflict";
    public const string PostNotChirp = "post_not_chirp";
    public const string GramCaptionTooLong = "gram_caption_too_long";
    public const string GramImageCount = "gram_image_count";
    public const string GramTooManyTags = "gram_too_many_tags";
    public const string GramInvalidTag = "gram_invalid_tag";
    public const string MediaUnsupportedType = "media_unsupported_type";
    public const string MediaTooLarge = "media_too_large";
    public const string ChatNotMember = "chat_not_member";
    public const string ChatNotMutualContact = "chat_not_mutual_contact";
    public const string ChatBlocked = "chat_blocked";
    public const string ChatNotOwner = "chat_not_owner";
    public const string ChatNotAdmin = "chat_not_admin";
    public const string ChatGroupFull = "chat_group_full";
    public const string ChatHistoryOrphaned = "chat_history_orphaned";
    public const string ChatStoryUnavailable = "chat_story_unavailable";
    public const string ChatMessagePolicy = "chat_message_policy";
    public const string ChatRecipientUnavailable = "chat_recipient_unavailable";
    public const string ChatMessageExpired = "chat_message_expired";
    public const string CommentLength = "comment_length";
    public const string AdLimitReached = "ad_limit_reached";
    public const string AdCooldown = "ad_cooldown";
    public const string AdNotLive = "ad_not_live";
    public const string AdRenewTooEarly = "ad_renew_too_early";
    public const string AdLinkInvalid = "ad_link_invalid";
    public const string AdInquiriesClosed = "ad_inquiries_closed";
    public const string KeyGenerationConflict = "key_generation_conflict";
    public const string KeyGenerationUnknown = "key_generation_unknown";
    public const string MessageEnvelopeMalformed = "message_envelope_malformed";
    public const string MessageEmpty = "message_empty";
    public const string MessageUnavailable = "message_unavailable";
    public const string KeyVersionConflict = "key_version_conflict";
    public const string MusterDescriptionRequired = "muster_description_required";
    public const string MusterDescriptionTooLong = "muster_description_too_long";
    public const string MusterSpotRequired = "muster_spot_required";
    public const string MusterAlreadyHosting = "muster_already_hosting";
    public const string MusterRsvpRequired = "muster_rsvp_required";
    public const string ReportTooManyMessages = "report_too_many_messages";
    public const string ReportSystemMessage = "report_system_message";
    public const string ReportEvidenceInvalid = "report_evidence_invalid";
    public const string StoryUnsupportedApp = "story_unsupported_app";
    public const string StoryCaptionTooLong = "story_caption_too_long";
    public const string StoryLimitReached = "story_limit_reached";
    public const string ProfileNameLength = "profile_name_length";
    public const string ProfileBioTooLong = "profile_bio_too_long";
    public const string ProfileHandleInvalid = "profile_handle_invalid";
    public const string ProfileHandleTaken = "profile_handle_taken";
    public const string RadioNoStation = "radio_no_station";
    public const string RadioStationSuspended = "radio_station_suspended";
    public const string RadioNameRequired = "radio_name_required";
    public const string RadioNameTooLong = "radio_name_too_long";
    public const string RadioLinkInvalid = "radio_link_invalid";
    public const string RadioScheduleTooFar = "radio_schedule_too_far";
    public const string ContactInvalidNumber = "contact_invalid_number";
    public const string ContactOwnNumber = "contact_own_number";
    public const string CasinoLimitOutOfRange = "casino_limit_out_of_range";
    public const string PollClosed = "poll_closed";
    public const string PhotoTagRejected = "photo_tag_rejected";
    public const string VelvetRequestsClosed = "velvet_requests_closed";
    public const string VelvetRequestsMutualsOnly = "velvet_requests_mutuals_only";
    public const string VelvetRegionBlocked = "velvet_region_blocked";
    public const string PatreonLinkExpired = "patreon_link_expired";
    public const string PatreonUnavailable = "patreon_unavailable";
    public const string PatreonAlreadyLinked = "patreon_already_linked";
    public const string FeedbackLength = "feedback_length";
    public const string FeedbackTooManyImages = "feedback_too_many_images";
}

internal sealed class FailureSlot
{
    private AepFailure failure;
    private string? cachedText;
    private LanguageInfo? cachedLanguage;

    public bool Failed => failure.Failed;

    public AepFailure Failure => failure;

    public void Set(AepFailure value)
    {
        if (cachedText is not null && failure == value)
        {
            return;
        }

        failure = value;
        cachedText = null;
        cachedLanguage = null;
    }

    public void Clear()
    {
        Set(AepFailure.None);
    }

    public string Text()
    {
        if (cachedText is not null && ReferenceEquals(cachedLanguage, Loc.Current))
        {
            return cachedText;
        }

        cachedText = FailureText.Resolve(failure);
        cachedLanguage = Loc.Current;
        return cachedText;
    }
}

internal static class FailureText
{
    public static string Resolve(AepFailure failure)
    {
        switch (failure.Kind)
        {
            case AepFailureKind.None:
                return string.Empty;
            case AepFailureKind.Offline:
                return Loc.T(L.Failure.Offline);
            case AepFailureKind.Timeout:
                return Loc.T(L.Failure.Timeout);
            case AepFailureKind.RateLimitPaused:
                return Loc.T(L.Failure.RateLimitPaused);
            case AepFailureKind.SignedOut:
                return Loc.T(L.Failure.SignedOut);
            case AepFailureKind.Cancelled:
                return string.Empty;
            case AepFailureKind.BadResponse:
                return Loc.T(L.Failure.BadResponse);
            default:
                return FromServer(failure);
        }
    }

    private static string FromServer(AepFailure failure)
    {
        switch (failure.Code)
        {
            case FailureCodes.PostEmpty:
                return Loc.T(L.Failure.PostEmpty);
            case FailureCodes.PostTooLong:
                return Valued(L.Failure.PostTooLong, failure);
            case FailureCodes.PostTooManyImages:
                return Valued(L.Failure.PostTooManyImages, failure);
            case FailureCodes.PostQuoteMissing:
                return Loc.T(L.Failure.PostQuoteMissing);
            case FailureCodes.PostQuoteNotChirp:
                return Loc.T(L.Failure.PostQuoteNotChirp);
            case FailureCodes.PostQuoteBlocked:
                return Loc.T(L.Failure.PostQuoteBlocked);
            case FailureCodes.PostCooldown:
                return Valued(L.Failure.PostCooldown, failure);
            case FailureCodes.MediaInvalidImage:
                return Loc.T(L.Failure.MediaInvalidImage);
            case FailureCodes.MediaInvalidAudio:
                return Loc.T(L.Failure.MediaInvalidAudio);
            case FailureCodes.MediaInvalidReference:
                return Loc.T(L.Failure.MediaInvalidReference);
            case FailureCodes.Suspended:
                return Loc.T(L.Failure.Suspended);
            case FailureCodes.Unauthorized:
                return Loc.T(L.Failure.Unauthorized);
            case FailureCodes.Forbidden:
                return Loc.T(L.Failure.Forbidden);
            case FailureCodes.NotFound:
                return Loc.T(L.Failure.NotFound);
            case FailureCodes.RateLimited:
                return Loc.T(L.Failure.RateLimited);
            case FailureCodes.TokenExpired:
                return Loc.T(L.Failure.TokenExpired);
            case FailureCodes.SessionRevoked:
                return Loc.T(L.Failure.SessionRevoked);
            case FailureCodes.SocialDisabled:
                return Loc.T(L.Failure.SocialDisabled);
            case FailureCodes.AppDisabled:
                return Loc.T(L.Failure.AppDisabled);
            case FailureCodes.ValidationFailed:
                return Loc.T(L.Failure.ValidationFailed);
            case FailureCodes.Conflict:
                return Loc.T(L.Failure.Conflict);
            case FailureCodes.PostNotChirp:
                return Loc.T(L.Failure.PostNotChirp);
            case FailureCodes.GramCaptionTooLong:
                return Valued(L.Failure.GramCaptionTooLong, failure);
            case FailureCodes.GramImageCount:
                return Valued(L.Failure.GramImageCount, failure);
            case FailureCodes.GramTooManyTags:
                return Valued(L.Failure.GramTooManyTags, failure);
            case FailureCodes.GramInvalidTag:
                return Loc.T(L.Failure.GramInvalidTag);
            case FailureCodes.MediaUnsupportedType:
                return Loc.T(L.Failure.MediaUnsupportedType);
            case FailureCodes.MediaTooLarge:
                return Loc.T(L.Failure.MediaTooLarge);
            case FailureCodes.ChatNotMember:
                return Loc.T(L.Failure.ChatNotMember);
            case FailureCodes.ChatNotMutualContact:
                return Loc.T(L.Failure.ChatNotMutualContact);
            case FailureCodes.ChatBlocked:
                return Loc.T(L.Failure.ChatBlocked);
            case FailureCodes.ChatNotOwner:
                return Loc.T(L.Failure.ChatNotOwner);
            case FailureCodes.ChatNotAdmin:
                return Loc.T(L.Failure.ChatNotAdmin);
            case FailureCodes.ChatGroupFull:
                return Valued(L.Failure.ChatGroupFull, failure);
            case FailureCodes.ChatHistoryOrphaned:
                return Loc.T(L.Failure.ChatHistoryOrphaned);
            case FailureCodes.ChatStoryUnavailable:
                return Loc.T(L.Failure.ChatStoryUnavailable);
            case FailureCodes.ChatMessagePolicy:
                return Loc.T(L.Failure.ChatMessagePolicy);
            case FailureCodes.ChatRecipientUnavailable:
                return Loc.T(L.Failure.ChatRecipientUnavailable);
            case FailureCodes.ChatMessageExpired:
                return Loc.T(L.Failure.ChatMessageExpired);
            case FailureCodes.CommentLength:
                return Valued(L.Failure.CommentLength, failure);
            case FailureCodes.AdLimitReached:
                return Loc.T(L.Failure.AdLimitReached);
            case FailureCodes.AdCooldown:
                return Loc.T(L.Failure.AdCooldown);
            case FailureCodes.AdNotLive:
                return Loc.T(L.Failure.AdNotLive);
            case FailureCodes.AdRenewTooEarly:
                return Loc.T(L.Failure.AdRenewTooEarly);
            case FailureCodes.AdLinkInvalid:
                return Loc.T(L.Failure.AdLinkInvalid);
            case FailureCodes.AdInquiriesClosed:
                return Loc.T(L.Failure.AdInquiriesClosed);
            case FailureCodes.KeyGenerationConflict:
                return Loc.T(L.Failure.KeyGenerationConflict);
            case FailureCodes.KeyGenerationUnknown:
                return Loc.T(L.Failure.KeyGenerationUnknown);
            case FailureCodes.MessageEnvelopeMalformed:
                return Loc.T(L.Failure.MessageEnvelopeMalformed);
            case FailureCodes.MessageEmpty:
                return Loc.T(L.Failure.MessageEmpty);
            case FailureCodes.MessageUnavailable:
                return Loc.T(L.Failure.MessageUnavailable);
            case FailureCodes.KeyVersionConflict:
                return Loc.T(L.Failure.KeyVersionConflict);
            case FailureCodes.MusterDescriptionRequired:
                return Loc.T(L.Failure.MusterDescriptionRequired);
            case FailureCodes.MusterDescriptionTooLong:
                return Valued(L.Failure.MusterDescriptionTooLong, failure);
            case FailureCodes.MusterSpotRequired:
                return Loc.T(L.Failure.MusterSpotRequired);
            case FailureCodes.MusterAlreadyHosting:
                return Loc.T(L.Failure.MusterAlreadyHosting);
            case FailureCodes.MusterRsvpRequired:
                return Loc.T(L.Failure.MusterRsvpRequired);
            case FailureCodes.ReportTooManyMessages:
                return Valued(L.Failure.ReportTooManyMessages, failure);
            case FailureCodes.ReportSystemMessage:
                return Loc.T(L.Failure.ReportSystemMessage);
            case FailureCodes.ReportEvidenceInvalid:
                return Loc.T(L.Failure.ReportEvidenceInvalid);
            case FailureCodes.StoryUnsupportedApp:
                return Loc.T(L.Failure.StoryUnsupportedApp);
            case FailureCodes.StoryCaptionTooLong:
                return Valued(L.Failure.StoryCaptionTooLong, failure);
            case FailureCodes.StoryLimitReached:
                return Valued(L.Failure.StoryLimitReached, failure);
            case FailureCodes.ProfileNameLength:
                return Valued(L.Failure.ProfileNameLength, failure);
            case FailureCodes.ProfileBioTooLong:
                return Valued(L.Failure.ProfileBioTooLong, failure);
            case FailureCodes.ProfileHandleInvalid:
                return Valued(L.Failure.ProfileHandleInvalid, failure);
            case FailureCodes.ProfileHandleTaken:
                return Loc.T(L.Failure.ProfileHandleTaken);
            case FailureCodes.RadioNoStation:
                return Loc.T(L.Failure.RadioNoStation);
            case FailureCodes.RadioStationSuspended:
                return Loc.T(L.Failure.RadioStationSuspended);
            case FailureCodes.RadioNameRequired:
                return Loc.T(L.Failure.RadioNameRequired);
            case FailureCodes.RadioNameTooLong:
                return Loc.T(L.Failure.RadioNameTooLong);
            case FailureCodes.RadioLinkInvalid:
                return Loc.T(L.Failure.RadioLinkInvalid);
            case FailureCodes.RadioScheduleTooFar:
                return Loc.T(L.Failure.RadioScheduleTooFar);
            case FailureCodes.ContactInvalidNumber:
                return Loc.T(L.Failure.ContactInvalidNumber);
            case FailureCodes.ContactOwnNumber:
                return Loc.T(L.Failure.ContactOwnNumber);
            case FailureCodes.CasinoLimitOutOfRange:
                return Valued(L.Failure.CasinoLimitOutOfRange, failure);
            case FailureCodes.PollClosed:
                return Loc.T(L.Failure.PollClosed);
            case FailureCodes.PhotoTagRejected:
                return Loc.T(L.Failure.PhotoTagRejected);
            case FailureCodes.VelvetRequestsClosed:
                return Loc.T(L.Failure.VelvetRequestsClosed);
            case FailureCodes.VelvetRequestsMutualsOnly:
                return Loc.T(L.Failure.VelvetRequestsMutualsOnly);
            case FailureCodes.PatreonLinkExpired:
                return Loc.T(L.Failure.PatreonLinkExpired);
            case FailureCodes.PatreonUnavailable:
                return Loc.T(L.Failure.PatreonUnavailable);
            case FailureCodes.PatreonAlreadyLinked:
                return Loc.T(L.Failure.PatreonAlreadyLinked);
            case FailureCodes.FeedbackLength:
                return Valued(L.Failure.FeedbackLength, failure);
            case FailureCodes.FeedbackTooManyImages:
                return Valued(L.Failure.FeedbackTooManyImages, failure);
            default:
                return FromStatus(failure);
        }
    }

    private static string FromStatus(AepFailure failure)
    {
        switch (failure.StatusCode)
        {
            case 401:
                return Loc.T(L.Failure.Unauthorized);
            case 403:
                return Loc.T(L.Failure.Forbidden);
            case 404:
                return Loc.T(L.Failure.NotFound);
            case 429:
                return Loc.T(L.Failure.RateLimited);
            default:
                return failure.StatusCode >= 500
                    ? Loc.T(L.Failure.ServerError, failure.Reference())
                    : Loc.T(L.Failure.Unknown, failure.Reference());
        }
    }

    private static string Valued(LocString entry, AepFailure failure)
    {
        return string.IsNullOrEmpty(failure.Value)
            ? Loc.T(L.Failure.Unknown, failure.Reference())
            : Loc.T(entry, failure.Value);
    }
}
