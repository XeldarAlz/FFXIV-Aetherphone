using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Character;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.MyCharacter;

internal sealed class MyCharacterApp : IPhoneApp
{
    private const float RefreshIntervalSeconds = 3f;

    public string Id => "character";

    public string DisplayName => Loc.T(L.Character.Activity);

    public string Glyph => "Ac";

    public Vector4 Accent => ActivityRings.RingOneTint;

    public int BadgeCount => 0;

    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly LodestoneService lodestone;
    private readonly CollectService collect;

    private LocalCharacter? character;
    private ActivitySnapshot? activity;
    private float sinceRefresh;

    public MyCharacterApp(GameData gameData, ITextureProvider textures, LodestoneService lodestone, CollectService collect)
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
        sinceRefresh = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh >= RefreshIntervalSeconds)
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
            DrawWallet(theme, snapshot);
            DrawCollect(theme, profile);
            DrawProfile(profile, theme, scale);
        }
    }

    private static void DrawRings(PhoneTheme theme, ActivitySnapshot snapshot, float scale)
    {
        ActivityRings.Draw(theme, snapshot.JobFraction, snapshot.TomestoneFraction, snapshot.CollectionFraction);
        DrawLegend(theme, scale, snapshot);
    }

    private static void DrawLegend(PhoneTheme theme, float scale, ActivitySnapshot snapshot)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var third = width / 3f;
        var height = 36f * scale;

        DrawLegendItem(new Vector2(origin.X + third * 0.5f, origin.Y), ActivityRings.RingOneTint, Loc.T(L.Character.RingJob), Percent(snapshot.JobFraction), theme);
        DrawLegendItem(new Vector2(origin.X + third * 1.5f, origin.Y), ActivityRings.RingTwoTint, Loc.T(L.Character.RingTomestones), Percent(snapshot.TomestoneFraction), theme);
        DrawLegendItem(new Vector2(origin.X + third * 2.5f, origin.Y), ActivityRings.RingThreeTint, Loc.T(L.Character.RingCollection), Percent(snapshot.CollectionFraction), theme);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawLegendItem(Vector2 top, Vector4 tint, string label, string value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dot = 5f * scale;
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var dotCenter = new Vector2(top.X - labelSize.X * 0.5f - dot - 4f * scale, top.Y + labelSize.Y * 0.5f);
        ImGui.GetWindowDrawList().AddCircleFilled(dotCenter, dot, ImGui.GetColorU32(tint));

        Typography.Draw(new Vector2(top.X - labelSize.X * 0.5f, top.Y), label, theme.TextMuted, TextStyles.Caption1);

        var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        Typography.Draw(new Vector2(top.X - valueSize.X * 0.5f, top.Y + 15f * scale), value, theme.TextStrong, TextStyles.SubheadlineEmphasized);
    }

    private void DrawActivitySummary(PhoneTheme theme, ActivitySnapshot snapshot)
    {
        SettingsSection.Header(Loc.T(L.Character.Summary), theme);
        var card = GroupCard.Begin(theme, 3, ActivityStatRow.RowHeight);

        var jobLabel = snapshot.JobName.Length > 0 ? $"{snapshot.JobName} · Lv {snapshot.Level}" : $"Lv {snapshot.Level}";
        var jobValue = snapshot.MaxLevel ? Loc.T(L.Character.JobLevelMax) : Percent(snapshot.JobFraction);
        var jobDetail = JobDetail(snapshot);
        ActivityStatRow.DrawProgress(card.NextRow(), theme, ActivityRings.RingOneTint, FontAwesomeIcon.Bolt, jobLabel, jobValue, snapshot.JobFraction, jobDetail);

        var tomeLabel = snapshot.TomestoneName.Length > 0 ? snapshot.TomestoneName : Loc.T(L.Character.WeeklyTomestones);
        var tomeValue = $"{Number(snapshot.TomestoneAmount)} / {Number(snapshot.TomestoneCap)}";
        ActivityStatRow.DrawProgress(card.NextRow(), theme, ActivityRings.RingTwoTint, FontAwesomeIcon.Coins, tomeLabel, tomeValue, snapshot.TomestoneFraction);

        var collectionValue = $"{Number(snapshot.CollectionOwned)} / {Number(snapshot.CollectionTotal)}";
        ActivityStatRow.DrawProgress(card.NextRow(), theme, ActivityRings.RingThreeTint, FontAwesomeIcon.Dragon, Loc.T(L.Character.Collection), collectionValue, snapshot.CollectionFraction);

        card.End();
    }

    private void DrawWallet(PhoneTheme theme, ActivitySnapshot snapshot)
    {
        var hasRetainers = snapshot.RetainerCount > 0;
        var rowCount = hasRetainers ? 3 : 2;

        SettingsSection.Header(Loc.T(L.Apps.Wallet), theme);
        var card = GroupCard.Begin(theme, rowCount, ActivityStatRow.RowHeight);

        ActivityStatRow.Draw(card.NextRow(), theme, Styling.AccentAmber, FontAwesomeIcon.Coins, Loc.T(L.Character.Gil), Number(snapshot.Gil));

        var mountsDetail = $"{Loc.T(L.Character.Mounts)} {Number(snapshot.MountsOwned)} · {Loc.T(L.Character.Minions)} {Number(snapshot.MinionsOwned)}";
        ActivityStatRow.Draw(card.NextRow(), theme, Styling.AccentMint, FontAwesomeIcon.Paw, Loc.T(L.Character.Collection), Percent(snapshot.CollectionFraction), mountsDetail);

        if (hasRetainers)
        {
            ActivityStatRow.Draw(card.NextRow(), theme, Styling.AccentBlue, FontAwesomeIcon.Briefcase, Loc.T(L.Character.Retainers), Number(snapshot.RetainerCount), RetainerDetail(snapshot));
        }

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
        ActivityStatRow.DrawProgress(card.NextRow(), theme, Styling.AccentViolet, FontAwesomeIcon.Trophy, $"{Loc.T(L.Character.Achievements)} · {value}", detail, fraction);

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
            ImGui.TextWrapped(text);
        }
    }

    private static string JobDetail(ActivitySnapshot snapshot)
    {
        if (snapshot.MaxLevel)
        {
            return snapshot.JobsTotal > 0 ? Loc.T(L.Character.JobsAtMax, snapshot.JobsAtMax, snapshot.JobsTotal) : string.Empty;
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
