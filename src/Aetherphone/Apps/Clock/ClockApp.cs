using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Clock;

internal sealed class ClockApp : IPhoneApp
{
    private const double EorzeaRate = 144.0 / 7.0;

    public string Id => "clock";

    public string DisplayName => "Clock";

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

        using (AppSurface.Begin(body))
        {
            var card = GroupCard.Begin(theme, 3, 92f);
            DrawRow(card.NextRow(), theme, "Local", LocalOffsetLabel(), local.ToString("HH:mm"), local.Hour, local.Minute, local.Second + local.Millisecond / 1000f);
            DrawRow(card.NextRow(), theme, "Eorzea", "In-game", EorzeaTime.Now().Formatted, (float)Math.Floor(eorzea / 3600 % 24), (float)Math.Floor(eorzea / 60 % 60), (float)(eorzea % 60));
            DrawRow(card.NextRow(), theme, "Server", "UTC", utc.ToString("HH:mm"), utc.Hour, utc.Minute, utc.Second + utc.Millisecond / 1000f);
            card.End();
        }
    }

    private static void DrawRow(Rect row, PhoneTheme theme, string name, string sublabel, string digital, float hours, float minutes, float seconds)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var clockRadius = (row.Height - 26f * scale) * 0.5f;
        var clockCenter = new Vector2(row.Min.X + clockRadius, row.Center.Y);
        AnalogClock.Draw(clockCenter, clockRadius, hours, minutes, seconds, theme);

        var textLeft = clockCenter.X + clockRadius + 16f * scale;
        Typography.Draw(new Vector2(textLeft, row.Center.Y - 17f * scale), name, theme.TextStrong, 1.15f);
        Typography.Draw(new Vector2(textLeft, row.Center.Y + 6f * scale), sublabel, theme.TextMuted, 0.8f);

        var digitalSize = Typography.Measure(digital, 1.9f);
        Typography.Draw(new Vector2(row.Max.X - digitalSize.X, row.Center.Y - digitalSize.Y * 0.5f), digital, theme.TextStrong, 1.9f);
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
