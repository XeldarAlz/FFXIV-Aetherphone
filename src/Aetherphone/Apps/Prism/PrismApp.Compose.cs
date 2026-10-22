using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Game;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;

namespace Aetherphone.Apps.Prism;

internal readonly record struct CapturedMod(string Key, string Label);

internal sealed partial class PrismApp
{
    private const int TitleMaxLength = 60;
    private const int TagsMaxLength = 80;
    private const int MaxTags = 6;
    private const float FieldUnits = 34f;
    private const double RecaptureSeconds = 1.0;

    private readonly PhotoComposeSession composeSession;
    private readonly HashSet<string> droppedMods = new(StringComparer.Ordinal);
    private CapturedMod[] capturedMods = Array.Empty<CapturedMod>();
    private GlamGearDto[]? capturedModsFor;
    private string? capturedModsLanguage;
    private readonly Action<bool> postSettled;
    private GlamGearDto[] captured = Array.Empty<GlamGearDto>();
    private bool composeDetails;
    private bool focusTitle;
    private bool capturedVanilla;
    private string glamTitle = string.Empty;
    private string glamTags = string.Empty;
    private volatile int postOutcome;
    private string postingCover = string.Empty;
    private string postedCover = string.Empty;
    private string postedId = string.Empty;
    private int composeDraft;
    private int postingDraft;
    private CachedText capturedText;
    private double recaptureAt;

    private void StartCompose()
    {
        composeDraft++;
        droppedMods.Clear();
        composeSession.Open(false);
        composeSession.MaxPhotos = GlamStore.MaxPhotos;
        composeDetails = false;
        captured = Array.Empty<GlamGearDto>();
        glamTitle = string.Empty;
        glamTags = string.Empty;
        Push(PrismScreen.Compose);
    }

    private void SettlePost(bool succeeded) => postOutcome = succeeded ? OutcomeSent : OutcomeFailed;

    private void DrawCompose(ImDrawListPtr drawList, Rect content, float scale)
    {
        composeSession.ConsumePendingImport();
        var top = DrawHeader(drawList, content, string.Empty, Loc.T(L.Prism.PostGlam), true, scale);
        if (composeDetails)
        {
            DrawComposeDetails(drawList, content, top, scale);
            return;
        }

        var gutter = PrismArt.Gutter(scale);
        top += Typography.DrawWrappedLeft(new Vector2(content.Min.X + gutter, top), Loc.T(L.Prism.PhotosHint),
            PrismArt.Muted, TextStyles.Subheadline, content.Width - gutter * 2f) + Metrics.Space.Md * scale;
        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, PickStyle, GlamStore.PhotoRatio, true, true, Loc.T(L.Prism.PostGlam));
        var strip = StripRect(content, scale);
        if (PrismArt.Strip(drawList, strip, Loc.T(L.Common.Next), Arrow, true, composeSession.HasSelection))
        {
            captured = glamour.Capture();
            capturedVanilla = GlamourService.IsVanilla(captured);
            composeDetails = true;
            focusTitle = true;
        }

        var gridRect = new Rect(new Vector2(content.Min.X, pane.Max.Y + Metrics.Space.Md * scale),
            new Vector2(content.Max.X, strip.Min.Y - Metrics.Space.Lg * scale));
        using (AppSurface.BeginEdgeToEdge(gridRect))
        {
            composeSession.DrawPickGrid(gridRect, scale, PickStyle, true, Loc.T(L.Common.ImportFromPc),
                Loc.T(L.Prism.PostGlam));
        }
    }

    private void DrawComposeDetails(ImDrawListPtr drawList, Rect content, float top, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var width = content.Width - gutter * 2f;
        if (focusTitle)
        {
            ImGui.SetKeyboardFocusHere();
            focusTitle = false;
        }

        top = DrawField(drawList, left, top, width, Loc.T(L.Prism.TitleLabel), Loc.T(L.Prism.TitleHint), "##glamTitle",
            ref glamTitle, TitleMaxLength, scale);
        top = DrawField(drawList, left, top, width, Loc.T(L.Prism.TagsLabel), Loc.T(L.Prism.TagsHint), "##glamTags",
            ref glamTags, TagsMaxLength, scale);
        if (RecaptureDue())
        {
            captured = glamour.Capture();
            capturedVanilla = GlamourService.IsVanilla(captured);
        }

        var strip = StripRect(content, scale);
        var failure = string.Empty;
        var failureHeight = 0f;
        if (glams.PostFailed && !glams.Posting && postingDraft == composeDraft)
        {
            var reason = glams.PostFailureText;
            failure = reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint);
            failureHeight = Typography.MeasureWrappedBlock(failure, TextStyles.Subheadline, width).Y;
            Typography.DrawWrappedLeft(new Vector2(left, strip.Min.Y - Metrics.Space.Lg * scale - failureHeight),
                failure, PrismArt.Muted, TextStyles.Subheadline, width);
            failureHeight += Metrics.Space.Lg * scale;
        }

        var body = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, strip.Min.Y - Metrics.Space.Lg * scale - failureHeight));
        using (BeginBody(body))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var bodyWidth = ScrollLayout.StableContentWidth() - gutter * 2f;
            var cursor = DrawCaptured(list, origin.X + gutter, origin.Y, bodyWidth, scale);
            ImGui.Dummy(new Vector2(bodyWidth, cursor - origin.Y));
        }

        var label = Loc.T(glams.Posting ? L.Prism.Posting : L.Prism.Post);
        var ready = !string.IsNullOrWhiteSpace(glamTitle) && !glams.Posting;
        if (PrismArt.Strip(drawList, strip, label, Arrow, true, ready, UiSound.Shutter))
        {
            PostGlam();
        }
    }

    private float DrawStillTryingOn(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (!glamour.TryingOn)
        {
            return top;
        }

        top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.StillTryingOn), PrismArt.Fore,
            TextStyles.Subheadline, width) + Metrics.Space.Sm * scale;
        if (PrismArt.Link(drawList, new Vector2(left, top), Loc.T(L.Prism.TakeOff), PrismArt.Fore, scale))
        {
            glamour.Revert();
            recaptureAt = ImGui.GetTime() + RecaptureSeconds;
        }

        return top + Metrics.Size.TapTarget * scale;
    }

    private bool RecaptureDue()
    {
        if (recaptureAt <= 0d || ImGui.GetTime() < recaptureAt)
        {
            return false;
        }

        recaptureAt = 0d;
        return true;
    }

    private float DrawField(ImDrawListPtr drawList, float left, float top, float width, string label, string hint,
        string id, ref string text, int maxLength, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        PrismType.Draw(drawList, new Vector2(left, top), label, PrismArt.Muted, PrismFace.Label, labelPixels);
        top += PrismType.LineHeight(PrismFace.Label, labelPixels);
        return DrawInput(drawList, left, top, width, hint, id, ref text, maxLength, scale) + Metrics.Space.Lg * scale;
    }

    private float DrawInput(ImDrawListPtr drawList, float left, float top, float width, string hint, string id,
        ref string text, int maxLength, float scale)
    {
        var height = FieldUnits * scale;
        var field = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        GlassField.Text(field, id, hint, ref text, PrismArt.Fore, PrismArt.Muted, 0f, maxLength);
        PrismArt.Hairline(drawList, left, left + width, top + height, ImGui.IsItemActive() ? PrismArt.Fore : PrismArt.Faint,
            scale);
        return top + height;
    }

    private float DrawCaptured(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.CapturedHeading), PrismArt.Muted, PrismFace.Label,
            labelPixels);
        PrismType.DrawRight(drawList, new Vector2(left + width, top),
            Loc.T(capturedVanilla ? L.Prism.VanillaStamp : L.Prism.ModdedStamp), PrismArt.Fore, PrismFace.Label,
            labelPixels);
        top += labelLine;
        SyncCapturedMods();
        PrismType.Draw(drawList, new Vector2(left, top), CapturedLine(), PrismArt.Fore, PrismFace.Label, labelPixels);
        top += labelLine + Metrics.Space.Sm * scale;
        var note = PrismType.Sentences(Loc.T(glamour.HasPenumbra ? L.Prism.CapturedNote : L.Prism.PostsAsVanilla),
            TextStyles.Subheadline, width);
        top += Typography.DrawWrappedLeft(new Vector2(left, top), note, PrismArt.Muted, TextStyles.Subheadline,
            PrismType.BalancedWidth(note, TextStyles.Subheadline, width)) + Metrics.Space.Md * scale;
        top = DrawModRows(drawList, left, top, width, scale);
        return DrawStillTryingOn(drawList, left, top, width, scale) + PrismArt.BottomMargin(scale);
    }

    private float DrawModRows(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var leaveOut = Loc.T(L.Prism.LeaveOutMod);
        var addBack = Loc.T(L.Prism.AddBackMod);
        var actionWidth = MathF.Max(PrismType.Measure(leaveOut, PrismFace.Label, labelPixels).X,
            PrismType.Measure(addBack, PrismFace.Label, labelPixels).X);
        var rowHeight = Metrics.Size.TapTarget * scale;
        for (var index = 0; index < capturedMods.Length; index++)
        {
            var mod = capturedMods[index];
            var dropped = droppedMods.Contains(mod.Key);
            var textTop = top + (rowHeight - labelLine) * 0.5f;
            var room = width - actionWidth - Metrics.Space.Md * scale;
            PrismType.Draw(drawList, new Vector2(left, textTop), PrismType.FitLabel(mod.Label, labelPixels, room),
                dropped ? PrismArt.Muted : PrismArt.Fore, PrismFace.Label, labelPixels);
            var action = dropped ? addBack : leaveOut;
            var actionLeft = left + width - PrismType.Measure(action, PrismFace.Label, labelPixels).X;
            if (PrismArt.Link(drawList, new Vector2(actionLeft, textTop), action, PrismArt.Muted, scale, rowHeight))
            {
                if (dropped)
                {
                    droppedMods.Remove(mod.Key);
                }
                else
                {
                    droppedMods.Add(mod.Key);
                }
            }

            PrismArt.Hairline(drawList, left, left + width, top + rowHeight, PrismArt.Faint, scale);
            top += rowHeight;
        }

        if (capturedMods.Length > 0)
        {
            top += Metrics.Space.Lg * scale;
        }

        return top;
    }

    private void SyncCapturedMods()
    {
        if (ReferenceEquals(captured, capturedModsFor) && ReferenceEquals(Loc.Current.Code, capturedModsLanguage))
        {
            return;
        }

        capturedModsFor = captured;
        capturedModsLanguage = Loc.Current.Code;
        var mods = new List<CapturedMod>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < captured.Length; index++)
        {
            var pieceMods = captured[index].Mods ?? Array.Empty<GlamModDto>();
            for (var modIndex = 0; modIndex < pieceMods.Length; modIndex++)
            {
                var mod = pieceMods[modIndex];
                var key = PostedMods.Key(mod);
                if (seen.Add(key))
                {
                    mods.Add(new CapturedMod(key,
                        mod.Author.Length > 0 ? Loc.T(L.Prism.ModBy, mod.Name, mod.Author) : mod.Name));
                }
            }
        }

        capturedMods = mods.ToArray();
    }

    private string CapturedLine()
    {
        var pieces = 0;
        for (var index = 0; index < captured.Length; index++)
        {
            if (captured[index].ItemId != 0)
            {
                pieces++;
            }
        }

        var mods = KeptModCount();
        var key = HashCode.Combine(pieces, mods, Loc.Current.Code);
        return capturedText.IsCurrent(key)
            ? capturedText.Value
            : capturedText.Store(key, Loc.T(L.Prism.CapturedCounts, pieces, mods));
    }

    private void PostGlam()
    {
        captured = glamour.Capture(out var race, out var gender);
        capturedVanilla = GlamourService.IsVanilla(captured);
        var gear = PostedMods.Without(captured, droppedMods);
        var paths = composeSession.SelectedArray();
        var crops = composeSession.CropsArray();
        var count = Math.Min(paths.Length, GlamStore.MaxPhotos);
        var photos = new GlamPhotoDraft[count];
        for (var index = 0; index < count; index++)
        {
            photos[index] = new GlamPhotoDraft(paths[index], index < crops.Length ? crops[index] : default);
        }

        if (race == 0)
        {
            (race, gender) = Appearance();
        }

        postOutcome = OutcomePending;
        postingDraft = composeDraft;
        postingCover = count > 0 ? photos[0].Path : string.Empty;
        glams.Post(photos, gear, glamTitle.Trim(), SplitTags(glamTags), race, gender, capturedVanilla,
            glamour.TryingOn ? wearingGlamId : string.Empty, postSettled);
    }

    private static string[] SplitTags(string text)
    {
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kept = new List<string>(Math.Min(parts.Length, MaxTags));
        for (var index = 0; index < parts.Length && kept.Count < MaxTags; index++)
        {
            var tag = parts[index].TrimStart('#').Trim().ToLowerInvariant();
            if (tag.Length > 0 && !kept.Contains(tag))
            {
                kept.Add(tag);
            }
        }

        return kept.ToArray();
    }

    private (int Race, int Gender) WornBody() =>
        glamour.TryBody(out var race, out var gender) ? (race, gender) : Appearance();

    private static (int Race, int Gender) Appearance()
    {
        var player = GameMemory.Attached ? Plugin.ObjectTable.LocalPlayer : null;
        return player is null
            ? (0, 0)
            : (player.Customize[(int)CustomizeIndex.Race], player.Customize[(int)CustomizeIndex.Gender]);
    }

    private void ConsumePostOutcome()
    {
        var outcome = postOutcome;
        if (outcome == OutcomePending)
        {
            return;
        }

        postOutcome = OutcomePending;
        if (outcome != OutcomeSent || glams.Posted is not { } created)
        {
            return;
        }

        postedId = created.Id;
        postedCover = postingCover;
        UiFeedback.Play(UiSound.Success);
        if (screen != PrismScreen.Compose || postingDraft != composeDraft)
        {
            return;
        }

        Go(PrismScreen.GlamDetail);
        ShowGlam(created);
    }
}
