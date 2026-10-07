using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hub;

internal sealed class PodiumCard
{
    public const int Places = 3;
    public const float FooterHeight = 44f;
    public const float Padding = Metrics.Space.Lg;

    private const float ColumnGap = Metrics.Space.Sm;
    private const float FirstRadius = 26f;
    private const float PlaceRadius = 21f;
    private const float CrownSize = 16f;
    private const float CrownOverlap = 4f;
    private const float StackGap = Metrics.Space.Xs;
    private const float StepRadius = 10f;
    private const float NameBarHeight = 9f;
    private const float NameBarWidth = 0.62f;
    private const float ValueBarWidth = 0.42f;
    private const float GhostAvatarAlpha = 0.6f;
    private const float MonogramScale = 0.95f;
    private const int AvatarSegments = 32;

    private static readonly Vector4 Silver = new(0.80f, 0.83f, 0.88f, 1f);
    private static readonly Vector4 Bronze = new(0.84f, 0.56f, 0.36f, 1f);
    private static readonly int[] SlotPlaces = [1, 0, 2];
    private static readonly float[] StepHeights = [44f, 32f, 24f];

    private readonly string[] names = new string[Places];
    private readonly string[] values = new string[Places];
    private readonly string?[] avatars = new string?[Places];
    private GameLeaderboardDto? labeled;
    private LanguageInfo? language;
    private ScoreKind labeledKind;
    private string labeledAccount = string.Empty;
    private int count;
    private int mineIndex = -1;

    public int Count => count;

    public int MineIndex => mineIndex;

    public static string Value(ScoreKind kind, int value) =>
        kind == ScoreKind.Time ? TimeText.MinutesSeconds(value) : GameNumber.Label(value);

    public static Vector4 MedalTint(int place) => place switch
    {
        0 => GamePalette.Star,
        1 => Silver,
        _ => Bronze,
    };

    public static float PodiumHeight(float scale) =>
        (Padding * 2f + CrownSize - CrownOverlap + FirstRadius * 2f + StackGap * 2f + StepHeights[0]) * scale
        + Typography.LineHeight(TextStyles.FootnoteEmphasized) + Typography.LineHeight(TextStyles.Headline);

    public static float Height(float scale, bool footer) =>
        PodiumHeight(scale) + (footer ? FooterHeight * scale : 0f);

    public void Reset() => labeled = null;

    public void Sync(GameLeaderboardDto data, ScoreKind kind, string accountId)
    {
        if (ReferenceEquals(labeled, data) && ReferenceEquals(language, Loc.Current) && labeledKind == kind
            && string.Equals(labeledAccount, accountId, StringComparison.Ordinal))
        {
            return;
        }

        labeled = data;
        language = Loc.Current;
        labeledKind = kind;
        labeledAccount = accountId;
        var entries = data.Entries ?? Array.Empty<GameLeaderboardEntryDto>();
        count = Math.Min(Places, entries.Length);
        mineIndex = -1;
        for (var place = 0; place < count; place++)
        {
            var entry = entries[place];
            names[place] = SocialIdentity.Name(entry.DisplayName, entry.Handle);
            values[place] = Value(kind, entry.Value);
            avatars[place] = entry.AvatarUrl;
            if (accountId.Length > 0 && string.Equals(entry.UserId, accountId, StringComparison.Ordinal))
            {
                mineIndex = place;
            }
        }
    }

    public Rect Draw(ImDrawListPtr drawList, AppSkin ui, Rect card, bool footer, Vector4 accent,
        RemoteImageCache images, LodestoneService lodestone, float scale)
    {
        var podium = Background(drawList, ui, card, footer, scale);
        var mineInk = ui.Ink.WithAccent(accent).AccentInk;
        for (var slot = 0; slot < Places; slot++)
        {
            var place = SlotPlaces[slot];
            var column = Column(podium, slot, scale);
            var step = Step(column, podium, place, scale);
            DrawStep(drawList, ui, step, place, place < count, scale);
            if (place >= count)
            {
                continue;
            }

            var radius = (place == 0 ? FirstRadius : PlaceRadius) * scale;
            var valueTop = step.Min.Y - StackGap * scale - Typography.LineHeight(TextStyles.Headline);
            var nameTop = valueTop - Typography.LineHeight(TextStyles.FootnoteEmphasized);
            var center = new Vector2(column.Center.X, nameTop - StackGap * scale - radius);
            AvatarView.DrawRemote(drawList, center, radius, ui.Theme, names[place], string.Empty, avatars[place],
                images, lodestone, MonogramScale, AvatarSegments);
            if (place == 0)
            {
                var crownCenter = new Vector2(center.X, center.Y - radius - (CrownSize * 0.5f - CrownOverlap) * scale);
                PhoneIcon.Draw(drawList, crownCenter, PhoneIcons.Crown, GamePalette.Star, CrownSize * scale);
            }

            var nameInk = place == mineIndex ? mineInk : ui.TitleInk;
            DrawCentered(drawList, names[place], column, nameTop, nameInk, TextStyles.FootnoteEmphasized);
            DrawCentered(drawList, values[place], column, valueTop, ui.TitleInk, TextStyles.Headline);
        }

        return FooterRect(card, podium, footer, scale);
    }

    public static Rect DrawPlaceholder(ImDrawListPtr drawList, AppSkin ui, Rect card, bool footer, bool loading,
        float scale)
    {
        var podium = Background(drawList, ui, card, footer, scale);
        var ghost = Surfaces.Fill(ui.TitleInk, FillLevel.Quaternary);
        for (var slot = 0; slot < Places; slot++)
        {
            var place = SlotPlaces[slot];
            var column = Column(podium, slot, scale);
            var step = Step(column, podium, place, scale);
            var radius = (place == 0 ? FirstRadius : PlaceRadius) * scale;
            var valueTop = step.Min.Y - StackGap * scale - Typography.LineHeight(TextStyles.Headline);
            var nameTop = valueTop - Typography.LineHeight(TextStyles.FootnoteEmphasized);
            var center = new Vector2(column.Center.X, nameTop - StackGap * scale - radius);
            if (!loading)
            {
                DrawStep(drawList, ui, step, place, false, scale);
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ghost with { W = ghost.W * GhostAvatarAlpha }),
                    AvatarSegments);
                continue;
            }

            Skeleton.Bar(drawList, step.Min, step.Max, StepRadius * scale);
            Skeleton.Disc(drawList, center, radius);
            var bar = NameBarHeight * scale;
            Skeleton.Bar(drawList, CenteredBar(column, nameTop, NameBarWidth, bar), CenteredBarMax(column, nameTop,
                NameBarWidth, bar), bar * 0.5f);
            Skeleton.Bar(drawList, CenteredBar(column, valueTop, ValueBarWidth, bar), CenteredBarMax(column,
                valueTop, ValueBarWidth, bar), bar * 0.5f);
        }

        return FooterRect(card, podium, footer, scale);
    }

    private static Rect Background(ImDrawListPtr drawList, AppSkin ui, Rect card, bool footer, float scale)
    {
        ui.Card(drawList, card.Min, card.Max, HubMetrics.CardRadius * scale);
        var podiumBottom = card.Min.Y + PodiumHeight(scale);
        if (footer)
        {
            var pad = Padding * scale;
            drawList.AddLine(new Vector2(card.Min.X + pad, podiumBottom), new Vector2(card.Max.X - pad, podiumBottom),
                ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
        }

        var inset = Padding * scale;
        return new Rect(new Vector2(card.Min.X + inset, card.Min.Y + inset),
            new Vector2(card.Max.X - inset, podiumBottom - inset));
    }

    private static Rect FooterRect(Rect card, Rect podium, bool footer, float scale)
    {
        if (!footer)
        {
            return default;
        }

        var top = podium.Max.Y + Padding * scale;
        return new Rect(new Vector2(podium.Min.X, top), new Vector2(podium.Max.X, card.Max.Y));
    }

    private static Rect Column(Rect podium, int slot, float scale)
    {
        var gap = ColumnGap * scale;
        var width = (podium.Width - gap * (Places - 1)) / Places;
        var left = podium.Min.X + slot * (width + gap);
        return new Rect(new Vector2(left, podium.Min.Y), new Vector2(left + width, podium.Max.Y));
    }

    private static Rect Step(Rect column, Rect podium, int place, float scale) =>
        new(new Vector2(column.Min.X, podium.Max.Y - StepHeights[place] * scale), new Vector2(column.Max.X,
            podium.Max.Y));

    private static void DrawStep(ImDrawListPtr drawList, AppSkin ui, Rect step, int place, bool filled, float scale)
    {
        var fill = Surfaces.Fill(ui.TitleInk, filled ? FillLevel.Tertiary : FillLevel.Quaternary);
        Squircle.Fill(drawList, step.Min, step.Max, StepRadius * scale, ImGui.GetColorU32(fill));
        var ink = filled ? MedalTint(place) : ui.MutedInk;
        Typography.DrawCentered(drawList, step.Center, GameNumber.Label(place + 1), ink, TextStyles.Title3);
    }

    private static void DrawCentered(ImDrawListPtr drawList, string text, Rect column, float top, Vector4 ink,
        in TextStyle style)
    {
        var fitted = Typography.FitText(text, column.Width, style);
        var width = Typography.Measure(fitted, style).X;
        Typography.Draw(drawList, new Vector2(column.Center.X - width * 0.5f, top), fitted, ink, style);
    }

    private static Vector2 CenteredBar(Rect column, float top, float fraction, float height) =>
        new(column.Center.X - column.Width * fraction * 0.5f, top + height * 0.5f);

    private static Vector2 CenteredBarMax(Rect column, float top, float fraction, float height) =>
        new(column.Center.X + column.Width * fraction * 0.5f, top + height * 1.5f);
}
