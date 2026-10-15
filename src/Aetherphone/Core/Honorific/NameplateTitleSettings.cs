using Newtonsoft.Json;

namespace Aetherphone.Core.Honorific;

[Flags]
internal enum NameplateStatus
{
    None = 0,
    MogCast = 1 << 0,
    Jam = 1 << 1,
    RadioOnAir = 1 << 2,
    Muster = 1 << 3,
    Chirper = 1 << 4,
    Velvet = 1 << 5,
    InCall = 1 << 6,
    NowPlaying = 1 << 7,
    Handle = 1 << 8,
    Aethergram = 1 << 9,
    Custom = 1 << 10,
    DoNotDisturb = 1 << 11,
    Games = 1 << 12,
    Gamba = 1 << 13,
    SlotsWin = 1 << 14,
    SlotsLoss = 1 << 15,
    Default = MogCast | Jam | RadioOnAir | Muster,
}

internal enum NameplateTitleStyle
{
    AppColors,
    MatchMine,
    Custom,
}

internal enum NameplateHandleApp
{
    Chirper,
    Aethergram,
    Velvet,
}

internal enum NameplateLongTitles
{
    TakeTurns,
    Shorten,
}

internal enum NameplateGlow
{
    Solid,
    Gradient,
}

internal enum NameplateGradientMotion
{
    Pulse,
    Wave,
    Static,
}

internal enum NameplateColorSlot
{
    Text,
    Glow,
    GradientFirst,
    GradientSecond,
}

internal sealed class NameplateTitleSettings
{
    public const int MaxTemplateLength = 64;
    public const int MinimumTurnSeconds = 4;
    public const int MaximumTurnSeconds = 30;
    public const int DefaultTurnSeconds = 10;
    private const int Migrated = -1;

    public bool Enabled { get; set; }
    public NameplateStatus Statuses { get; set; } = NameplateStatus.Default;
    public NameplateStatus[] Order { get; set; } = Array.Empty<NameplateStatus>();
    public Dictionary<NameplateStatus, string> Templates { get; set; } = new();
    public string CustomText { get; set; } = string.Empty;
    public NameplateTitleStyle Style { get; set; }
    public uint TextColor { get; set; } = NameplatePalette.White;
    public uint GlowColor { get; set; } = NameplatePalette.DarkGlowPacked;
    public NameplateGlow Glow { get; set; }
    public int Gradient { get; set; }
    public NameplateGradientMotion Motion { get; set; } = NameplateGradientMotion.Wave;
    public uint GradientFirst { get; set; } = NameplatePalette.Lilac;
    public uint GradientSecond { get; set; } = NameplatePalette.Sky;

    [JsonProperty("CustomColor")]
    public int LegacyCustomColor { get; set; } = Migrated;

    [JsonProperty("CustomGlow")]
    public int LegacyCustomGlow { get; set; } = Migrated;

    public bool Prefix { get; set; }
    public bool IncludePcMedia { get; set; }
    public NameplateHandleApp HandleApp { get; set; }
    public NameplateLongTitles LongTitles { get; set; }
    public int TurnSeconds { get; set; } = DefaultTurnSeconds;

    public bool Shows(NameplateStatus status) => (Statuses & status) != 0;

    public void Set(NameplateStatus status, bool shown) =>
        Statuses = shown ? Statuses | status : Statuses & ~status;

    public string Template(NameplateStatus status) =>
        Templates.TryGetValue(status, out var template) ? template : string.Empty;

    public uint ColorOf(NameplateColorSlot slot) => slot switch
    {
        NameplateColorSlot.Glow => GlowColor,
        NameplateColorSlot.GradientFirst => GradientFirst,
        NameplateColorSlot.GradientSecond => GradientSecond,
        _ => TextColor,
    };

    public void SetColor(NameplateColorSlot slot, uint color)
    {
        switch (slot)
        {
            case NameplateColorSlot.Glow:
                GlowColor = color;
                return;
            case NameplateColorSlot.GradientFirst:
                GradientFirst = color;
                return;
            case NameplateColorSlot.GradientSecond:
                GradientSecond = color;
                return;
            default:
                TextColor = color;
                return;
        }
    }

    public TitleLook CustomLook()
    {
        var text = NameplatePalette.Unpack(TextColor);
        if (Glow == NameplateGlow.Solid || !NameplateGradients.IsValid(Gradient))
        {
            return TitleLook.Solid(text, NameplatePalette.Unpack(GlowColor));
        }

        return Gradient == NameplateGradients.TwoColors
            ? new TitleLook(text, NameplatePalette.Unpack(GradientFirst), NameplatePalette.Unpack(GradientSecond),
                Gradient, (int)Motion)
            : new TitleLook(text, NameplatePalette.Unpack(GlowColor), null, Gradient, (int)Motion);
    }

    public void SetTemplate(NameplateStatus status, string template)
    {
        var clean = template.Trim();
        if (clean.Length == 0)
        {
            Templates.Remove(status);
            return;
        }

        Templates[status] = clean;
    }

    public void Move(int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || index >= Order.Length || target < 0 || target >= Order.Length)
        {
            return;
        }

        (Order[index], Order[target]) = (Order[target], Order[index]);
    }

    public void Normalize()
    {
        TurnSeconds = Math.Clamp(TurnSeconds, MinimumTurnSeconds, MaximumTurnSeconds);
        MigrateLegacyColors();
        if (!NameplateGradients.IsValid(Gradient))
        {
            Gradient = 0;
        }

        var ranked = NameplateStatusCatalog.Ranked;
        if (Order.Length == 0)
        {
            if (Shows(NameplateStatus.Chirper))
            {
                Set(NameplateStatus.Aethergram, true);
            }

            if (Shows(NameplateStatus.InCall))
            {
                Set(NameplateStatus.DoNotDisturb, true);
            }
        }

        var normalized = new NameplateStatus[ranked.Length];
        var count = 0;
        for (var index = 0; index < Order.Length; index++)
        {
            var status = Order[index];
            if (Array.IndexOf(ranked, status) >= 0 && Array.IndexOf(normalized, status, 0, count) < 0)
            {
                normalized[count] = status;
                count++;
            }
        }

        var fallback = Array.IndexOf(normalized, NameplateStatus.Handle, 0, count);
        for (var index = 0; index < ranked.Length; index++)
        {
            var status = ranked[index];
            if (Array.IndexOf(normalized, status, 0, count) >= 0)
            {
                continue;
            }

            if (fallback < 0)
            {
                normalized[count] = status;
                count++;
                continue;
            }

            Array.Copy(normalized, fallback, normalized, fallback + 1, count - fallback);
            normalized[fallback] = status;
            fallback++;
            count++;
        }

        Order = normalized;
    }

    private void MigrateLegacyColors()
    {
        if (LegacyCustomColor != Migrated)
        {
            TextColor = NameplatePalette.PackedAt(LegacyCustomColor);
            LegacyCustomColor = Migrated;
        }

        if (LegacyCustomGlow != Migrated)
        {
            GlowColor = NameplatePalette.PackedAt(LegacyCustomGlow);
            LegacyCustomGlow = Migrated;
        }
    }
}
