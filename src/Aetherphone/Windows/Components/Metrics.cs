namespace Aetherphone.Windows.Components;

/// <summary>
/// Central sizing tokens for the UI toolkit. Values are unscaled pixels; multiply by
/// <c>ImGuiHelpers.GlobalScale</c> at the call site. Change a value here to retune every app that
/// consumes it. Roles (padding, card rounding, field height) are named by intent, not by number.
/// </summary>
internal static class Metrics
{
    internal static class Space
    {
        public const float Xxs = 4f;
        public const float Xs = 6f;
        public const float Sm = 8f;
        public const float Md = 12f;
        public const float Lg = 16f;
        public const float Xl = 22f;
        public const float Xxl = 32f;
    }

    internal static class Radius
    {
        public const float Field = 9f;
        public const float Sm = 8f;
        public const float Md = 12f;
        public const float Card = 16f;
        public const float Lg = 18f;
        public const float TileFactor = 0.28f;
    }

    internal static class Size
    {
        public const float Header = 42f;
        public const float Row = 46f;
        public const float FieldHeight = 34f;
        public const float FieldMultiline = 88f;
        public const float ToggleWidth = 46f;
        public const float ToggleHeight = 28f;
        public const float IconTile = 28f;
        public const float HeroRing = 56f;
    }

    internal static class Stroke
    {
        public const float Hairline = 1f;
        public const float Thin = 1.4f;
        public const float Ring = 2f;
    }
}
