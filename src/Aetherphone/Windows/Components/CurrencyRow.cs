using System.Globalization;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallet;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Windows.Components;

internal static class CurrencyRow
{
    public const float Height = 54f;

    public static void Draw(Rect row, WalletEntry entry, ITextureProvider textures, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();

        var iconSize = 30f * scale;
        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        DrawIcon(drawList, entry.IconId, iconMin, iconMax, scale, textures);

        var textLeft = iconMax.X + 12f * scale;
        var gap = 10f * scale;
        var amountText = Format(entry.Amount);

        if (entry.Cap <= 0)
        {
            var amountSize = Typography.Measure(amountText, 1.1f);
            var amountX = row.Max.X - amountSize.X;
            Typography.Draw(new Vector2(amountX, row.Center.Y - amountSize.Y * 0.5f), amountText, theme.Accent, 1.1f);

            var name = Fit(entry.Name, amountX - gap - textLeft, 1f);
            var nameSize = Typography.Measure(name);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name, theme.TextStrong);
            return;
        }

        var topY = row.Min.Y + 9f * scale;
        var capText = " / " + Format(entry.Cap);
        var capSize = Typography.Measure(capText, 0.82f);
        var amountSize2 = Typography.Measure(amountText, 1.1f);
        var amountX2 = row.Max.X - capSize.X - amountSize2.X;
        Typography.Draw(new Vector2(amountX2, topY), amountText, theme.Accent, 1.1f);
        Typography.Draw(new Vector2(row.Max.X - capSize.X, topY + 3f * scale), capText, theme.TextMuted, 0.82f);

        var fittedName = Fit(entry.Name, amountX2 - gap - textLeft, 1f);
        Typography.Draw(new Vector2(textLeft, topY), fittedName, theme.TextStrong);

        var barTop = row.Max.Y - 14f * scale;
        var barMin = new Vector2(textLeft, barTop);
        var barMax = new Vector2(row.Max.X, barTop + 5f * scale);
        var rounding = (barMax.Y - barMin.Y) * 0.5f;
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(theme.SurfaceMuted), rounding);

        var fraction = Math.Clamp((float)((double)entry.Amount / entry.Cap), 0f, 1f);
        if (fraction > 0.001f)
        {
            var fillColor = entry.Amount >= entry.Cap ? theme.Danger : theme.Accent;
            var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * fraction, barMax.Y);
            drawList.AddRectFilled(barMin, fillMax, ImGui.GetColorU32(fillColor), rounding);
        }
    }

    public static void Hero(WalletEntry gil, ITextureProvider textures, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var centerX = origin.X + width * 0.5f;

        Typography.DrawCentered(new Vector2(centerX, origin.Y + 11f * scale), "GIL BALANCE", theme.TextMuted, 0.78f);

        var amountText = Format(gil.Amount);
        var amountSize = Typography.Measure(amountText, 2.0f);
        var iconSize = 32f * scale;
        var gap = 10f * scale;
        var hasIcon = gil.IconId != 0;
        var totalWidth = amountSize.X + (hasIcon ? iconSize + gap : 0f);
        var rowY = origin.Y + 34f * scale;
        var startX = centerX - totalWidth * 0.5f;

        if (hasIcon)
        {
            var iconMin = new Vector2(startX, rowY + amountSize.Y * 0.5f - iconSize * 0.5f);
            DrawIcon(drawList, gil.IconId, iconMin, iconMin + new Vector2(iconSize, iconSize), scale, textures);
            startX += iconSize + gap;
        }

        Typography.Draw(new Vector2(startX, rowY), amountText, theme.TextStrong, 2.0f);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 80f * scale));
    }

    private static void DrawIcon(ImDrawListPtr drawList, uint iconId, Vector2 min, Vector2 max, float scale, ITextureProvider textures)
    {
        if (iconId == 0)
        {
            return;
        }

        var texture = textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, 6f * scale);
    }

    private static string Format(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

    private static string Fit(string text, float maxWidth, float scale)
    {
        if (text.Length == 0)
        {
            return text;
        }

        if (maxWidth <= 0f)
        {
            return string.Empty;
        }

        if (Typography.Measure(text, scale).X <= maxWidth)
        {
            return text;
        }

        var low = 1;
        var high = text.Length;
        var best = "…";
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var candidate = text.Substring(0, mid) + "…";
            if (Typography.Measure(candidate, scale).X <= maxWidth)
            {
                best = candidate;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }
}
