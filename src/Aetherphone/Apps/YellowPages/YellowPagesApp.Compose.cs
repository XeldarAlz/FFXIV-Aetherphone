using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const int TitleMaxLength = 80;
    private const int BodyMaxLength = 1000;
    private const int BodyBufferLength = 1200;
    private const int NoteMaxLength = 80;
    private const int TagsBufferLength = 200;
    private const float ArchetypeCardHeight = 84f;

    private static readonly int[] WeekDays = { 1, 2, 3, 4, 5, 6, 0 };
    private static readonly int[] StartMinutesLocal = { 720, 1020, 1080, 1140, 1200, 1260, 1320, 1380 };
    private static readonly int[] SlotDurationsMinutes = { 60, 120, 180, 240, 360 };

    private int composeArchetype = -1;
    private int composeCategory;
    private string composeTitle = string.Empty;
    private string composeBody = string.Empty;
    private string composeTags = string.Empty;
    private SharedLocation? composeLocation;
    private string composeAddressNote = string.Empty;
    private readonly bool[] composeDays = new bool[7];
    private int composeStartIndex = 4;
    private int composeDurationIndex = 2;
    private int composePriceMode;
    private string composePriceText = string.Empty;
    private string composeTurnaround = string.Empty;
    private string composeSlotsLine = string.Empty;
    private string composeRequirements = string.Empty;
    private bool composeAfterDark;
    private bool composeBusy;
    private bool composeSucceeded;
    private AdCreateOutcome? composeOutcome;
    private int lastBodyLength = -1;
    private string bodyCounter = string.Empty;

    private void DrawCompose(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.YellowPages.NewAd), back);
        if (composeSucceeded)
        {
            composeSucceeded = false;
            ResetComposeForm();
            router.Pop(false);
            router.Push(YellowPagesRoute.Mine, false);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
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
        var height = ArchetypeCardHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Lg * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var tileSide = 36f * scale;
        var tileCenter = new Vector2(card.Min.X + 14f * scale + tileSide * 0.5f, card.Center.Y);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), icon);
        var textLeft = tileCenter.X + tileSide * 0.5f + 12f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 14f * scale), title,
            AppPalettes.YellowPages.TitleInk, TextStyles.Headline);
        var hintWidth = card.Max.X - 16f * scale - textLeft;
        Typography.DrawWrappedLeft(new Vector2(textLeft, card.Min.Y + 36f * scale),
            Typography.FitText(hint, hintWidth * 2f, TextStyles.Footnote), AppPalettes.YellowPages.MutedInk,
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

        ui.ToggleRow(Loc.T(L.YellowPages.AfterDarkToggle), ref composeAfterDark);
        ui.HelpText(Loc.T(L.YellowPages.AfterDarkHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        DrawComposeSubmit(scale);
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
        ui.SectionLabel(Loc.T(L.YellowPages.OpensLabel));
        for (var index = 0; index < StartMinutesLocal.Length; index++)
        {
            chipLabels[index] = TimeText.Clock(NextLocalPreview(StartMinutesLocal[index]));
            chipActive[index] = index == composeStartIndex;
        }

        var tappedStart = DrawChipFlow(StartMinutesLocal.Length, scale);
        if (tappedStart >= 0)
        {
            composeStartIndex = tappedStart;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        ui.SectionLabel(Loc.T(L.YellowPages.DurationLabel));
        for (var index = 0; index < SlotDurationsMinutes.Length; index++)
        {
            chipLabels[index] = Loc.T(L.YellowPages.DurationHours, SlotDurationsMinutes[index] / 60);
            chipActive[index] = index == composeDurationIndex;
        }

        var tappedDuration = DrawChipFlow(SlotDurationsMinutes.Length, scale);
        if (tappedDuration >= 0)
        {
            composeDurationIndex = tappedDuration;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private static DateTime NextLocalPreview(int localMinutes)
    {
        return DateTime.Now.Date.AddMinutes(localMinutes);
    }

    private void DrawComposeService(float scale)
    {
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
        var valid = titleLength > 0 && bodyLength > 0 && composeBody.Length <= BodyMaxLength && hasDataCenter;
        var cursorY = origin.Y;
        if (composeOutcome is { } outcome)
        {
            Typography.Draw(new Vector2(origin.X, cursorY), OutcomeText(outcome), theme.Danger,
                TextStyles.FootnoteEmphasized);
            cursorY += 22f * scale;
        }
        else if (!valid)
        {
            var hint = titleLength == 0 ? Loc.T(L.YellowPages.NeedTitle)
                : bodyLength == 0 ? Loc.T(L.YellowPages.NeedBody) : Loc.T(L.YellowPages.NeedDataCenter);
            Typography.Draw(new Vector2(origin.X, cursorY), hint, AppPalettes.YellowPages.MutedInk,
                TextStyles.Footnote);
            cursorY += 22f * scale;
        }

        var rect = new Rect(new Vector2(origin.X, cursorY),
            new Vector2(origin.X + width, cursorY + ActionHeight * scale));
        if (composeBusy)
        {
            LoadingPulse.Spinner(rect.Center, 10f * scale, ui.Accent);
        }
        else if (valid)
        {
            if (ui.PillButton(rect, Loc.T(L.YellowPages.PublishAd), true))
            {
                SubmitCompose();
            }
        }
        else
        {
            AppSkin.PillButton(rect, Loc.T(L.YellowPages.PublishAd), true, false, theme);
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

        var duration = SlotDurationsMinutes[composeDurationIndex];
        var startMinute = StartMinutesLocal[composeStartIndex];
        var slots = new List<AdScheduleSlot>(7);
        for (var day = 0; day < composeDays.Length; day++)
        {
            if (composeDays[day])
            {
                slots.Add(AdText.ToUtcSlot(day, startMinute, duration));
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
            null);
        composeBusy = true;
        composeOutcome = null;
        store.Create(request, outcome =>
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
        });
    }

    private void ResetComposeForm()
    {
        composeArchetype = -1;
        composeCategory = AdCategories.VenueNight;
        composeTitle = string.Empty;
        composeBody = string.Empty;
        composeTags = string.Empty;
        composeLocation = null;
        composeAddressNote = string.Empty;
        Array.Clear(composeDays);
        composeStartIndex = 4;
        composeDurationIndex = 2;
        composePriceMode = 0;
        composePriceText = string.Empty;
        composeTurnaround = string.Empty;
        composeSlotsLine = string.Empty;
        composeRequirements = string.Empty;
        composeAfterDark = false;
        composeBusy = false;
        composeOutcome = null;
        lastBodyLength = -1;
    }
}
