using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Chirper;

internal static class ChirperInk
{
    public static readonly SocialInk Standard = new(AppPalettes.Chirper);
    public static readonly SocialInk Moonlit = new(AppPalettes.ChirperMoonlit);
    private static readonly Vector4 StandardMine = Palette.Lighten(AppPalettes.Chirper.Accent, 0.38f);
    private static readonly Vector4 MoonlitMine = Palette.Lighten(AppPalettes.ChirperMoonlit.Accent, 0.38f);
    private static readonly Vector4 StandardQuoteBody = Palette.WithAlpha(AppPalettes.Chirper.BodyInk, 0.85f);
    private static readonly Vector4 MoonlitQuoteBody = Palette.WithAlpha(AppPalettes.ChirperMoonlit.BodyInk, 0.85f);

    public static SocialInk Shared => SeasonalTheme.Halloween ? Moonlit : Standard;
    public static AppPalette CurrentPalette => SeasonalTheme.Halloween ? AppPalettes.ChirperMoonlit : AppPalettes.Chirper;

    public static Vector4 Accent => Shared.Accent;
    public static Vector4 TitleInk => Shared.TitleInk;
    public static Vector4 BodyInk => Shared.BodyInk;
    public static Vector4 MutedInk => Shared.MutedInk;
    public static Vector4 BackdropTop => Shared.BackdropTop;
    public static Vector4 FaintInk => Shared.FaintInk;
    public static Vector4 AccentDeep => Shared.AccentDeep;
    public static Vector4 AccentLink => Shared.AccentLink;
    public static Vector4 AccentWash => Shared.AccentWash;
    public static Vector4 Hairline => Shared.Hairline;
    public static Vector4 ChipFill => Shared.ChipFill;
    public static Vector4 ChipStroke => Shared.ChipStroke;
    public static Vector4 Danger => Shared.Danger;
    public static Vector4 LikeRed => Shared.LikeRed;
    public static Vector4 HoverTint => Shared.HoverTint;
    public static Vector4 SegmentIdleInk => Shared.SegmentIdleInk;
    public static Vector4 GlassPanel => Shared.GlassPanel;
    public static Vector4 GlassStroke => Shared.GlassStroke;
    public static Vector4 FieldFill => Shared.FieldFill;
    public static Vector4 White => Shared.White;
    public static Vector4 MineInk => SeasonalTheme.Halloween ? MoonlitMine : StandardMine;
    public static Vector4 QuoteBodyInk => SeasonalTheme.Halloween ? MoonlitQuoteBody : StandardQuoteBody;

    public static readonly Vector4 RechirpGreen = new(0.188f, 0.820f, 0.345f, 1f);
    public static readonly Vector4 Warning = new(1f, 0.690f, 0.180f, 1f);
    public static readonly Vector4 QuoteFill = new(1f, 1f, 1f, 0.028f);
    public static readonly Vector4 QuoteHover = new(1f, 1f, 1f, 0.05f);
}
