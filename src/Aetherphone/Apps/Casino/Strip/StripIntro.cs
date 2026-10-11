using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Strip;

internal enum StripIntroResult : byte
{
    Showing,
    Finished,
}

internal sealed class StripIntro
{
    public const int PageCount = 3;
    public const float CardWidth = 320f;
    public const float Pad = 22f;
    public const float IconSize = 64f;
    public const float Veil = 0.72f;

    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private static readonly FontAwesomeIcon[] Icons =
        { FontAwesomeIcon.Coins, FontAwesomeIcon.Gift, FontAwesomeIcon.Couch };

    private static readonly Vector4[] Tints = { CasinoColors.Money, CasinoColors.LightA, CasinoColors.LightB };

    private static readonly LocString[] Titles = { L.Strip.IntroChipsTitle, L.Strip.IntroBonusTitle, L.Strip.IntroHostTitle };

    private readonly string[] bodies = new string[PageCount];
    private LanguageInfo? bodiesLanguage;
    private long bodiesRate;
    private Spring reveal;
    private int page;

    public int Page => page;

    public static int NextPage(int page) => Math.Min(PageCount, page + 1);

    public static bool IsLast(int page) => page >= PageCount - 1;

    public void Reset()
    {
        page = 0;
        reveal.SnapTo(0f);
    }

    public static void Gate()
    {
        UiInteract.BlockThisFrame();
    }

    public StripIntroResult Draw(Rect screen, AppSkin ui, long rate, float deltaSeconds)
    {
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##casinoIntro", screen.Size, false, OverlayFlags))
        {
            return DrawCard(screen, ui, rate, deltaSeconds);
        }
    }

    private StripIntroResult DrawCard(Rect screen, AppSkin ui, long rate, float deltaSeconds)
    {
        var scale = UiScale.Current;
        var shown = reveal.Step(1f, Motion.Sheet, deltaSeconds);
        RefreshBodies(rate);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, Veil * shown)));
        var width = MathF.Min(screen.Width - Pad * 2f * scale, CardWidth * scale);
        var pad = Pad * scale;
        var textWidth = width - pad * 2f;
        var body = bodies[page];
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, textWidth).Y;
        var titleHeight = Typography.MeasureWrappedBlock(Loc.T(Titles[page]), TextStyles.Title2, textWidth).Y;
        var buttonHeight = Button.LargeHeight * scale;
        var height = pad * 2f + IconSize * scale + Metrics.Space.Lg * scale + titleHeight + Metrics.Space.Sm * scale
                     + bodyHeight + Metrics.Space.Lg * scale + 8f * scale + Metrics.Space.Lg * scale + buttonHeight * 2f
                     + Metrics.Space.Sm * scale;
        var lift = (1f - shown) * 24f * scale;
        var min = new Vector2(screen.Center.X - width * 0.5f, screen.Center.Y - height * 0.5f + lift);
        var max = new Vector2(min.X + width, min.Y + height);
        var radius = Metrics.Radius.Widget * scale;
        Squircle.FillVerticalGradient(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(0.10f, 0.06f, 0.20f, shown)),
            ImGui.GetColorU32(new Vector4(0.03f, 0.02f, 0.07f, shown)));
        CasinoLights.BulbChase(drawList, new Rect(min, max).Inset(6f * scale), radius - 6f * scale, scale,
            (float)ImGui.GetTime(), CasinoLights.BulbPitch * 1.6f, CasinoColors.Money, CasinoColors.LightA, 0.6f * shown);
        var top = min.Y + pad;
        var iconCenter = new Vector2(screen.Center.X, top + IconSize * scale * 0.5f);
        drawList.AddCircleFilled(iconCenter, IconSize * scale * 0.5f, ImGui.GetColorU32(Tints[page] with { W = 0.2f * shown }), 40);
        AppSkin.Icon(drawList, iconCenter, IconGlyph.Of(Icons[page]), Tints[page] with { W = shown }, 1.8f);
        top += IconSize * scale + Metrics.Space.Lg * scale;
        Typography.DrawWrappedCentered(drawList, Loc.T(Titles[page]), TextStyles.Title2,
            CasinoColors.InkTitle with { W = shown }, new Vector2(screen.Center.X, top), textWidth);
        top += titleHeight + Metrics.Space.Sm * scale;
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, CasinoColors.InkBody with { W = shown },
            new Vector2(screen.Center.X, top), textWidth);
        top += bodyHeight + Metrics.Space.Lg * scale;
        PhotoCarousel.DrawDots(drawList, new Vector2(screen.Center.X, top + 4f * scale), PageCount, page, textWidth,
            CasinoColors.InkTitle);
        top += 8f * scale + Metrics.Space.Lg * scale;
        var primaryRect = new Rect(new Vector2(min.X + pad, top), new Vector2(max.X - pad, top + buttonHeight));
        var label = IsLast(page) ? Loc.T(L.Strip.IntroStart) : Loc.T(L.Strip.IntroNext);
        var result = StripIntroResult.Showing;
        if (Button.Draw(drawList, primaryRect, label, ui.Ink, ButtonStyle.Prominent, overlay: true, id: "casino.intro.next"))
        {
            if (IsLast(page))
            {
                result = StripIntroResult.Finished;
            }
            else
            {
                page = NextPage(page);
            }
        }

        var skipRect = new Rect(new Vector2(min.X + pad, primaryRect.Max.Y + Metrics.Space.Sm * scale),
            new Vector2(max.X - pad, primaryRect.Max.Y + Metrics.Space.Sm * scale + buttonHeight));
        if (!IsLast(page) && Button.Draw(drawList, skipRect, Loc.T(L.Strip.IntroSkip), ui.Ink, ButtonStyle.Gray,
                overlay: true, id: "casino.intro.skip"))
        {
            result = StripIntroResult.Finished;
        }

        return result;
    }

    private void RefreshBodies(long rate)
    {
        if (ReferenceEquals(bodiesLanguage, Loc.Current) && bodiesRate == rate)
        {
            return;
        }

        bodiesLanguage = Loc.Current;
        bodiesRate = rate;
        bodies[0] = Loc.T(L.Strip.IntroChipsBody, NumberText.Group(rate));
        bodies[1] = Loc.T(L.Strip.IntroBonusBody);
        bodies[2] = Loc.T(L.Strip.IntroHostBody);
    }
}
