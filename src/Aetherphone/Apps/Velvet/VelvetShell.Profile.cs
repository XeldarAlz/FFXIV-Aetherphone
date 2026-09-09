using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal enum VelvetProfileTab
{
    About,
    Posts,
}

internal sealed partial class VelvetShell
{
    private const float ProfileAvatarRadius = 44f;
    private const float ProfileHeadTop = 14f;
    private const float ProfileStatsGap = 20f;
    private const float ProfileStatColumnPad = 10f;
    private const float ProfileBlockGap = 10f;
    private const float ProfileTabHeight = 44f;
    private const float ProfileActionHeight = 40f;
    private const float ProfileActionGap = 10f;
    private const float ProfileBottomPad = 14f;
    private const float ProfileGridGap = 1.5f;
    private const float ProfileTabUnderline = 2f;
    private const float ProfileTabSmoothTime = 0.1f;
    private const float ProfileAboutLead = 4f;
    private const int ProfileColumns = 3;
    private const int MaxFacts = 4;
    private const int MaxSharedLabels = 64;

    private static readonly Vector4 RoleTone = new(0.62f, 0.22f, 0.60f, 1f);
    private static readonly Vector4 KinkTone = new(0.647f, 0.482f, 0.839f, 1f);
    private static readonly TextStyle ProfileNameStyle = new(1.35f, FontWeight.Bold);
    private static readonly TextStyle ProfileStatValueStyle = new(1.1f, FontWeight.Bold);
    private static readonly TextStyle ProfileStatLabelStyle = TextStyles.Subheadline;
    private static readonly TextStyle ProfileTabStyle = new(1.02f, FontWeight.SemiBold);
    private static readonly TextStyle ProfileTabIdleStyle = new(1.02f, FontWeight.Medium);
    private static readonly UnderlineTabStyle ProfileTabsStyle = new(ProfileTabStyle, ProfileTabIdleStyle,
        VelvetTheme.TitleInk, VelvetTheme.MutedInk, VelvetTheme.Rose, ProfileTabUnderline, SocialChrome.CellPadX,
        ProfileTabSmoothTime);

    private readonly VFact[] facts = new VFact[MaxFacts];
    private readonly float[] factHeights = new float[MaxFacts];
    private readonly string?[] sharedLabels = new string?[MaxSharedLabels];
    private VelvetProfileTab profileTab = VelvetProfileTab.About;
    private Spring profileTabSlide;
    private string roleSummaryRaw = string.Empty;
    private string roleSummary = string.Empty;
    private LanguageInfo? roleSummaryLanguage;
    private LanguageInfo? sharedLabelLanguage;

    private void DrawProfile(Rect area, string userId)
    {
        var scale = UiScale.Current;
        var user = store.ProfileUserId == userId ? store.ProfileUser : null;
        var title = user != null ? DisplayNameOf(user.DisplayName, user.Handle) : Loc.T(L.Velvet.ProfileTitle);
        if (VHeader.Push(area, title, 1))
        {
            router.Pop();
            return;
        }

        if (user != null && VIcon.Button(VHeader.Slot(area, 0), VHeader.IconRadius, PhoneIcons.Dots, VIcon.Overflow,
                VelvetTheme.TitleInk, Loc.T(L.Velvet.More), HoverLabelSide.Below))
        {
            OpenProfileMenu(user);
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        if (user == null)
        {
            if (store.ProfileLoading)
            {
                DrawEmpty(body, Loc.T(L.Common.Loading), string.Empty);
            }
            else if (store.ProfileFailed)
            {
                if (EmptyState.Draw(body, ui, PhoneIcons.CloudDownload, Loc.T(L.Velvet.ProfileUnavailable),
                        Loc.T(L.Velvet.ProfileUnavailableHint), Loc.T(L.Common.Retry)))
                {
                    store.OpenProfile(userId);
                }
            }
            else
            {
                EmptyState.Draw(body, ui, PhoneIcons.User, Loc.T(L.Velvet.ProfileUnavailable),
                    Loc.T(L.Velvet.ProfileUnavailableHint));
            }

            return;
        }

        DrawProfileBody(body, user);
    }

    private void DrawProfileBody(Rect body, VelvetProfileDto user)
    {
        var isMe = store.Me?.UserId == user.UserId;
        var connected = isMe || user.ConnectionState == VelvetConnectionState.Connected;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawProfileHead(user, isMe, width);
            var picked = UnderlineTabs.Draw(Reserve(ProfileTabHeight), Loc.T(L.Velvet.CardAbout),
                Loc.T(L.Velvet.Posts), profileTab == VelvetProfileTab.Posts, ref profileTabSlide, VelvetInk.Shared,
                ProfileTabsStyle);
            if (picked >= 0)
            {
                profileTab = picked == 1 ? VelvetProfileTab.Posts : VelvetProfileTab.About;
            }

            if (profileTab == VelvetProfileTab.Posts)
            {
                Gap(14f);
                DrawPostGrid(user, isMe, connected, width);
            }
            else
            {
                Gap(ProfileAboutLead);
                DrawProfileAbout(user, width);
            }

            Gap(40f);
        }
    }

    private void DrawProfileHead(VelvetProfileDto user, bool isMe, float width)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var innerLeft = origin.X + pad;
        var innerRight = origin.X + width - pad;
        var innerWidth = MathF.Max(1f, innerRight - innerLeft);
        var radius = ProfileAvatarRadius * scale;
        var frame = Frames.Of(user.FrameId);
        var frameReach = AvatarView.Reserve(frame, radius);
        var avatarCenter = new Vector2(innerLeft + frameReach + radius,
            origin.Y + ProfileHeadTop * scale + frameReach + radius);
        var ring = isMe
            ? VelvetTheme.Rose
            : user.ConnectionState == VelvetConnectionState.Connected
                ? VelvetTheme.Moonlight
                : (Vector4?)null;
        var name = DisplayNameOf(user.DisplayName, user.Handle);
        VAvatar.Draw(drawList, avatarCenter, radius, theme, name, isMe ? user.World : string.Empty, user.AvatarUrl,
            images, lodestone, -1, ring, frame);
        avatarLightbox.TryOpen(avatarCenter, radius, user.AvatarUrl, images);

        var statCount = isMe ? 2 : 1;
        var statsLeft = avatarCenter.X + radius + frameReach + ProfileStatsGap * scale;
        var statsInline = StatsFitInline(innerRight - statsLeft, statCount, scale);
        var statsHeight = Typography.LineHeight(ProfileStatValueStyle)
            + Typography.LineHeight(ProfileStatLabelStyle);
        var statsRowTop = avatarCenter.Y + radius + frameReach + ProfileBlockGap * scale;
        if (statsInline)
        {
            DrawProfileStats(drawList, user, statCount, statsLeft, innerRight, avatarCenter.Y);
        }
        else
        {
            DrawProfileStats(drawList, user, statCount, innerLeft, innerRight, statsRowTop + statsHeight * 0.5f);
        }

        var nameTop = statsInline ? statsRowTop : statsRowTop + statsHeight + ProfileBlockGap * scale;
        UserName.DrawAuto(drawList, "velvet.profile.name." + user.UserId, name, user.Badges, user.BadgeIds, innerLeft,
            nameTop, innerWidth, ProfileNameStyle, VelvetTheme.TitleInk, theme, 2);

        var handleTop = nameTop + Typography.LineHeight(ProfileNameStyle);
        var handle = SocialIdentity.ProfileMeta(user.Handle, RegionCodeOf(user));
        var handleHeight = handle.Length > 0
            ? Typography.DrawWrappedLeft(new Vector2(innerLeft, handleTop), handle, VelvetTheme.MutedInk,
                TextStyles.Subheadline, innerWidth)
            : 0f;

        var intentTop = handleTop + handleHeight + 8f * scale;
        var intentHeight = Typography.DrawWrappedLeft(new Vector2(innerLeft, intentTop),
            VelvetIntent.Summary(user.LookingFor), VelvetTheme.RoseInk, TextStyles.Headline, innerWidth);

        var chipsTop = intentTop + intentHeight + ProfileBlockGap * scale;
        var chipsHeight = DrawProfileChips(drawList, user, innerLeft, innerRight,
            chipsTop + SocialChrome.MetaChipHeight * scale * 0.5f);

        var actionTop = chipsTop + chipsHeight + ProfileBlockGap * scale;
        DrawProfileAction(user, isMe, new Rect(new Vector2(innerLeft, actionTop),
            new Vector2(innerRight, actionTop + ProfileActionHeight * scale)));

        var bottom = actionTop + ProfileActionHeight * scale + ProfileBottomPad * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y));
    }

    private static bool StatsFitInline(float available, int count, float scale)
    {
        var widest = Typography.Measure(Loc.T(L.Velvet.Posts), ProfileStatLabelStyle).X;
        if (count > 1)
        {
            widest = MathF.Max(widest,
                Typography.Measure(Loc.T(L.Velvet.ProfileConnections), ProfileStatLabelStyle).X);
        }

        return (widest + ProfileStatColumnPad * scale) * count <= available;
    }

    private void DrawProfileStats(ImDrawListPtr drawList, VelvetProfileDto user, int count, float left, float right,
        float centerY)
    {
        store.EnsureUserPosts(user.UserId);
        var postCount = store.UserPostsUserId == user.UserId && store.UserPostsLoaded ? store.UserPostsTotal : 0;
        var column = MathF.Max(1f, (right - left) / count);
        var valueHeight = Typography.LineHeight(ProfileStatValueStyle);
        var top = centerY - (valueHeight + Typography.LineHeight(ProfileStatLabelStyle)) * 0.5f;
        DrawProfileStat(drawList, left, top, column, valueHeight, postCount, Loc.T(L.Velvet.Posts));
        if (count < 2)
        {
            return;
        }

        DrawProfileStat(drawList, left + column, top, column, valueHeight, store.Connections.Length,
            Loc.T(L.Velvet.ProfileConnections));
    }

    private static void DrawProfileStat(ImDrawListPtr drawList, float left, float top, float column, float valueHeight,
        int count, string label)
    {
        var scale = UiScale.Current;
        var maxWidth = MathF.Max(1f, column - ProfileStatColumnPad * scale);
        Typography.Draw(drawList, new Vector2(left, top), CountText.Compact(count), VelvetTheme.TitleInk,
            ProfileStatValueStyle);
        Typography.Draw(drawList, new Vector2(left, top + valueHeight),
            Typography.FitText(label, maxWidth, ProfileStatLabelStyle), VelvetTheme.MutedInk, ProfileStatLabelStyle);
    }

    private float DrawProfileChips(ImDrawListPtr drawList, VelvetProfileDto user, float left, float right,
        float centerY)
    {
        var scale = UiScale.Current;
        var cursor = left;
        if (user.RelationshipStatus != VelvetRelationship.NotSaying)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursor, right, centerY, PhoneIcons.HeartHandshake,
                VelvetRelationship.Label(user.RelationshipStatus), VelvetInk.Shared, TextStyles.Footnote);
        }

        if (user.Pronouns.Length > 0)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursor, right, centerY, PhoneIcons.User, user.Pronouns,
                VelvetInk.Shared, TextStyles.Footnote);
        }

        if (user.ShareTimeZone && user.UtcOffsetMinutes is { } offset)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursor, right, centerY, PhoneIcons.Clock,
                SocialTimeZone.Describe(offset), VelvetInk.Shared, TextStyles.Footnote);
        }

        return cursor > left ? SocialChrome.MetaChipHeight * scale : 0f;
    }

    private void DrawProfileAction(VelvetProfileDto user, bool isMe, Rect rect)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = rect.Height * 0.5f;
        if (isMe)
        {
            if (SocialPill.Outline(drawList, rect, Loc.T(L.Velvet.EditProfile), VelvetInk.Shared,
                    TextStyles.SubheadlineEmphasized, rounding, VelvetInk.Shared.ButtonFill))
            {
                BeginEditProfile();
                router.Push(VelvetView.EditProfile);
            }

            return;
        }

        switch (user.ConnectionState)
        {
            case VelvetConnectionState.Connected:
                if (SocialPill.Accent(drawList, rect, Loc.T(L.Velvet.Message), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding))
                {
                    OpenThread(user.UserId);
                }

                break;
            case VelvetConnectionState.OutgoingRequest:
                if (SocialPill.Outline(drawList, rect, Loc.T(L.Velvet.Requested), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding, VelvetInk.Shared.ButtonFill))
                {
                    store.CancelRequest(user.UserId);
                }

                break;
            case VelvetConnectionState.IncomingRequest:
                if (SocialPill.Accent(drawList, rect, Loc.T(L.Velvet.Reply), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding))
                {
                    OpenThread(user.UserId);
                }

                break;
            case VelvetConnectionState.Blocked:
                if (SocialPill.Outline(drawList, rect, Loc.T(L.Velvet.Unblock), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding, VelvetInk.Shared.ButtonFill))
                {
                    store.Unblock(user.UserId);
                }

                break;
            default:
            {
                var half = (rect.Width - ProfileActionGap * scale) * 0.5f;
                var connect = new Rect(rect.Min, new Vector2(rect.Min.X + half, rect.Max.Y));
                var say = new Rect(new Vector2(rect.Max.X - half, rect.Min.Y), rect.Max);
                if (SocialPill.Accent(drawList, connect, Loc.T(L.Velvet.Connect), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding))
                {
                    store.Connect(user.UserId);
                }

                if (SocialPill.Outline(drawList, say, Loc.T(L.Velvet.DeckSay), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding, VelvetInk.Shared.ButtonFill))
                {
                    RequestIntro(user.UserId, user.DisplayName, user.Handle, user.AvatarUrl);
                }

                break;
            }
        }
    }

    private void DrawProfileAbout(VelvetProfileDto user, float width)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var innerWidth = MathF.Max(1f, width - pad * 2f);
        ImGui.Indent(pad);
        DrawIntroCard(user, innerWidth);
        var viewer = ViewerAgainst(user);
        DrawFactsCard(user, innerWidth);
        if (VelvetIntent.IncludesErp(user.LookingFor))
        {
            DrawTokenCard(L.Velvet.CardKinks, PhoneIcons.Flame, KinkTone, user.Kinks, innerWidth, viewer,
                VelvetTokenGroup.Kinks);
        }

        DrawTokenCard(L.Velvet.CardTags, PhoneIcons.Hash, VelvetTheme.Rose, user.Tags, innerWidth, viewer,
            VelvetTokenGroup.Tags);
        DrawTokenCard(L.Velvet.CardLimits, PhoneIcons.Shield, VelvetTheme.Gold, user.Limits, innerWidth, viewer,
            VelvetTokenGroup.Limits);
        ImGui.Unindent(pad);
    }

    private VelvetProfileDto? ViewerAgainst(VelvetProfileDto user) =>
        store.Me is { } me && me.UserId != user.UserId ? me : null;

    private void DrawIntroCard(VelvetProfileDto user, float width)
    {
        if (user.Intro.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var introKey = new TranslationKey(TranslationSurface.Bio, user.UserId);
        var introText = translation.View(introKey, user.Intro).Text;
        var contentWidth = MathF.Max(1f, width - VCard.Pad * 2f * scale);
        var textHeight = Typography.MeasureWrappedBlock(introText, TextStyles.Body, contentWidth).Y;
        var linkHeight = TranslateLink.Height(translation, introKey, user.IntroLang, scale);
        Gap(VCard.Gap);
        var card = VCard.Begin(drawList, width, textHeight + linkHeight, scale);
        Typography.DrawWrappedLeft(card.ContentOrigin, introText, VelvetTheme.BodyInk, TextStyles.Body,
            card.ContentWidth);
        if (linkHeight > 0f)
        {
            TranslateLink.Draw(translation, confirm, introKey, user.IntroLang, user.Intro,
                new Vector2(card.ContentOrigin.X, card.ContentOrigin.Y + textHeight), card.ContentWidth,
                VelvetTheme.MutedInk, VelvetTheme.RoseGlow, scale);
        }

        VCard.End(card);
    }

    private void DrawFactsCard(VelvetProfileDto user, float width)
    {
        var count = 0;
        var gender = VelvetGender.Summary(user.Gender);
        if (gender.Length > 0)
        {
            facts[count++] = new VFact(PhoneIcons.Gender, VelvetTheme.Rose, Loc.T(L.Velvet.CardGender), gender);
        }

        var sexuality = VelvetSexuality.Summary(user.Sexuality);
        if (sexuality.Length > 0)
        {
            facts[count++] = new VFact(PhoneIcons.Rainbow, VelvetTheme.Rose, Loc.T(L.Velvet.CardSexuality),
                sexuality);
        }

        var languages = VelvetLanguages.Summary(user.Languages);
        if (languages.Length > 0)
        {
            facts[count++] = new VFact(PhoneIcons.Language, VelvetTheme.RegionAccent, Loc.T(L.Velvet.CardLanguages),
                languages);
        }

        if (VelvetIntent.IncludesErp(user.LookingFor))
        {
            var role = RoleSummary(user.Dynamic);
            if (role.Length > 0)
            {
                facts[count++] = new VFact(PhoneIcons.Heart, RoleTone, Loc.T(L.Velvet.CardRole), role);
            }
        }

        if (count == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var contentWidth = MathF.Max(1f, width - VCard.Pad * 2f * scale);
        var contentHeight = 0f;
        for (var index = 0; index < count; index++)
        {
            factHeights[index] = VCard.FactHeight(facts[index], contentWidth, scale);
            contentHeight += factHeights[index];
        }

        Gap(VCard.Gap);
        var card = VCard.Begin(drawList, width, contentHeight, scale, 0f);
        var rowTop = card.ContentOrigin.Y;
        for (var index = 0; index < count; index++)
        {
            VCard.Fact(drawList, new Vector2(card.ContentOrigin.X, rowTop), card.ContentWidth, facts[index],
                factHeights[index], index < count - 1, scale);
            rowTop += factHeights[index];
        }

        VCard.End(card);
    }

    private string RoleSummary(string raw)
    {
        if (string.Equals(raw, roleSummaryRaw, StringComparison.Ordinal)
            && ReferenceEquals(roleSummaryLanguage, Loc.Current))
        {
            return roleSummary;
        }

        roleSummaryRaw = raw;
        roleSummaryLanguage = Loc.Current;
        var tokens = VelvetTags.Parse(raw);
        var labels = new string[tokens.Length];
        for (var index = 0; index < tokens.Length; index++)
        {
            labels[index] = VelvetTokenLabels.Of(tokens[index]);
        }

        roleSummary = string.Join(", ", labels);
        return roleSummary;
    }

    private void DrawTokenCard(LocString title, string glyph, Vector4 tone, string[]? tokens, float width,
        VelvetProfileDto? viewer, VelvetTokenGroup group)
    {
        if (tokens is null || tokens.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        chipModels.Clear();
        var sharedCount = 0;
        for (var index = 0; index < tokens.Length; index++)
        {
            var match = VelvetFit.Match(viewer, group, tokens[index]);
            if (match == VelvetTokenMatch.Shared)
            {
                sharedCount++;
            }

            chipModels.Add(TokenChip(tokens[index], tone, match));
        }

        var trailing = SharedLabel(sharedCount);
        var contentWidth = MathF.Max(1f, width - VCard.Pad * 2f * scale);
        var chipsHeight = MeasureChipFlow(contentWidth, scale);
        Gap(VCard.Gap);
        var card = VCard.Begin(drawList, width, VCard.HeaderBlock * scale + chipsHeight, scale);
        VCard.Header(drawList, card.ContentOrigin, card.ContentWidth, glyph, tone, Loc.T(title), scale, trailing);
        ImGui.SetCursorScreenPos(new Vector2(card.ContentOrigin.X,
            card.ContentOrigin.Y + VCard.HeaderBlock * scale));
        DrawChipFlow(card.ContentWidth, scale);
        VCard.End(card);
    }

    private void AskDisconnect(string userId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Velvet.DisconnectConfirmMessage),
            ConfirmLabel = Loc.T(L.Velvet.Disconnect),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Sheet = true,
            Confirm = () =>
            {
                store.Disconnect(userId);
                router.Reset();
            },
        });
    }

    private int DrawDisplayTokens(string[] tokens, Vector4 tone, float width)
    {
        if (tokens.Length == 0)
        {
            return -1;
        }

        chipModels.Clear();
        for (var index = 0; index < tokens.Length; index++)
        {
            chipModels.Add(TokenChip(tokens[index], tone, VelvetTokenMatch.None));
        }

        return DrawChipFlow(width, UiScale.Current);
    }

    private string SharedLabel(int count)
    {
        if (count <= 0 || count >= MaxSharedLabels)
        {
            return string.Empty;
        }

        if (!ReferenceEquals(sharedLabelLanguage, Loc.Current))
        {
            sharedLabelLanguage = Loc.Current;
            Array.Clear(sharedLabels);
        }

        return sharedLabels[count] ??= Loc.Plural(L.Velvet.CardShared, count);
    }

    private static VChipModel TokenChip(string token, Vector4 tone, VelvetTokenMatch match)
    {
        var label = VelvetTokenLabels.Of(token);
        return match switch
        {
            VelvetTokenMatch.Shared => new VChipModel(label, VChipStyle.Match, tone, PhoneIcons.Users),
            VelvetTokenMatch.Conflict => new VChipModel(label, VChipStyle.Tint, VelvetTheme.Danger, PhoneIcons.Ban),
            _ => new VChipModel(label, VChipStyle.Tint, tone),
        };
    }
}
