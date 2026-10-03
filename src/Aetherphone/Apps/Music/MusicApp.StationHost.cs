using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float HostCardPad = 14f;
    private const float HostButtonHeight = 32f;
    private const int PinComposerMaxLines = 3;
    private const float PinFieldBaseHeight = 36f;

    private static readonly LocString[] PinTemplates =
    {
        L.Music.Live.TemplateBreak,
        L.Music.Live.TemplateBack,
        L.Music.Live.TemplateReturned,
        L.Music.Live.TemplateNext,
        L.Music.Live.TemplateRequests,
    };

    private readonly SoftWrapEditor pinEditor = new();
    private readonly ChipRail pinTemplateRail = new();
    private readonly string[] pinTemplateLabels = new string[PinTemplates.Length];
    private readonly bool[] pinTemplateActive = new bool[PinTemplates.Length];
    private readonly RadioCountLabel hostRoomLabel = new();
    private readonly RadioWrappedText hostSubText = new();
    private readonly RadioWrappedText hostPinnedText = new();
    private readonly RadioFittedText hostTitleFit = new();

    private void DrawStationHost(Rect panel, float scale)
    {
        ImGui.PushID("radio.host");
        using (AppSurface.Begin(panel))
        {
            DrawHostStatus(scale);
            DrawRequestsToggle(scale);
            SectionHeader.Draw(ui, Loc.T(L.Music.Live.PinnedNotice), false, 0f);
            DrawHostPinned(scale);
            DrawPinComposer(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        ImGui.PopID();
    }

    private void DrawHostStatus(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = HostCardPad * scale;
        var innerWidth = width - pad * 2f;
        var live = room.IsLive;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var pillHeight = LivePill.Height(scale);
        var showSettings = !live && community.OwnsStation;
        if (!live)
        {
            hostSubText.Wrap(Loc.T(L.Music.Live.OffAirSub), innerWidth, TextStyles.Footnote);
        }

        var height = pad * 2f + titleHeight + Metrics.Space.Xs * scale
                     + (live ? pillHeight : hostSubText.Height)
                     + (showSettings ? Metrics.Space.Md * scale + HostButtonHeight * scale : 0f);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        var title = live ? Loc.T(L.Music.Live.OnAirTitle) : Loc.T(L.Music.Live.OffAirTitle);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), hostTitleFit.Fit(title, innerWidth,
            TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        var detailTop = min.Y + pad + titleHeight + Metrics.Space.Xs * scale;
        if (live)
        {
            var label = hostRoomLabel.Prefixed(Loc.T(L.Music.LiveBadge), L.Music.Live.InRoomCount, room.ListenerCount);
            LivePill.Draw(drawList, new Vector2(min.X + pad, detailTop), label, ui.Theme.Danger, clock, scale);
        }
        else
        {
            hostSubText.Draw(drawList, new Vector2(min.X + pad, detailTop), ui.MutedInk, TextStyles.Footnote);
        }

        var openSettings = false;
        if (showSettings)
        {
            var buttonTop = detailTop + hostSubText.Height + Metrics.Space.Md * scale;
            var label = Loc.T(L.Music.Live.BroadcastSettings);
            var buttonWidth = MathF.Min(innerWidth, AppSkin.PillWidthFor(label, HostButtonHeight * scale));
            var buttonMin = new Vector2(min.X + pad, buttonTop);
            openSettings = ui.GhostButton(new Rect(buttonMin,
                buttonMin + new Vector2(buttonWidth, HostButtonHeight * scale)), label);
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        if (openSettings)
        {
            OpenMyStation();
        }
    }

    private void DrawHostPinned(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = HostCardPad * scale;
        var pinned = room.Pinned;
        var unpinLabel = Loc.T(L.Music.Live.Unpin);
        var buttonHeight = HostButtonHeight * scale;
        var buttonWidth = AppSkin.PillWidthFor(unpinLabel, buttonHeight);
        var textWidth = width - pad * 3f - buttonWidth;
        var text = pinned ?? Loc.T(L.Music.Live.NothingPinned);
        hostPinnedText.Wrap(text, pinned is null ? width - pad * 2f : textWidth, TextStyles.Subheadline);
        var height = MathF.Max(buttonHeight, hostPinnedText.Height) + pad * 2f;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        hostPinnedText.Draw(drawList, new Vector2(min.X + pad, min.Y + (height - hostPinnedText.Height) * 0.5f),
            pinned is null ? ui.MutedInk : ui.TitleInk, TextStyles.Subheadline);
        var unpin = false;
        if (pinned is not null)
        {
            var buttonMin = new Vector2(max.X - pad - buttonWidth, min.Y + (height - buttonHeight) * 0.5f);
            unpin = ui.GhostButton(new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, buttonHeight)),
                unpinLabel);
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        if (unpin)
        {
            room.Unpin();
        }
    }

    private void DrawPinComposer(float scale)
    {
        for (var index = 0; index < PinTemplates.Length; index++)
        {
            pinTemplateLabels[index] = Loc.T(PinTemplates[index]);
            pinTemplateActive[index] = false;
        }

        var tapped = pinTemplateRail.Draw(ui, pinTemplateLabels, pinTemplateActive);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
        if (tapped >= 0)
        {
            pinEditor.Adopt(pinTemplateLabels[tapped]);
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var buttonLabel = Loc.T(L.Music.Live.Pin);
        var buttonHeight = HostButtonHeight * scale;
        var buttonWidth = AppSkin.PillWidthFor(buttonLabel, buttonHeight);
        var fieldHeight = PinFieldBaseHeight * scale + pinEditor.Growth(PinComposerMaxLines);
        var field = new Rect(origin, new Vector2(origin.X + width - buttonWidth - Metrics.Space.Sm * scale,
            origin.Y + fieldHeight));
        var submitted = SubmitField.Multiline(field, "##radioPinComposer", Loc.T(L.Music.Live.PinHint), pinEditor,
            theme, RadioRoomSession.MaxPinnedLength, PinComposerMaxLines, FontAwesomeIcon.Thumbtack);
        var canPin = pinEditor.HasContent && room.IsAttached && room.CanModerate;
        var buttonMin = new Vector2(field.Max.X + Metrics.Space.Sm * scale, field.Max.Y - buttonHeight
            - (fieldHeight - buttonHeight) * 0.5f * (pinEditor.LineCount > 1 ? 0f : 1f));
        var pinTapped = AppSkin.PillButton(new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, buttonHeight)),
            buttonLabel, true, canPin, theme);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, fieldHeight + Metrics.Space.Sm * scale));
        if ((submitted || pinTapped) && canPin && room.Pin(pinEditor.Text))
        {
            pinEditor.Adopt(string.Empty);
        }
    }
}
