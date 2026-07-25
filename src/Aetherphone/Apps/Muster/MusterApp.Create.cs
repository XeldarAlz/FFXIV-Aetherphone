using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const int DescriptionMaxLength = 200;
    private const int DescriptionBufferLength = 300;
    private const int SpotMaxLength = 80;
    private const int MinAttendees = 2;
    private const int MaxAttendeesCap = 200;
    private const int DefaultMaxAttendees = 8;

    private static readonly int[] StartOffsetsMinutes = { 0, 15, 30, 60, 120, 240, 480 };
    private static readonly int[] DurationsMinutes = { 30, 60, 120, 180, 240, 360, 480 };

    private int createCategory;
    private string createDescription = string.Empty;
    private SharedLocation? createLocation;
    private string createSpot = string.Empty;
    private int createStartIndex;
    private int createDurationIndex = 1;
    private bool createLimit;
    private int createMaxAttendees = DefaultMaxAttendees;
    private string maxAttendeesText = "8";
    private bool createUnlistWhenFull;
    private bool createIsPublic = true;
    private bool createBusy;
    private bool createSucceeded;
    private MusterCreateOutcome? createOutcome;
    private int lastDescriptionLength = -1;
    private string descriptionCounter = string.Empty;

    private void DrawCreate(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Muster.NewMuster), back);
        if (createSucceeded)
        {
            createSucceeded = false;
            ResetCreateForm();
            router.Pop(false);
            router.Push(MusterRoute.Manage, false);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawCreateCategory(scale);
            DrawCreateDescription(scale);
            DrawCreateWhere(scale);
            DrawCreateWhen(scale);
            DrawCreateWho(scale);
            DrawCreateSubmit(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawCreateCategory(float scale)
    {
        ui.SectionHeading(Loc.T(L.Muster.CategorySection));
        var categories = MusterCategories.All;
        for (var index = 0; index < categories.Length; index++)
        {
            chipLabels[index] = Loc.T(MusterCategories.Label(categories[index]));
            chipActive[index] = categories[index] == createCategory;
        }

        var tapped = DrawChipFlow(categories.Length, scale);
        if (tapped >= 0)
        {
            createCategory = categories[tapped];
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawCreateDescription(float scale)
    {
        ui.Field(Loc.T(L.Muster.DescriptionLabel), "##musterDescription", ref createDescription,
            DescriptionBufferLength, true);
        if (createDescription.Length != lastDescriptionLength)
        {
            lastDescriptionLength = createDescription.Length;
            descriptionCounter = Loc.T(L.Common.PhotoCounter, createDescription.Length, DescriptionMaxLength);
        }

        var counterSize = Typography.Measure(descriptionCounter, TextStyles.Caption1);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var over = createDescription.Length > DescriptionMaxLength;
        Typography.Draw(new Vector2(origin.X + width - counterSize.X, origin.Y + 2f * scale), descriptionCounter,
            over ? theme.Danger : AppPalettes.Muster.MutedInk, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, counterSize.Y + Metrics.Space.Md * scale));
    }

    private void DrawCreateWhere(float scale)
    {
        ui.SectionHeading(Loc.T(L.Muster.WhereSection));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = 38f * scale;
        if (createLocation is { } location)
        {
            var clearLabel = Loc.T(L.Muster.ClearLocation);
            var clearWidth = Typography.Measure(clearLabel, 0.9f, FontWeight.SemiBold).X + 30f * scale;
            var summary = LocationShare.Summary(in location);
            var summaryHeight = Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + 4f * scale), summary,
                AppPalettes.Muster.BodyInk, TextStyles.Subheadline, width - clearWidth - Metrics.Space.Md * scale);
            var clearRect = new Rect(new Vector2(origin.X + width - clearWidth, origin.Y),
                new Vector2(origin.X + width, origin.Y + 30f * scale));
            if (ui.GhostButton(clearRect, clearLabel))
            {
                createLocation = null;
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, MathF.Max(summaryHeight + 8f * scale, 34f * scale)
                + Metrics.Space.Sm * scale));
        }
        else
        {
            var captureRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + rowHeight));
            if (ui.PillButton(captureRect, Loc.T(L.Muster.UseMyLocation), false))
            {
                createLocation = LocationShare.Capture();
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Sm * scale));
        }

        ui.Field(Loc.T(L.Muster.MeetingSpot), "##musterSpot", ref createSpot, SpotMaxLength, false);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawCreateWhen(float scale)
    {
        ui.SectionHeading(Loc.T(L.Muster.WhenSection));
        ui.SectionLabel(Loc.T(L.Muster.StartLabel));
        for (var index = 0; index < StartOffsetsMinutes.Length; index++)
        {
            chipLabels[index] = OffsetLabel(StartOffsetsMinutes[index]);
            chipActive[index] = index == createStartIndex;
        }

        var tappedStart = DrawChipFlow(StartOffsetsMinutes.Length, scale);
        if (tappedStart >= 0)
        {
            createStartIndex = tappedStart;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        ui.SectionLabel(Loc.T(L.Muster.DurationLabel));
        for (var index = 0; index < DurationsMinutes.Length; index++)
        {
            chipLabels[index] = OffsetLabel(DurationsMinutes[index]);
            chipActive[index] = index == createDurationIndex;
        }

        var tappedDuration = DrawChipFlow(DurationsMinutes.Length, scale);
        if (tappedDuration >= 0)
        {
            createDurationIndex = tappedDuration;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private static string OffsetLabel(int minutes)
    {
        if (minutes == 0)
        {
            return Loc.T(L.Muster.Now);
        }

        if (minutes < 60)
        {
            return Loc.T(L.Muster.DurationMinutes, minutes);
        }

        return Loc.T(L.Muster.DurationHours, minutes / 60);
    }

    private void DrawCreateWho(float scale)
    {
        ui.SectionHeading(Loc.T(L.Muster.WhoSection));
        ui.ToggleRow(Loc.T(L.Muster.LimitAttendance), ref createLimit);
        if (createLimit)
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var stepperRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + 40f * scale));
            StepperField.Draw(ui, stepperRect, maxAttendeesText, scale, decrementMaxAttendees,
                incrementMaxAttendees);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, 40f * scale + Metrics.Space.Sm * scale));
            ui.ToggleRow(Loc.T(L.Muster.UnlistWhenFull), ref createUnlistWhenFull);
        }

        ui.ToggleRow(Loc.T(L.Muster.ListPublicly), ref createIsPublic);
        ui.HelpText(Loc.T(L.Muster.PublicHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void SetMaxAttendees(int value)
    {
        createMaxAttendees = Math.Clamp(value, MinAttendees, MaxAttendeesCap);
        maxAttendeesText = createMaxAttendees.ToString(Loc.Culture);
    }

    private void DrawCreateSubmit(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var descriptionLength = TrimmedLength(createDescription);
        var hasWhere = createLocation is not null || TrimmedLength(createSpot) > 0;
        var hasDataCenter = ResolveCreateDataCenter(out _) != 0;
        var valid = descriptionLength > 0 && createDescription.Length <= DescriptionMaxLength && hasWhere
            && hasDataCenter;
        var cursorY = origin.Y;
        if (createOutcome is { } outcome)
        {
            Typography.Draw(new Vector2(origin.X, cursorY), OutcomeText(outcome), theme.Danger,
                TextStyles.FootnoteEmphasized);
            cursorY += 22f * scale;
        }
        else if (!valid)
        {
            var hint = descriptionLength == 0 ? Loc.T(L.Muster.NeedDescription)
                : !hasWhere ? Loc.T(L.Muster.NeedWhere) : Loc.T(L.Muster.NeedDataCenter);
            Typography.Draw(new Vector2(origin.X, cursorY), hint, AppPalettes.Muster.MutedInk, TextStyles.Footnote);
            cursorY += 22f * scale;
        }

        var rect = new Rect(new Vector2(origin.X, cursorY), new Vector2(origin.X + width,
            cursorY + ActionHeight * scale));
        if (createBusy)
        {
            LoadingPulse.Spinner(rect.Center, 10f * scale, ui.Accent);
        }
        else if (valid)
        {
            if (ui.PillButton(rect, Loc.T(L.Muster.CallIt), true))
            {
                SubmitCreate();
            }
        }
        else
        {
            AppSkin.PillButton(rect, Loc.T(L.Muster.CallIt), true, false, theme);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rect.Max.Y - origin.Y));
    }

    private static string OutcomeText(MusterCreateOutcome outcome) =>
        outcome switch
        {
            MusterCreateOutcome.AlreadyHosting => Loc.T(L.Muster.ErrorAlreadyHosting),
            MusterCreateOutcome.Invalid => Loc.T(L.Muster.ErrorInvalid),
            MusterCreateOutcome.RateLimited => Loc.T(L.Muster.ErrorRateLimited),
            _ => Loc.T(L.Muster.ErrorFailed),
        };

    private int ResolveCreateDataCenter(out uint worldId)
    {
        worldId = createLocation?.WorldId ?? gameData.LocalCurrentWorldId;
        return MusterWorlds.DataCenterIdForWorld(worldId);
    }

    private void SubmitCreate()
    {
        var location = createLocation;
        var dataCenterId = ResolveCreateDataCenter(out var worldId);
        if (dataCenterId == 0)
        {
            return;
        }

        var request = new CreateMusterRequest(
            createCategory,
            createDescription.Trim(),
            (int)(location?.TerritoryId ?? 0u),
            (int)(location?.MapId ?? 0u),
            location?.MapX ?? 0f,
            location?.MapY ?? 0f,
            (int)worldId,
            location?.Ward ?? 0,
            location?.Plot ?? 0,
            location?.Room ?? 0,
            createSpot.Trim(),
            MusterCategories.RegionBitForWorld(worldId),
            dataCenterId,
            StartOffsetsMinutes[createStartIndex],
            DurationsMinutes[createDurationIndex],
            createLimit ? createMaxAttendees : 0,
            createLimit && createUnlistWhenFull,
            createIsPublic);
        createBusy = true;
        createOutcome = null;
        store.Create(request, outcome =>
        {
            createBusy = false;
            if (outcome == MusterCreateOutcome.Created)
            {
                createSucceeded = true;
            }
            else
            {
                createOutcome = outcome;
            }
        });
    }

    private void ResetCreateForm()
    {
        createCategory = MusterCategories.Social;
        createDescription = string.Empty;
        createLocation = null;
        createSpot = string.Empty;
        createStartIndex = 0;
        createDurationIndex = 1;
        createLimit = false;
        createMaxAttendees = DefaultMaxAttendees;
        maxAttendeesText = createMaxAttendees.ToString(Loc.Culture);
        createUnlistWhenFull = false;
        createIsPublic = true;
        createBusy = false;
        createOutcome = null;
        lastDescriptionLength = -1;
    }
}
