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
    private const double HeaderSweepMs = 900d;
    private const float HeaderSweepArc = 1.6f;
    private const float HeaderGlyphShare = 0.475f;
    private const float ComposePhotoShare = 0.5f;
    private const float ComposePhotoHeightShare = 0.36f;
    private const int ModNameMaxLength = 80;
    private const int MaxNamedMods = 20;
    private const string ComposeRaceId = "prism.composeRace";
    private const string ComposeGenderId = "prism.composeGender";

    private readonly PhotoComposeSession composeSession;
    private readonly HashSet<string> droppedMods = new(StringComparer.Ordinal);
    private readonly ModDraft composeMods = new();
    private readonly ChipRail modTypeRail = new();
    private readonly string[] modTypeLabels = new string[GearSlots.All.Length + 1];
    private readonly bool[] modTypeActive = new bool[GearSlots.All.Length + 1];
    private readonly int[] modTypeSlots = new int[GearSlots.All.Length + 1];
    private readonly string[] kindLabels = new string[2];
    private CapturedMod[] capturedMods = Array.Empty<CapturedMod>();
    private GlamGearDto[]? capturedModsFor;
    private string? capturedModsLanguage;
    private readonly Action<bool> postSettled;
    private GlamGearDto[] captured = Array.Empty<GlamGearDto>();
    private bool composeDetails;
    private bool focusTitle;
    private bool? composeVanilla;
    private string glamTitle = string.Empty;
    private string glamTags = string.Empty;
    private volatile int postOutcome;
    private string postingCover = string.Empty;
    private string postedCover = string.Empty;
    private string postedId = string.Empty;
    private int composeDraft;
    private int postingDraft;

    private void StartCompose()
    {
        composeDraft++;
        composeSession.Open(false);
        composeSession.MaxPhotos = GlamStore.MaxPhotos;
        composeDetails = false;
        glamGear.Clear();
        composeMods.Clear();
        composeVanilla = null;
        composeRace = 0;
        composeGender = NoGender;
        glamTitle = string.Empty;
        glamTags = string.Empty;
        gearCatalog.EnsureBuilt();
        Push(PrismScreen.Compose);
    }

    private void SettlePost(bool succeeded) => postOutcome = succeeded ? OutcomeSent : OutcomeFailed;

    private void DrawCompose(ImDrawListPtr drawList, Rect content, float scale)
    {
        composeSession.ConsumePendingImport();
        if (composeDetails)
        {
            DrawComposeDetails(drawList, content, scale);
            return;
        }

        var top = DrawActionHeader(drawList, content, PhoneIcons.X, Loc.T(L.Common.Cancel), Loc.T(L.Prism.PostGlam),
            PhoneIcons.ChevronRight, Loc.T(L.Common.Next), composeSession.HasSelection, string.Empty, UiSound.Tap,
            scale, out var advance) + Metrics.Space.Sm * scale;
        if (advance)
        {
            composeDetails = true;
            focusTitle = true;
        }

        top += Typography.DrawWrappedLeft(new Vector2(content.Min.X, top), Loc.T(L.Prism.PhotosHint), PrismArt.Muted,
            PrismType.Body, content.Width) + Metrics.Space.Md * scale;
        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, PickStyle, GlamStore.PhotoRatio, GlamStore.RevealsWholePhoto, true,
            Loc.T(L.Prism.PostGlam));
        var gridRect = new Rect(new Vector2(content.Min.X, pane.Max.Y + Metrics.Space.Md * scale),
            new Vector2(content.Max.X, content.Max.Y - PrismArt.BottomMargin(scale)));
        using (AppSurface.BeginEdgeToEdge(gridRect, smoothWheel: true))
        {
            composeSession.DrawPickGrid(gridRect, scale, PickStyle, true, Loc.T(L.Common.ImportFromPc),
                Loc.T(L.Prism.PostGlam));
        }
    }

    private bool DrawPhotoPick(ImDrawListPtr drawList, Rect content, string title, float aspect, float scale)
    {
        composeSession.ConsumePendingImport();
        var top = DrawActionHeader(drawList, content, PhoneIcons.X, Loc.T(L.Common.Cancel), title, PhoneIcons.Check,
            Loc.T(L.Prism.UsePhoto), composeSession.HasSelection, string.Empty, UiSound.Tap, scale, out var chosen) +
                  Metrics.Space.Sm * scale;
        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, PickStyle, aspect, false, true, title);
        if (chosen)
        {
            return true;
        }

        var gridRect = new Rect(new Vector2(content.Min.X, pane.Max.Y + Metrics.Space.Md * scale),
            new Vector2(content.Max.X, content.Max.Y - PrismArt.BottomMargin(scale)));
        using (AppSurface.BeginEdgeToEdge(gridRect, smoothWheel: true))
        {
            composeSession.DrawPickGrid(gridRect, scale, PickStyle, false, Loc.T(L.Common.ImportFromPc), title);
        }

        return false;
    }

    private float DrawActionHeader(ImDrawListPtr drawList, Rect content, string leaveGlyph, string leave,
        string heading, string actionGlyph, string action, bool enabled, string busy, UiSound tapSound, float scale,
        out bool acted)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var row = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + tap));
        if (IconButton(drawList, new Vector2(row.Min.X + tap * 0.5f, row.Center.Y), leaveGlyph, leave, scale,
                HoverLabelSide.Below))
        {
            LeaveScreen();
        }

        acted = DrawHeaderAction(drawList, new Vector2(row.Max.X - tap * 0.5f, row.Center.Y), actionGlyph, action,
            enabled, busy, tapSound, scale);
        var room = MathF.Max(1f, row.Width - (tap + Metrics.Space.Sm * scale) * 2f);
        var style = FitStyle(heading, room, PrismType.Headline);
        var title = Typography.FitText(heading, room, style);
        var size = Typography.Measure(title, style);
        Typography.Draw(drawList, new Vector2(row.Center.X - size.X * 0.5f, row.Center.Y - size.Y * 0.5f), title,
            PrismArt.Fore, style);
        return row.Max.Y;
    }

    private bool DrawHeaderAction(ImDrawListPtr drawList, Vector2 center, string glyph, string label, bool enabled,
        string busy, UiSound tapSound, float scale)
    {
        var radius = Metrics.Size.TapTarget * 0.5f * scale;
        var reach = new Vector2(radius, radius);
        var rect = new Rect(center - reach, center + reach);
        if (enabled)
        {
            var key = ImGui.GetID(label);
            var hovered = UiInteract.Hover(rect.Min, rect.Max);
            var face = RoundButton.Surface(drawList, rect, controls, ButtonStyle.Prominent, true, hovered, key);
            PhoneIcon.Draw(drawList, center, glyph, PrismArt.OnFore, face.Face.Width * HeaderGlyphShare);
            HoverTooltip.Show(key, rect, label, hovered, HoverLabelSide.Below);
            return UiInteract.Click(rect.Min, rect.Max, hovered, tapSound);
        }

        RoundButton.Icon(drawList, center, radius, glyph, controls, ButtonStyle.Bare, enabled: false);
        if (busy.Length > 0)
        {
            ProgressRing.Sweep(center, radius - Metrics.Stroke.Ring * scale, Metrics.Stroke.Ring * scale,
                PrismArt.Fore, HeaderSweepMs, HeaderSweepArc, 1f, drawList);
        }

        HoverTooltip.Show(rect, busy.Length > 0 ? busy : label, HoverLabelSide.Below);
        return false;
    }

    private void DrawComposeDetails(ImDrawListPtr drawList, Rect content, float scale)
    {
        bodyRaceMenu.Gate();
        var bodyChosen = BodyChosen(composeRace, composeGender);
        var gearReady = composeVanilla != true || DraftPieces() > 0;
        var ready = !string.IsNullOrWhiteSpace(glamTitle) && !glams.Posting && composeVanilla is not null &&
                    bodyChosen && gearReady;
        var top = DrawActionHeader(drawList, content, PhoneIcons.ChevronLeft, Loc.T(L.Prism.Back),
            Loc.T(L.Prism.PostGlam), PhoneIcons.Check, Loc.T(L.Prism.Post), ready,
            glams.Posting ? Loc.T(L.Prism.Posting) : string.Empty, UiSound.Shutter, scale, out var post) +
                  Metrics.Space.Sm * scale;
        if (post)
        {
            PostGlam();
        }

        var left = content.Min.X;
        var width = content.Width;
        var bottom = content.Max.Y - PrismArt.BottomMargin(scale);
        var failureHeight = 0f;
        var notice = string.Empty;
        if (glams.PostFailed && !glams.Posting && postingDraft == composeDraft)
        {
            var reason = glams.PostFailureText;
            notice = reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint);
        }
        else if (composeVanilla is null)
        {
            notice = Loc.T(L.Prism.TrackNeeded);
        }
        else if (!bodyChosen)
        {
            notice = Loc.T(L.Prism.BodyNeeded);
        }
        else if (!gearReady)
        {
            notice = Loc.T(L.Prism.GearNeeded);
        }

        if (notice.Length > 0)
        {
            failureHeight = Typography.MeasureWrappedBlock(notice, PrismType.Body, width).Y;
            Typography.DrawWrappedLeft(new Vector2(left, bottom - failureHeight),
                notice, PrismArt.Muted, PrismType.Body, width);
            failureHeight += Metrics.Space.Lg * scale;
        }

        var body = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, bottom - failureHeight));
        using (BeginBody(body))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var bodyWidth = ScrollLayout.StableContentWidth();
            var cursor = DrawComposePhotos(list, origin.X, origin.Y, bodyWidth,
                content.Height * ComposePhotoHeightShare, scale) + Metrics.Space.Lg * scale;
            if (focusTitle)
            {
                ImGui.SetKeyboardFocusHere();
                focusTitle = false;
            }

            cursor = DrawField(list, origin.X, cursor, bodyWidth, Loc.T(L.Prism.TitleLabel), Loc.T(L.Prism.TitleHint),
                "##glamTitle", ref glamTitle, TitleMaxLength, scale);
            cursor = DrawField(list, origin.X, cursor, bodyWidth, Loc.T(L.Prism.TagsLabel), Loc.T(L.Prism.TagsHint),
                "##glamTags", ref glamTags, TagsMaxLength, scale);
            cursor = DrawTrackChoice(origin.X, cursor, bodyWidth, scale);
            cursor = DrawBodyField(list, origin.X, cursor, bodyWidth, ComposeRaceId, ComposeGenderId,
                ref composeRace, ref composeGender, scale);
            cursor = DrawChosenGear(list, origin.X, cursor, bodyWidth, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(bodyWidth, cursor - origin.Y));
        }

        DrawBodyRaceMenu(content, ref composeRace);
    }

    private float DrawComposePhotos(ImDrawListPtr drawList, float left, float top, float width, float maxHeight,
        float scale)
    {
        var count = composeSession.SelectedCount;
        if (count == 0)
        {
            return top;
        }

        var gap = Metrics.Space.Xs * scale;
        var cardWidth = MathF.Min(MathF.Min(width * ComposePhotoShare, maxHeight * GlamStore.PhotoRatio),
            (width - gap * (count - 1)) / count);
        var cardHeight = cardWidth / GlamStore.PhotoRatio;
        var start = left + (width - cardWidth * count - gap * (count - 1)) * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(start + index * (cardWidth + gap), top);
            var card = new Rect(min, min + new Vector2(cardWidth, cardHeight));
            if (composeSession.TryGetPreviewUv(index, GlamStore.PhotoRatio, GlamStore.RevealsWholePhoto,
                    out var texture, out var uv0, out var uv1))
            {
                drawList.AddImage(texture.Handle, card.Min, card.Max, uv0, uv1);
            }
            else
            {
                PrismArt.Placeholder(drawList, card, false, scale);
            }

            var hovered = HoverWash(drawList, card);
            if (UiInteract.Click(card.Min, card.Max, hovered))
            {
                composeSession.SelectCurrent(index);
                composeDetails = false;
            }
        }

        return top + cardHeight;
    }

    private float DrawField(ImDrawListPtr drawList, float left, float top, float width, string label, string hint,
        string id, ref string text, int maxLength, float scale)
    {
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(label, width, PrismType.Caption),
            PrismArt.Muted, PrismType.Caption);
        top += Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xxs * scale;
        return DrawInput(drawList, left, top, width, hint, id, ref text, maxLength, scale) + Metrics.Space.Md * scale;
    }

    private float DrawInput(ImDrawListPtr drawList, float left, float top, float width, string hint, string id,
        ref string text, int maxLength, float scale)
    {
        var field = new Rect(new Vector2(left, top), new Vector2(left + width, top + GlassField.HeightUnits * scale));
        SearchBar.Surface(drawList, field, controls);
        GlassField.Text(field, id, hint, ref text, PrismArt.Fore, PrismArt.Muted, Metrics.Space.Md * scale, maxLength);
        return field.Max.Y;
    }

    private float DrawTrackChoice(float left, float top, float width, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        kindLabels[0] = Loc.T(L.Prism.ModdedStamp);
        kindLabels[1] = Loc.T(L.Prism.VanillaStamp);
        var shown = composeVanilla switch
        {
            true => 1,
            false => 0,
            null => -1,
        };
        var picked = DrawTrackSegments("prism.composeKind",
            new Rect(new Vector2(left, top), new Vector2(left + width, top + tap)), kindLabels, shown, scale);
        if (picked >= 0)
        {
            composeVanilla = picked == 1;
        }

        return top + tap + Metrics.Space.Lg * scale;
    }

    private float DrawChosenGear(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        top = DrawSectionTitle(drawList, left, left + width, top + Metrics.Space.Sm * scale,
            Loc.T(L.Prism.PiecesHeading), string.Empty, scale) + Metrics.Space.Xs * scale;
        top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.GlamGearNote), PrismArt.Muted,
            PrismType.Body, width) + Metrics.Space.Md * scale;
        var fill = new Rect(new Vector2(left, top), new Vector2(left + width, top + Button.RegularHeight * scale));
        if (DrawButton(drawList, fill, Loc.T(L.Prism.FillFromWearing), ButtonStyle.Tinted, scale,
                id: "prism.compose.fill"))
        {
            FillFromWearing(glamGear);
        }

        top = DrawGearSlots(drawList, left, fill.Max.Y + Metrics.Space.Lg * scale, width, scale);
        if (composeVanilla != false)
        {
            return top + PrismArt.BottomMargin(scale);
        }

        top = DrawSectionTitle(drawList, left, left + width, top + Metrics.Space.Lg * scale,
            Loc.T(L.Prism.ModsHeading), string.Empty, scale) + Metrics.Space.Md * scale;
        top = DrawPickModsButton(drawList, left, top, width, composeMods, "prism.compose.pickMods", scale);
        return DrawNamedMods(drawList, left, top, width, composeMods, glamGear, "##glamMod", scale) +
               PrismArt.BottomMargin(scale);
    }

    private int SyncModTypes(GearDraft gear, ModDraft mods)
    {
        modTypeLabels[0] = Loc.T(L.Prism.CharacterHeading);
        modTypeSlots[0] = GearSlots.None;
        var count = 1;
        var found = mods.Slot == GearSlots.None;
        for (var index = 0; index < gear.Items.Length; index++)
        {
            if (gear.Items[index] == 0)
            {
                continue;
            }

            modTypeLabels[count] = Loc.T(SlotNames[index]);
            modTypeSlots[count] = (int)GearSlots.All[index];
            found |= modTypeSlots[count] == mods.Slot;
            count++;
        }

        if (!found)
        {
            mods.Slot = GearSlots.None;
        }

        for (var index = 0; index < count; index++)
        {
            modTypeActive[index] = modTypeSlots[index] == mods.Slot;
        }

        return count;
    }

    private string ModTypeName(int slot)
    {
        var index = Array.IndexOf(GearSlots.All, (GearSlot)slot);
        return Loc.T(index >= 0 ? SlotNames[index] : L.Prism.CharacterHeading);
    }

    private float DrawNamedMods(ImDrawListPtr drawList, float left, float top, float width, ModDraft mods,
        GearDraft gear, string inputId, float scale)
    {
        top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.ModTypeHint), PrismArt.Muted,
            PrismType.Body, width) + Metrics.Space.Sm * scale;
        var types = SyncModTypes(gear, mods);
        var chips = new Rect(new Vector2(left, top), new Vector2(left + width, top + ChipRail.RowHeight * scale));
        var tapped = modTypeRail.Draw(chips, ui, modTypeLabels.AsSpan(0, types), modTypeActive.AsSpan(0, types));
        if (tapped >= 0)
        {
            mods.Slot = modTypeSlots[tapped];
        }

        top = chips.Max.Y + Metrics.Space.Sm * scale;
        var add = Loc.T(L.Prism.AddMod);
        var addWidth = ButtonWidth([add], width * 0.5f, scale);
        var gap = Metrics.Space.Sm * scale;
        var name = mods.Name;
        var bottom = DrawInput(drawList, left, top, width - addWidth - gap, Loc.T(L.Prism.ModHint), inputId,
            ref name, ModNameMaxLength, scale);
        mods.Name = name;
        var ready = !string.IsNullOrWhiteSpace(name) && mods.Count < MaxNamedMods;
        if (DrawButton(drawList, new Rect(new Vector2(left + width - addWidth, top), new Vector2(left + width, bottom)),
                add, ButtonStyle.Tinted, scale, enabled: ready, id: "prism.addMod"))
        {
            mods.Add(new GlamModDto(name.Trim()), mods.Slot);
            mods.Name = string.Empty;
        }

        return DrawModList(drawList, left, bottom + Metrics.Space.Sm * scale, width, mods, scale);
    }

    private float DrawModList(ImDrawListPtr drawList, float left, float top, float width, ModDraft mods, float scale)
    {
        var removeWidth = Metrics.Size.TapTarget * scale;
        var nameHeight = Typography.LineHeight(PrismType.Headline);
        var captionHeight = Typography.LineHeight(PrismType.Caption);
        var rowHeight = MathF.Max(Metrics.Size.TapTarget * scale, nameHeight + captionHeight);
        var room = MathF.Max(1f, width - removeWidth - Metrics.Space.Md * scale);
        for (var index = 0; index < mods.Count; index++)
        {
            var textTop = top + (rowHeight - nameHeight - captionHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(left, textTop),
                Typography.FitText(mods.Mods[index].Name, room, PrismType.Headline), PrismArt.Fore,
                PrismType.Headline);
            Typography.Draw(drawList, new Vector2(left, textTop + nameHeight),
                Typography.FitText(ModTypeName(mods.Slots[index]), room, PrismType.Caption), PrismArt.Muted,
                PrismType.Caption);
            ImGui.PushID(index);
            var removed = RoundButton.Icon(drawList,
                new Vector2(left + width - removeWidth * 0.5f, top + rowHeight * 0.5f), removeWidth * 0.5f,
                PhoneIcons.X, controls, ButtonStyle.Bare, Loc.T(L.Prism.RemovePiece), glyphInk: PrismArt.Muted);
            ImGui.PopID();
            top += rowHeight;
            if (removed)
            {
                mods.RemoveAt(index--);
            }
        }

        return top;
    }

    private GlamGearDto[] ChosenGear()
    {
        var pieces = DraftOutfit();
        return composeVanilla == true ? pieces : composeMods.AttachTo(pieces);
    }

    private float DrawModRows(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var leaveOut = Loc.T(L.Prism.LeaveOutMod);
        var addBack = Loc.T(L.Prism.AddBackMod);
        var actionWidth = ButtonWidth([leaveOut, addBack], width * 0.5f, scale);
        var rowHeight = Metrics.Size.TapTarget * scale;
        var nameHeight = Typography.LineHeight(PrismType.Headline);
        var room = MathF.Max(1f, width - actionWidth - Metrics.Space.Md * scale);
        for (var index = 0; index < capturedMods.Length; index++)
        {
            var mod = capturedMods[index];
            var dropped = droppedMods.Contains(mod.Key);
            Typography.Draw(drawList, new Vector2(left, top + (rowHeight - nameHeight) * 0.5f),
                Typography.FitText(mod.Label, room, PrismType.Headline), dropped ? PrismArt.Muted : PrismArt.Fore,
                PrismType.Headline);
            var action = new Rect(new Vector2(left + width - actionWidth, top), new Vector2(left + width, top + rowHeight));
            if (DrawButton(drawList, action, dropped ? addBack : leaveOut, ButtonStyle.Plain, scale, id: mod.Key))
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

    private void PostGlam()
    {
        var vanilla = composeVanilla == true;
        var gear = ChosenGear();
        var paths = composeSession.SelectedArray();
        var crops = composeSession.CropsArray();
        var count = Math.Min(paths.Length, GlamStore.MaxPhotos);
        var photos = new GlamPhotoDraft[count];
        for (var index = 0; index < count; index++)
        {
            photos[index] = new GlamPhotoDraft(paths[index], index < crops.Length ? crops[index] : default);
        }

        postOutcome = OutcomePending;
        postingDraft = composeDraft;
        postingCover = count > 0 ? photos[0].Path : string.Empty;
        glams.Post(photos, gear, glamTitle.Trim(), SplitTags(glamTags), composeRace, composeGender, vanilla,
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
