using Aetherphone.Core;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class ChatHeaderControls
{
    private const float IconRadius = 16f;
    private const float LockOffset = 24f;
    private const float SearchOffset = 52f;
    private const float BannerHeight = 26f;
    public const float ReservedRightWidth = SearchOffset + IconRadius;

    public static void DrawLock(AppSkin ui, Rect area, float rowCenterY, bool encrypted, KeyVaultState vault,
        Action onOpen)
    {
        var scale = UiScale.Current;
        var tooltip = encrypted
            ? Loc.T(L.Encryption.EncryptedIndicator)
            : vault == KeyVaultState.Provisioning
                ? Loc.T(L.Encryption.SettingUp)
                : vault == KeyVaultState.Locked
                    ? Loc.T(L.Encryption.StateLocked)
                    : Loc.T(L.Encryption.PlaintextIndicator);
        var glyph = encrypted ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
        var center = new Vector2(area.Max.X - LockOffset * scale, rowCenterY);
        if (ui.IconButton(center, IconRadius * scale, glyph.ToIconString(), encrypted ? ui.Accent : ui.MutedInk,
                AppSkin.Transparent, 1f, tooltip, HoverLabelSide.Below))
        {
            onOpen();
        }
    }

    public static void DrawSearchToggle(AppSkin ui, Rect area, float rowCenterY, bool open, Action onToggle)
    {
        var scale = UiScale.Current;
        var center = new Vector2(area.Max.X - SearchOffset * scale, rowCenterY);
        if (ui.IconButton(center, IconRadius * scale, FontAwesomeIcon.Search.ToIconString(),
                open ? ui.Accent : ui.MutedInk, AppSkin.Transparent, 0.95f, Loc.T(L.Common.Search),
                HoverLabelSide.Below))
        {
            onToggle();
        }
    }

    public static void DrawBanner(AppSkin ui, ref Rect listRect, string text, Vector4 mutedInk, Action onDismiss)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var height = BannerHeight * scale;
        var min = listRect.Min;
        var max = new Vector2(listRect.Max.X, listRect.Min.Y + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.14f)));
        Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f),
            text, mutedInk, 0.76f, FontWeight.Medium);
        if (UiInteract.Hover(min, max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            onDismiss();
        }

        listRect = new Rect(new Vector2(listRect.Min.X, max.Y), listRect.Max);
    }

    public static void DrawPromptBanner(AppSkin ui, ref Rect listRect, string text, Vector4 mutedInk, Action onOpen,
        Action onDismiss)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var height = BannerHeight * scale;
        var min = listRect.Min;
        var max = new Vector2(listRect.Max.X, listRect.Min.Y + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.14f)));
        Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f),
            text, mutedInk, 0.76f, FontWeight.Medium);
        var dismissMin = new Vector2(max.X - height, min.Y);
        AppSkin.Icon(new Vector2(max.X - height * 0.5f, (min.Y + max.Y) * 0.5f),
            FontAwesomeIcon.Times.ToIconString(), mutedInk, 0.6f);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (UiInteract.Hover(dismissMin, max))
            {
                onDismiss();
            }
            else if (UiInteract.Hover(min, max))
            {
                onOpen();
            }
        }

        listRect = new Rect(new Vector2(listRect.Min.X, max.Y), listRect.Max);
    }
}
