using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const int OnboardStepCount = 4;
    private const int OnboardStepIdentity = 0;
    private const int OnboardStepIntent = 1;
    private const int OnboardStepAbout = 2;
    private const int OnboardStepReady = 3;
    private const int OnboardIdentitySaved = 1;
    private const int OnboardIdentityRejected = 2;
    private const float OnboardChromeHeight = 132f;
    private const float OnboardFooterHeight = 96f;
    private const float OnboardProgressTop = 30f;
    private const float OnboardProgressHeight = 4f;
    private const float OnboardProgressGap = 6f;
    private const float OnboardProgressMaxWidth = 240f;
    private const float OnboardTitleTop = 68f;
    private const float OnboardSubtitleTop = 98f;
    private const float OnboardSubtitleMaxWidth = 300f;
    private const float OnboardBackInset = 24f;
    private const float OnboardCtaHeight = 50f;
    private const float OnboardCtaMaxWidth = 320f;
    private const float OnboardCtaBottom = 26f;
    private const float OnboardCtaDisabledFill = 0.28f;
    private const float OnboardCtaDisabledInk = 0.55f;
    private const float OnboardPhotoHeight = 200f;
    private const float OnboardPhotoBlock = 236f;
    private const float OnboardPhotoHintGap = 10f;
    private const float OnboardPhotoBadgeRadius = 14f;
    private const float OnboardPhotoBadgeInset = 0.72f;
    private const float OnboardFieldRowHeight = 50f;
    private const float OnboardFieldLabelWidth = 108f;
    private const float OnboardFieldPrefixGap = 2f;
    private const float OnboardFootGap = 10f;
    private const float OnboardFootInset = 4f;
    private const float OnboardIntroHeight = 120f;
    private const float OnboardIntentCardHeight = 72f;
    private const float OnboardIntentTile = 44f;
    private const float OnboardIntentGlyph = 22f;
    private const float OnboardIntentCheckRadius = 11f;
    private const float OnboardCoverAspect = 1.05f;
    private const float OnboardCoverMaxShare = 0.62f;
    private const float OnboardCoverInset = 12f;
    private const float OnboardSettingsRowHeight = 52f;
    private const float OnboardSegmentHeight = 34f;
    private const float OnboardSegmentPadBottom = 12f;

    private static readonly VelvetEditSection[] OnboardSections =
    {
        VelvetEditSection.Gender,
        VelvetEditSection.Sexuality,
        VelvetEditSection.Languages,
        VelvetEditSection.Role,
        VelvetEditSection.Kinks,
        VelvetEditSection.Limits,
        VelvetEditSection.Tags,
    };

    private static readonly int[] OnboardSlots = BuildOnboardSlots();

    private bool onboardSeeded;
    private int onboardStep;
    private int onboardWho;
    private bool onboardDiscoverable = true;
    private bool onboardPhotoEditing;
    private volatile bool onboardIdentityBusy;
    private volatile int onboardIdentityOutcome;
    private AepFailure onboardIdentityFailure;
    private readonly FailureSlot onboardIdentityError = new();
    private bool onboardHandleInvalid;
    private VelvetProfileDto? onboardPreview;
    private string onboardPreviewNameId = string.Empty;
    private string onboardPreviewMeta = string.Empty;

    private static int[] BuildOnboardSlots()
    {
        var slots = new int[OnboardSections.Length];
        for (var index = 0; index < OnboardSections.Length; index++)
        {
            slots[index] = Array.IndexOf(EditSections, OnboardSections[index]);
        }

        return slots;
    }

    private void DrawOnboarding(Rect area)
    {
        var scale = UiScale.Current;
        store.EnsureMe();
        SeedOnboarding();
        ConsumeIdentityOutcome();

        if (onboardPhotoEditing)
        {
            var overlay = SceneChrome.ScreenFrom(area, theme, scale);
            ui.Backdrop(overlay);
            var overlayContext = new PhoneContext(area, theme, navigation);
            if (cardPhotos.Draw(area, overlayContext, ui.Accent))
            {
                onboardPhotoEditing = false;
            }

            return;
        }

        var screen = SceneChrome.ScreenFrom(area, theme, scale);
        ui.Backdrop(screen);
        var drawList = ImGui.GetWindowDrawList();
        VelvetArt.Bloom(drawList, screen, theme.ScreenRounding * scale, 0.85f);

        DrawOnboardChrome(area);

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + OnboardChromeHeight * scale),
            new Vector2(area.Max.X, area.Max.Y - OnboardFooterHeight * scale));
        switch (onboardStep)
        {
            case OnboardStepIntent:
                DrawOnboardIntentStep(body);
                break;
            case OnboardStepAbout:
                DrawOnboardAboutStep(body);
                break;
            case OnboardStepReady:
                DrawOnboardReadyStep(body);
                break;
            default:
                DrawOnboardIdentityStep(body);
                break;
        }

        DrawOnboardFooter(area);
    }

    private void SeedOnboarding()
    {
        if (onboardSeeded || store.Me is not { } seed)
        {
            return;
        }

        BeginEditProfile();
        if (editLanguages == 0)
        {
            editLanguages = VelvetLanguages.Suggested();
            editSummariesDirty = true;
        }

        onboardWho = seed.WhoCanMessage;
        onboardDiscoverable = seed.Discoverable;
        onboardSeeded = true;
    }

    private void ConsumeIdentityOutcome()
    {
        var outcome = onboardIdentityOutcome;
        if (outcome == 0)
        {
            return;
        }

        onboardIdentityOutcome = 0;
        if (outcome == OnboardIdentitySaved)
        {
            onboardIdentityError.Clear();
            onboardStep = OnboardStepIntent;
            return;
        }

        onboardIdentityError.Set(onboardIdentityFailure.Failed
            ? onboardIdentityFailure
            : AepFailure.Transport(AepFailureKind.Offline));
    }

    private void DrawOnboardChrome(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();

        var trackWidth = MathF.Min(area.Width - 96f * scale, OnboardProgressMaxWidth * scale);
        var segGap = OnboardProgressGap * scale;
        var segWidth = (trackWidth - segGap * (OnboardStepCount - 1)) / OnboardStepCount;
        var segHeight = OnboardProgressHeight * scale;
        var trackLeft = area.Center.X - trackWidth * 0.5f;
        var trackY = area.Min.Y + OnboardProgressTop * scale;
        for (var index = 0; index < OnboardStepCount; index++)
        {
            var segMin = new Vector2(trackLeft + index * (segWidth + segGap), trackY);
            var segMax = new Vector2(segMin.X + segWidth, segMin.Y + segHeight);
            var color = index <= onboardStep ? VelvetTheme.Rose : VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.18f);
            Squircle.Fill(drawList, segMin, segMax, segHeight * 0.5f, color.Packed());
        }

        if (onboardStep > 0 && !onboardIdentityBusy)
        {
            var backCenter = new Vector2(area.Min.X + OnboardBackInset * scale, area.Min.Y + OnboardTitleTop * scale);
            if (VIcon.Button(backCenter, 16f * scale, PhoneIcons.ChevronLeft, VIcon.Header,
                    VelvetTheme.TitleInk, Loc.T(L.Velvet.Back), HoverLabelSide.Below))
            {
                onboardStep--;
            }
        }

        var title = onboardStep switch
        {
            OnboardStepIntent => Loc.T(L.Velvet.OnboardIntent),
            OnboardStepAbout => Loc.T(L.Velvet.ObTitleAbout),
            OnboardStepReady => Loc.T(L.Velvet.ObTitleReady),
            _ => Loc.T(L.Velvet.ObTitleIdentity),
        };
        var subtitle = onboardStep switch
        {
            OnboardStepIntent => Loc.T(L.Velvet.ObSubIntent),
            OnboardStepAbout => Loc.T(L.Velvet.ObSubAbout),
            OnboardStepReady => Loc.T(L.Velvet.ObSubReady),
            _ => Loc.T(L.Velvet.ObSubIdentity),
        };
        Typography.DrawCentered(new Vector2(area.Center.X, area.Min.Y + OnboardTitleTop * scale), title,
            VelvetTheme.TitleInk, TextStyles.Title1);
        Typography.DrawWrappedCentered(new Vector2(area.Center.X, area.Min.Y + OnboardSubtitleTop * scale), subtitle,
            VelvetTheme.MutedInk, TextStyles.Subheadline,
            MathF.Min(area.Width - 64f * scale, OnboardSubtitleMaxWidth * scale));
    }

    private void DrawOnboardFooter(Rect area)
    {
        var scale = UiScale.Current;
        var buttonWidth = MathF.Min(area.Width - 44f * scale, OnboardCtaMaxWidth * scale);
        var buttonHeight = OnboardCtaHeight * scale;
        var left = area.Center.X - buttonWidth * 0.5f;
        var top = area.Max.Y - buttonHeight - OnboardCtaBottom * scale;
        var rect = new Rect(new Vector2(left, top), new Vector2(left + buttonWidth, top + buttonHeight));
        var isLast = onboardStep == OnboardStepReady;
        var label = onboardIdentityBusy
            ? Loc.T(L.Velvet.Saving)
            : isLast ? Loc.T(L.Velvet.EnterVelvet) : Loc.T(L.Velvet.Continue);
        if (!DrawOnboardPrimary(rect, label, CanAdvanceOnboard()))
        {
            return;
        }

        AdvanceOnboard();
    }

    private void AdvanceOnboard()
    {
        switch (onboardStep)
        {
            case OnboardStepIdentity:
                ContinueFromIdentity();
                break;
            case OnboardStepAbout:
                BuildOnboardPreview();
                onboardStep = OnboardStepReady;
                break;
            case OnboardStepReady:
                FinishOnboarding();
                break;
            default:
                onboardStep++;
                break;
        }
    }

    private bool DrawOnboardPrimary(Rect rect, string label, bool enabled)
    {
        if (enabled)
        {
            return ui.PillButton(rect, label, true);
        }

        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f,
            VelvetTheme.Alpha(VelvetTheme.Rose, OnboardCtaDisabledFill).Packed());
        Typography.DrawCentered(rect.Center, label, VelvetTheme.Alpha(VelvetTheme.OnAccent, OnboardCtaDisabledInk),
            0.9f, FontWeight.SemiBold);
        return false;
    }

    private bool CanAdvanceOnboard() => onboardStep switch
    {
        OnboardStepIdentity => !onboardIdentityBusy && onboardSeeded && HasIdentityBasics(),
        OnboardStepIntent => VelvetIntent.Sanitize(editIntent) != 0,
        OnboardStepAbout => editIntro.Trim().Length > 0,
        _ => true,
    };

    private bool HasIdentityBasics() =>
        editDisplayName.Trim().Length > 0 && editHandle.Trim().Length > 0
        && store.Me is { Photos: { Length: > 0 } };

    private void ContinueFromIdentity()
    {
        var name = editDisplayName.Trim();
        var handle = editHandle.Trim();
        onboardHandleInvalid = !SocialIdentity.IsHandleValid(handle);
        if (onboardHandleInvalid || store.Me is not { } me)
        {
            return;
        }

        onboardIdentityError.Clear();
        if (name == me.DisplayName && handle == me.Handle)
        {
            onboardStep = OnboardStepIntent;
            return;
        }

        onboardIdentityBusy = true;
        onboardIdentityFailure = AepFailure.None;
        store.UpdateIdentity(name, handle, saved =>
        {
            onboardIdentityBusy = false;
            onboardIdentityOutcome = saved ? OnboardIdentitySaved : OnboardIdentityRejected;
        }, failure => onboardIdentityFailure = failure);
    }

    private void DrawOnboardIdentityStep(Rect body)
    {
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            Gap(4f);
            DrawOnboardCardPhoto();
            Gap(14f);
            DrawOnboardIdentityCard(width);
            Gap(OnboardFootGap);
            DrawOnboardIdentityFoot(width);
            Gap(24f);
        }
    }

    private void DrawOnboardCardPhoto()
    {
        var scale = UiScale.Current;
        var block = Reserve(OnboardPhotoBlock);
        var drawList = ImGui.GetWindowDrawList();
        var tileHeight = OnboardPhotoHeight * scale;
        var tileWidth = tileHeight * CardPhotoAspect;
        var min = new Vector2(block.Center.X - tileWidth * 0.5f, block.Min.Y);
        var max = new Vector2(min.X + tileWidth, min.Y + tileHeight);
        var photos = store.Me is { } me ? CardPhotos(me) : NoCardPhotos;
        var busy = store.CardPhotoBusy;
        var tapped = photos.Length > 0
            ? DrawCardPhotoTile(drawList, min, max, photos[0], true)
            : DrawEmptyPhotoSlot(drawList, min, max, true, busy);

        if (photos.Length > 0)
        {
            var badgeCenter = new Vector2(max.X - OnboardPhotoBadgeRadius * scale * OnboardPhotoBadgeInset,
                max.Y - OnboardPhotoBadgeRadius * scale * OnboardPhotoBadgeInset);
            drawList.AddCircleFilled(badgeCenter, (OnboardPhotoBadgeRadius + 2f) * scale,
                VelvetTheme.GroundBottom.Packed(), 24);
            drawList.AddCircleFilled(badgeCenter, OnboardPhotoBadgeRadius * scale, VelvetTheme.Rose.Packed(), 24);
            PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.Camera, VelvetTheme.OnAccent, VIcon.Small * scale);
        }
        else
        {
            var hint = Loc.T(L.Velvet.AddPhoto);
            var hintSize = Typography.Measure(hint, TextStyles.Footnote);
            Typography.Draw(drawList,
                new Vector2(block.Center.X - hintSize.X * 0.5f, max.Y + OnboardPhotoHintGap * scale), hint,
                VelvetTheme.RoseInk, TextStyles.Footnote);
        }

        if (tapped && !busy && photos.Length < MaxCardPhotos)
        {
            cardPhotos.Open();
            onboardPhotoEditing = true;
        }
    }

    private void DrawOnboardIdentityCard(float width)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = OnboardFieldRowHeight * scale;
        var card = VCard.Begin(drawList, width, rowHeight * 2f, scale, 0f);
        var labelWidth = OnboardFieldLabelWidth * scale;
        var right = card.ContentOrigin.X + card.ContentWidth;

        var nameTop = card.ContentOrigin.Y;
        DrawOnboardFieldLabel(drawList, card.ContentOrigin.X, nameTop, rowHeight, Loc.T(L.Velvet.DisplayNameLabel));
        DrawOnboardInput("##ob_name", card.ContentOrigin.X + labelWidth, right, nameTop, rowHeight,
            ref editDisplayName, SocialProfilePages.DisplayNameMax, ImGuiInputTextFlags.None, VelvetTheme.TitleInk);
        FeedCell.Hairline(drawList, card.ContentOrigin.X, right, nameTop + rowHeight, VelvetTheme.Hairline);

        var handleTop = nameTop + rowHeight;
        DrawOnboardFieldLabel(drawList, card.ContentOrigin.X, handleTop, rowHeight, Loc.T(L.Velvet.HandleLabel));
        var prefixSize = Typography.Measure("@", TextStyles.Body);
        Typography.Draw(drawList,
            new Vector2(card.ContentOrigin.X + labelWidth, handleTop + (rowHeight - prefixSize.Y) * 0.5f), "@",
            VelvetTheme.Faint, TextStyles.Body);
        var handleValid = !onboardHandleInvalid || SocialIdentity.IsHandleValid(editHandle);
        if (DrawOnboardInput("##ob_handle", card.ContentOrigin.X + labelWidth + prefixSize.X + OnboardFieldPrefixGap * scale,
                right, handleTop, rowHeight, ref editHandle, SocialIdentity.HandleMaxLength,
                ImGuiInputTextFlags.CharsNoBlank, handleValid ? VelvetTheme.TitleInk : VelvetTheme.Danger))
        {
            editHandle = editHandle.ToLowerInvariant();
            onboardHandleInvalid = false;
            onboardIdentityError.Clear();
        }

        VCard.End(card);
    }

    private static void DrawOnboardFieldLabel(ImDrawListPtr drawList, float left, float rowTop, float rowHeight,
        string label)
    {
        var size = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(left, rowTop + (rowHeight - size.Y) * 0.5f), label,
            VelvetTheme.MutedInk, TextStyles.Body);
    }

    private static bool DrawOnboardInput(string id, float left, float right, float rowTop, float rowHeight,
        ref string value, int maxLength, ImGuiInputTextFlags flags, Vector4 ink)
    {
        ImGui.SetCursorScreenPos(new Vector2(left, rowTop + rowHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, right - left));
        Plugin.Fonts.NoticeText(value);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ink))
        {
            return ImGui.InputText(id, ref value, maxLength, flags);
        }
    }

    private void DrawOnboardIdentityFoot(float width)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var failed = onboardIdentityError.Failed;
        var text = failed ? onboardIdentityError.Text() : Loc.T(L.Velvet.ObHandleRules);
        var ink = failed || onboardHandleInvalid ? VelvetTheme.Danger : VelvetTheme.Faint;
        var inset = OnboardFootInset * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + inset, origin.Y), text, ink,
            TextStyles.Footnote, MathF.Max(1f, width - inset * 2f));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawOnboardIntentStep(Rect body)
    {
        using (AppSurface.Begin(body))
        {
            Gap(2f);
            var defs = VelvetIntent.All;
            for (var index = 0; index < defs.Length; index++)
            {
                var def = defs[index];
                var rect = Reserve(OnboardIntentCardHeight);
                if (DrawOnboardIntentCard(rect, def, VelvetIntent.Has(editIntent, def.Flag)))
                {
                    editIntent = VelvetIntent.Toggle(editIntent, def.Flag);
                    editSummariesDirty = true;
                }

                Gap(10f);
            }

            Gap(20f);
        }
    }

    private bool DrawOnboardIntentCard(Rect rect, in VelvetIntentDef def, bool selected)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = Metrics.Radius.Card * scale;

        var fill = selected
            ? VelvetTheme.Alpha(def.Hue, hovered ? 0.24f : 0.18f)
            : hovered ? VelvetTheme.CardHi : VelvetTheme.Card;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, fill.Packed());
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            (selected ? VelvetTheme.Alpha(def.Hue, 0.85f) : VelvetTheme.CardStroke).Packed(),
            (selected ? Metrics.Stroke.Thin : Metrics.Stroke.Hairline) * scale);

        var tileSize = OnboardIntentTile * scale;
        var tileMin = new Vector2(rect.Min.X + 14f * scale, rect.Center.Y - tileSize * 0.5f);
        var tileMax = new Vector2(tileMin.X + tileSize, tileMin.Y + tileSize);
        Squircle.Fill(drawList, tileMin, tileMax, tileSize * 0.32f, def.Hue.Packed());
        PhoneIcon.Draw(drawList, new Vector2((tileMin.X + tileMax.X) * 0.5f, (tileMin.Y + tileMax.Y) * 0.5f),
            def.Glyph, VelvetTheme.OnAccent, OnboardIntentGlyph * scale);

        var textLeft = tileMax.X + 14f * scale;
        var textWidth = rect.Max.X - 46f * scale - textLeft;
        var blurb = Typography.FitText(Loc.T(def.Blurb), textWidth, TextStyles.Subheadline);
        var label = Typography.FitText(Loc.T(def.Label), textWidth, TextStyles.Headline);
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        var blurbSize = Typography.Measure(blurb, TextStyles.Subheadline);
        var textTop = rect.Center.Y - (labelSize.Y + 3f * scale + blurbSize.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), label, VelvetTheme.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + labelSize.Y + 3f * scale), blurb,
            VelvetTheme.MutedInk, TextStyles.Subheadline);

        DrawOnboardCheck(drawList, new Vector2(rect.Max.X - 26f * scale, rect.Center.Y), selected, def.Hue, scale);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static void DrawOnboardCheck(ImDrawListPtr drawList, Vector2 center, bool selected, Vector4 hue, float scale)
    {
        var radius = OnboardIntentCheckRadius * scale;
        if (!selected)
        {
            drawList.AddCircle(center, radius, VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.30f).Packed(), 24,
                Metrics.Stroke.Thin * scale);
            return;
        }

        drawList.AddCircleFilled(center, radius, hue.Packed(), 24);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Check, VelvetTheme.OnAccent, VIcon.Chip * scale);
    }

    private void DrawOnboardAboutStep(Rect body)
    {
        using (AppSurface.Begin(body))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            var width = ScrollLayout.StableContentWidth();
            Gap(4f);
            DrawOnboardIntroCard(width);
            SyncEditSummaries();
            var includesErp = VelvetIntent.IncludesErp(editIntent);
            for (var index = 0; index < OnboardSections.Length; index++)
            {
                var section = OnboardSections[index];
                if (!includesErp && IsErpSection(section))
                {
                    continue;
                }

                DrawEditSection(section, OnboardSlots[index]);
            }

            Gap(30f);
        }
    }

    private static bool IsErpSection(VelvetEditSection section) =>
        section is VelvetEditSection.Role or VelvetEditSection.Kinks or VelvetEditSection.Limits;

    private void DrawOnboardIntroCard(float width)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var fieldHeight = OnboardIntroHeight * scale;
        var card = VCard.Begin(drawList, width, fieldHeight, scale);
        ImGui.SetCursorScreenPos(card.ContentOrigin);
        var fieldSize = new Vector2(card.ContentWidth, fieldHeight);
        var framePad = ImGui.GetStyle().FramePadding;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, VelvetTheme.TitleInk))
        {
            var wrapWidth = fieldSize.X - framePad.X * 2f - 4f * scale;
            SoftWrapField.Multiline("##ob_intro", ref editIntro, EditIntroMaxLength, fieldSize, wrapWidth);
        }

        if (editIntro.Length == 0 && !ImGui.IsItemActive())
        {
            Typography.Draw(drawList, card.ContentOrigin + framePad, Loc.T(L.Velvet.IntroduceYourself),
                VelvetTheme.Faint, TextStyles.Body);
        }

        VCard.End(card);
    }

    private void BuildOnboardPreview()
    {
        if (store.Me is not { } me)
        {
            onboardPreview = null;
            return;
        }

        onboardPreview = me with
        {
            DisplayName = editDisplayName.Trim(),
            Handle = editHandle.Trim(),
            Intro = editIntro.Trim(),
            LookingFor = VelvetIntent.Sanitize(editIntent),
            Gender = VelvetGender.Sanitize(editGender),
            Sexuality = VelvetSexuality.Sanitize(editSexuality),
            Languages = VelvetLanguages.Sanitize(editLanguages),
        };
        onboardPreviewNameId = "velvet.onboard.name." + me.UserId;
        onboardPreviewMeta = CardMetaLine(onboardPreview);
    }

    private void DrawOnboardReadyStep(Rect body)
    {
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            Gap(4f);
            if (onboardPreview is { } preview && store.Me is { } me)
            {
                var drawList = ImGui.GetWindowDrawList();
                var inset = OnboardCoverInset * scale;
                var origin = ImGui.GetCursorScreenPos();
                var innerWidth = MathF.Max(1f, width - inset * 2f);
                var coverHeight = MathF.Min(innerWidth * OnboardCoverAspect, body.Height * OnboardCoverMaxShare);
                var cover = new Rect(new Vector2(origin.X + inset, origin.Y),
                    new Vector2(origin.X + inset + innerWidth, origin.Y + coverHeight));
                DrawCardCover(drawList, preview, CardPhotos(me), cover, scale, onboardPreviewNameId,
                    onboardPreviewMeta, false);
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, coverHeight));
            }

            Gap(VCard.Gap);
            DrawOnboardSettingsCard(width);
            Gap(30f);
        }
    }

    private void DrawOnboardSettingsCard(float width)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = OnboardSettingsRowHeight * scale;
        var segmentHeight = OnboardSegmentHeight * scale;
        var contentHeight = rowHeight * 2f + segmentHeight + OnboardSegmentPadBottom * scale;
        var card = VCard.Begin(drawList, width, contentHeight, scale, 0f);
        var left = card.ContentOrigin.X;
        var right = left + card.ContentWidth;

        var discoverTop = card.ContentOrigin.Y;
        var discoverRow = new Rect(new Vector2(left, discoverTop), new Vector2(right, discoverTop + rowHeight));
        VCard.RowLabel(drawList, discoverRow.Min, rowHeight, PhoneIcons.Compass, VelvetTheme.Rose,
            Loc.T(L.Velvet.DiscoverableLabel), card.ContentWidth - (VToggle.TrackWidth + Metrics.Space.Md) * scale,
            scale);
        onboardDiscoverable = VToggle.Draw(drawList, "velvetObDiscoverable", discoverRow, onboardDiscoverable, scale);
        FeedCell.Hairline(drawList, left, right, discoverTop + rowHeight, VelvetTheme.Hairline);

        var whoTop = discoverTop + rowHeight;
        VCard.RowLabel(drawList, new Vector2(left, whoTop), rowHeight, PhoneIcons.MessageCircle,
            VelvetTheme.Moonlight, Loc.T(L.Velvet.WhoCanMessage), card.ContentWidth, scale);
        var segmentTop = whoTop + rowHeight;
        FillWhoLabels();
        var who = VSegmented.Draw("velvetObWho",
            new Rect(new Vector2(left, segmentTop), new Vector2(right, segmentTop + segmentHeight)), whoLabels,
            onboardWho, scale);
        if (who >= 0)
        {
            onboardWho = who;
        }

        VCard.End(card);
    }

    private void FinishOnboarding()
    {
        configuration.VelvetOnboarded = true;
        configuration.VelvetOnboardedVersion = Configuration.VelvetOnboardVersion;
        configuration.Save();
        store.UpdateProfile(BuildEditRequest(onboardDiscoverable, onboardWho), _ => { });
    }
}
