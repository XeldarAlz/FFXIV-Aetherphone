using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal readonly ref struct ChatSearchModel
{
    public readonly AppSkin Ui;
    public readonly ReadOnlySpan<TranscriptMessage> Messages;
    public readonly Action<string> ScrollTo;

    public ChatSearchModel(AppSkin ui, ReadOnlySpan<TranscriptMessage> messages, Action<string> scrollTo)
    {
        Ui = ui;
        Messages = messages;
        ScrollTo = scrollTo;
    }
}

internal sealed class ChatSearchController
{
    private const int SystemKind = 2;
    private const float FieldHeight = 32f;
    private const float ControlsWidth = 136f;
    private const int QueryMaxLength = 80;

    private readonly List<string> matches = new();
    private bool open;
    private bool focus;
    private string query = string.Empty;
    private string lastQuery = string.Empty;
    private int index;

    public bool Open => open;

    public void Toggle()
    {
        if (open)
        {
            Close();
        }
        else
        {
            open = true;
            focus = true;
        }
    }

    public void Close()
    {
        open = false;
        query = string.Empty;
        lastQuery = string.Empty;
        matches.Clear();
        index = 0;
    }

    public void Draw(Rect area, in ChatSearchModel model)
    {
        var ui = model.Ui;
        var theme = ui.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(new Vector2(area.Min.X, area.Max.Y), area.Max, ImGui.GetColorU32(theme.Separator), 1f);
        var fieldHeight = FieldHeight * scale;
        var controlsWidth = ControlsWidth * scale;
        var fieldMin = new Vector2(area.Min.X + 14f * scale, area.Center.Y - fieldHeight * 0.5f);
        var fieldMax = new Vector2(area.Max.X - controlsWidth, area.Center.Y + fieldHeight * 0.5f);
        Squircle.Fill(drawList, fieldMin, fieldMax, fieldHeight * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 12f * scale,
            area.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldMax.X - fieldMin.X - 20f * scale);
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }

        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputTextWithHint("##threadSearch", Loc.T(L.Common.Search), ref query, QueryMaxLength);
        }

        Sync(model.Messages, model.ScrollTo);
        var countText = matches.Count > 0
            ? string.Concat((index + 1).ToString(Loc.Culture), "/", matches.Count.ToString(Loc.Culture))
            : lastQuery.Length > 0 ? "0/0" : string.Empty;
        var buttonRadius = 12f * scale;
        var upCenter = new Vector2(area.Max.X - 66f * scale, area.Center.Y);
        var downCenter = new Vector2(area.Max.X - 42f * scale, area.Center.Y);
        var closeCenter = new Vector2(area.Max.X - 18f * scale, area.Center.Y);
        var countSize = Typography.Measure(countText, TextStyles.Footnote);
        Typography.Draw(new Vector2(upCenter.X - buttonRadius - 4f * scale - countSize.X,
            area.Center.Y - countSize.Y * 0.5f), countText, ui.MutedInk, TextStyles.Footnote);
        var hasMatches = matches.Count > 0;
        if (ui.IconButton(upCenter, buttonRadius, FontAwesomeIcon.ChevronUp.ToIconString(),
                hasMatches ? ui.BodyInk : ui.MutedInk, AppSkin.Transparent, 0.85f) && hasMatches)
        {
            index = (index - 1 + matches.Count) % matches.Count;
            model.ScrollTo(matches[index]);
        }

        if (ui.IconButton(downCenter, buttonRadius, FontAwesomeIcon.ChevronDown.ToIconString(),
                hasMatches ? ui.BodyInk : ui.MutedInk, AppSkin.Transparent, 0.85f) && hasMatches)
        {
            index = (index + 1) % matches.Count;
            model.ScrollTo(matches[index]);
        }

        if (ui.IconButton(closeCenter, buttonRadius, FontAwesomeIcon.Times.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 0.85f)
            || ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Close();
        }
    }

    private void Sync(ReadOnlySpan<TranscriptMessage> messages, Action<string> scrollTo)
    {
        var trimmed = query.Trim();
        var queryChanged = trimmed != lastQuery;
        lastQuery = trimmed;
        matches.Clear();
        if (trimmed.Length == 0)
        {
            index = 0;
            return;
        }

        for (var messageIndex = 0; messageIndex < messages.Length; messageIndex++)
        {
            var message = messages[messageIndex];
            if (message.Kind == SystemKind || (message.Flags & TranscriptFlags.Deleted) != 0)
            {
                continue;
            }

            if (message.Body.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(message.Id);
            }
        }

        index = Math.Clamp(index, 0, Math.Max(0, matches.Count - 1));
        if (queryChanged && matches.Count > 0)
        {
            index = matches.Count - 1;
            scrollTo(matches[index]);
        }
    }
}
