using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class PersonPicker
{
    private const float RowHeight = 46f;
    private const float DebounceSeconds = 0.20f;
    private const double RevealSeconds = 0.16;

    private readonly MentionSuggestions suggestions;
    private string query = string.Empty;
    private string applied = string.Empty;
    private float debounce;
    private double openedAt = -1d;
    private int openedFrame = -1;

    public PersonPicker(MentionSuggestions suggestions)
    {
        this.suggestions = suggestions;
    }

    public bool IsOpen { get; private set; }

    public void Open()
    {
        IsOpen = true;
        query = string.Empty;
        applied = string.Empty;
        debounce = 0f;
        openedAt = ImGui.GetTime();
        openedFrame = ImGui.GetFrameCount();
        suggestions.Clear();
    }

    public void Close()
    {
        IsOpen = false;
        openedAt = -1d;
        query = string.Empty;
        applied = string.Empty;
        suggestions.Clear();
    }

    public void Gate()
    {
        if (IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public MentionSuggestDto? Draw(Rect screen, PhoneTheme theme, RemoteImageCache images, LodestoneService lodestone)
    {
        if (!IsOpen)
        {
            return null;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var reveal = Easing.EaseOutQuint(Math.Clamp((float)((ImGui.GetTime() - openedAt) / RevealSeconds), 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.7f, 0f, 1f));

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(screen.Min, screen.Max,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f * alpha)));

        var sheetHeight = MathF.Min(screen.Height * 0.62f, 360f * scale);
        var sheetTop = screen.Max.Y - sheetHeight * reveal;
        var min = new Vector2(screen.Min.X, sheetTop);
        var max = new Vector2(screen.Max.X, screen.Max.Y);
        Squircle.Fill(drawList, min, max, 18f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, MathF.Min(0.99f, theme.GroupedCard.W + 0.4f))));

        var pad = 14f * scale;
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), Loc.T(L.PhotoTag.PickPerson),
            theme.TextStrong, TextStyles.Headline);

        var fieldTop = min.Y + pad + 26f * scale;
        ImGui.SetCursorScreenPos(new Vector2(min.X + pad, fieldTop));
        ImGui.SetNextItemWidth(max.X - min.X - pad * 2f);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, theme.SurfaceMuted))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.GetFrameCount() == openedFrame + 1)
            {
                ImGui.SetKeyboardFocusHere();
            }

            ImGui.InputTextWithHint("##personPicker", Loc.T(L.PhotoTag.SearchHint), ref query, 32);
        }

        TickSearch();

        var rows = suggestions.Results;
        var listTop = fieldTop + ImGui.GetFrameHeight() + 8f * scale;
        MentionSuggestDto? picked = null;
        if (rows.Length == 0)
        {
            var message = suggestions.Loading ? Loc.T(L.Social.MentionSearching) : Loc.T(L.PhotoTag.NoPeople);
            Typography.DrawCentered(drawList, new Vector2(screen.Center.X, listTop + 20f * scale), message,
                theme.TextMuted, 0.9f);
        }

        for (var index = 0; index < rows.Length; index++)
        {
            var rowMin = new Vector2(min.X + pad * 0.5f, listTop + index * RowHeight * scale);
            var rowMax = new Vector2(max.X - pad * 0.5f, rowMin.Y + RowHeight * scale);
            if (rowMax.Y > max.Y - 6f * scale)
            {
                break;
            }

            if (ImGui.IsMouseHoveringRect(rowMin, rowMax))
            {
                Squircle.Fill(drawList, rowMin, rowMax, 9f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f)));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    picked = rows[index];
                }
            }

            var row = rows[index];
            var avatarRadius = 15f * scale;
            var avatarCenter = new Vector2(rowMin.X + 8f * scale + avatarRadius, (rowMin.Y + rowMax.Y) * 0.5f);
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, row.DisplayName, string.Empty,
                row.AvatarUrl, images, lodestone, 0.8f, 28);
            var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
            var name = SocialIdentity.Name(row.DisplayName, row.Handle);
            var nameSize = Typography.Measure(name, 0.95f, FontWeight.SemiBold);
            Typography.Draw(drawList, new Vector2(textLeft, rowMin.Y + 6f * scale), name, theme.TextStrong, 0.95f,
                FontWeight.SemiBold);
            Typography.Draw(drawList, new Vector2(textLeft, rowMin.Y + 6f * scale + nameSize.Y), "@" + row.Handle,
                theme.TextMuted, 0.82f);
        }

        if (ImGui.GetFrameCount() > openedFrame + 1
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !ImGui.IsMouseHoveringRect(min, max))
        {
            Close();
            return null;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Close();
            return null;
        }

        if (picked is not null)
        {
            Close();
        }

        return picked;
    }

    private void TickSearch()
    {
        var trimmed = query.Trim();
        if (trimmed.Length < 1)
        {
            applied = string.Empty;
            suggestions.Clear();
            return;
        }

        if (string.Equals(trimmed, applied, StringComparison.Ordinal))
        {
            return;
        }

        debounce += ImGui.GetIO().DeltaTime;
        if (debounce < DebounceSeconds)
        {
            return;
        }

        debounce = 0f;
        applied = trimmed;
        suggestions.Request(trimmed);
    }
}
