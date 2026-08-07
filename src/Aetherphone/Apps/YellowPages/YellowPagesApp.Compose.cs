using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Platform;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const int TitleMaxLength = 80;
    private const int BodyMaxLength = 1000;
    private const int BodyBufferLength = 1200;
    private const int NoteMaxLength = 80;
    private const int TagsBufferLength = 200;
    private const float ArchetypeCardHeight = 84f;
    private const float ComposeChipHeight = 32f;
    private const int PickerColumns = 3;
    private const int PhotoColumns = 4;
    private const int LinkMaxLength = 200;
    private const int MinutesPerDay = 1440;
    private const int DefaultOpenMinute = 1200;
    private const int DefaultCloseMinute = 1380;
    private const int MinOpenMinutes = 15;
    private const float TimeFieldHeight = 42f;

    private static readonly int[] WeekDays = { 1, 2, 3, 4, 5, 6, 0 };
    private static readonly Vector4 PhotoWhite = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 AddTileStroke = new(1f, 1f, 1f, 0.18f);

    private readonly List<string> composePhotos = new();
    private readonly List<string> composeKeptUrls = new();
    private string? editingAdId;
    private bool picking;
    private string[] pickerPaths = Array.Empty<string>();
    private string? pendingPickedPath;
    private int composeArchetype = -1;
    private int composeCategory;
    private string composeTitle = string.Empty;
    private string composeBody = string.Empty;
    private string composeTags = string.Empty;
    private SharedLocation? composeLocation;
    private string composeAddressNote = string.Empty;
    private readonly bool[] composeDays = new bool[7];
    private int composeOpenMinute = DefaultOpenMinute;
    private int composeCloseMinute = DefaultCloseMinute;
    private int composePriceMode;
    private string composePriceText = string.Empty;
    private string composeTurnaround = string.Empty;
    private string composeSlotsLine = string.Empty;
    private string composeRequirements = string.Empty;
    private string composeLink = string.Empty;
    private bool composeAllowInquiries = true;
    private bool composeAfterDark;
    private bool composeBusy;
    private bool composeSucceeded;
    private AdCreateOutcome? composeOutcome;
    private int lastBodyLength = -1;
    private string bodyCounter = string.Empty;

    private void DrawCompose(Rect area)
    {
        if (picking)
        {
            DrawPhotoPicker(area);
            return;
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(editingAdId is null ? L.YellowPages.NewAd : L.YellowPages.EditAdTitle), back);
        if (composeSucceeded)
        {
            composeSucceeded = false;
            var wasEditing = editingAdId is not null;
            ResetComposeForm();
            router.Pop(false);
            if (!wasEditing)
            {
                activeTab = YellowPagesTab.Mine;
                store.SyncNow();
            }

            return;
        }

        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawComposeSteps(scale);
            if (composeArchetype < 0)
            {
                DrawArchetypePicker(scale);
            }
            else
            {
                DrawComposeForm(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawComposeSteps(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var barWidth = 22f * scale;
        var barHeight = 4f * scale;
        var gap = 5f * scale;
        var left = origin.X + (width - barWidth * 2f - gap) * 0.5f;
        var activeStep = composeArchetype < 0 ? 0 : 1;
        for (var index = 0; index < 2; index++)
        {
            var min = new Vector2(left + (barWidth + gap) * index, origin.Y);
            var max = min + new Vector2(barWidth, barHeight);
            Squircle.Fill(drawList, min, max, barHeight * 0.5f,
                ImGui.GetColorU32(index == activeStep ? ui.Accent : AppPalettes.YellowPages.FieldSurface));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, barHeight + Metrics.Space.Md * scale));
    }

    private void DrawArchetypePicker(float scale)
    {
        ui.SectionHeading(Loc.T(L.YellowPages.WhatPosting));
        DrawArchetypeCard(AdArchetypes.Place, FontAwesomeIcon.Cocktail, Loc.T(L.YellowPages.ArchetypePlace),
            Loc.T(L.YellowPages.ArchetypePlaceHint), scale);
        DrawArchetypeCard(AdArchetypes.Service, FontAwesomeIcon.Hammer, Loc.T(L.YellowPages.ArchetypeService),
            Loc.T(L.YellowPages.ArchetypeServiceHint), scale);
        DrawArchetypeCard(AdArchetypes.Call, FontAwesomeIcon.Flag, Loc.T(L.YellowPages.ArchetypeCall),
            Loc.T(L.YellowPages.ArchetypeCallHint), scale);
        ui.HelpText(Loc.T(L.YellowPages.PostRules));
    }

    private void DrawArchetypeCard(int archetype, FontAwesomeIcon icon, string title, string hint, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var tileSide = 36f * scale;
        var textLeft = origin.X + 14f * scale + tileSide + 12f * scale;
        var hintWidth = origin.X + width - 16f * scale - textLeft;
        var hintTop = origin.Y + 36f * scale;
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, hintWidth).Y;
        var height = MathF.Max(ArchetypeCardHeight * scale, hintTop - origin.Y + hintHeight + 14f * scale);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Lg * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var tileCenter = new Vector2(card.Min.X + 14f * scale + tileSide * 0.5f, card.Min.Y + 14f * scale + tileSide * 0.5f);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), icon);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 14f * scale), title,
            AppPalettes.YellowPages.TitleInk, TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(textLeft, hintTop), hint, AppPalettes.YellowPages.MutedInk,
            TextStyles.Footnote, hintWidth);
        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            composeArchetype = archetype;
            composeCategory = AdCategories.ForIntent(archetype switch
            {
                AdArchetypes.Service => AdIntents.Hire,
                AdArchetypes.Call => AdIntents.Join,
                _ => AdIntents.Go,
            })[0];
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawComposeForm(float scale)
    {
        DrawComposeCategory(scale);
        ui.Field(Loc.T(L.YellowPages.TitleLabel), "##adTitle", ref composeTitle, TitleMaxLength, false);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        DrawComposeBody(scale);
        ui.Field(Loc.T(L.YellowPages.TagsLabel), "##adTags", ref composeTags, TagsBufferLength, false);
        ui.HelpText(Loc.T(L.YellowPages.TagsHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        switch (composeArchetype)
        {
            case AdArchetypes.Place:
                DrawComposePlace(scale);
                break;
            case AdArchetypes.Service:
                DrawComposeService(scale);
                break;
            default:
                DrawComposeCall(scale);
                break;
        }

        DrawComposePhotos(scale);
        ui.ToggleRow(Loc.T(L.YellowPages.AllowInquiriesToggle), ref composeAllowInquiries);
        ui.HelpText(Loc.T(L.YellowPages.AllowInquiriesHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        ui.ToggleRow(Loc.T(L.YellowPages.AfterDarkToggle), ref composeAfterDark);
        ui.HelpText(Loc.T(L.YellowPages.AfterDarkHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        DrawComposeSubmit(scale);
    }

    private void DrawComposePhotos(float scale)
    {
        ui.SectionHeading(Loc.T(L.Apps.Photos));
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = Metrics.Space.Sm * scale;
        var tile = (width - gap * (PhotoColumns - 1)) / PhotoColumns;
        var rounding = 10f * scale;
        var total = composeKeptUrls.Count + composePhotos.Count;
        var slot = 0;
        var removeKeptIndex = -1;
        var removeLocalIndex = -1;
        for (var index = 0; index < composeKeptUrls.Count; index++, slot++)
        {
            var min = PhotoSlot(origin, slot, tile, gap);
            if (DrawComposeThumb(drawList, images.Get(composeKeptUrls[index]), min,
                    min + new Vector2(tile, tile), rounding, scale))
            {
                removeKeptIndex = index;
            }
        }

        for (var index = 0; index < composePhotos.Count; index++, slot++)
        {
            var min = PhotoSlot(origin, slot, tile, gap);
            if (DrawComposeThumb(drawList, wallpaperImages.Get(composePhotos[index]), min,
                    min + new Vector2(tile, tile), rounding, scale))
            {
                removeLocalIndex = index;
            }
        }

        if (total < YellowPagesStore.MaxPhotos)
        {
            var min = PhotoSlot(origin, slot, tile, gap);
            var max = min + new Vector2(tile, tile);
            var hovered = UiInteract.Hover(min, max);
            Squircle.Fill(drawList, min, max, rounding,
                ImGui.GetColorU32(hovered ? ui.HoverTint : AppPalettes.YellowPages.FieldSurface));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(AddTileStroke), 1f);
            AppSkin.Icon(drawList, (min + max) * 0.5f, FontAwesomeIcon.Plus.ToIconString(),
                AppPalettes.YellowPages.BodyInk, 0.9f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                pickerPaths = library.List();
                picking = true;
            }
        }

        if (removeKeptIndex >= 0)
        {
            composeKeptUrls.RemoveAt(removeKeptIndex);
        }

        if (removeLocalIndex >= 0)
        {
            composePhotos.RemoveAt(removeLocalIndex);
        }

        var filled = Math.Min(total + 1, YellowPagesStore.MaxPhotos);
        var rows = Math.Max(1, (filled + PhotoColumns - 1) / PhotoColumns);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * tile + (rows - 1) * gap + Metrics.Space.Md * scale));
    }

    private static Vector2 PhotoSlot(Vector2 origin, int slot, float tile, float gap) =>
        new(origin.X + (tile + gap) * (slot % PhotoColumns), origin.Y + (tile + gap) * (slot / PhotoColumns));

    private bool DrawComposeThumb(ImDrawListPtr drawList, Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? texture,
        Vector2 min, Vector2 max, float rounding, float scale)
    {
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }

        var badgeRadius = 8.5f * scale;
        var badgeCenter = new Vector2(max.X - badgeRadius - 2f * scale, min.Y + badgeRadius + 2f * scale);
        var badgeHovered = UiInteract.Hover(badgeCenter - new Vector2(badgeRadius, badgeRadius),
            badgeCenter + new Vector2(badgeRadius, badgeRadius));
        drawList.AddCircleFilled(badgeCenter, badgeRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, badgeHovered ? 0.9f : 0.62f)), 20);
        AppSkin.Icon(drawList, badgeCenter, FontAwesomeIcon.Times.ToIconString(), PhotoWhite, 0.6f);
        if (badgeHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(badgeCenter - new Vector2(badgeRadius, badgeRadius),
            badgeCenter + new Vector2(badgeRadius, badgeRadius), badgeHovered);
    }

    private void DrawPhotoPicker(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Feedback.AddPhotos), () => picking = false);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Feedback.ImportFromPc), true))
        {
            FilePicker.PickImage(Loc.T(L.Feedback.AddPhotos),
                path => Interlocked.Exchange(ref pendingPickedPath, path));
        }

        var gridTop = importRect.Max.Y + 12f * scale;
        var gridRect = new Rect(new Vector2(area.Min.X, gridTop), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (pickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Feedback.NoGallery), AppPalettes.YellowPages.MutedInk);
                return;
            }

            var gap = 6f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - gap * (PickerColumns - 1)) / PickerColumns;
            var origin = ImGui.GetCursorScreenPos();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var margin = cell + 60f * scale;
            for (var index = 0; index < pickerPaths.Length; index++)
            {
                var column = index % PickerColumns;
                var rowIndex = index / PickerColumns;
                var rowTop = rowIndex * (cell + gap);
                if (rowTop + cell < scrollY - margin || rowTop > scrollY + viewHeight + margin)
                {
                    continue;
                }

                var min = new Vector2(origin.X + column * (cell + gap), origin.Y + rowTop);
                var max = new Vector2(min.X + cell, min.Y + cell);
                var hovered = UiInteract.Hover(min, max);
                DrawPickerThumbnail(pickerPaths[index], min, max, scale, hovered);
                if (UiInteract.Click(min, max, hovered))
                {
                    AddComposePhoto(pickerPaths[index]);
                }
            }

            var rows = (pickerPaths.Length + PickerColumns - 1) / PickerColumns;
            var totalHeight = rows * (cell + gap);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, totalHeight));
        }
    }

    private void DrawPickerThumbnail(string path, Vector2 min, Vector2 max, float scale, bool hovered)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = wallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void AddComposePhoto(string path)
    {
        picking = false;
        if (string.IsNullOrEmpty(path)
            || composeKeptUrls.Count + composePhotos.Count >= YellowPagesStore.MaxPhotos)
        {
            return;
        }

        for (var index = 0; index < composePhotos.Count; index++)
        {
            if (string.Equals(composePhotos[index], path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        composePhotos.Add(path);
    }

    public void StartEdit(AdDto ad)
    {
        ResetComposeForm();
        editingAdId = ad.Id;
        composeArchetype = ad.Archetype;
        composeCategory = ad.Category;
        composeTitle = ad.Title;
        composeBody = ad.Body;
        composeTags = string.Join(", ", ad.Tags);
        composeAddressNote = ad.AddressNote;
        composeAllowInquiries = ad.AllowInquiries;
        composeAfterDark = ad.AfterDark;
        composeKeptUrls.AddRange(ad.MediaUrls);
        if (ad.TerritoryId > 0 || ad.Ward > 0)
        {
            composeLocation = AdText.Location(ad);
        }

        if (AdCategories.IsLinkOnly(ad.Category))
        {
            composeLink = ad.LinkUrl;
        }
        else if (ad.Archetype == AdArchetypes.Service)
        {
            composePriceMode = ad.PriceMode;
            composePriceText = ad.PriceGil > 0 ? ad.PriceGil.ToString(Loc.Culture) : string.Empty;
            composeTurnaround = ad.Turnaround;
        }
        else if (ad.Archetype == AdArchetypes.Call)
        {
            composeSlotsLine = ad.SlotsLine;
            composeRequirements = ad.Requirements;
        }

        if (ad.Schedule.Length > 0)
        {
            AdText.ToLocalSlot(ad.Schedule[0], out _, out var localStartMinute);
            composeOpenMinute = localStartMinute;
            composeCloseMinute = (localStartMinute + ad.Schedule[0].DurationMinutes) % MinutesPerDay;
            for (var index = 0; index < ad.Schedule.Length; index++)
            {
                AdText.ToLocalSlot(ad.Schedule[index], out var localDay, out _);
                composeDays[localDay] = true;
            }
        }
    }

    private void DrawComposeCategory(float scale)
    {
        ui.SectionHeading(Loc.T(L.YellowPages.CategorySection));
        var categories = AdCategories.ForIntent(composeArchetype switch
        {
            AdArchetypes.Service => AdIntents.Hire,
            AdArchetypes.Call => AdIntents.Join,
            _ => AdIntents.Go,
        });
        for (var index = 0; index < categories.Length; index++)
        {
            chipLabels[index] = Loc.T(AdCategories.Label(categories[index]));
            chipActive[index] = categories[index] == composeCategory;
        }

        var tapped = DrawChipFlow(categories.Length, scale);
        if (tapped >= 0)
        {
            composeCategory = categories[tapped];
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawComposeBody(float scale)
    {
        ui.Field(Loc.T(L.YellowPages.BodyLabel), "##adBody", ref composeBody, BodyBufferLength, true);
        if (composeBody.Length != lastBodyLength)
        {
            lastBodyLength = composeBody.Length;
            bodyCounter = Loc.T(L.Common.PhotoCounter, composeBody.Length, BodyMaxLength);
        }

        var counterSize = Typography.Measure(bodyCounter, TextStyles.Caption1);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var over = composeBody.Length > BodyMaxLength;
        Typography.Draw(new Vector2(origin.X + width - counterSize.X, origin.Y + 2f * scale), bodyCounter,
            over ? theme.Danger : AppPalettes.YellowPages.MutedInk, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, counterSize.Y + Metrics.Space.Md * scale));
    }

    private void DrawComposePlace(float scale)
    {
        ui.SectionHeading(Loc.T(L.YellowPages.WhereSection));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = 38f * scale;
        if (composeLocation is { } location)
        {
            var clearLabel = Loc.T(L.YellowPages.ClearLocation);
            var clearWidth = Typography.Measure(clearLabel, 0.9f, FontWeight.SemiBold).X + 30f * scale;
            var summary = LocationShare.Summary(in location);
            var summaryHeight = Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + 4f * scale), summary,
                AppPalettes.YellowPages.BodyInk, TextStyles.Subheadline,
                width - clearWidth - Metrics.Space.Md * scale);
            var clearRect = new Rect(new Vector2(origin.X + width - clearWidth, origin.Y),
                new Vector2(origin.X + width, origin.Y + 30f * scale));
            if (ui.GhostButton(clearRect, clearLabel))
            {
                composeLocation = null;
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, MathF.Max(summaryHeight + 8f * scale, 34f * scale)
                + Metrics.Space.Sm * scale));
        }
        else
        {
            var captureRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + rowHeight));
            if (ui.PillButton(captureRect, Loc.T(L.YellowPages.UseMyLocation), false))
            {
                composeLocation = LocationShare.Capture();
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Sm * scale));
        }

        ui.Field(Loc.T(L.YellowPages.AddressNoteLabel), "##adAddressNote", ref composeAddressNote, NoteMaxLength,
            false);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));

        ui.SectionHeading(Loc.T(L.YellowPages.ScheduleSection));
        ui.HelpText(Loc.T(L.YellowPages.ScheduleHint));
        ui.SectionLabel(Loc.T(L.YellowPages.DaysLabel));
        var dayNames = Loc.Culture.DateTimeFormat.AbbreviatedDayNames;
        for (var index = 0; index < WeekDays.Length; index++)
        {
            chipLabels[index] = dayNames[WeekDays[index]];
            chipActive[index] = composeDays[WeekDays[index]];
        }

        var tappedDay = DrawChipFlow(WeekDays.Length, scale);
        if (tappedDay >= 0)
        {
            composeDays[WeekDays[tappedDay]] = !composeDays[WeekDays[tappedDay]];
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        composeOpenMinute = DrawTimeField(Loc.T(L.YellowPages.OpensLabel), composeOpenMinute, scale);
        composeCloseMinute = DrawTimeField(Loc.T(L.YellowPages.ClosesLabel), composeCloseMinute, scale);
        DrawOpenForRow(scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private int DrawTimeField(string label, int minuteOfDay, float scale)
    {
        ui.SectionLabel(label);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = TimeFieldHeight * scale;
        var edited = TimeOfDayField.Draw(ui,
            new Rect(origin, new Vector2(origin.X + width, origin.Y + height)), minuteOfDay, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        return edited;
    }

    private void DrawOpenForRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var label = Loc.T(L.YellowPages.DurationLabel);
        var value = DurationText(ComposeOpenMinutes());
        var valueSize = Typography.Measure(value, TextStyles.FootnoteEmphasized);
        Typography.Draw(origin, label, AppPalettes.YellowPages.MutedInk, TextStyles.Footnote);
        Typography.Draw(new Vector2(origin.X + width - valueSize.X, origin.Y), value,
            AppPalettes.YellowPages.TitleInk, TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, valueSize.Y + Metrics.Space.Xs * scale));
    }

    private int ComposeOpenMinutes()
    {
        var span = composeCloseMinute - composeOpenMinute;
        return span > 0 ? span : span + MinutesPerDay;
    }

    private static string DurationText(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;
        if (hours == 0)
        {
            return Loc.T(L.YellowPages.DurationMinutes, rest);
        }

        return rest == 0
            ? Loc.T(L.YellowPages.DurationHours, hours)
            : Loc.T(L.YellowPages.DurationHoursMinutes, hours, rest);
    }

    private bool HasComposeDays()
    {
        for (var index = 0; index < composeDays.Length; index++)
        {
            if (composeDays[index])
            {
                return true;
            }
        }

        return false;
    }

    private int DrawChipFlow(int count, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var right = origin.X + width;
        var gap = Metrics.Space.Sm * scale;
        var chipHeight = ComposeChipHeight * scale;
        var lineAdvance = chipHeight + gap;
        var cursorX = origin.X;
        var lineTop = origin.Y;
        var tapped = -1;
        for (var index = 0; index < count; index++)
        {
            var label = chipLabels[index];
            var chipWidth = Typography.Measure(label, 0.85f, FontWeight.Medium).X + 26f * scale;
            if (cursorX + chipWidth > right && cursorX > origin.X)
            {
                cursorX = origin.X;
                lineTop += lineAdvance;
            }

            var centerY = lineTop + chipHeight * 0.5f;
            if (ui.FlowChip(ref cursorX, centerY, gap, label, chipActive[index]))
            {
                tapped = index;
            }
        }

        var totalHeight = lineTop + chipHeight - origin.Y;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, totalHeight));
        return tapped;
    }

    private void DrawComposeService(float scale)
    {
        if (AdCategories.IsLinkOnly(composeCategory))
        {
            DrawComposeModLink(scale);
            return;
        }

        ui.SectionHeading(Loc.T(L.YellowPages.PriceSection));
        chipLabels[0] = Loc.T(L.YellowPages.PriceAsk);
        chipLabels[1] = Loc.T(L.YellowPages.PriceFixed);
        chipLabels[2] = Loc.T(L.YellowPages.PriceFromLabel);
        for (var index = 0; index < 3; index++)
        {
            chipActive[index] = index == composePriceMode;
        }

        var tapped = DrawChipFlow(3, scale);
        if (tapped >= 0)
        {
            composePriceMode = tapped;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        if (composePriceMode != AdPriceModes.Ask)
        {
            ui.Field(Loc.T(L.YellowPages.PriceGilLabel), "##adPriceGil", ref composePriceText, 15, false);
        }

        ui.Field(Loc.T(L.YellowPages.TurnaroundLabel), "##adTurnaround", ref composeTurnaround, NoteMaxLength,
            false);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawComposeModLink(float scale)
    {
        ui.SectionHeading(Loc.T(L.YellowPages.ModLinkLabel));
        ui.Field(Loc.T(L.YellowPages.ModLinkLabel), "##adModLink", ref composeLink, LinkMaxLength, false);
        ui.HelpText(Loc.T(L.YellowPages.ModLinkHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawComposeCall(float scale)
    {
        ui.SectionHeading(Loc.T(L.YellowPages.CallSection));
        ui.Field(Loc.T(L.YellowPages.SlotsLabel), "##adSlots", ref composeSlotsLine, NoteMaxLength, false);
        ui.Field(Loc.T(L.YellowPages.RequirementsLabel), "##adRequirements", ref composeRequirements, 200, true);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawComposeSubmit(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var titleLength = TrimmedLength(composeTitle);
        var bodyLength = TrimmedLength(composeBody);
        var hasDataCenter = ResolveComposeDataCenter(out _) != 0;
        var needsLink = AdCategories.IsLinkOnly(composeCategory) && TrimmedLength(composeLink) == 0;
        var shortWindow = composeArchetype == AdArchetypes.Place && HasComposeDays()
            && ComposeOpenMinutes() < MinOpenMinutes;
        var valid = titleLength > 0 && bodyLength > 0 && composeBody.Length <= BodyMaxLength && hasDataCenter
            && !needsLink && !shortWindow;
        var cursorY = origin.Y;
        if (composeOutcome is { } outcome)
        {
            var outcomeHeight = Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), OutcomeText(outcome),
                theme.Danger, TextStyles.FootnoteEmphasized, width);
            cursorY += outcomeHeight + Metrics.Space.Xs * scale;
        }
        else if (!valid)
        {
            var hint = titleLength == 0 ? Loc.T(L.YellowPages.NeedTitle)
                : bodyLength == 0 ? Loc.T(L.YellowPages.NeedBody)
                : needsLink ? Loc.T(L.YellowPages.NeedModLink)
                : shortWindow ? Loc.T(L.YellowPages.NeedOpenWindow, MinOpenMinutes)
                : Loc.T(L.YellowPages.NeedDataCenter);
            var hintHeight = Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), hint,
                AppPalettes.YellowPages.MutedInk, TextStyles.Footnote, width);
            cursorY += hintHeight + Metrics.Space.Xs * scale;
        }

        var rect = new Rect(new Vector2(origin.X, cursorY),
            new Vector2(origin.X + width, cursorY + ActionHeight * scale));
        var submitLabel = Loc.T(editingAdId is null ? L.YellowPages.PublishAd : L.YellowPages.SaveChanges);
        if (composeBusy)
        {
            LoadingPulse.Spinner(rect.Center, 10f * scale, ui.Accent);
        }
        else if (valid)
        {
            if (ui.PillButton(rect, submitLabel, true))
            {
                SubmitCompose();
            }
        }
        else
        {
            AppSkin.PillButton(rect, submitLabel, true, false, theme);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rect.Max.Y - origin.Y));
    }

    private static string OutcomeText(AdCreateOutcome outcome) =>
        outcome switch
        {
            AdCreateOutcome.TooMany => Loc.T(L.YellowPages.ErrorTooMany),
            AdCreateOutcome.Invalid => Loc.T(L.YellowPages.ErrorInvalid),
            AdCreateOutcome.RateLimited => Loc.T(L.YellowPages.ErrorRateLimited),
            _ => Loc.T(L.YellowPages.ErrorFailed),
        };

    private int ResolveComposeDataCenter(out uint worldId)
    {
        worldId = composeLocation?.WorldId ?? gameData.LocalCurrentWorldId;
        return MusterWorlds.DataCenterIdForWorld(worldId);
    }

    private AdScheduleSlot[]? BuildSchedule()
    {
        if (composeArchetype != AdArchetypes.Place)
        {
            return null;
        }

        var duration = ComposeOpenMinutes();
        var slots = new List<AdScheduleSlot>(7);
        for (var day = 0; day < composeDays.Length; day++)
        {
            if (composeDays[day])
            {
                slots.Add(AdText.ToUtcSlot(day, composeOpenMinute, duration));
            }
        }

        return slots.Count > 0 ? slots.ToArray() : null;
    }

    private string[]? BuildTags()
    {
        if (TrimmedLength(composeTags) == 0)
        {
            return null;
        }

        var parts = composeTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts : null;
    }

    private long ParsePriceGil()
    {
        if (composePriceMode == AdPriceModes.Ask)
        {
            return 0L;
        }

        var digits = 0L;
        var seen = false;
        for (var index = 0; index < composePriceText.Length && digits < 100_000_000_000L; index++)
        {
            var character = composePriceText[index];
            if (character is >= '0' and <= '9')
            {
                digits = digits * 10L + (character - '0');
                seen = true;
            }
        }

        return seen ? digits : 0L;
    }

    private void SubmitCompose()
    {
        var location = composeLocation;
        var dataCenterId = ResolveComposeDataCenter(out var worldId);
        if (dataCenterId == 0)
        {
            return;
        }

        var request = new CreateAdRequest(
            composeCategory,
            composeTitle.Trim(),
            composeBody.Trim(),
            BuildTags(),
            MusterCategories.RegionBitForWorld(worldId),
            dataCenterId,
            (int)worldId,
            (int)(location?.TerritoryId ?? 0u),
            (int)(location?.MapId ?? 0u),
            location?.MapX ?? 0f,
            location?.MapY ?? 0f,
            location?.Ward ?? 0,
            location?.Plot ?? 0,
            composeAddressNote.Trim(),
            BuildSchedule(),
            composePriceMode,
            ParsePriceGil(),
            composeTurnaround.Trim(),
            composeSlotsLine.Trim(),
            composeRequirements.Trim(),
            composeAfterDark,
            null,
            composeLink.Trim(),
            composeAllowInquiries);
        composeBusy = true;
        composeOutcome = null;
        var photos = composePhotos.ToArray();
        void Done(AdCreateOutcome outcome)
        {
            composeBusy = false;
            if (outcome == AdCreateOutcome.Created)
            {
                composeSucceeded = true;
            }
            else
            {
                composeOutcome = outcome;
            }
        }

        if (editingAdId is { } adId)
        {
            store.Update(adId, request, composeKeptUrls.ToArray(), photos, Done);
        }
        else
        {
            store.Create(request, photos, Done);
        }
    }

    private void ResetComposeForm()
    {
        editingAdId = null;
        composePhotos.Clear();
        composeKeptUrls.Clear();
        picking = false;
        composeArchetype = -1;
        composeCategory = AdCategories.VenueNight;
        composeTitle = string.Empty;
        composeBody = string.Empty;
        composeTags = string.Empty;
        composeLocation = null;
        composeAddressNote = string.Empty;
        Array.Clear(composeDays);
        composeOpenMinute = DefaultOpenMinute;
        composeCloseMinute = DefaultCloseMinute;
        composePriceMode = 0;
        composePriceText = string.Empty;
        composeTurnaround = string.Empty;
        composeSlotsLine = string.Empty;
        composeRequirements = string.Empty;
        composeLink = string.Empty;
        composeAllowInquiries = true;
        composeAfterDark = false;
        composeBusy = false;
        composeOutcome = null;
        lastBodyLength = -1;
    }
}
