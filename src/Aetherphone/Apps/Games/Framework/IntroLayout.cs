using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Games.Framework;

internal readonly record struct IntroBlocks(float Daily, float Title, float Hook, float Pills, float Modes,
    float SeatsCaption, float Seats, float Level, float Play, float Links, float Icon = 0f);

internal readonly struct IntroLayout
{
    public const float HookMaxWidth = 300f;
    public const float StripMaxWidth = 280f;
    public const float PlayWidth = 220f;
    private const float LeftShare = 0.55f;

    public bool Columns { get; private init; }

    public Rect TextColumn { get; private init; }

    public Rect ActionColumn { get; private init; }

    public Rect Icon { get; private init; }

    public Rect Daily { get; private init; }

    public Rect Title { get; private init; }

    public Rect Hook { get; private init; }

    public Rect Pills { get; private init; }

    public Rect Modes { get; private init; }

    public Rect SeatsCaption { get; private init; }

    public Rect Seats { get; private init; }

    public Rect Level { get; private init; }

    public Rect Play { get; private init; }

    public Rect Links { get; private init; }

    public static Rect Content(Rect full, float scale) =>
        new(new Vector2(full.Min.X + StageLayout.SafeSide * scale, full.Min.Y + StageLayout.ChromeBand * scale),
            new Vector2(full.Max.X - StageLayout.SafeSide * scale, full.Max.Y - StageLayout.SafeBottom * scale));

    public static Rect TextColumnOf(Rect full, Rect safe, bool columns, float scale)
    {
        if (!columns)
        {
            return safe;
        }

        var content = Content(full, scale);
        var leftWidth = (content.Width - Metrics.Space.Xl * scale) * LeftShare;
        return new Rect(content.Min, new Vector2(content.Min.X + leftWidth, content.Max.Y));
    }

    public static float HookWidth(Rect textColumn, float scale) => MathF.Min(textColumn.Width, HookMaxWidth * scale);

    public static IntroLayout Compute(Rect full, Rect safe, bool columns, in IntroBlocks blocks, float scale)
    {
        if (!columns)
        {
            var stacked = FitIcon(blocks, safe.Height, true, scale);
            var stack = TextHeight(stacked, scale) + ActionHeight(stacked, true, scale);
            var top = MathF.Max(safe.Min.Y, full.Center.Y - stack * 0.5f);
            var column = new Rect(new Vector2(safe.Min.X, top), new Vector2(safe.Max.X, safe.Max.Y));
            var single = PlaceText(stacked, column, column.Center.X, top, scale);
            return PlaceActions(single, stacked, column, single.Pills.Max.Y, true, scale);
        }

        var content = Content(full, scale);
        var split = FitIcon(blocks, content.Height, false, scale);
        var textColumn = TextColumnOf(full, safe, true, scale);
        var actionColumn = new Rect(new Vector2(textColumn.Max.X + Metrics.Space.Xl * scale, content.Min.Y),
            content.Max);
        var textTop = MathF.Max(content.Min.Y, content.Center.Y - TextHeight(split, scale) * 0.5f);
        var actionTop = MathF.Max(content.Min.Y, content.Center.Y - ActionHeight(split, false, scale) * 0.5f);
        var text = PlaceText(split, textColumn, textColumn.Center.X, textTop, scale) with
        {
            Columns = true,
            TextColumn = textColumn,
            ActionColumn = actionColumn,
        };
        return PlaceActions(text, split, actionColumn, actionTop, false, scale);
    }

    private static IntroBlocks FitIcon(in IntroBlocks blocks, float available, bool attached, float scale)
    {
        if (blocks.Icon <= 0f)
        {
            return blocks;
        }

        var needed = TextHeight(blocks, scale) + (attached ? ActionHeight(blocks, true, scale) : 0f);
        return needed <= available ? blocks : blocks with { Icon = 0f };
    }

    private static float TextHeight(in IntroBlocks blocks, float scale)
    {
        var height = blocks.Title + Metrics.Space.Md * scale + blocks.Pills;
        if (blocks.Icon > 0f)
        {
            height += blocks.Icon + Metrics.Space.Md * scale;
        }

        if (blocks.Daily > 0f)
        {
            height += blocks.Daily + Metrics.Space.Sm * scale;
        }

        if (blocks.Hook > 0f)
        {
            height += Metrics.Space.Sm * scale + blocks.Hook;
        }

        return height;
    }

    private static float ActionHeight(in IntroBlocks blocks, bool attached, float scale)
    {
        var height = 0f;
        if (blocks.Modes > 0f)
        {
            height += (attached ? Metrics.Space.Lg * scale : 0f) + blocks.Modes;
        }

        if (blocks.Seats > 0f)
        {
            height += (attached || height > 0f ? Metrics.Space.Lg * scale : 0f) + blocks.SeatsCaption +
                      Metrics.Space.Xs * scale + blocks.Seats;
        }

        if (attached || height > 0f)
        {
            height += Metrics.Space.Xl * scale;
        }

        if (blocks.Level > 0f)
        {
            height += blocks.Level + Metrics.Space.Md * scale;
        }

        return height + blocks.Play + Metrics.Space.Md * scale + blocks.Links;
    }

    private static IntroLayout PlaceText(in IntroBlocks blocks, Rect column, float centerX, float top, float scale)
    {
        var cursor = top;
        var icon = Centered(centerX, cursor, 0f, 0f);
        if (blocks.Icon > 0f)
        {
            icon = Centered(centerX, cursor, blocks.Icon, blocks.Icon);
            cursor += blocks.Icon + Metrics.Space.Md * scale;
        }

        var daily = Band(column, cursor, blocks.Daily);
        if (blocks.Daily > 0f)
        {
            cursor += blocks.Daily + Metrics.Space.Sm * scale;
        }

        var title = Band(column, cursor, blocks.Title);
        cursor += blocks.Title;
        var hook = Band(column, cursor, 0f);
        if (blocks.Hook > 0f)
        {
            cursor += Metrics.Space.Sm * scale;
            hook = Centered(centerX, cursor, HookWidth(column, scale), blocks.Hook);
            cursor += blocks.Hook;
        }

        cursor += Metrics.Space.Md * scale;
        return new IntroLayout
        {
            TextColumn = column,
            ActionColumn = column,
            Icon = icon,
            Daily = daily,
            Title = title,
            Hook = hook,
            Pills = Band(column, cursor, blocks.Pills),
        };
    }

    private static IntroLayout PlaceActions(in IntroLayout text, in IntroBlocks blocks, Rect column, float top,
        bool attached, float scale)
    {
        var centerX = column.Center.X;
        var stripWidth = MathF.Min(column.Width, StripMaxWidth * scale);
        var cursor = top;
        var placed = false;
        var modes = Centered(centerX, cursor, stripWidth, 0f);
        if (blocks.Modes > 0f)
        {
            cursor += attached ? Metrics.Space.Lg * scale : 0f;
            modes = Centered(centerX, cursor, stripWidth, blocks.Modes);
            cursor += blocks.Modes;
            placed = true;
        }

        var caption = Centered(centerX, cursor, stripWidth, 0f);
        var seats = caption;
        if (blocks.Seats > 0f)
        {
            cursor += attached || placed ? Metrics.Space.Lg * scale : 0f;
            caption = Centered(centerX, cursor, stripWidth, blocks.SeatsCaption);
            cursor += blocks.SeatsCaption + Metrics.Space.Xs * scale;
            seats = Centered(centerX, cursor, stripWidth, blocks.Seats);
            cursor += blocks.Seats;
            placed = true;
        }

        if (attached || placed)
        {
            cursor += Metrics.Space.Xl * scale;
        }

        var level = Centered(centerX, cursor, column.Width, 0f);
        if (blocks.Level > 0f)
        {
            level = Centered(centerX, cursor, column.Width, blocks.Level);
            cursor += blocks.Level + Metrics.Space.Md * scale;
        }

        var play = Centered(centerX, cursor, MathF.Min(column.Width, PlayWidth * scale), blocks.Play);
        cursor += blocks.Play + Metrics.Space.Md * scale;
        return text with
        {
            ActionColumn = column,
            Modes = modes,
            SeatsCaption = caption,
            Seats = seats,
            Level = level,
            Play = play,
            Links = Centered(centerX, cursor, column.Width, blocks.Links),
        };
    }

    private static Rect Band(Rect column, float top, float height) =>
        new(new Vector2(column.Min.X, top), new Vector2(column.Max.X, top + height));

    private static Rect Centered(float centerX, float top, float width, float height) =>
        new(new Vector2(centerX - width * 0.5f, top), new Vector2(centerX + width * 0.5f, top + height));
}
