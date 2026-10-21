using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Report;
using Aetherphone.Core.Sharing;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float MutedOnPhoto = 0.72f;
    private const float ThumbUnits = 44f;
    private const float DetailTitleUnits = 28f;
    private const float DetailTitleMinUnits = 20f;
    private const string Arrow = "→";

    private readonly Dictionary<string, string> handleLines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> tagLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, (string Name, uint Icon)> itemRows = new();
    private readonly HashSet<uint> ownedIds = new();
    private string? statsFor;
    private bool statsStorage;
    private CachedText ownedText;
    private CachedText modsText;
    private CachedText triedText;
    private CachedText bodyText;
    private CachedText moreText;
    private int ownedPieces;
    private int totalPieces;
    private int missingMods;
    private int totalMods;
    private int photoIndex;

    private void OpenGlam(GlamDto glam)
    {
        Push(PrismScreen.GlamDetail);
        ShowGlam(glam);
    }

    private void ShowGlam(GlamDto glam)
    {
        glams.Open(glam);
        photoIndex = 0;
        statsFor = null;
    }

    private IDalamudTextureWrap? ViewerPage(int index) =>
        glams.Detail?.Photos is { } photos && index < photos.Length ? images.Get(photos[index].Url) : null;

    private static string WearKey(GlamDto glam) =>
        glam.Id.Length > 0 ? glam.Id : glam.Photos is { Length: > 0 } photos ? photos[0].Url : string.Empty;

    private bool GearItem(uint itemId, out string name, out uint iconId)
    {
        if (!itemRows.TryGetValue(itemId, out var row))
        {
            if (!gameData.TryGetItem(itemId, out var found, out var icon, out _))
            {
                name = string.Empty;
                iconId = 0;
                return false;
            }

            row = (found, icon);
            itemRows[itemId] = row;
        }

        name = row.Name;
        iconId = row.Icon;
        return true;
    }

    private string AuthorLine(GlamAuthorDto? author)
    {
        if (author is null || author.Handle.Length == 0)
        {
            return author?.DisplayName ?? string.Empty;
        }

        if (!handleLines.TryGetValue(author.Handle, out var line))
        {
            line = "@" + author.Handle;
            handleLines[author.Handle] = line;
        }

        return line;
    }

    private void DrawGlamDetail(ImDrawListPtr drawList, Rect content, float scale)
    {
        var pending = glams.DetailPending;
        var missing = glams.DetailMissing;
        if (glams.Detail is not { } glam)
        {
            GoBack();
            return;
        }

        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var gutter = PrismArt.Gutter(scale);
        if (pending || missing)
        {
            top += Typography.DrawWrappedLeft(new Vector2(content.Min.X + gutter, top),
                Loc.T(missing ? L.Common.LoadFailedHint : L.Common.Loading), PrismArt.Muted, TextStyles.Subheadline,
                content.Width - gutter * 2f) + Metrics.Space.Md * scale;
            if (missing && PrismArt.Link(drawList, new Vector2(content.Min.X + gutter, top), Loc.T(L.Common.Retry),
                    PrismArt.Fore, scale))
            {
                glams.Open(glam);
            }

            return;
        }

        RefreshStats(glam);
        DrawGlamMenu(drawList, content, glam, scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var cursor = DrawGlamPhotos(list, left, origin.Y, width, glam, scale);
        cursor = DrawGlamHeading(list, left, cursor, width, glam, scale);
        cursor = DrawGlamActions(list, left, cursor, width, glam, scale);
        cursor = DrawGear(list, left, cursor, width, glam, scale);
        cursor = DrawMoreFrom(list, left, cursor, width, glam, scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private void RefreshStats(GlamDto glam)
    {
        var key = WearKey(glam);
        var storage = OwnedItems.StorageLoaded;
        if (statsFor == key && statsStorage == storage)
        {
            return;
        }

        statsFor = key;
        statsStorage = storage;
        fashionFor = null;
        modLines.Clear();
        var gear = glam.Gear ?? Array.Empty<GlamGearDto>();
        ownedPieces = GlamourService.OwnedPieces(gear, out totalPieces, ownedIds);
        missingMods = glamour.MissingMods(gear, out totalMods);
    }

    private void DrawGlamMenu(ImDrawListPtr drawList, Rect content, GlamDto glam, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var gutter = PrismArt.Gutter(scale);
        var top = content.Min.Y + HeaderGapUnits * scale;
        var width = PrismType.Measure(MoreGlyph, PrismFace.Label, labelPixels).X;
        if (glam.Id.Length == 0 ||
            !PrismArt.Link(drawList, new Vector2(content.Max.X - gutter - width, top), MoreGlyph, PrismArt.Muted, scale))
        {
            return;
        }

        if (glam.Author?.Id == glams.MyId)
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Prism.DeleteGlamTitle),
                Message = Loc.T(L.Prism.DeleteGlamBody),
                ConfirmLabel = Loc.T(L.Common.Delete),
                CancelLabel = Loc.T(L.Common.Cancel),
                Danger = true,
                Confirm = () =>
                {
                    glams.Delete(glam);
                    GoBack();
                },
            });
            return;
        }

        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Prism.ReportGlam),
            Submit = (reason, done) => glams.Report(glam.Id, reason, done),
        });
    }

    private float DrawGlamPhotos(ImDrawListPtr drawList, float left, float top, float width, GlamDto glam, float scale)
    {
        var photos = glam.Photos ?? Array.Empty<GlamPhotoDto>();
        var height = width / GlamStore.PhotoRatio;
        var frame = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        drawList.AddRectFilled(frame.Min, frame.Max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Fore, PickFillAlpha)));
        photoIndex = Math.Clamp(photoIndex, 0, Math.Max(0, photos.Length - 1));
        if (photos.Length > 0)
        {
            DrawLook(drawList, frame, photos[photoIndex].Url, false);
            var hovered = UiInteract.Hover(frame.Min, frame.Max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(frame.Min, frame.Max, hovered))
            {
                photoViewer.Open(this, photos.Length, photoIndex, viewerPage);
            }
        }

        var cursor = frame.Max.Y + Metrics.Space.Sm * scale;
        if (photos.Length < 2)
        {
            return cursor + Metrics.Space.Md * scale;
        }

        var thumb = ThumbUnits * scale;
        for (var index = 0; index < photos.Length; index++)
        {
            var min = new Vector2(left + index * (thumb + Metrics.Space.Sm * scale), cursor);
            var max = new Vector2(min.X + thumb, min.Y + thumb / GlamStore.PhotoRatio);
            DrawLook(drawList, new Rect(min, max), photos[index].Url, false);
            drawList.AddRect(min, max, ImGui.GetColorU32(index == photoIndex ? PrismArt.Fore : PrismArt.Faint), 0f,
                ImDrawFlags.None, 1f);
            var hovered = UiInteract.Hover(min, max);
            if (UiInteract.Click(min, max, hovered))
            {
                photoIndex = index;
            }
        }

        return cursor + thumb / GlamStore.PhotoRatio + Metrics.Space.Lg * scale;
    }

    private float DrawGlamHeading(ImDrawListPtr drawList, float left, float top, float width, GlamDto glam, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var pixels = PrismType.Fit(glam.Title, PrismFace.Headline, width, DetailTitleUnits * scale,
            DetailTitleMinUnits * scale, 2, out var lines);
        top += PrismType.DrawLines(drawList, new Vector2(left, top), lines, PrismArt.Fore, PrismFace.Headline, pixels) +
               Metrics.Space.Xs * scale;
        var author = AuthorLine(glam.Author);
        if (glam.Author is not { Id.Length: > 0 } who)
        {
            PrismType.Draw(drawList, new Vector2(left, top), author, PrismArt.Muted, PrismFace.Label, labelPixels);
        }
        else if (PrismArt.Link(drawList, new Vector2(left, top), author, PrismArt.Muted, scale,
                     labelLine + Metrics.Space.Sm * scale))
        {
            OpenProfile(who);
        }

        var stamp = Loc.T(glam.Vanilla ? L.Prism.VanillaStamp : L.Prism.ModdedStamp);
        PrismType.DrawRight(drawList, new Vector2(left + width, top), stamp, PrismArt.Fore, PrismFace.Label, labelPixels);
        top += labelLine;
        if (glam.TryOns > 0)
        {
            var key = HashCode.Combine(glam.TryOns, Loc.Current.Code);
            var tried = triedText.IsCurrent(key)
                ? triedText.Value
                : triedText.Store(key, Loc.Plural(L.Prism.TriedOnCount, (int)Math.Min(glam.TryOns, int.MaxValue)));
            PrismType.Draw(drawList, new Vector2(left, top), tried, PrismArt.Muted, PrismFace.Label, labelPixels);
        }

        var body = BodyLine(glam.Race, glam.Gender);
        if (body.Length > 0)
        {
            PrismType.DrawRight(drawList, new Vector2(left + width, top), body, PrismArt.Muted, PrismFace.Label,
                labelPixels);
        }

        if (glam.TryOns > 0 || body.Length > 0)
        {
            top += labelLine;
        }

        top = DrawTags(drawList, left, top, width, glam.Tags, scale);
        if (glam.InspiredBy.Length > 0)
        {
            top += Metrics.Space.Sm * scale;
            if (PrismArt.Link(drawList, new Vector2(left, top), Loc.T(L.Prism.InspiredBy), PrismArt.Muted, scale,
                    labelLine + Metrics.Space.Sm * scale))
            {
                OpenGlam(new GlamDto(glam.InspiredBy));
            }

            top += labelLine;
        }

        return top + Metrics.Space.Lg * scale;
    }

    private float DrawTags(ImDrawListPtr drawList, float left, float top, float width, string[]? tags, float scale)
    {
        if (tags is not { Length: > 0 })
        {
            return top;
        }

        var labelPixels = PrismArt.LabelPixels();
        var gap = Metrics.Space.Lg * scale;
        top += Metrics.Space.Sm * scale;
        var x = left;
        for (var index = 0; index < tags.Length; index++)
        {
            var label = TagLabel(tags[index]);
            var tagWidth = PrismType.Measure(label, PrismFace.Label, labelPixels).X;
            if (x + tagWidth > left + width)
            {
                break;
            }

            if (PrismArt.Link(drawList, new Vector2(x, top), label, PrismArt.Fore, scale,
                    PrismType.LineHeight(PrismFace.Label, labelPixels) + Metrics.Space.Sm * scale))
            {
                OpenTagGlams(tags[index]);
            }

            x += tagWidth + gap;
        }

        return top + PrismType.LineHeight(PrismFace.Label, labelPixels);
    }

    private string TagLabel(string tag)
    {
        if (!tagLabels.TryGetValue(tag, out var label))
        {
            label = "#" + tag;
            tagLabels[tag] = label;
        }

        return label;
    }

    private float DrawMoreFrom(ImDrawListPtr drawList, float left, float top, float width, GlamDto glam, float scale)
    {
        if (glam.Id.Length == 0)
        {
            return top;
        }

        glams.OpenMoreFrom(glam);
        var more = glams.MoreFrom(glam.Id);
        if (more.Length == 0 || glam.Author is not { } author)
        {
            return top;
        }

        var name = author.DisplayName.Length > 0 ? author.DisplayName : AuthorLine(author);
        var key = HashCode.Combine(name, Loc.Current.Code);
        var title = moreText.IsCurrent(key) ? moreText.Value : moreText.Store(key, Loc.T(L.Prism.MoreFrom, name));
        top = DrawSectionLabel(drawList, left, top + Metrics.Space.Xl * scale, width, 2, title, scale);
        return DrawGlamGrid(drawList, left, top, width, more, scale);
    }

    private string BodyLine(int race, int gender)
    {
        if (race <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(race, gender, Loc.Current.Code);
        if (bodyText.IsCurrent(key))
        {
            return bodyText.Value;
        }

        var name = gameData.RaceName((uint)race, gender == 1);
        return bodyText.Store(key,
            name.Length == 0 ? string.Empty : Loc.T(gender == 1 ? L.Prism.BodyFemale : L.Prism.BodyMale, name));
    }

    private float DrawGlamActions(ImDrawListPtr drawList, float left, float top, float width, GlamDto glam, float scale)
    {
        var height = PrismArt.StripUnits * scale;
        var gap = Metrics.Space.Md * scale;
        var listed = glam.Id.Length > 0;
        var half = listed ? (width - gap) * 0.5f : width;
        var tryOn = new Rect(new Vector2(left, top), new Vector2(left + half, top + height));
        var save = new Rect(new Vector2(left + half + gap, top), new Vector2(left + width, top + height));
        var key = WearKey(glam);
        var wearing = Wearing(key);
        if (PrismArt.Strip(drawList, tryOn, Loc.T(wearing ? L.Prism.TakeOff : L.Prism.TryOnGlam), string.Empty,
                true))
        {
            if (wearing)
            {
                glamour.Revert();
            }
            else
            {
                glamour.TryOn(glam.Gear ?? Array.Empty<GlamGearDto>());
                StartWearing(key, glam.Title);
                if (listed)
                {
                    wearingGlamId = glam.Id;
                    glams.TriedOn(glam);
                }
            }
        }

        if (listed && PrismArt.Strip(drawList, save, Loc.T(glam.Saved ? L.Prism.Saved : L.Prism.Save), string.Empty,
                false))
        {
            glams.ToggleSave(glam);
        }

        top = tryOn.Max.Y + Metrics.Space.Md * scale;
        var labelPixels = PrismArt.LabelPixels();
        var row = PrismType.LineHeight(PrismFace.Label, labelPixels) + Metrics.Space.Sm * scale;
        if (listed && PrismArt.Link(drawList, new Vector2(left, top), Loc.T(glam.Liked ? L.Prism.Liked : L.Prism.Like),
                glam.Liked ? PrismArt.Fore : PrismArt.Muted, scale, row))
        {
            glams.ToggleLike(glam);
        }

        var shareable = listed && postedCover.Length > 0 && glam.Id == postedId;
        var leftRows = (listed ? 2 : 1) + (shareable ? 1 : 0);
        DrawWardrobeLink(drawList, left, top + (listed ? row : 0f), glam, key, row, scale);
        if (shareable && PrismArt.Link(drawList, new Vector2(left, top + row * (leftRows - 1)), Loc.T(L.Share.Action),
                PrismArt.Fore, scale, row))
        {
            share.Offer(new ShareItem(ShareKind.Photo, postedCover, Id));
        }

        var lines = 0;
        if (totalPieces > 0)
        {
            PrismType.DrawRight(drawList, new Vector2(left + width, top), OwnedLine(), PrismArt.Muted, PrismFace.Label,
                labelPixels);
            lines++;
        }

        if (totalMods > 0)
        {
            PrismType.DrawRight(drawList, new Vector2(left + width, top + row * lines),
                glamour.HasPenumbra ? ModsLine() : Loc.T(L.Prism.ModsNeedPenumbra), PrismArt.Muted, PrismFace.Label,
                labelPixels);
            lines++;
        }

        top += row * Math.Max(lines, leftRows) + Metrics.Space.Sm * scale;
        LocString? note = !glamour.HasGlamourer ? L.Prism.TryOnWithoutGlamourer
            : ownedPieces < totalPieces && !OwnedItems.StorageLoaded ? L.Prism.OpenStorage : null;
        if (note is { } shown)
        {
            top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(shown), PrismArt.Muted, TextStyles.Footnote,
                width) + Metrics.Space.Md * scale;
        }

        return top + Metrics.Space.Lg * scale;
    }

    private void DrawWardrobeLink(ImDrawListPtr drawList, float left, float top, GlamDto glam, string key, float row,
        float scale)
    {
        wardrobe.Ensure();
        if (wardrobe.Loading)
        {
            return;
        }

        var saved = wardrobe.FindByGlam(key);
        var label = Loc.T(saved is null ? L.Prism.AddToWardrobe : L.Prism.InWardrobe);
        if (!PrismArt.Link(drawList, new Vector2(left, top), label, PrismArt.Muted, scale, row))
        {
            return;
        }

        if (saved is not null)
        {
            Push(PrismScreen.WardrobeOutfit);
            outfitId = saved.Id;
            renaming = false;
            return;
        }

        wardrobe.Add(new WardrobeOutfit(Guid.NewGuid().ToString("N"), glam.Title, string.Empty, Guid.Empty,
            glam.Gear ?? Array.Empty<GlamGearDto>(), false, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), key));
    }

    private string OwnedLine()
    {
        var key = HashCode.Combine(ownedPieces, totalPieces, Loc.Current.Code);
        return ownedText.IsCurrent(key)
            ? ownedText.Value
            : ownedText.Store(key, Loc.T(L.Prism.OwnPieces, ownedPieces, totalPieces));
    }

    private string ModsLine()
    {
        var key = HashCode.Combine(totalMods - missingMods, totalMods, Loc.Current.Code);
        return modsText.IsCurrent(key)
            ? modsText.Value
            : modsText.Store(key, Loc.T(L.Prism.ModsInstalled, totalMods - missingMods, totalMods));
    }

    private float DrawGear(ImDrawListPtr drawList, float left, float top, float width, GlamDto glam, float scale)
    {
        var gear = glam.Gear ?? Array.Empty<GlamGearDto>();
        if (gear.Length == 0)
        {
            return top;
        }

        top = DrawSectionLabel(drawList, left, top, width, 0, Loc.T(L.Prism.GearHeading), scale);
        top = DrawGearList(drawList, left, top, width, gear, true, scale);
        if (glam.WeekId.Length > 0)
        {
            top += Typography.DrawWrappedLeft(new Vector2(left, top), Loc.T(L.Prism.OutfitCaveat), PrismArt.Muted,
                TextStyles.Footnote, width);
        }

        return top;
    }

    private float DrawGearList(ImDrawListPtr drawList, float left, float top, float width, GlamGearDto[] gear,
        bool interactive, float scale)
    {
        for (var index = 0; index < gear.Length; index++)
        {
            if (gear[index].ItemId != 0)
            {
                top = DrawGearRow(drawList, left, top, width, gear[index], interactive, scale);
            }
        }

        for (var index = 0; index < gear.Length; index++)
        {
            if (gear[index].ItemId == 0 && gear[index].Mods is { Length: > 0 } mods)
            {
                top = DrawSectionLabel(drawList, left, top + Metrics.Space.Lg * scale, width, 1,
                    Loc.T(L.Prism.CharacterHeading), scale);
                top = DrawModChips(drawList, left, top, width, mods, interactive, scale);
            }
        }

        return top;
    }

    private float DrawGearRow(ImDrawListPtr drawList, float left, float top, float width, GlamGearDto piece,
        bool interactive, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var icon = GearIconUnits * scale;
        if (GearItem(piece.ItemId, out var name, out var iconId))
        {
            GameIconTile.Draw(drawList, textures, iconId, new Vector2(left, top), new Vector2(left + icon, top + icon), 0f,
                scale);
        }

        var textLeft = left + icon + Metrics.Space.Md * scale;
        var tryLabel = Loc.T(L.Prism.TryOnGlam);
        var tryWidth = interactive ? PrismType.Measure(tryLabel, PrismFace.Label, labelPixels).X : 0f;
        var room = width - (textLeft - left) - (interactive ? tryWidth + Metrics.Space.Md * scale : 0f);
        var fitted = PrismType.FitLabel(name, labelPixels, room);
        if (!interactive)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, top), fitted, PrismArt.Fore, PrismFace.Label, labelPixels);
        }
        else if (PrismArt.Link(drawList, new Vector2(textLeft, top), fitted, PrismArt.Fore, scale))
        {
            OpenItemGlams(piece.ItemId);
        }

        if (interactive &&
            PrismArt.Link(drawList, new Vector2(left + width - tryWidth, top), tryLabel, PrismArt.Muted, scale))
        {
            glamour.TryOnPiece(piece);
            StartWearing(string.Empty, name);
        }

        if (interactive && screen is PrismScreen.GlamDetail or PrismScreen.FashionReport &&
            ownedIds.Contains(piece.ItemId))
        {
            PrismType.DrawRight(drawList, new Vector2(left + width, top + labelLine), Loc.T(L.Prism.Owned), PrismArt.Muted,
                PrismFace.Label, labelPixels);
        }

        var dyes = DyeLine(piece.Stain0, piece.Stain1);
        if (dyes.Length > 0)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, top + labelLine), PrismType.FitLabel(dyes, labelPixels, room),
                PrismArt.Muted, PrismFace.Label, labelPixels);
        }

        var bottom = top + MathF.Max(icon, labelLine * 2f) + Metrics.Space.Sm * scale;
        if (piece.Mods is { Length: > 0 } mods)
        {
            bottom = DrawModChips(drawList, textLeft, bottom, width - (textLeft - left), mods, interactive, scale);
        }

        PrismArt.Hairline(drawList, left, left + width, bottom, PrismArt.Faint, scale);
        return bottom + Metrics.Space.Md * scale;
    }

    private string DyeLine(byte stain0, byte stain1)
    {
        var key = stain0 | stain1 << 8;
        if (!dyeLines.TryGetValue(key, out var line))
        {
            var first = gameData.StainName(stain0);
            var second = gameData.StainName(stain1);
            line = first.Length == 0 ? second : second.Length == 0 ? first : string.Concat(first, ", ", second);
            dyeLines[key] = line;
        }

        return line;
    }

    private readonly Dictionary<int, string> dyeLines = new();

    private float DrawModChips(ImDrawListPtr drawList, float left, float top, float width, GlamModDto[] mods,
        bool interactive, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var pitch = PrismType.LineHeight(PrismFace.Label, labelPixels) + Metrics.Space.Sm * scale;
        var getKey = Loc.Current.Code.GetHashCode(StringComparison.Ordinal);
        var get = getText.IsCurrent(getKey) ? getText.Value : getText.Store(getKey, Loc.T(L.Prism.GetMod) + " " + Arrow);
        var getWidth = PrismType.Measure(get, PrismFace.Label, labelPixels).X;
        for (var index = 0; index < mods.Length; index++)
        {
            var mod = mods[index];
            var (line, allowed, installed) = ModLine(mod);
            installed &= interactive;
            allowed &= interactive && !installed;
            var have = installed ? Loc.T(L.Prism.Installed) : string.Empty;
            var haveWidth = installed ? PrismType.Measure(have, PrismFace.Label, labelPixels).X : 0f;
            var side = allowed ? getWidth : haveWidth;
            var room = width - (side > 0f ? side + Metrics.Space.Md * scale : 0f);
            var fitted = PrismType.FitLabel(line, labelPixels, room);
            if (!interactive)
            {
                PrismType.Draw(drawList, new Vector2(left, top), fitted, PrismArt.Fore, PrismFace.Label, labelPixels);
            }
            else if (PrismArt.Link(drawList, new Vector2(left, top), fitted, PrismArt.Fore, scale, pitch))
            {
                OpenModGlams(mod);
            }

            if (installed)
            {
                PrismType.DrawRight(drawList, new Vector2(left + width, top), have, PrismArt.Muted, PrismFace.Label,
                    labelPixels);
            }

            if (allowed && PrismArt.Link(drawList, new Vector2(left + width - getWidth, top), get, PrismArt.Muted, scale,
                    pitch))
            {
                UrlActions.AskThenOpen(mod.Link);
            }

            top += pitch;
        }

        return top + Metrics.Space.Sm * scale;
    }

    private readonly Dictionary<GlamModDto, (string Line, bool Allowed, bool Installed)> modLines = new();
    private CachedText getText;
    private string linesLanguage = string.Empty;

    private (string Line, bool Allowed, bool Installed) ModLine(GlamModDto mod)
    {
        if (!ReferenceEquals(linesLanguage, Loc.Current.Code))
        {
            linesLanguage = Loc.Current.Code;
            modLines.Clear();
            dyeLines.Clear();
        }

        if (!modLines.TryGetValue(mod, out var cached))
        {
            cached = (mod.Author.Length > 0 ? Loc.T(L.Prism.ModBy, mod.Name, mod.Author) : mod.Name,
                ModLinks.IsAllowed(mod.Link), glamour.HasMod(mod));
            modLines[mod] = cached;
        }

        return cached;
    }
}
