using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherphone.Apps.Jobs.Widgets;

internal sealed unsafe class JobWidget : IHomeWidget
{
    private const int RefreshMilliseconds = 2000;
    private const int FastRefreshMilliseconds = 250;
    private const int FastWindowMilliseconds = 3000;
    private const int GearsetCapacity = 100;
    private const int ButtonCount = 3;
    private const int ButtonControlBase = 10;
    private const float ButtonUnits = 30f;
    private const float IconFraction = 1.3f;
    private const float IconRadiusFraction = 0.18f;
    private const float RingColumnFraction = 0.32f;
    private const uint HandCategoryId = 33;
    private const uint LandCategoryId = 32;

    private static readonly Vector4 JobsAccent = AppAccents.For("jobs");
    private static readonly string[] SlotKeys = { "gearset1", "gearset2", "gearset3" };

    private readonly GameData gameData;
    private readonly IReadOnlyList<WidgetOption> options;
    private readonly int[] gearsetIds = new int[GearsetCapacity];
    private readonly uint[] gearsetJobs = new uint[GearsetCapacity];
    private readonly FontAwesomeIcon[] gearsetIcons = new FontAwesomeIcon[GearsetCapacity];
    private readonly int[] chosen = new int[ButtonCount];
    private readonly Dictionary<uint, string> abbreviations = new();
    private readonly Dictionary<uint, string> names = new();
    private readonly WidgetStates<RingState> rings = new();
    private int gearsetCount;
    private int activeGearsetId = -1;
    private uint classJobId;
    private int level;
    private int itemLevel = -1;
    private float experience;
    private bool maxLevel;
    private bool hasJob;
    private long fastUntilTick;
    private WidgetRefresh refresh;
    private CachedText levelText;
    private CachedText itemLevelText;
    private CachedText experienceText;

    public JobWidget(GameData gameData)
    {
        this.gameData = gameData;
        options = new[]
        {
            new WidgetOption(SlotKeys[0], L.WidgetsAdventure.SlotFirst, CollectGearsets),
            new WidgetOption(SlotKeys[1], L.WidgetsAdventure.SlotSecond, CollectGearsets),
            new WidgetOption(SlotKeys[2], L.WidgetsAdventure.SlotThird, CollectGearsets),
        };
    }

    public string Id => "jobs.current";
    public string DisplayName => Loc.T(L.WidgetsAdventure.JobName);
    public string Description => Loc.T(L.WidgetsAdventure.JobDescription);
    public string AppId => "jobs";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;
    public IReadOnlyList<WidgetOption> Options => options;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var loggedIn = AdventureWidgetArt.IsLoggedIn;
        var sample = !loggedIn && context.Preview;
        if (loggedIn)
        {
            var interval = Environment.TickCount64 < fastUntilTick ? FastRefreshMilliseconds : RefreshMilliseconds;
            if (refresh.Due(interval))
            {
                Refresh();
            }
        }

        if (!sample && (!loggedIn || !hasJob))
        {
            var top = WidgetChrome.Header(context, ink, AppId, L.WidgetsAdventure.JobName, JobsAccent);
            WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, top), FontAwesomeIcon.UserCircle, default,
                Loc.T(L.WidgetsAdventure.LogIn), string.Empty);
            return;
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, sample);
            return;
        }

        DrawMedium(context, ink, sample);
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, bool sample)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var jobId = sample ? WidgetSamples.JobClassJobId : classJobId;
        var headerBottom = WidgetChrome.Header(context, ink, AppId, JobName(jobId), JobsAccent);
        var headlineHeight = WidgetText.LineHeight(WidgetType.Headline);
        var lineTop = content.Max.Y - headlineHeight;
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, lineTop), LevelText(sample), ink.Primary,
            WidgetType.Headline, content.Width * 0.55f);
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        WidgetText.DrawRight(context.DrawList, content.Max.X,
            lineTop + (headlineHeight - captionHeight) * 0.5f, ExperienceText(sample), ink.Secondary,
            WidgetType.Caption);
        var ringTop = headerBottom + gutter;
        var ringBottom = lineTop - gutter;
        var radius = MathF.Max(1f, MathF.Min(ringBottom - ringTop, content.Width) * 0.5f);
        DrawRing(context, ink, sample, new Vector2(content.Center.X, (ringTop + ringBottom) * 0.5f), radius, jobId);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, bool sample)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var drawList = context.DrawList;
        var jobId = sample ? WidgetSamples.JobClassJobId : classJobId;
        var buttonHeight = ButtonUnits * scale;
        var buttonsTop = content.Max.Y - buttonHeight;
        var top = new Rect(content.Min, new Vector2(content.Max.X, buttonsTop - gutter));
        var diameter = MathF.Min(top.Height, top.Width * RingColumnFraction);
        var radius = diameter * 0.5f;
        DrawRing(context, ink, sample, new Vector2(top.Min.X + radius, top.Center.Y), radius, jobId);

        var textLeft = top.Min.X + diameter + gutter * 1.5f;
        var textWidth = MathF.Max(1f, top.Max.X - textLeft);
        var titleHeight = WidgetText.LineHeight(WidgetType.Title);
        var bodyHeight = WidgetText.LineHeight(WidgetType.Body);
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var gap = WidgetMetrics.RowGap * scale;
        var blockTop = top.Center.Y - (titleHeight + bodyHeight + captionHeight + gap * 2f) * 0.5f;
        WidgetText.Draw(drawList, new Vector2(textLeft, blockTop), JobName(jobId), ink.Primary, WidgetType.Title,
            textWidth);
        var levelTop = blockTop + titleHeight + gap;
        var experienceLabel = ExperienceText(sample);
        var experienceWidth = Typography.Measure(experienceLabel, WidgetType.Caption).X;
        WidgetText.Draw(drawList, new Vector2(textLeft, levelTop), LevelText(sample), ink.Primary, WidgetType.Body,
            MathF.Max(1f, textWidth - experienceWidth - gutter));
        WidgetText.DrawRight(drawList, top.Max.X, levelTop + (bodyHeight - captionHeight) * 0.5f,
            experienceLabel, ink.Secondary, WidgetType.Caption);
        var itemLevelLabel = ItemLevelText(sample);
        if (itemLevelLabel.Length > 0)
        {
            WidgetText.Draw(drawList, new Vector2(textLeft, levelTop + bodyHeight + gap), itemLevelLabel,
                ink.Secondary, WidgetType.Caption, textWidth);
        }

        DrawButtons(context, ink, sample, new Rect(new Vector2(content.Min.X, buttonsTop), content.Max));
    }

    private void DrawRing(in WidgetContext context, in WidgetInk ink, bool sample, Vector2 center, float radius,
        uint jobId)
    {
        var target = sample ? WidgetSamples.JobExperience : maxLevel ? 1f : experience;
        var fraction = rings.For(context).Fill.Fraction(target, context.Delta, !context.Preview);
        WidgetChrome.Ring(context.DrawList, ink, center, radius, WidgetChrome.RingThickness(radius, context.Scale),
            fraction, JobsAccent);
        var inner = radius - WidgetChrome.RingThickness(radius, context.Scale);
        var half = inner * IconFraction * 0.5f;
        var size = half * 2f;
        if (!AdventureWidgetArt.GameIcon(context.DrawList, GameData.JobIconId(jobId), center - new Vector2(half),
                center + new Vector2(half), size * IconRadiusFraction, ink))
        {
            ProgressRing.CenterIcon(context.DrawList, center, FontAwesomeIcon.UserShield, ink.Accent(JobsAccent),
                inner * 0.8f);
        }
    }

    private void DrawButtons(in WidgetContext context, in WidgetInk ink, bool sample, Rect row)
    {
        var count = ResolveButtons(context.Config, sample);
        if (count == 0)
        {
            return;
        }

        var gutter = WidgetMetrics.Gutter * context.Scale;
        var width = (row.Width - gutter * (ButtonCount - 1)) / ButtonCount;
        for (var index = 0; index < count; index++)
        {
            var left = row.Min.X + index * (width + gutter);
            var rect = new Rect(new Vector2(left, row.Min.Y), new Vector2(left + width, row.Max.Y));
            var gearsetId = chosen[index];
            var jobId = sample ? WidgetSamples.GearsetJobIds[index] : JobOf(gearsetId);
            var icon = sample ? RoleIcon(jobId) : IconOf(gearsetId);
            var active = !sample && gearsetId == activeGearsetId;
            if (!WidgetControls.Button(context, ink, ButtonControlBase + index, rect, icon, Abbreviation(jobId),
                    active ? JobsAccent : default) || sample)
            {
                continue;
            }

            var result = GearsetActions.Equip(gearsetId);
            if (result != GearsetEquipResult.Sent)
            {
                ShellToast.Show(Loc.T(result == GearsetEquipResult.Busy
                    ? L.Jobs.EquipBusy
                    : L.WidgetsAdventure.EquipFailed));
                continue;
            }

            fastUntilTick = Environment.TickCount64 + FastWindowMilliseconds;
            refresh.Expire();
        }
    }

    private int ResolveButtons(string config, bool sample)
    {
        if (sample)
        {
            for (var index = 0; index < ButtonCount; index++)
            {
                chosen[index] = index;
            }

            return ButtonCount;
        }

        var count = 0;
        for (var slot = 0; slot < ButtonCount; slot++)
        {
            var configured = WidgetConfig.GetInt(config, SlotKeys[slot], -1);
            if (configured >= 0 && IndexOfGearset(configured) >= 0 && !IsChosen(configured, count))
            {
                chosen[count] = configured;
                count++;
            }
        }

        for (var index = 0; index < gearsetCount && count < ButtonCount; index++)
        {
            if (IsChosen(gearsetIds[index], count))
            {
                continue;
            }

            chosen[count] = gearsetIds[index];
            count++;
        }

        return count;
    }

    private bool IsChosen(int gearsetId, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (chosen[index] == gearsetId)
            {
                return true;
            }
        }

        return false;
    }

    private int IndexOfGearset(int gearsetId)
    {
        for (var index = 0; index < gearsetCount; index++)
        {
            if (gearsetIds[index] == gearsetId)
            {
                return index;
            }
        }

        return -1;
    }

    private uint JobOf(int gearsetId)
    {
        var index = IndexOfGearset(gearsetId);
        return index < 0 ? 0u : gearsetJobs[index];
    }

    private FontAwesomeIcon IconOf(int gearsetId)
    {
        var index = IndexOfGearset(gearsetId);
        return index < 0 ? FontAwesomeIcon.UserShield : gearsetIcons[index];
    }

    private void Refresh()
    {
        var player = gameData.LocalPlayer;
        var playerState = PlayerState.Instance();
        if (player is null || playerState is null)
        {
            return;
        }

        classJobId = player.ClassJob.RowId;
        var expArrayIndex = gameData.JobExpArrayIndex(classJobId);
        var levels = playerState->ClassJobLevels;
        level = expArrayIndex >= 0 && expArrayIndex < levels.Length ? levels[expArrayIndex] : 0;
        maxLevel = playerState->MaxLevel > 0 && level >= playerState->MaxLevel;
        var needed = maxLevel ? 0L : (long)playerState->GetCurrentClassJobNeededExp();
        var current = maxLevel ? 0L : (long)playerState->GetCurrentClassJobExp();
        experience = needed > 0 ? Math.Clamp(current / (float)needed, 0f, 1f) : maxLevel ? 1f : 0f;
        hasJob = classJobId != 0;
        ReadGearsets();
    }

    private void ReadGearsets()
    {
        gearsetCount = 0;
        activeGearsetId = -1;
        itemLevel = -1;
        var module = RaptureGearsetModule.Instance();
        if (module is null)
        {
            return;
        }

        activeGearsetId = module->CurrentGearsetIndex;
        var entries = module->Entries;
        for (var index = 0; index < entries.Length && gearsetCount < GearsetCapacity; index++)
        {
            var entry = entries[index];
            if ((entry.Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0)
            {
                continue;
            }

            var jobId = (uint)entry.ClassJob;
            gearsetIds[gearsetCount] = entry.Id;
            gearsetJobs[gearsetCount] = jobId;
            gearsetIcons[gearsetCount] = RoleIcon(jobId);
            gearsetCount++;
            if (entry.Id == activeGearsetId)
            {
                itemLevel = entry.ItemLevel;
            }
        }
    }

    private void CollectGearsets(List<WidgetChoice> target)
    {
        target.Add(new WidgetChoice(string.Empty, L.WidgetsAdventure.Automatic));
        var module = RaptureGearsetModule.Instance();
        if (module is null || !AdventureWidgetArt.IsLoggedIn)
        {
            return;
        }

        var entries = module->Entries;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if ((entry.Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0)
            {
                continue;
            }

            var name = entry.NameString;
            var abbreviation = Abbreviation((uint)entry.ClassJob);
            var text = name.Length == 0 ? abbreviation : string.Concat(abbreviation, " · ", name);
            target.Add(new WidgetChoice(entry.Id.ToString(CultureInfo.InvariantCulture), text));
        }
    }

    private FontAwesomeIcon RoleIcon(uint jobId)
    {
        if (!gameData.TryGetClassJobDivision(jobId, out var jobType, out var role, out _, out var categoryId))
        {
            return FontAwesomeIcon.UserShield;
        }

        if (categoryId == HandCategoryId)
        {
            return FontAwesomeIcon.Hammer;
        }

        if (categoryId == LandCategoryId)
        {
            return FontAwesomeIcon.Leaf;
        }

        return (jobType, role) switch
        {
            (1, _) or (_, 1) => FontAwesomeIcon.ShieldAlt,
            (2, _) or (6, _) or (_, 4) => FontAwesomeIcon.Heartbeat,
            (3, _) or (_, 2) => FontAwesomeIcon.FistRaised,
            (4, _) => FontAwesomeIcon.Crosshairs,
            _ => FontAwesomeIcon.Magic,
        };
    }

    private string Abbreviation(uint jobId)
    {
        if (abbreviations.TryGetValue(jobId, out var cached))
        {
            return cached;
        }

        var abbreviation = gameData.JobAbbreviation(jobId);
        abbreviations[jobId] = abbreviation;
        return abbreviation;
    }

    private string JobName(uint jobId)
    {
        if (names.TryGetValue(jobId, out var cached))
        {
            return cached;
        }

        var name = gameData.JobName(jobId);
        names[jobId] = name;
        return name;
    }

    private string LevelText(bool sample)
    {
        var value = sample ? WidgetSamples.JobLevel : level;
        return levelText.IsCurrent(value)
            ? levelText.Value
            : levelText.Store(value, Loc.T(L.WidgetsAdventure.Level, value));
    }

    private string ItemLevelText(bool sample)
    {
        var value = sample ? WidgetSamples.JobItemLevel : itemLevel;
        if (value <= 0)
        {
            return string.Empty;
        }

        return itemLevelText.IsCurrent(value)
            ? itemLevelText.Value
            : itemLevelText.Store(value, Loc.T(L.WidgetsAdventure.ItemLevel, value));
    }

    private string ExperienceText(bool sample)
    {
        if (!sample && maxLevel)
        {
            return experienceText.IsCurrent(-1)
                ? experienceText.Value
                : experienceText.Store(-1, Loc.T(L.WidgetsAdventure.MaxLevel));
        }

        var percent = (int)MathF.Floor((sample ? WidgetSamples.JobExperience : experience) * 100f);
        return experienceText.IsCurrent(percent)
            ? experienceText.Value
            : experienceText.Store(percent, Loc.T(L.WidgetsAdventure.Experience, percent));
    }

    public void Dispose()
    {
    }

    private sealed class RingState
    {
        public WidgetEase Fill;
    }
}
