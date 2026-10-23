using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float EditAvatarUnits = 88f;
    private const int EditAvatarSegments = 64;
    private const float BioFieldUnits = 96f;
    private const string HandlePrefix = "@";

    private string editName = string.Empty;
    private string editHandle = string.Empty;
    private string editBio = string.Empty;
    private GlamPhotoDraft? editPhoto;
    private bool editLoaded;
    private bool editFailed;
    private volatile int editOutcome;
    private CachedText bioCountText;
    private Action<bool>? profileSettled;

    private void OpenEditProfile()
    {
        editLoaded = false;
        editFailed = false;
        editPhoto = null;
        editOutcome = OutcomePending;
        Push(PrismScreen.EditProfile);
    }

    private void OpenProfilePhoto()
    {
        composeSession.Open(true);
        Push(PrismScreen.ProfilePhoto);
    }

    private void DrawEditProfile(ImDrawListPtr drawList, Rect content, float scale)
    {
        var outcome = editOutcome;
        if (outcome != OutcomePending)
        {
            editOutcome = OutcomePending;
            editFailed = outcome == OutcomeFailed;
            if (!editFailed)
            {
                GoBack();
                return;
            }
        }

        var me = glams.Me;
        var saving = glams.SavingProfile;
        var valid = me is not null && !string.IsNullOrWhiteSpace(editName) && SocialIdentity.IsHandleValid(editHandle);
        var top = DrawActionHeader(drawList, content, PhoneIcons.X, Loc.T(L.Common.Cancel), Loc.T(L.Prism.EditProfile),
            PhoneIcons.Check, Loc.T(L.Prism.Done), valid && !saving, saving ? Loc.T(L.Account.Saving) : string.Empty,
            UiSound.Tap, scale, out var save) + Metrics.Space.Sm * scale;
        if (me is null)
        {
            glams.EnsureMe();
            var loading = Loc.T(L.Common.Loading);
            Typography.Draw(drawList, content.Center - Typography.Measure(loading, PrismType.Caption) * 0.5f, loading,
                PrismArt.Muted, PrismType.Caption);
            return;
        }

        if (!editLoaded)
        {
            editLoaded = true;
            editName = me.DisplayName;
            editHandle = me.Handle;
            editBio = me.Bio;
            editBanner = OwnBanner() is { GlamId.Length: > 0 } banner ? banner : null;
            editBannerGlam = profileCover?.Id == editBanner?.GlamId ? profileCover : null;
        }

        if (save)
        {
            SaveProfile(me);
        }

        using (BeginBody(new Rect(new Vector2(content.Min.X, top), content.Max)))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursor = DrawEditAvatar(list, origin.X + width * 0.5f, origin.Y + Metrics.Space.Sm * scale, me, scale);
            cursor = DrawField(list, origin.X, cursor + Metrics.Space.Lg * scale, width,
                Loc.T(L.Account.DisplayNameLabel), string.Empty, "##prismEditName", ref editName,
                SocialProfilePages.DisplayNameMax, scale);
            cursor = DrawHandleField(list, origin.X, cursor, width, scale);
            cursor = DrawBioField(list, origin.X, cursor, width, scale);
            cursor = DrawBannerRow(list, origin.X, cursor, width, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        }
    }

    private float DrawEditAvatar(ImDrawListPtr drawList, float centerX, float top, GlamAuthorDto me, float scale)
    {
        var radius = EditAvatarUnits * scale * 0.5f;
        var center = new Vector2(centerX, top + radius);
        var extent = new Vector2(radius, radius);
        if (editPhoto is not null && composeSession.TryGetPreviewUv(0, 1f, false, out var texture, out var uv0, out var uv1))
        {
            drawList.AddImageRounded(texture.Handle, center - extent, center + extent, uv0, uv1, uint.MaxValue, radius);
        }
        else
        {
            var name = AuthorName(me);
            AvatarView.Draw(drawList, center, radius, PrismArt.Muted, Initials.Of(name), TextStyles.Title1.Scale,
                images.Avatar(me.AvatarUrl, radius * 2f), EditAvatarSegments);
        }

        var label = Loc.T(L.Prism.EditPicture);
        var pixels = PrismArt.LabelPixels();
        var size = PrismType.Measure(label, PrismFace.Label, pixels);
        var linkTop = center.Y + radius + Metrics.Space.Md * scale;
        if (PrismArt.Link(drawList, new Vector2(centerX - size.X * 0.5f, linkTop), label, PrismArt.Fore, scale) ||
            UiInteract.HoverClick(center - extent, center + extent))
        {
            OpenProfilePhoto();
        }

        return linkTop + size.Y;
    }

    private float DrawHandleField(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(Loc.T(L.Account.HandleLabel), width, PrismType.Caption), PrismArt.Muted,
            PrismType.Caption);
        top += Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xxs * scale;
        var field = new Rect(new Vector2(left, top), new Vector2(left + width, top + GlassField.HeightUnits * scale));
        SearchBar.Surface(drawList, field, controls);
        var inset = Metrics.Space.Md * scale;
        var prefix = Typography.Measure(HandlePrefix, TextStyles.Body);
        Typography.Draw(drawList,
            new Vector2(left + inset + ImGui.GetStyle().FramePadding.X, field.Center.Y - prefix.Y * 0.5f), HandlePrefix,
            PrismArt.Muted, TextStyles.Body);
        var valid = editHandle.Length == 0 || SocialIdentity.IsHandleValid(editHandle);
        if (GlassField.Text(field, "##prismEditHandle", string.Empty, ref editHandle,
                valid ? PrismArt.Fore : controls.Danger, PrismArt.Muted, inset + prefix.X,
                SocialIdentity.HandleMaxLength))
        {
            editHandle = editHandle.ToLowerInvariant();
        }

        var failure = editFailed ? glams.ProfileFailureText : string.Empty;
        var note = editFailed
            ? (failure.Length > 0 ? failure : Loc.T(L.Common.LoadFailedHint))
            : Loc.T(L.Account.HandleRules);
        top = field.Max.Y + Metrics.Space.Xs * scale;
        return top + Typography.DrawWrappedLeft(new Vector2(left, top), note,
            valid && !editFailed ? PrismArt.Muted : controls.Danger, PrismType.Caption, width) + Metrics.Space.Md * scale;
    }

    private float DrawBioField(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var key = HashCode.Combine(editBio.Length, Loc.Current.Code);
        var count = bioCountText.IsCurrent(key)
            ? bioCountText.Value
            : bioCountText.Store(key, string.Concat(NumberText.Group(editBio.Length), "/",
                NumberText.Group(SocialProfilePages.BioMax)));
        var countWidth = Typography.Measure(count, PrismType.Caption).X;
        Typography.Draw(drawList, new Vector2(left + width - countWidth, top), count, PrismArt.Muted, PrismType.Caption);
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(Loc.T(L.Prism.BioLabel), MathF.Max(1f, width - countWidth - Metrics.Space.Sm * scale),
                PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        top += Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xxs * scale;
        var box = new Rect(new Vector2(left, top), new Vector2(left + width, top + BioFieldUnits * scale));
        Squircle.Fill(drawList, box.Min, box.Max, SearchBar.HeightUnits * scale * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(controls, FillLevel.Tertiary)));
        var pad = Metrics.Space.Sm * scale;
        var inner = new Vector2(box.Width - pad * 2f, box.Height - pad * 2f);
        ImGui.SetCursorScreenPos(box.Min + new Vector2(pad, pad));
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent)
                   .Push(ImGuiCol.FrameBgHovered, AppSkin.Transparent)
                   .Push(ImGuiCol.FrameBgActive, AppSkin.Transparent)
                   .Push(ImGuiCol.Text, PrismArt.Fore))
        {
            SoftWrapField.Multiline("##prismEditBio", ref editBio, SocialProfilePages.BioMax, inner,
                inner.X - ImGui.GetStyle().FramePadding.X * 2f - Metrics.Space.Xxs * scale);
        }

        return box.Max.Y + Metrics.Space.Md * scale;
    }

    private void SaveProfile(GlamAuthorDto me)
    {
        var name = editName.Trim();
        var handle = editHandle.Trim();
        var bio = editBio.Trim();
        var banner = SameBanner(editBanner, OwnBanner()) ? null : editBanner ?? AutomaticBanner;
        if (editPhoto is null && banner is null && name == me.DisplayName && handle == me.Handle && bio == me.Bio)
        {
            GoBack();
            return;
        }

        editFailed = false;
        glams.UpdateProfile(name, handle, bio, editPhoto, banner,
            profileSettled ??= succeeded => editOutcome = succeeded ? OutcomeSent : OutcomeFailed);
    }

    private void DrawProfilePhoto(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (DrawPhotoPick(drawList, content, Loc.T(L.Prism.EditPicture), 1f, scale))
        {
            editPhoto = new GlamPhotoDraft(composeSession.FirstSelected, composeSession.CropsArray()[0]);
            GoBack();
        }
    }
}
