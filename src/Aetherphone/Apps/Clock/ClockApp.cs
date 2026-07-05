using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Clock;

internal sealed class ClockApp : IPhoneApp
{
    private const double EorzeaRate = 144.0 / 7.0;
    private const float WorldRowHeight = 76f;
    public string Id => "clock";
    public string DisplayName => Loc.T(L.Apps.Clock);
    public string Glyph => "T";
    public Vector4 Accent => new(0.18f, 0.18f, 0.22f, 1f);
    public int BadgeCount => 0;

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        AppHeader.Draw(context, DisplayName);
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        var local = DateTime.Now;
        var utc = DateTime.UtcNow;
        var eorzea = EorzeaSeconds();
        var localSeconds = local.Second + local.Millisecond / 1000f;
        var utcSeconds = utc.Second + utc.Millisecond / 1000f;
        var eorzeaTime = EorzeaTime.Now();
        var eorzeaSecondsOfMinute = (float)(eorzea % 60.0);
        using (AppSurface.Begin(body))
        {
            var available = ImGui.GetContentRegionAvail().Y;
            var spacer = 14f * scale;
            var worldHeight = 2f * WorldRowHeight * scale;
            var heroHeight = Math.Clamp(available - worldHeight - spacer, 132f * scale, 208f * scale);
            DrawHero(theme, local, localSeconds, heroHeight);
            ImGui.Dummy(new Vector2(0f, spacer));
            var card = GroupCard.Begin(theme, 2, WorldRowHeight);
            DrawWorldRow(card.NextRow(), theme, "Eorzea", Loc.T(L.Clock.InGame), eorzeaTime.Formatted, eorzeaTime.Hour,
                eorzeaTime.Minute, eorzeaSecondsOfMinute);
            DrawWorldRow(card.NextRow(), theme, Loc.T(L.Clock.Server), "UTC", utc.ToString("HH:mm"), utc.Hour,
                utc.Minute, utcSeconds);
            card.End();
        }
    }

    private static void DrawHero(PhoneTheme theme, DateTime local, float localSeconds, float heroHeight)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var heroMin = origin;
        var heroMax = new Vector2(origin.X + width, origin.Y + heroHeight);
        var rounding = 24f * scale;
        Elevation.Card(drawList, heroMin, heroMax, rounding, scale);
        Squircle.Fill(drawList, heroMin, heroMax, rounding, ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(drawList, heroMin, heroMax, rounding, scale);
        var pad = 20f * scale;
        var clockRadius = (heroHeight - pad * 2f) * 0.5f;
        var clockCenter = new Vector2(heroMin.X + pad + clockRadius, heroMin.Y + heroHeight * 0.5f);
        ProgressRing.Glow(clockCenter, clockRadius * 0.92f, theme.Accent, 0.45f);
        AnalogClock.Draw(clockCenter, clockRadius, local.Hour, local.Minute, localSeconds, theme);
        var textX = clockCenter.X + clockRadius + 22f * scale;
        var digital = local.ToString("HH:mm");
        var date = local.ToString("ddd d MMM", Loc.Culture);
        var zone = $"{Loc.T(L.Clock.Local)} · {LocalOffsetLabel()}";
        var digitalSize = Typography.Measure(digital, TextStyles.LargeTitle);
        var dateSize = Typography.Measure(date, TextStyles.Subheadline);
        var zoneSize = Typography.Measure(zone, TextStyles.FootnoteEmphasized);
        var stackHeight = digitalSize.Y + 6f * scale + dateSize.Y + 4f * scale + zoneSize.Y;
        var startY = clockCenter.Y - stackHeight * 0.5f;
        Typography.Draw(new Vector2(textX, startY), digital, theme.TextStrong, TextStyles.LargeTitle);
        Typography.Draw(new Vector2(textX, startY + digitalSize.Y + 6f * scale), date, theme.TextMuted,
            TextStyles.Subheadline);
        Typography.Draw(new Vector2(textX, startY + digitalSize.Y + dateSize.Y + 10f * scale), zone, theme.Accent,
            TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, heroHeight));
    }

    private static void DrawWorldRow(Rect row, PhoneTheme theme, string name, string sublabel, string digital,
        float hours, float minutes, float seconds)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dialRadius = (row.Height - 22f * scale) * 0.5f;
        var dialCenter = new Vector2(row.Min.X + dialRadius, row.Center.Y);
        AnalogClock.Draw(dialCenter, dialRadius, hours, minutes, seconds, theme);
        var textLeft = dialCenter.X + dialRadius + 16f * scale;
        Typography.Draw(new Vector2(textLeft, row.Center.Y - 17f * scale), name, theme.TextStrong, TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, row.Center.Y + 4f * scale), sublabel, theme.TextMuted,
            TextStyles.Footnote);
        var digitalSize = Typography.Measure(digital, TextStyles.Title1);
        Typography.Draw(new Vector2(row.Max.X - digitalSize.X, row.Center.Y - digitalSize.Y * 0.5f), digital,
            theme.TextStrong, TextStyles.Title1);
    }

    private static double EorzeaSeconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 * EorzeaRate;

    private static string LocalOffsetLabel()
    {
        var offset = DateTimeOffset.Now.Offset;
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return offset.Minutes == 0
            ? $"UTC{sign}{Math.Abs(offset.Hours)}"
            : $"UTC{sign}{Math.Abs(offset.Hours)}:{Math.Abs(offset.Minutes):D2}";
    }

    public void Dispose()
    {
    }
}
