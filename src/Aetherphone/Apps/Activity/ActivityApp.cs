using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Character;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Activity;

internal sealed class ActivityApp : IPhoneApp
{
    private const float RefreshIntervalSeconds = 3f;
    public string Id => "character";
    public string DisplayName => Loc.T(L.Character.Activity);
    public string Glyph => "Ac";
    public int BadgeCount => 0;
    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly LodestoneService lodestone;
    private readonly CollectService collect;
    private LocalCharacter? character;
    private ActivitySnapshot? activity;
    private RefreshCadence refreshCadence;

    public ActivityApp(GameData gameData, ITextureProvider textures, LodestoneService lodestone,
        CollectService collect)
    {
        this.gameData = gameData;
        this.textures = textures;
        this.lodestone = lodestone;
        this.collect = collect;
    }

    public void OnOpened() => Refresh();

    public void OnClosed()
    {
    }

    private void Refresh()
    {
        character = LocalCharacterReader.Read(gameData);
        activity = ActivityReader.Read(gameData);
        refreshCadence.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        if (refreshCadence.Advance(ImGui.GetIO().DeltaTime, RefreshIntervalSeconds))
        {
            Refresh();
        }

        AppHeader.Draw(context, DisplayName);
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        if (character is not { } profile || activity is not { } snapshot)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Character.LogInToView), theme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawRings(theme, snapshot, scale);
            DrawActivitySummary(theme, snapshot);
            DrawRetainers(theme, snapshot);
            DrawCollect(theme, profile);
            DrawProfile(profile, theme, scale);
        }
    }

    private static void DrawRings(PhoneTheme theme, ActivitySnapshot snapshot, float scale)
    {
        var ringsOrigin = ImGui.GetCursorScreenPos();
        var ringsWidth = ImGui.GetContentRegionAvail().X;
        UiAnchors.Report("character.rings",
            new Rect(ringsOrigin, ringsOrigin + new Vector2(ringsWidth, ActivityRings.Height * scale)));
        ActivityRings.Draw(theme, snapshot.JobFraction, snapshot.MasteryFraction, snapshot.CollectionFraction);
        DrawLegend(theme, scale, snapshot);
    }

    private static void DrawLegend(PhoneTheme theme, float scale, ActivitySnapshot snapshot)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var third = width / 3f;
        var height = 48f * scale;
        DrawLegendItem(new Vector2(origin.X + third * 0.5f, origin.Y), ActivityRings.RingOneTint,
            Loc.T(L.Character.RingJob), Percent(snapshot.JobFraction), theme);
        DrawLegendItem(new Vector2(origin.X + third * 1.5f, origin.Y), ActivityRings.RingTwoTint,
            Loc.T(L.Character.RingMastery), Percent(snapshot.MasteryFraction), theme);
        DrawLegendItem(new Vector2(origin.X + third * 2.5f, origin.Y), ActivityRings.RingThreeTint,
            Loc.T(L.Character.RingCollection), Percent(snapshot.CollectionFraction), theme);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawLegendItem(Vector2 top, Vector4 tint, string label, string value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dot = 6f * scale;
        var labelSize = Typography.Measure(label, TextStyles.Callout);
        var dotCenter = new Vector2(top.X - labelSize.X * 0.5f - dot - 5f * scale, top.Y + labelSize.Y * 0.5f);
        ImGui.GetWindowDrawList().AddCircleFilled(dotCenter, dot, ImGui.GetColorU32(tint));
        Typography.Draw(new Vector2(top.X - labelSize.X * 0.5f, top.Y), label, theme.TextMuted, TextStyles.Callout);
        var valueSize = Typography.Measure(value, TextStyles.Title3);
        Typography.Draw(new Vector2(top.X - valueSize.X * 0.5f, top.Y + labelSize.Y + 5f * scale), value,
            theme.TextStrong, TextStyles.Title3);
    }

    private void DrawActivitySummary(PhoneTheme theme, ActivitySnapshot snapshot)
    {
        SettingsSection.Header(Loc.T(L.Character.Summary), theme);
        var summaryOrigin = ImGui.GetCursorScreenPos();
        var summaryWidth = ImGui.GetContentRegionAvail().X;
        UiAnchors.Report("character.summary", new Rect(summaryOrigin,
            summaryOrigin + new Vector2(summaryWidth, 3f * ActivityStatRow.RowHeight * ImGuiHelpers.GlobalScale)));
        var card = GroupCard.Begin(theme, 3, ActivityStatRow.RowHeight);
        var jobLabel = snapshot.JobName.Length > 0
            ? $"{snapshot.JobName} · Lv {snapshot.Level}"
            : $"Lv {snapshot.Level}";
        var jobValue = snapshot.MaxLevel ? Loc.T(L.Character.JobLevelMax) : Percent(snapshot.JobFraction);
        var jobDetail = JobDetail(snapshot);
        ActivityStatRow.DrawProgress(card.NextRow(), theme, ActivityRings.RingOneTint, FontAwesomeIcon.Bolt, jobLabel,
            jobValue, snapshot.JobFraction, jobDetail);
        var masteryDetail = snapshot.JobsTotal > 0
            ? Loc.T(L.Character.JobsAtMax, snapshot.JobsAtMax, snapshot.JobsTotal)
            : string.Empty;
        ActivityStatRow.DrawProgress(card.NextRow(), theme, ActivityRings.RingTwoTint, FontAwesomeIcon.Crown,
            Loc.T(L.Character.JobMastery), Percent(snapshot.MasteryFraction), snapshot.MasteryFraction, masteryDetail);
        var collectionValue = $"{Number(snapshot.CollectionOwned)} / {Number(snapshot.CollectionTotal)}";
        var collectionDetail =
            $"{Loc.T(L.Character.Mounts)} {Number(snapshot.MountsOwned)} · {Loc.T(L.Character.Minions)} {Number(snapshot.MinionsOwned)}";
        ActivityStatRow.DrawProgress(card.NextRow(), theme, ActivityRings.RingThreeTint, FontAwesomeIcon.Dragon,
            Loc.T(L.Character.Collection), collectionValue, snapshot.CollectionFraction, collectionDetail);
        card.End();
    }

    private void DrawRetainers(PhoneTheme theme, ActivitySnapshot snapshot)
    {
        if (snapshot.RetainerCount <= 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Character.Retainers), theme);
        var card = GroupCard.Begin(theme, 1, ActivityStatRow.RowHeight);
        ActivityStatRow.Draw(card.NextRow(), theme, Accent.Blue, FontAwesomeIcon.Briefcase,
            Loc.T(L.Character.Retainers), Number(snapshot.RetainerCount), RetainerDetail(snapshot));
        card.End();
    }

    private void DrawCollect(PhoneTheme theme, LocalCharacter profile)
    {
        if (!lodestone.TryGetCachedId(profile.Name, profile.WorldName, out var lodestoneId))
        {
            SettingsSection.Header(Loc.T(L.Character.Achievements), theme);
            DrawHint(Loc.T(L.Character.CollectHint), theme);
            return;
        }

        var entry = collect.Request(lodestoneId);
        if (entry.State != CollectState.Ready || entry.Character is not { Achievements: { } achievements })
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Character.Achievements), theme);
        var card = GroupCard.Begin(theme, 1, ActivityStatRow.RowHeight);
        var fraction = achievements.Total <= 0 ? 0f : (float)((double)achievements.Count / achievements.Total);
        var value = $"{Number(achievements.Count)} / {Number(achievements.Total)}";
        var detail = $"{Number(achievements.Points)} {Loc.T(L.Character.AchievementPoints)}";
        ActivityStatRow.DrawProgress(card.NextRow(), theme, Accent.Violet, FontAwesomeIcon.Trophy,
            $"{Loc.T(L.Character.Achievements)} · {value}", detail, fraction);
        card.End();
    }

    private void DrawProfile(LocalCharacter profile, PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Character.Profile), theme);
        CharacterHeader.Draw(profile, theme, lodestone);
        var card = GroupCard.Begin(theme, 7, ProfileRow.RowHeight);
        ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Race), profile.Race, theme);
        ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Clan), profile.Clan, theme);
        ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Gender), profile.Gender, theme);
        ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Nameday), profile.Nameday, theme);
        ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Guardian), profile.Guardian, theme);
        ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.CityState), profile.CityState, theme);
        ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.GrandCompany), profile.GrandCompany, theme);
        card.End();
        if (profile.Gear.Count > 0)
        {
            SettingsSection.Header(Loc.T(L.Character.Equipment), theme);
            GearGrid.Draw(profile.Gear, textures, theme);
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
    }

    private static void DrawHint(string text, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
        using (Plugin.Fonts.Push(0.85f))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(text);
        }
    }

    private static string JobDetail(ActivitySnapshot snapshot)
    {
        if (snapshot.MaxLevel)
        {
            return string.Empty;
        }

        var remaining = snapshot.NeededExp - snapshot.CurrentExp;
        if (remaining <= 0)
        {
            return string.Empty;
        }

        return Loc.T(L.Character.ExpToLevel, Number(remaining), snapshot.Level + 1);
    }

    private static string RetainerDetail(ActivitySnapshot snapshot)
    {
        if (snapshot.RetainerVenturesReady > 0)
        {
            return Loc.T(L.Character.VenturesReady, snapshot.RetainerVenturesReady);
        }

        if (snapshot.RetainerVenturesActive > 0)
        {
            return Loc.T(L.Character.VenturesActive, snapshot.RetainerVenturesActive);
        }

        return string.Empty;
    }

    private static string Percent(float fraction) => $"{(int)MathF.Round(Math.Clamp(fraction, 0f, 1f) * 100f)}%";
    private static string Number(long value) => value.ToString("N0", Loc.Culture);

    public void Dispose()
    {
    }
}
