using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float HeroTextInset = 12f;

    private void DrawProfile(Rect area, string userId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var user = store.ProfileUserId == userId ? store.ProfileUser : null;
        var title = user != null ? DisplayNameOf(user.DisplayName, user.Handle) : Loc.T(L.Velvet.ProfileTitle);
        if (VHeader.Push(area, title, theme))
        {
            router.Pop();
            return;
        }

        if (user != null && store.Me?.UserId != user.UserId)
        {
            var flagCenter = new Vector2(area.Max.X - 22f * scale, area.Min.Y + VHeader.Height * scale * 0.5f);
            if (ui.IconButton(flagCenter, 15f * scale, FontAwesomeIcon.Flag.ToIconString(), VelvetTheme.MutedInk,
                    AppSkin.Transparent, 0.82f, Loc.T(L.Velvet.Report)))
            {
                OpenReport("velvet_profile", user.UserId, Loc.T(L.Velvet.ReportProfile));
            }
        }
        else if (user != null)
        {
            var rulesCenter = new Vector2(area.Max.X - 22f * scale, area.Min.Y + VHeader.Height * scale * 0.5f);
            if (ui.IconButton(rulesCenter, 15f * scale, FontAwesomeIcon.QuestionCircle.ToIconString(),
                    VelvetTheme.MutedInk, AppSkin.Transparent, 0.82f, Loc.T(L.Conduct.Eyebrow)))
            {
                conduct.ShowRules(Id);
            }
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        if (user == null)
        {
            if (store.ProfileLoading)
            {
                Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), VelvetTheme.MutedInk, TextStyles.Callout);
            }
            else
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.User, Loc.T(L.Velvet.ProfileUnavailable),
                    Loc.T(L.Velvet.ProfileUnavailableHint));
            }

            return;
        }

        DrawProfileBody(body, user);
    }

    private void DrawProfileBody(Rect body, VelvetProfileDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var isMe = store.Me?.UserId == user.UserId;
        var connected = isMe || user.ConnectionState == VelvetConnectionState.Connected;
        using (AppSurface.Begin(body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            var drawList = ImGui.GetWindowDrawList();
            var heroTop = ImGui.GetCursorScreenPos();
            var centerX = heroTop.X + width * 0.5f;
            var name = DisplayNameOf(user.DisplayName, user.Handle);
            var radius = 56f * scale;
            var avatarCenter = new Vector2(centerX, heroTop.Y + 14f * scale + radius);
            Vector4? ring = isMe ? VelvetTheme.Rose : connected ? VelvetTheme.Moonlight : null;
            VAvatar.Draw(drawList, avatarCenter, radius, theme, name, user.World, user.AvatarUrl, images, lodestone, -1,
                ring);
            avatarLightbox.TryOpen(avatarCenter, radius, user.AvatarUrl, images);

            var textWidth = width - HeroTextInset * 2f * scale;
            var lineTop = avatarCenter.Y + radius + 16f * scale;
            var badgeSpace = user.Verified ? 24f * scale : 0f;
            var displayName = Typography.FitText(name, textWidth - badgeSpace, TextStyles.Title1);
            var nameSize = Typography.Measure(displayName, TextStyles.Title1);
            var nameX = centerX - (nameSize.X + badgeSpace) * 0.5f;
            Typography.Draw(new Vector2(nameX, lineTop), displayName, VelvetTheme.TitleInk, TextStyles.Title1);
            if (user.Verified)
            {
                DrawVerifiedBadge(drawList, new Vector2(nameX + nameSize.X + 13f * scale, lineTop + nameSize.Y * 0.5f),
                    scale);
            }

            lineTop += nameSize.Y + 4f * scale;
            var region = RegionOf(user.World);
            var meta = SocialIdentity.ProfileMeta(user.Handle, region);
            if (user.Pronouns.Length > 0)
            {
                meta = meta.Length > 0 ? meta + " · " + user.Pronouns : user.Pronouns;
            }

            if (meta.Length > 0)
            {
                lineTop += Typography.DrawWrappedCentered(new Vector2(centerX, lineTop), meta, VelvetTheme.MutedInk,
                    TextStyles.Subheadline, textWidth);
            }

            lineTop += 10f * scale;
            lineTop += Typography.DrawWrappedCentered(new Vector2(centerX, lineTop),
                VelvetIntent.Summary(user.LookingFor), VelvetTheme.RoseInk, TextStyles.Headline, textWidth);

            var sub = user.RelationshipStatus != VelvetRelationship.NotSaying
                ? VelvetRelationship.Label(user.RelationshipStatus)
                : string.Empty;
            if (user.ShareTimeZone && user.UtcOffsetMinutes is { } offset)
            {
                var timeZone = SocialTimeZone.Describe(offset);
                sub = sub.Length > 0 ? sub + " · " + timeZone : timeZone;
            }

            if (sub.Length > 0)
            {
                lineTop += 6f * scale;
                lineTop += Typography.DrawWrappedCentered(new Vector2(centerX, lineTop), sub, VelvetTheme.MutedInk,
                    TextStyles.Footnote, textWidth);
            }

            var heroHeight = lineTop - heroTop.Y + 8f * scale;
            ImGui.SetCursorScreenPos(heroTop);
            ImGui.Dummy(new Vector2(width, heroHeight));

            Gap(6f);
            DrawRelationshipAction(user, isMe);

            if (isMe)
            {
                Gap(10f);
                var settingsRect = Reserve(44f);
                if (ui.GhostButton(settingsRect, Loc.T(L.Velvet.Settings)))
                {
                    settingsLoaded = false;
                    router.Push(VelvetView.Settings);
                }
            }

            Gap(24f);
            DrawGallery(user, isMe, connected);

            var genderLabel = VelvetGender.Label(VelvetGender.Sanitize(user.Gender));
            if (genderLabel.Length > 0)
            {
                Gap(20f);
                VSectionHeader.Bar(Loc.T(L.Velvet.CardGender));
                Gap(4f);
                DrawDisplayTokens(new[] { genderLabel }, VChipStyle.Tint, VelvetTheme.Rose);
            }

            if (user.Intro.Length > 0)
            {
                Gap(20f);
                VSectionHeader.Bar(Loc.T(L.Velvet.CardAbout));
                Gap(4f);
                WrapText(user.Intro, VelvetTheme.BodyInk, TextStyles.Body);
            }

            if (VelvetIntent.IncludesErp(user.LookingFor) && user.Dynamic.Length > 0)
            {
                Gap(18f);
                VSectionHeader.Bar(Loc.T(L.Velvet.CardRole));
                Gap(4f);
                DrawDisplayTokens(VelvetTags.Parse(user.Dynamic), VChipStyle.Tint, new Vector4(0.62f, 0.22f, 0.60f, 1f));
            }

            if (user.Tags.Length > 0)
            {
                Gap(18f);
                VSectionHeader.Bar(Loc.T(L.Velvet.CardTags));
                Gap(4f);
                DrawDisplayTokens(user.Tags, VChipStyle.Tint, VelvetTheme.Rose);
            }

            if (user.Limits.Length > 0)
            {
                Gap(18f);
                VSectionHeader.Bar(Loc.T(L.Velvet.CardLimits));
                Gap(4f);
                DrawDisplayTokens(user.Limits, VChipStyle.Outline, VelvetTheme.Gold);
            }

            if (!isMe)
            {
                Gap(26f);
                if (user.ConnectionState == VelvetConnectionState.Blocked)
                {
                    if (ui.GhostButton(Reserve(42f), Loc.T(L.Velvet.Unblock)))
                    {
                        store.Unblock(user.UserId);
                    }
                }
                else
                {
                    if (user.ConnectionState == VelvetConnectionState.Connected)
                    {
                        if (ui.GhostButton(Reserve(42f), Loc.T(L.Velvet.Disconnect)))
                        {
                            AskDisconnect(user.UserId);
                        }

                        Gap(10f);
                    }

                    if (ui.DangerGhostButton(Reserve(42f), Loc.T(L.Velvet.Block)))
                    {
                        store.Block(user.UserId, _ => { });
                    }
                }
            }

            Gap(40f);
        }
    }

    private void AskDisconnect(string userId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Velvet.DisconnectConfirmMessage),
            ConfirmLabel = Loc.T(L.Velvet.Disconnect),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Confirm = () =>
            {
                store.Disconnect(userId);
                router.Reset();
            },
        });
    }

    private static void DrawVerifiedBadge(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        drawList.AddCircleFilled(center, 8f * scale, VelvetTheme.Rose.Packed(), 20);
        var ink = VelvetTheme.OnAccent.Packed();
        drawList.AddLine(new Vector2(center.X - 3.4f * scale, center.Y + 0.4f * scale),
            new Vector2(center.X - 1f * scale, center.Y + 2.8f * scale), ink, 1.6f * scale);
        drawList.AddLine(new Vector2(center.X - 1f * scale, center.Y + 2.8f * scale),
            new Vector2(center.X + 3.6f * scale, center.Y - 2.6f * scale), ink, 1.6f * scale);
    }

    private void DrawGallery(VelvetProfileDto user, bool isMe, bool connected)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (!store.FeedLoaded && !store.LoadingFeed)
        {
            store.RefreshFeed();
        }

        var feed = store.Feed;
        var owned = new List<VelvetPostDto>();
        for (var index = 0; index < feed.Length; index++)
        {
            if (feed[index].OwnerId == user.UserId)
            {
                owned.Add(feed[index]);
            }
        }

        var width = ScrollLayout.StableContentWidth();
        VSectionHeader.Bar(isMe ? Loc.T(L.Velvet.MyPhotos) : Loc.T(L.Velvet.Photos),
            owned.Count > 0 ? owned.Count.ToString(Loc.Culture) : string.Empty);
        Gap(6f);

        if (owned.Count == 0)
        {
            if (!isMe && !connected)
            {
                DrawLockedGallery(DisplayNameOf(user.DisplayName, user.Handle), width);
            }
            else
            {
                Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(0f, 2f * scale),
                    isMe ? Loc.T(L.Velvet.NoPhotosMine) : Loc.T(L.Velvet.NoPhotosShared), VelvetTheme.MutedInk,
                    TextStyles.Footnote);
                Gap(24f);
            }

            return;
        }

        const int columns = 3;
        var cellGap = 6f * scale;
        var cell = (width - cellGap * (columns - 1)) / columns;
        var rows = (owned.Count + columns - 1) / columns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < owned.Count; index++)
        {
            var row = index / columns;
            var col = index % columns;
            var min = new Vector2(origin.X + col * (cell + cellGap), origin.Y + row * (cell + cellGap));
            var max = new Vector2(min.X + cell, min.Y + cell);
            DrawMedia(drawList, min, max, owned[index].MediaUrl, Metrics.Radius.Md * scale);
            if (PostMedia.Photos(owned[index].MediaUrls, owned[index].MediaUrl).Length > 1)
            {
                MultiPhotoBadge.Draw(drawList, new Vector2(max.X - 8f * scale, min.Y + 8f * scale), scale);
            }

            if (UiInteract.Click(min, max))
            {
                store.EnsurePost(owned[index].Id);
                router.Push(VelvetView.PostDetail(owned[index].Id));
            }
        }

        var gridHeight = rows * cell + (rows - 1) * cellGap;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, gridHeight));
    }

    private void DrawLockedGallery(string name, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        const int columns = 3;
        var cellGap = 6f * scale;
        var cell = (width - cellGap * (columns - 1)) / columns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        for (var col = 0; col < columns; col++)
        {
            var min = new Vector2(origin.X + col * (cell + cellGap), origin.Y);
            var max = new Vector2(min.X + cell, min.Y + cell);
            VMediaTile.Conceal(drawList, min, max, Metrics.Radius.Md * scale, string.Empty, 0f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cell));
        Gap(12f);
        Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, ImGui.GetCursorScreenPos().Y),
            Loc.T(L.Velvet.ConnectToSeePhotos, name), VelvetTheme.RoseInk, TextStyles.Callout, width - 40f * scale);
        Gap(30f);
    }

    private void DrawRelationshipAction(VelvetProfileDto user, bool isMe)
    {
        var rect = Reserve(46f);
        if (isMe)
        {
            if (ui.PillButton(rect, Loc.T(L.Velvet.EditProfile), true))
            {
                BeginEditProfile();
                router.Push(VelvetView.EditProfile);
            }

            return;
        }

        switch (user.ConnectionState)
        {
            case VelvetConnectionState.Connected:
                if (ui.PillButton(rect, Loc.T(L.Velvet.Message), true))
                {
                    OpenThread(user.UserId);
                }

                break;
            case VelvetConnectionState.OutgoingRequest:
                if (ui.GhostButton(rect, Loc.T(L.Velvet.Requested)))
                {
                    store.CancelRequest(user.UserId);
                }

                break;
            case VelvetConnectionState.IncomingRequest:
                if (ui.PillButton(rect, Loc.T(L.Velvet.Reply), true))
                {
                    OpenThread(user.UserId);
                }

                break;
            case VelvetConnectionState.Blocked:
                break;
            default:
                if (ui.PillButton(rect, Loc.T(L.Velvet.IntroduceYourself), true))
                {
                    RequestIntro(user.UserId, DisplayNameOf(user.DisplayName, user.Handle));
                }

                break;
        }
    }

    private void DrawDisplayTokens(string[] tokens, VChipStyle style, Vector4 tone)
    {
        if (tokens.Length == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var models = new VChipModel[tokens.Length];
        for (var index = 0; index < tokens.Length; index++)
        {
            models[index] = new VChipModel(tokens[index], style, tone);
        }

        VChipFlow.Draw(models, width, scale);
    }
}
