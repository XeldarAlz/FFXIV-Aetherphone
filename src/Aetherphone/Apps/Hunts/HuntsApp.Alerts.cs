using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private const float AlertsDisabledAlpha = 0.45f;
    private const float FeatureCardHeight = 76f;
    private const float FeatureTileSize = 40f;
    private const float AlertRowHeight = 52f;
    private const float AlertRowTile = 30f;
    private const float FooterButtonHeight = 40f;
    private const int RankColumns = 5;
    private const int ExpansionColumns = 3;
    private const int WorldColumns = 2;

    private static readonly string[] RankLabels = { "SS", "S", "A", "B", "F" };

    private static readonly string[] RankTileIds =
    {
        "hunts.tile.rank.ss", "hunts.tile.rank.s", "hunts.tile.rank.a", "hunts.tile.rank.b", "hunts.tile.rank.f",
    };

    private static readonly string[] FilterRankTileIds =
    {
        "hunts.filter.rank.ss", "hunts.filter.rank.s", "hunts.filter.rank.a", "hunts.filter.rank.b",
        "hunts.filter.rank.f",
    };

    private readonly List<HuntMobOverrideEntry> alertOverrides = new();
    private readonly List<string> alertOverrideLabels = new();
    private readonly List<string> alertOverrideRanks = new();
    private bool alertSettingsDirty;
    private string alertOverridesLanguage = string.Empty;
    private volatile bool alertOverridesDirty = true;

    private void MarkAlertOverridesDirty() => alertOverridesDirty = true;

    private void DrawAlerts(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("hunts.alerts"))
        using (AppSurface.Begin(navBar.Body))
        {
            DrawAlertsBody(scale);
            BottomSpacer(scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "hunts.alerts.nav", Loc.T(L.Hunts.AlertsTab),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawAlertsBody(float scale)
    {
        var signedIn = hunts.IsAuthenticated;
        if (!signedIn)
        {
            DrawAlertsSignInCard(scale);
        }

        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (signedIn ? 1f : AlertsDisabledAlpha)))
        {
            DrawRankTiles(signedIn, scale);
            DrawExpansionTiles(signedIn, scale);
            DrawWorldTiles(signedIn, scale);
            DrawMarkOverrides(signedIn, scale);
        }

        DrawMapMarkersCard(scale);
        DrawAlertsFooter(signedIn, scale);
    }

    private void DrawAlertsSignInCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var tile = FeatureTileSize * scale;
        var inner = width - pad * 3f - tile;
        var hint = Loc.T(L.Hunts.NotificationsSignInHint);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, inner).Y;
        var buttonHeight = FooterButtonHeight * scale;
        var height = pad + MathF.Max(tile, titleHeight + HuntsArt.LineGap * scale + hintHeight) +
                     HuntsArt.RowGap * scale + buttonHeight + pad;
        var card = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(drawList, card.Min, card.Max, HuntsArt.CardRadius * scale, elevated: true);
        var tileMin = new Vector2(card.Min.X + pad, card.Min.Y + pad);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        PhoneIcon.Draw(drawList, tileMin + new Vector2(tile, tile) * 0.5f, PhoneIcons.BellFilled, AccentRing.Ink,
            tile * 0.5f);
        var left = tileMin.X + tile + pad;
        Typography.Draw(drawList, new Vector2(left, tileMin.Y),
            Typography.FitText(Loc.T(L.Hunts.AlertsSignInTitle), inner, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(left, tileMin.Y + titleHeight + HuntsArt.LineGap * scale), hint,
            ui.MutedInk, TextStyles.Subheadline, inner);
        var button = new Rect(new Vector2(card.Min.X + pad, card.Max.Y - pad - buttonHeight),
            new Vector2(card.Max.X - pad, card.Max.Y - pad));
        if (ui.PillButton(button, Loc.T(L.Hunts.SignupLoginButton), true, "hunts.alerts.signIn"))
        {
            OpenAccount();
        }

        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
    }

    private void DrawRankTiles(bool interactive, float scale)
    {
        ui.SectionLabel(Loc.T(L.Hunts.RanksLabel), TextStyles.FootnoteEmphasized, 6f);
        var settings = hunts.NotificationSettings;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        UiAnchors.Report("hunts.alerts.ranks", new Rect(origin, origin + new Vector2(width,
            HuntsArt.TileGridHeight(RankLabels.Length, RankColumns, scale))));
        for (var index = 0; index < RankLabels.Length; index++)
        {
            var active = AlertRank(settings, index);
            var rect = HuntsArt.TileRect(origin, width, index, RankColumns, scale);
            if (!HuntsArt.ToggleTile(ui, RankTileIds[index], rect, RankLabels[index],
                    string.Empty, active, HuntsArt.RankColor(RankLabels[index], ui.Accent), interactive, scale))
            {
                continue;
            }

            SetAlertRank(settings, index, !active);
            alertSettingsDirty = true;
            UiFeedback.Play(active ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        ImGui.Dummy(new Vector2(width, HuntsArt.TileGridHeight(RankLabels.Length, RankColumns, scale) +
                                       HuntsArt.CardGap * scale));
    }

    private static bool AlertRank(HuntsNotificationSettings settings, int index) => index switch
    {
        0 => settings.RankSS,
        1 => settings.RankS,
        2 => settings.RankA,
        3 => settings.RankB,
        _ => settings.RankF,
    };

    private static void SetAlertRank(HuntsNotificationSettings settings, int index, bool value)
    {
        switch (index)
        {
            case 0:
                settings.RankSS = value;
                break;
            case 1:
                settings.RankS = value;
                break;
            case 2:
                settings.RankA = value;
                break;
            case 3:
                settings.RankB = value;
                break;
            default:
                settings.RankF = value;
                break;
        }
    }

    private void DrawExpansionTiles(bool interactive, float scale)
    {
        ui.SectionLabel(Loc.T(L.Hunts.ExpansionsLabel), TextStyles.FootnoteEmphasized, 6f);
        var settings = hunts.NotificationSettings;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var count = HuntExpansions.Ids.Length;
        for (var index = 0; index < count; index++)
        {
            var active = settings.IsExpansionActive(index);
            var rect = HuntsArt.TileRect(origin, width, index, ExpansionColumns, scale);
            if (!HuntsArt.ToggleTile(ui, ExpansionTileId(index), rect, ExpansionName(index),
                    HuntExpansions.Labels[index], active, ui.Accent, interactive, scale))
            {
                continue;
            }

            settings.ToggleExpansion(index);
            alertSettingsDirty = true;
            UiFeedback.Play(active ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        ImGui.Dummy(new Vector2(width, HuntsArt.TileGridHeight(count, ExpansionColumns, scale) +
                                       HuntsArt.CardGap * scale));
    }

    private static string ExpansionTileId(int index) => index switch
    {
        0 => "hunts.tile.expansion.0",
        1 => "hunts.tile.expansion.1",
        2 => "hunts.tile.expansion.2",
        3 => "hunts.tile.expansion.3",
        4 => "hunts.tile.expansion.4",
        _ => "hunts.tile.expansion.5",
    };

    private void DrawWorldTiles(bool interactive, float scale)
    {
        if (hunts.CurrentDataCenter is not { Length: > 0 } dataCenter)
        {
            return;
        }

        var worlds = HuntDataCenterWorlds.WorldsFor(dataCenter);
        if (worlds.Length == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Hunts.WorldsLabel), TextStyles.FootnoteEmphasized, 6f);
        var settings = hunts.NotificationSettings;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        for (var index = 0; index < worlds.Length; index++)
        {
            var active = settings.IsWorldEnabled(worlds[index]);
            var rect = HuntsArt.TileRect(origin, width, index, WorldColumns, scale);
            var label = ResolveWorldLabel(worlds[index]);
            if (!HuntsArt.ToggleTile(ui, label, rect, label, string.Empty, active, ui.Accent, interactive, scale))
            {
                continue;
            }

            settings.ToggleWorld(worlds[index]);
            alertSettingsDirty = true;
            UiFeedback.Play(active ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        ImGui.Dummy(new Vector2(width, HuntsArt.TileGridHeight(worlds.Length, WorldColumns, scale) +
                                       HuntsArt.CardGap * scale));
    }

    private void EnsureAlertOverrides()
    {
        if (!alertOverridesDirty && string.Equals(configuration.Language, alertOverridesLanguage, StringComparison.Ordinal))
        {
            return;
        }

        alertOverridesDirty = false;
        alertOverridesLanguage = configuration.Language;
        hunts.NotificationSettings.CollectMobOverrides(alertOverrides);
        alertOverrides.Sort((left, right) => string.Compare(ResolveMobLabel(mobCatalog.Find(left.MobId), left.MobId),
            ResolveMobLabel(mobCatalog.Find(right.MobId), right.MobId), StringComparison.CurrentCultureIgnoreCase));
        alertOverrideLabels.Clear();
        alertOverrideRanks.Clear();
        for (var index = 0; index < alertOverrides.Count; index++)
        {
            var mob = mobCatalog.Find(alertOverrides[index].MobId);
            alertOverrideLabels.Add(ResolveMobLabel(mob, alertOverrides[index].MobId));
            alertOverrideRanks.Add(mob?.Rank ?? string.Empty);
        }
    }

    private void DrawMarkOverrides(bool interactive, float scale)
    {
        EnsureAlertOverrides();
        ui.SectionLabel(Loc.T(L.Hunts.MarkNotificationsTitle), TextStyles.FootnoteEmphasized, 6f);
        if (alertOverrides.Count == 0)
        {
            DrawHintCard(Loc.T(L.Hunts.MarkNotificationsEmptyHint), scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(ui, alertOverrides.Count, AlertRowHeight);
        card.SeparatorInset = AlertRowTile + HuntsArt.RowGap;
        for (var index = 0; index < alertOverrides.Count; index++)
        {
            var row = card.NextRow();
            var entry = alertOverrides[index];
            var tile = AlertRowTile * scale;
            HuntsArt.RankTile(drawList, new Vector2(row.Min.X, row.Center.Y - tile * 0.5f), tile,
                alertOverrideRanks[index], HuntsArt.RankColor(alertOverrideRanks[index], ui.Accent));
            var modeText = entry.Mode switch
            {
                HuntMobNotificationMode.Enabled => Loc.T(L.Hunts.AlertModeOn),
                HuntMobNotificationMode.EnabledOnWorld => ResolveWorldLabel(entry.WorldId ?? string.Empty),
                HuntMobNotificationMode.Disabled => Loc.T(L.Hunts.AlertModeOff),
                _ => Loc.T(L.Hunts.AlertModeDefault),
            };
            var capsuleWidth = HuntsArt.CapsuleWidth(modeText, scale);
            var capsuleHeight = HuntsArt.CapsuleHeight(scale);
            var capsuleMin = new Vector2(row.Max.X - capsuleWidth, row.Center.Y - capsuleHeight * 0.5f);
            var off = entry.Mode == HuntMobNotificationMode.Disabled;
            HuntsArt.Capsule(drawList, capsuleMin, modeText,
                off ? Palette.WithAlpha(ui.TitleInk, 0.10f) : IconTile.Surface(ui.Accent),
                off ? ui.MutedInk : AccentRing.Ink, scale);
            var left = row.Min.X + tile + HuntsArt.RowGap * scale;
            var nameHeight = Typography.LineHeight(TextStyles.Body);
            Typography.Draw(drawList, new Vector2(left, row.Center.Y - nameHeight * 0.5f),
                Typography.FitText(alertOverrideLabels[index],
                    MathF.Max(1f, capsuleMin.X - HuntsArt.RowGap * scale - left), TextStyles.Body), ui.TitleInk,
                TextStyles.Body);
            var bounds = new Rect(new Vector2(card.Bounds.Min.X, row.Min.Y), new Vector2(card.Bounds.Max.X, row.Max.Y));
            var hovered = interactive && UiInteract.Hover(bounds.Min, bounds.Max);
            if (hovered)
            {
                drawList.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(ui.HoverWash));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (interactive && UiInteract.Click(bounds.Min, bounds.Max, hovered))
            {
                OpenOverrideDetail(entry);
            }
        }

        card.End();
        DrawFootnote(Loc.T(L.Hunts.MarkOverridesHint), scale);
    }

    private void OpenOverrideDetail(in HuntMobOverrideEntry entry)
    {
        SaveAlertsIfDirty();
        var windows = hunts.Windows;
        HuntWindowDto? match = null;
        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            if (!string.Equals(window.MobId, entry.MobId, StringComparison.Ordinal))
            {
                continue;
            }

            match ??= window;
            if (entry.WorldId is { } preferred &&
                string.Equals(window.WorldId, preferred, StringComparison.OrdinalIgnoreCase))
            {
                match = window;
                break;
            }
        }

        if (match is not null)
        {
            OpenDetail(match, Loc.T(L.Hunts.AlertsTab));
            return;
        }

        var worlds = HuntDataCenterWorlds.WorldsFor(hunts.CurrentDataCenter ?? string.Empty);
        var worldId = entry.WorldId ?? (worlds.Length > 0 ? worlds[0] : string.Empty);
        OpenDetailFor(entry.MobId, worldId, 0, Loc.T(L.Hunts.AlertsTab));
    }

    private void DrawHintCard(string text, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var height = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, width - pad * 2f).Y + pad * 2f;
        ui.Card(drawList, origin, origin + new Vector2(width, height), HuntsArt.CardRadius * scale);
        Typography.DrawWrappedLeft(origin + new Vector2(pad, pad), text, ui.MutedInk, TextStyles.Subheadline,
            width - pad * 2f);
        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
    }

    private void DrawFootnote(string text, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var top = origin.Y + HuntsArt.TileGap * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + pad, top), text, ui.MutedInk,
            TextStyles.Footnote, width - pad * 2f);
        ImGui.Dummy(new Vector2(width, height + HuntsArt.TileGap * scale + HuntsArt.CardGap * scale));
    }

    private void DrawMapMarkersCard(float scale)
    {
        ui.SectionLabel(Loc.T(L.Hunts.MapSection), TextStyles.FootnoteEmphasized, 6f);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var tile = FeatureTileSize * scale;
        var toggleSize = new Vector2(Metrics.Size.ToggleWidth * scale, Metrics.Size.ToggleHeight * scale);
        var textLeft = origin.X + pad + tile + HuntsArt.RowGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - toggleSize.X - HuntsArt.RowGap * scale - textLeft);
        var description = Loc.T(L.Hunts.MapMarkersHint);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var descriptionHeight = Typography.MeasureWrappedBlock(description, TextStyles.Footnote, textWidth).Y;
        var height = MathF.Max(FeatureCardHeight * scale,
            pad * 2f + titleHeight + HuntsArt.LineGap * scale + descriptionHeight);
        var card = new Rect(origin, origin + new Vector2(width, height));
        UiAnchors.Report("hunts.alerts.map", card);
        ui.Card(drawList, card.Min, card.Max, HuntsArt.CardRadius * scale);
        var tileMin = new Vector2(card.Min.X + pad, card.Center.Y - tile * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(HuntsArt.OpenColor));
        PhoneIcon.Draw(drawList, tileMin + new Vector2(tile, tile) * 0.5f, PhoneIcons.MapPin, AccentRing.Ink,
            tile * 0.5f);
        var top = card.Center.Y - (titleHeight + HuntsArt.LineGap * scale + descriptionHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(L.Hunts.NativeMapMarkersLabel), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + titleHeight + HuntsArt.LineGap * scale), description,
            ui.MutedInk, TextStyles.Footnote, textWidth);
        var toggleMin = new Vector2(card.Max.X - pad - toggleSize.X, card.Center.Y - toggleSize.Y * 0.5f);
        var value = Toggle.Draw("hunts.alerts.mapMarkers", new Rect(toggleMin, toggleMin + toggleSize),
            configuration.HuntsNativeMapMarkers, frameTheme);
        if (value != configuration.HuntsNativeMapMarkers)
        {
            configuration.HuntsNativeMapMarkers = value;
            configuration.Save();
            if (value)
            {
                huntsMapMarkers.ForceRedraw();
            }
        }

        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
    }

    private void DrawAlertsFooter(bool signedIn, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = HuntsArt.TileGap * scale;
        var height = FooterButtonHeight * scale;
        var half = (width - gap) * 0.5f;
        var resetRect = new Rect(origin, origin + new Vector2(half, height));
        var tourRect = new Rect(new Vector2(origin.X + half + gap, origin.Y), origin + new Vector2(width, height));
        if (signedIn && ui.DangerGhostButton(resetRect, Loc.T(L.Hunts.ResetToDefault)))
        {
            hunts.NotificationSettings.ResetToDefault();
            alertSettingsDirty = true;
            UiFeedback.Play(UiSound.Refresh);
        }

        if (ui.GhostButton(signedIn ? tourRect : new Rect(origin, origin + new Vector2(width, height)),
                Loc.T(L.Hunts.ResetTutorial)))
        {
            SaveAlertsIfDirty();
            OnboardingState.Reset(Id);
            navigation.GoHome();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void SaveAlertsIfDirty()
    {
        if (!alertSettingsDirty)
        {
            return;
        }

        alertSettingsDirty = false;
        hunts.SaveNotificationSettings();
    }
}
