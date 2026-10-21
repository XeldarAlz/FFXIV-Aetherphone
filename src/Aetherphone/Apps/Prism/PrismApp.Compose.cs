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

internal sealed partial class PrismApp
{
    private const int TitleMaxLength = 60;
    private const int TagsMaxLength = 80;
    private const int MaxTags = 6;
    private const float FieldUnits = 34f;

    private readonly PhotoComposeSession composeSession;
    private readonly Action<bool> postSettled;
    private GlamGearDto[] captured = Array.Empty<GlamGearDto>();
    private bool composeDetails;
    private bool capturedVanilla;
    private string glamTitle = string.Empty;
    private string glamTags = string.Empty;
    private volatile int postOutcome;
    private CachedText capturedText;

    private void StartCompose()
    {
        postOutcome = OutcomePending;
        composeSession.Open(false);
        composeSession.MaxPhotos = GlamStore.MaxPhotos;
        composeDetails = false;
        captured = Array.Empty<GlamGearDto>();
        glamTitle = string.Empty;
        glamTags = string.Empty;
        Push(PrismScreen.Compose);
    }

    private void SettlePost(bool succeeded) => postOutcome = succeeded ? OutcomeSent : OutcomeFailed;

    private void DrawCompose(ImDrawListPtr drawList, Rect content, float scale)
    {
        ConsumePostOutcome();
        composeSession.ConsumePendingImport();
        var top = DrawHeader(drawList, content, string.Empty, Loc.T(L.Prism.PostGlam), true, scale);
        if (composeDetails)
        {
            DrawComposeDetails(drawList, content, top, scale);
            return;
        }

        var gutter = PrismArt.Gutter(scale);
        top += Typography.DrawWrappedLeft(new Vector2(content.Min.X + gutter, top), Loc.T(L.Prism.PhotosHint),
            PrismArt.Muted, TextStyles.Subheadline, content.Width - gutter * 2f) + Metrics.Space.Md * scale;
        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, PickStyle, GlamStore.PhotoRatio, true, true, Loc.T(L.Prism.PostGlam));
        var strip = StripRect(content, scale);
        if (PrismArt.Strip(drawList, strip, Loc.T(L.Common.Next), Arrow, true, composeSession.HasSelection))
        {
            captured = glamour.Capture();
            capturedVanilla = GlamourService.IsVanilla(captured) && PhotosAreVanilla();
            composeDetails = true;
        }

        var gridRect = new Rect(new Vector2(content.Min.X, pane.Max.Y + Metrics.Space.Md * scale),
            new Vector2(content.Max.X, strip.Min.Y - Metrics.Space.Lg * scale));
        using (AppSurface.BeginEdgeToEdge(gridRect))
        {
            composeSession.DrawPickGrid(gridRect, scale, PickStyle, true, Loc.T(L.Common.ImportFromPc),
                Loc.T(L.Prism.PostGlam));
        }
    }

    private void DrawComposeDetails(ImDrawListPtr drawList, Rect content, float top, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var width = content.Width - gutter * 2f;
        top = DrawField(drawList, left, top, width, Loc.T(L.Prism.TitleLabel), Loc.T(L.Prism.TitleHint), "##glamTitle",
            ref glamTitle, TitleMaxLength, scale);
        top = DrawField(drawList, left, top, width, Loc.T(L.Prism.TagsLabel), Loc.T(L.Prism.TagsHint), "##glamTags",
            ref glamTags, TagsMaxLength, scale);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.CapturedHeading), PrismArt.Muted, PrismFace.Label,
            labelPixels);
        PrismType.DrawRight(drawList, new Vector2(left + width, top),
            Loc.T(capturedVanilla ? L.Prism.VanillaStamp : L.Prism.ModdedStamp), PrismArt.Fore, PrismFace.Label,
            labelPixels);
        top += labelLine;
        PrismType.Draw(drawList, new Vector2(left, top), CapturedLine(), PrismArt.Fore, PrismFace.Label, labelPixels);
        top += labelLine + Metrics.Space.Sm * scale;
        var note = PrismType.Sentences(Loc.T(L.Prism.CapturedNote), TextStyles.Subheadline, width);
        Typography.DrawWrappedLeft(new Vector2(left, top), note, PrismArt.Muted, TextStyles.Subheadline,
            PrismType.BalancedWidth(note, TextStyles.Subheadline, width));
        var strip = StripRect(content, scale);
        if (glams.PostFailed && !glams.Posting)
        {
            var reason = glams.PostFailureText;
            var text = reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint);
            var height = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, width).Y;
            Typography.DrawWrappedLeft(new Vector2(left, strip.Min.Y - Metrics.Space.Lg * scale - height), text,
                PrismArt.Muted, TextStyles.Subheadline, width);
        }

        var label = Loc.T(glams.Posting ? L.Prism.Posting : L.Prism.Post);
        var ready = glamTitle.Trim().Length > 0 && !glams.Posting;
        if (PrismArt.Strip(drawList, strip, label, Arrow, true, ready, UiSound.Shutter))
        {
            PostGlam();
        }
    }

    private float DrawField(ImDrawListPtr drawList, float left, float top, float width, string label, string hint,
        string id, ref string text, int maxLength, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        PrismType.Draw(drawList, new Vector2(left, top), label, PrismArt.Muted, PrismFace.Label, labelPixels);
        top += PrismType.LineHeight(PrismFace.Label, labelPixels);
        var height = FieldUnits * scale;
        ImGui.SetCursorScreenPos(new Vector2(left, top + (height - ImGui.GetFrameHeight()) * 0.5f));
        ImGui.SetNextItemWidth(width);
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(text);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, PrismArt.Fore);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, PrismArt.Muted);
        ImGui.InputTextWithHint(id, hint, ref text, maxLength);
        ImGui.PopStyleColor(3);
        PrismArt.Hairline(drawList, left, left + width, top + height, ImGui.IsItemActive() ? PrismArt.Fore : PrismArt.Faint,
            scale);
        return top + height + Metrics.Space.Lg * scale;
    }

    private string CapturedLine()
    {
        var pieces = 0;
        var mods = 0;
        for (var index = 0; index < captured.Length; index++)
        {
            if (captured[index].ItemId != 0)
            {
                pieces++;
            }

            mods += captured[index].Mods?.Length ?? 0;
        }

        var key = HashCode.Combine(pieces, mods, Loc.Current.Code);
        return capturedText.IsCurrent(key)
            ? capturedText.Value
            : capturedText.Store(key, Loc.T(L.Prism.CapturedCounts, pieces, mods));
    }

    private bool PhotosAreVanilla()
    {
        var paths = composeSession.SelectedArray();
        for (var index = 0; index < paths.Length; index++)
        {
            if (ModScan.Stamped(configuration.PhotoModSigns, paths[index]) != ModSigns.None)
            {
                return false;
            }
        }

        return true;
    }

    private void PostGlam()
    {
        captured = glamour.Capture();
        capturedVanilla = GlamourService.IsVanilla(captured) && PhotosAreVanilla();
        var paths = composeSession.SelectedArray();
        var crops = composeSession.CropsArray();
        var count = Math.Min(paths.Length, GlamStore.MaxPhotos);
        var photos = new GlamPhotoDraft[count];
        for (var index = 0; index < count; index++)
        {
            photos[index] = new GlamPhotoDraft(paths[index], index < crops.Length ? crops[index] : default);
        }

        var (race, gender) = Appearance();
        postOutcome = OutcomePending;
        glams.Post(photos, captured, glamTitle.Trim(), SplitTags(glamTags), race, gender, capturedVanilla, postSettled);
    }

    private static string[] SplitTags(string text)
    {
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= MaxTags)
        {
            return parts;
        }

        var kept = new string[MaxTags];
        Array.Copy(parts, kept, MaxTags);
        return kept;
    }

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
        if (outcome == OutcomeSent && glams.Posted is { } created)
        {
            Go(PrismScreen.GlamDetail);
            ShowGlam(created);
        }
    }
}
