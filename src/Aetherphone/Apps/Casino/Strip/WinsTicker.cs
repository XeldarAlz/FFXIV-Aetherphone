using System.Text;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class WinsTicker
{
    public const float Height = 40f;
    public const string Separator = "     ·     ";

    private const float Pad = 14f;
    private const string MarqueeKey = "casino.floor.ticker";

    private readonly StringBuilder builder = new();
    private string text = string.Empty;
    private int builtVersion = -1;
    private LanguageInfo? builtLanguage;

    public static float HeightFor(float scale) => Height * scale;

    public void Reset()
    {
        builtVersion = -1;
        text = string.Empty;
    }

    public float Draw(ImDrawListPtr drawList, CasinoFloorTickDto[] ticks, int version, Vector2 origin, float width,
        float scale)
    {
        if (ticks.Length == 0)
        {
            return origin.Y;
        }

        if (version != builtVersion || !ReferenceEquals(builtLanguage, Loc.Current))
        {
            builtVersion = version;
            builtLanguage = Loc.Current;
            text = Compose(ticks, builder);
        }

        var height = Height * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = height * 0.5f;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(StageText.CapsuleFill));
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(CasinoColors.Money with { W = 0.35f }),
            MathF.Max(1f, scale));
        var pad = Pad * scale;
        var eyebrow = Loc.T(L.Strip.TickerEyebrow);
        var eyebrowStyle = TextStyles.FootnoteEmphasized;
        var eyebrowSize = Typography.Measure(eyebrow, eyebrowStyle);
        var dotCenter = new Vector2(min.X + pad + CasinoArt.LiveDotRadius * scale, min.Y + height * 0.5f);
        CasinoArt.LiveDot(drawList, dotCenter, scale, CasinoColors.LightA, true);
        var eyebrowLeft = dotCenter.X + (CasinoArt.LiveDotRadius + 6f) * scale;
        Typography.Draw(drawList, new Vector2(eyebrowLeft, min.Y + (height - eyebrowSize.Y) * 0.5f), eyebrow,
            CasinoColors.Money, eyebrowStyle);
        var lineLeft = eyebrowLeft + eyebrowSize.X + Metrics.Space.Md * scale;
        var lineWidth = MathF.Max(1f, max.X - pad - lineLeft);
        var style = TextStyles.Subheadline;
        var lineTop = min.Y + (height - Typography.LineHeight(style)) * 0.5f;
        drawList.PushClipRect(new Vector2(lineLeft, min.Y), new Vector2(lineLeft + lineWidth, max.Y), true);
        Marquee.DrawLeft(drawList, MarqueeKey, text, lineLeft, lineTop, lineWidth, style, CasinoColors.InkTitle, true);
        drawList.PopClipRect();
        return max.Y;
    }

    internal static string Compose(CasinoFloorTickDto[] ticks, StringBuilder into)
    {
        into.Clear();
        for (var index = 0; index < ticks.Length; index++)
        {
            var line = Line(ticks[index]);
            if (line.Length == 0)
            {
                continue;
            }

            if (into.Length > 0)
            {
                into.Append(Separator);
            }

            into.Append(line);
        }

        return into.ToString();
    }

    internal static string Line(CasinoFloorTickDto tick)
    {
        var name = NameOf(tick.Player);
        return tick.Kind switch
        {
            CasinoTickKinds.Win => Loc.T(L.Strip.TickWin, name, CasinoMultiples.Label(tick.MultiplierTenths * 10),
                Loc.T(CasinoGameNames.Of(CasinoRecentGames.ClientGameId(tick.GameKind)))),
            CasinoTickKinds.Jackpot => Loc.T(L.Strip.TickJackpot, name, NumberText.Compact(tick.Amount)),
            CasinoTickKinds.Rain => Loc.T(L.Strip.TickRain, NumberText.Compact(tick.Amount),
                NumberText.Group(tick.Recipients)),
            CasinoTickKinds.Challenge => Loc.T(L.Strip.TickChallenge, name, NumberText.Compact(tick.Amount)),
            _ => string.Empty,
        };
    }

    internal static string NameOf(CasinoPlayerRefDto? player)
    {
        if (player is null)
        {
            return Loc.T(L.Club.Hidden);
        }

        return player.DisplayName.Length > 0 ? player.DisplayName : player.Handle;
    }
}
