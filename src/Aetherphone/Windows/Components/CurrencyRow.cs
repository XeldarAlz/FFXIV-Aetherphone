using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallet;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Windows.Components;

internal static class CurrencyRow
{
    public const float Height = 62f;
    private const float HeroHeight = 132f;
    private const float HeroRounding = 24f;
    private const float IconSize = 38f;
    private const float TextGap = 14f;

    private static readonly Vector4 CappedTint = new(0.98f, 0.80f, 0.36f, 1f);
    private static readonly Vector4 IconBacking = new(1f, 1f, 1f, 0.05f);

    public static Rect Hero(WalletEntry gil, ITextureProvider textures, in AppPalette palette)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = HeroHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = HeroRounding * scale;
        var surface = Palette.Lighten(palette.BackdropTop, 0.12f) with { W = 1f };
        Elevation.Card(drawList, min, max, rounding, scale, 0.9f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(surface));
        Material.TopGlow(drawList, min, max, rounding, palette.Accent, 0.92f, 0.22f);
        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var centerX = min.X + width * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 27f * scale), Loc.T(L.Wallet.GilBalance),
            palette.HeaderInk, TextStyles.FootnoteEmphasized);

        var amountText = Format(gil.Amount);
        var hasIcon = gil.IconId != 0;
        var iconSize = 36f * scale;
        var gap = 12f * scale;
        var available = width - 44f * scale - (hasIcon ? iconSize + gap : 0f);
        var amountScale = FitScale(amountText, available, 2.35f, FontWeight.Bold);
        var amountSize = Typography.Measure(amountText, amountScale, FontWeight.Bold);
        var totalWidth = amountSize.X + (hasIcon ? iconSize + gap : 0f);
        var rowCenterY = min.Y + height * 0.62f;
        var startX = centerX - totalWidth * 0.5f;
        if (hasIcon)
        {
            var iconMin = new Vector2(startX, rowCenterY - iconSize * 0.5f);
            DrawIcon(drawList, gil.IconId, iconMin, iconMin + new Vector2(iconSize, iconSize), scale, textures);
            startX += iconSize + gap;
        }

        Typography.Draw(drawList, new Vector2(startX, rowCenterY - amountSize.Y * 0.5f), amountText, palette.TitleInk,
            amountScale, FontWeight.Bold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
        return new Rect(min, max);
    }

    public static void Draw(Rect band, Rect content, WalletEntry entry, ITextureProvider textures,
        in AppPalette palette, float cardRounding, bool roundTop, bool roundBottom)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        DrawHover(drawList, band, cardRounding, roundTop, roundBottom, scale);

        var iconSize = IconSize * scale;
        var iconMin = new Vector2(content.Min.X, content.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        Squircle.Fill(drawList, iconMin, iconMax, iconSize * Metrics.Radius.TileFactor, ImGui.GetColorU32(IconBacking));
        DrawIcon(drawList, entry.IconId, iconMin, iconMax, scale, textures);
        var textLeft = iconMax.X + TextGap * scale;
        var amountText = Format(entry.Amount);

        if (entry.Cap <= 0)
        {
            var amountSize = Typography.Measure(amountText, TextStyles.Title2);
            var amountX = content.Max.X - amountSize.X;
            Typography.Draw(drawList, new Vector2(amountX, content.Center.Y - amountSize.Y * 0.5f), amountText,
                palette.Accent, TextStyles.Title2);
            var name = Typography.FitText(entry.Name, amountX - 12f * scale - textLeft, TextStyles.Headline);
            var nameSize = Typography.Measure(name, TextStyles.Headline);
            Typography.Draw(drawList, new Vector2(textLeft, content.Center.Y - nameSize.Y * 0.5f), name,
                palette.TitleInk, TextStyles.Headline);
            return;
        }

        var capped = entry.Amount >= entry.Cap;
        var accent = capped ? CappedTint : palette.Accent;
        var barTop = content.Max.Y - 15f * scale;
        var lineCenterY = (content.Min.Y + barTop) * 0.5f;
        var capText = " / " + Format(entry.Cap);
        var capSize = Typography.Measure(capText, TextStyles.Footnote);
        var amountMeasure = Typography.Measure(amountText, TextStyles.Title3);
        var amountX2 = content.Max.X - capSize.X - amountMeasure.X;
        var amountY = lineCenterY - amountMeasure.Y * 0.5f;
        Typography.Draw(drawList, new Vector2(amountX2, amountY), amountText, accent, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(content.Max.X - capSize.X, amountY + 4f * scale), capText,
            palette.MutedInk, TextStyles.Footnote);

        var label = Typography.FitText(entry.Name, amountX2 - 12f * scale - textLeft, TextStyles.Headline);
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, lineCenterY - labelSize.Y * 0.5f), label, palette.TitleInk,
            TextStyles.Headline);

        var barMin = new Vector2(textLeft, barTop);
        var barMax = new Vector2(content.Max.X, barTop + 6f * scale);
        var barRounding = (barMax.Y - barMin.Y) * 0.5f;
        Squircle.Fill(drawList, barMin, barMax, barRounding, ImGui.GetColorU32(Palette.WithAlpha(palette.TitleInk, 0.10f)));
        var fraction = Math.Clamp((float)((double)entry.Amount / entry.Cap), 0f, 1f);
        if (fraction > 0.001f)
        {
            var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * fraction, barMax.Y);
            Squircle.Fill(drawList, barMin, fillMax, barRounding, ImGui.GetColorU32(accent));
        }
    }

    private static void DrawHover(ImDrawListPtr drawList, Rect band, float cardRounding, bool roundTop,
        bool roundBottom, float scale)
    {
        if (!UiInteract.Hover(band.Min, band.Max))
        {
            return;
        }

        var inset = 1.5f * scale;
        var min = new Vector2(band.Min.X + inset, band.Min.Y + (roundTop ? inset : 0f));
        var max = new Vector2(band.Max.X - inset, band.Max.Y - (roundBottom ? inset : 0f));
        var rounding = MathF.Max(0f, cardRounding - inset);
        ImDrawFlags flags;
        if (roundTop && roundBottom)
        {
            flags = ImDrawFlags.RoundCornersAll;
        }
        else if (roundTop)
        {
            flags = ImDrawFlags.RoundCornersTop;
        }
        else if (roundBottom)
        {
            flags = ImDrawFlags.RoundCornersBottom;
        }
        else
        {
            flags = ImDrawFlags.RoundCornersAll;
            rounding = 0f;
        }

        var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0.10f : 0.055f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding, flags);
    }

    private static void DrawIcon(ImDrawListPtr drawList, uint iconId, Vector2 min, Vector2 max, float scale,
        ITextureProvider textures)
    {
        if (iconId == 0)
        {
            return;
        }

        var texture = textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, 7f * scale);
    }

    private static float FitScale(string text, float maxWidth, float maxScale, FontWeight weight)
    {
        var candidate = maxScale;
        while (candidate > 1.2f && Typography.Measure(text, candidate, weight).X > maxWidth)
        {
            candidate -= 0.15f;
        }

        return candidate;
    }

    private static string Format(long amount) => amount.ToString("N0", Loc.Culture);
}
