using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private bool gateBusy;

    private void GateMenus()
    {
        postSheet.Gate();
        threadSheet.Gate();
        threadView.GateMenus();
    }

    private void DrawGate(Rect area)
    {
        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(area, theme, scale);
        ui.Backdrop(screen);
        var drawList = ImGui.GetWindowDrawList();
        VelvetArt.Bloom(drawList, screen, theme.ScreenRounding * scale, 1.15f);

        var moonCenter = new Vector2(area.Center.X, area.Min.Y + area.Height * 0.24f);
        VelvetArt.Moon(drawList, moonCenter, 24f * scale, VelvetTheme.Moonlight, VelvetTheme.GroundTop);
        Typography.DrawCentered(new Vector2(area.Center.X, moonCenter.Y + 64f * scale), "Velvet", VelvetTheme.TitleInk,
            TextStyles.LargeTitle);

        var textWidth = MathF.Min(area.Width - 60f * scale, 320f * scale);
        var bodyHeight = Typography.DrawWrappedCentered(new Vector2(area.Center.X, moonCenter.Y + 106f * scale),
            Loc.T(L.Velvet.GateTagline), VelvetTheme.BodyInk, TextStyles.Body, textWidth);
        Typography.DrawWrappedCentered(new Vector2(area.Center.X, moonCenter.Y + 106f * scale + bodyHeight + 14f * scale),
            Loc.T(L.Velvet.GateConsent), VelvetTheme.MutedInk, TextStyles.Subheadline, textWidth);

        var buttonWidth = MathF.Min(area.Width - 48f * scale, 300f * scale);
        var buttonHeight = 46f * scale;
        var enterMin = new Vector2(area.Center.X - buttonWidth * 0.5f, area.Max.Y - buttonHeight * 2f - 30f * scale);
        var enterRect = new Rect(enterMin, new Vector2(enterMin.X + buttonWidth, enterMin.Y + buttonHeight));
        if (ui.PillButton(enterRect, gateBusy ? Loc.T(L.Velvet.GateWorking) : Loc.T(L.Velvet.GateEnterAction), true) &&
            !gateBusy)
        {
            AcceptGate();
        }

        var leaveRect = new Rect(new Vector2(enterMin.X, enterRect.Max.Y + 10f * scale),
            new Vector2(enterMin.X + buttonWidth, enterRect.Max.Y + 10f * scale + buttonHeight));
        if (ui.GhostButton(leaveRect, Loc.T(L.Velvet.GateLeave)))
        {
            navigation.GoHome();
        }
    }

    private void AcceptGate()
    {
        gateBusy = true;
        configuration.VelvetAcknowledgedGate = true;
        configuration.VelvetAcknowledgedGateVersion = Configuration.VelvetGateVersion;
        configuration.Save();
        store.AcceptGate(Configuration.VelvetGateVersion, _ => gateBusy = false);
        store.EnsureMe();
        stories.RefreshTray();
    }

    private VelvetTagCategory[]? orphanUnionSource;
    private string[] orphanUnion = Array.Empty<string>();

    private static bool ContainsTag(string[] tags, string tag)
    {
        for (var index = 0; index < tags.Length; index++)
        {
            if (string.Equals(tags[index], tag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnlisted(List<string> selected, string[] known)
    {
        for (var index = 0; index < selected.Count; index++)
        {
            if (!ContainsTag(known, selected[index]))
            {
                return true;
            }
        }

        return false;
    }

    private string[] CategoryUnion(VelvetTagCategory[] categories)
    {
        if (ReferenceEquals(orphanUnionSource, categories))
        {
            return orphanUnion;
        }

        var count = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            count += categories[index].Tags.Length;
        }

        var union = new string[count];
        var cursor = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            var tags = categories[index].Tags;
            for (var tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                union[cursor] = tags[tagIndex];
                cursor++;
            }
        }

        orphanUnionSource = categories;
        orphanUnion = union;
        return union;
    }
}
