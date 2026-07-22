namespace Aetherphone.Core.Emulation;

internal enum EmulatorInputProfile : byte
{
    Standard,
    SegaSixButton,
    PcEngine,
    NeoGeo,
    NeoGeoPocket,
    WonderSwan,
    PlayStation,
    Nintendo64,
}

internal sealed record EmulatorCoreOptionDefinition(
    string Key,
    string Label,
    IReadOnlyList<string> Values,
    IReadOnlyList<string>? DisplayValues = null,
    string Hint = "",
    bool RestartRequired = true)
{
    public string Display(string value)
    {
        var index = -1;
        for (var candidate = 0; candidate < Values.Count; candidate++)
        {
            if (string.Equals(Values[candidate], value, StringComparison.OrdinalIgnoreCase))
            {
                index = candidate;
                break;
            }
        }
        return DisplayValues is not null && index >= 0 && index < DisplayValues.Count
            ? DisplayValues[index]
            : value;
    }
}

internal sealed record EmulatorFirmwareDefinition(string FileName, string Description, bool Required);

internal sealed record EmulatorSystemDefinition(
    string Id,
    string Name,
    string ShortName,
    string CoreFileName,
    EmulatorButtons Controls,
    params string[] Extensions)
{
    public EmulatorInputProfile InputProfile { get; init; } = EmulatorInputProfile.Standard;
    public bool DiscBased { get; init; }
    public string Description { get; init; } = string.Empty;
    public string SaveDescription { get; init; } = "Battery save + save states";
    public IReadOnlyList<EmulatorCoreOptionDefinition> CoreOptions { get; init; } =
        Array.Empty<EmulatorCoreOptionDefinition>();
    public IReadOnlyList<EmulatorFirmwareDefinition> Firmware { get; init; } =
        Array.Empty<EmulatorFirmwareDefinition>();
    public IReadOnlyDictionary<string, string> DefaultCoreOptions { get; init; } =
        new Dictionary<string, string>();

    public bool Supports(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public string ButtonLabel(EmulatorButtons button) => InputProfile switch
    {
        EmulatorInputProfile.PcEngine => button switch
        {
            EmulatorButtons.A => "I",
            EmulatorButtons.B => "II",
            EmulatorButtons.X => "IV",
            EmulatorButtons.Y => "III",
            EmulatorButtons.L => "V",
            EmulatorButtons.R => "VI",
            EmulatorButtons.Start => "Run",
            _ => DefaultButtonLabel(button),
        },
        EmulatorInputProfile.SegaSixButton => button switch
        {
            EmulatorButtons.B => "B",
            EmulatorButtons.Y => "A",
            EmulatorButtons.Select => "Mode",
            EmulatorButtons.A => "C",
            EmulatorButtons.X => "Y",
            EmulatorButtons.L => "X",
            EmulatorButtons.R => "Z",
            _ => DefaultButtonLabel(button),
        },
        EmulatorInputProfile.NeoGeo => button switch
        {
            EmulatorButtons.B => "A",
            EmulatorButtons.A => "B",
            EmulatorButtons.Y => "C",
            EmulatorButtons.X => "D",
            EmulatorButtons.Select => "Coin",
            _ => DefaultButtonLabel(button),
        },
        EmulatorInputProfile.NeoGeoPocket => button switch
        {
            EmulatorButtons.B => "A",
            EmulatorButtons.A => "B",
            EmulatorButtons.Y => "Option",
            _ => DefaultButtonLabel(button),
        },
        EmulatorInputProfile.WonderSwan => button switch
        {
            EmulatorButtons.B => "B",
            EmulatorButtons.A => "A",
            EmulatorButtons.Y => "Rotate",
            EmulatorButtons.Select => "Start",
            EmulatorButtons.X => "Y ←",
            EmulatorButtons.L => "Y →",
            EmulatorButtons.R => "Y ↓",
            EmulatorButtons.L2 => "Y ↑",
            _ => DefaultButtonLabel(button),
        },
        EmulatorInputProfile.PlayStation => button switch
        {
            EmulatorButtons.B => "×",
            EmulatorButtons.A => "○",
            EmulatorButtons.Y => "□",
            EmulatorButtons.X => "△",
            EmulatorButtons.L => "L1",
            EmulatorButtons.R => "R1",
            _ => DefaultButtonLabel(button),
        },
        EmulatorInputProfile.Nintendo64 => button switch
        {
            EmulatorButtons.A => "A",
            EmulatorButtons.B => "B",
            EmulatorButtons.L2 => "Z",
            _ => DefaultButtonLabel(button),
        },
        _ => DefaultButtonLabel(button),
    };

    private static string DefaultButtonLabel(EmulatorButtons button) => button switch
    {
        EmulatorButtons.Up => "Up",
        EmulatorButtons.Down => "Down",
        EmulatorButtons.Left => "Left",
        EmulatorButtons.Right => "Right",
        EmulatorButtons.Start => "Start",
        EmulatorButtons.Select => "Select",
        _ => button.ToString(),
    };
}

internal sealed record RomEntry(string Path, EmulatorSystemDefinition System);

internal static class EmulatorSystemCatalog
{
    private const EmulatorButtons Directions = EmulatorButtons.Up | EmulatorButtons.Down |
                                               EmulatorButtons.Left | EmulatorButtons.Right;
    private const EmulatorButtons TwoButtonPad = Directions | EmulatorButtons.A | EmulatorButtons.B |
                                                 EmulatorButtons.Start | EmulatorButtons.Select;
    private const EmulatorButtons SixButtonPad = TwoButtonPad | EmulatorButtons.X | EmulatorButtons.Y |
                                                 EmulatorButtons.L | EmulatorButtons.R;
    private const EmulatorButtons PlayStationPad = SixButtonPad | EmulatorButtons.L2 | EmulatorButtons.R2 |
                                                    EmulatorButtons.L3 | EmulatorButtons.R3;
    private const EmulatorButtons Nintendo64Pad = Directions | EmulatorButtons.A | EmulatorButtons.B |
                                                   EmulatorButtons.L | EmulatorButtons.R | EmulatorButtons.L2 |
                                                   EmulatorButtons.Start;

    public static readonly EmulatorSystemDefinition GameBoy = new(
        "gb", "Game Boy / Color", "GB/GBC", "sameboy_libretro.dll", TwoButtonPad, ".gb", ".gbc")
    {
        Description = "Game Boy and Game Boy Color with SameBoy link-ready emulation.",
    };

    public static readonly EmulatorSystemDefinition GameBoyAdvance = new(
        "gba", "Game Boy Advance", "GBA", "gpsp_libretro.dll",
        TwoButtonPad | EmulatorButtons.L | EmulatorButtons.R, ".gba")
    {
        Description = "Game Boy Advance using gpSP, prepared for wireless-adapter netplay.",
        Firmware = new[] { new EmulatorFirmwareDefinition("gba_bios.bin", "Game Boy Advance BIOS", false), },
    };

    public static readonly EmulatorSystemDefinition Nes = new(
        "nes", "Nintendo / Famicom", "NES", "nestopia_libretro.dll", TwoButtonPad,
        ".nes", ".unf", ".unif")
    {
        Description = "Nintendo Entertainment System and Famicom.",
    };

    public static readonly EmulatorSystemDefinition Snes = new(
        "snes", "Super Nintendo", "SNES", "bsnes_libretro.dll", SixButtonPad,
        ".sfc", ".smc", ".fig", ".swc")
    {
        Description = "Accurate Super Nintendo and Super Famicom emulation with bsnes.",
    };

    public static readonly EmulatorSystemDefinition MegaDrive = new(
        "megadrive", "Mega Drive / Genesis", "MD", "blastem_libretro.dll", SixButtonPad,
        ".md", ".gen", ".smd", ".68k", ".sgd")
    {
        InputProfile = EmulatorInputProfile.SegaSixButton,
        Description = "Mega Drive and Genesis cartridge games using BlastEm.",
    };

    public static readonly EmulatorSystemDefinition SegaCd = new(
        "segacd", "Sega CD / Mega-CD", "SEGA CD", "clownmdemu_libretro.dll", SixButtonPad,
        ".cue", ".iso", ".chd")
    {
        InputProfile = EmulatorInputProfile.SegaSixButton,
        DiscBased = true,
        Description = "Sega CD and Mega-CD using ClownMDEmu. Prefer CHD or a CUE kept beside all BIN tracks.",
        SaveDescription = "Backup RAM + save states",
        CoreOptions = new[]
        {
            OptionWithDisplay("clownmdemu_tv_standard", "TV standard",
                new[] { "ntsc", "pal" }, new[] { "NTSC (60 Hz)", "PAL (50 Hz)" }),
            OptionWithDisplay("clownmdemu_overseas_region", "Console region",
                new[] { "elsewhere", "japan" }, new[] { "USA / Europe", "Japan" }),
            Option("clownmdemu_tall_interlace_mode_2", "Tall interlace mode", "disabled", "enabled"),
            Option("clownmdemu_widescreen_tiles", "Widescreen extra tiles", "0", "1", "2", "3", "4", "5", "6"),
            Option("clownmdemu_lowpass_filter", "Audio low-pass filter", "enabled", "disabled"),
            Option("clownmdemu_ladder_effect", "Low-volume distortion", "enabled", "disabled"),
        },
    };

    public static readonly EmulatorSystemDefinition Sega8Bit = new(
        "sega8", "Master System / Game Gear", "MS / GG", "smsplus_libretro.dll", TwoButtonPad,
        ".sms", ".gg", ".rom")
    {
        Description = "Master System and Game Gear using SMS Plus GX.",
    };

    public static readonly EmulatorSystemDefinition Atari2600 = new(
        "atari2600", "Atari 2600", "A2600", "stella_libretro.dll", TwoButtonPad, ".a26")
    {
        Description = "Atari 2600 cartridge games using Stella.",
    };

    public static readonly EmulatorSystemDefinition PcEngine = new(
        "pcengine", "PC Engine / TurboGrafx", "PCE", "geargrafx_libretro.dll", SixButtonPad,
        ".pce", ".sgx", ".hes", ".cue", ".chd")
    {
        InputProfile = EmulatorInputProfile.PcEngine,
        DiscBased = true,
        Description = "PC Engine, TurboGrafx-16, SuperGrafx and CD-ROM² in one core.",
        SaveDescription = "Backup RAM / MB128 + save states",
        Firmware = new[]
        {
            new EmulatorFirmwareDefinition("syscard3.pce", "Super CD-ROM² System Card 3", true),
            new EmulatorFirmwareDefinition("syscard2.pce", "CD-ROM² System Card 2", false),
            new EmulatorFirmwareDefinition("syscard1.pce", "CD-ROM² System Card 1", false),
            new EmulatorFirmwareDefinition("gexpress.pce", "Game Express CD Card", false),
        },
        DefaultCoreOptions = Options(("geargrafx_deterministic_netplay", "Enabled")),
        CoreOptions = new[]
        {
            Option("geargrafx_console_type", "Console", "Auto", "PC Engine (JAP)", "SuperGrafx (JAP)",
                "TurboGrafx-16 (USA)"),
            Option("geargrafx_aspect_ratio", "Aspect ratio", "1:1 PAR", "4:3 DAR", "6:5 DAR", "16:9 DAR", "16:10 DAR"),
            Option("geargrafx_cdrom_type", "CD-ROM type", "Auto", "Standard", "Super CD-ROM", "Arcade CD-ROM"),
            Option("geargrafx_cdrom_bios", "CD-ROM BIOS", "Auto", "System Card 1", "System Card 2", "System Card 3", "Game Express"),
            Option("geargrafx_turbotap", "TurboTap", "Disabled", "Enabled"),
            Option("geargrafx_deterministic_netplay", "Deterministic netplay", "Disabled", "Enabled"),
            Option("geargrafx_cdrom_preload", "Preload CD", "Disabled", "Enabled"),
            Option("geargrafx_mb128", "MB128 memory", "Auto", "Enabled", "Disabled"),
        },
    };

    public static readonly EmulatorSystemDefinition NeoGeo = new(
        "neogeo", "Neo Geo AES / MVS", "NEO GEO", "geolith_libretro.dll", SixButtonPad, ".neo")
    {
        InputProfile = EmulatorInputProfile.NeoGeo,
        Description = "Neo Geo home-console and arcade cartridges using Geolith single-file ROMs.",
        SaveDescription = "Memory card / NVRAM + save states",
        Firmware = new[]
        {
            new EmulatorFirmwareDefinition("aes.zip", "Neo Geo AES BIOS", true),
            new EmulatorFirmwareDefinition("neogeo.zip", "Neo Geo MVS / UniBIOS", true),
        },
        CoreOptions = new[]
        {
            OptionWithDisplay("geolith_system_type", "System",
                new[] { "aes", "mvs", "uni" },
                new[] { "Neo Geo AES (Home Console)", "Neo Geo MVS (Arcade)",
                    "Universe BIOS (Community-enhanced BIOS)" }),
            OptionWithDisplay("geolith_region", "Region",
                new[] { "us", "jp", "as", "eu" }, new[] { "USA", "Japan", "Asia", "Europe" }),
            Option("geolith_freeplay", "Free play", "Off", "On"),
            Option("geolith_4player", "Four-player MVS", "Off", "On"),
            Option("geolith_settingmode", "MVS setting mode", "Off", "On"),
            OptionWithDisplay("geolith_aspect", "Aspect ratio",
                new[] { "1:1", "45:44", "4:3" },
                new[] { "Perfectly Square Pixels (1:1 PAR)",
                    "Ostensibly Accurate NTSC Aspect Ratio (45:44 PAR)",
                    "Very Traditional NTSC Aspect Ratio (4:3 DAR)" }),
        },
    };

    public static readonly EmulatorSystemDefinition NeoGeoPocket = new(
        "ngp", "Neo Geo Pocket / Color", "NGP/C", "mednafen_ngp_libretro.dll",
        Directions | EmulatorButtons.A | EmulatorButtons.B | EmulatorButtons.Y, ".ngp", ".ngc")
    {
        InputProfile = EmulatorInputProfile.NeoGeoPocket,
        Description = "Neo Geo Pocket and Neo Geo Pocket Color using Beetle NeoPop.",
        CoreOptions = new[] { Option("ngp_language", "System language", "english", "japanese"), },
    };

    public static readonly EmulatorSystemDefinition WonderSwan = new(
        "wonderswan", "WonderSwan / Color", "WS/WSC", "mednafen_wswan_libretro.dll",
        Directions | EmulatorButtons.A | EmulatorButtons.B | EmulatorButtons.X | EmulatorButtons.Y |
        EmulatorButtons.L | EmulatorButtons.R | EmulatorButtons.L2 | EmulatorButtons.Select,
        ".ws", ".wsc", ".pc2")
    {
        InputProfile = EmulatorInputProfile.WonderSwan,
        Description = "WonderSwan and WonderSwan Color with both X/Y cursor sets and rotation control.",
    };

    public static readonly EmulatorSystemDefinition PlayStation = new(
        "ps1", "Sony PlayStation", "PS1", "pcsx_rearmed_libretro.dll", PlayStationPad,
        ".cue", ".chd", ".pbp", ".m3u", ".toc", ".img", ".mdf", ".iso", ".exe")
    {
        InputProfile = EmulatorInputProfile.PlayStation,
        DiscBased = true,
        Description = "Software-rendered PlayStation emulation with memory cards and multi-disc playlists.",
        SaveDescription = "Memory card 1/2 + save states",
        Firmware = new[]
        {
            new EmulatorFirmwareDefinition("scph5501.bin", "PlayStation BIOS (recommended)", false),
            new EmulatorFirmwareDefinition("scph1001.bin", "PlayStation BIOS (alternative)", false),
        },
        CoreOptions = new[]
        {
            Option("pcsx_rearmed_bios", "BIOS", "auto", "HLE"),
            Option("pcsx_rearmed_region", "Region", "auto", "NTSC", "PAL"),
            OptionWithDisplay("pcsx_rearmed_memcard1", "Memory card 1",
                new[] { "libretro", "serial", "shared", "none" },
                new[] { "Per game (.srm)", "Per game code (.mcd)", "Shared", "None" }),
            OptionWithDisplay("pcsx_rearmed_memcard2", "Memory card 2",
                new[] { "shared", "serial", "none" },
                new[] { "Shared", "Per game code (.mcd)", "None" }),
            OptionWithDisplay("pcsx_rearmed_multitap", "Multitap",
                new[] { "disabled", "port 1", "port 2", "ports 1 and 2" },
                new[] { "Disabled", "Port 1", "Port 2", "Ports 1 and 2" }),
            Option("pcsx_rearmed_dithering", "Dithering", "enabled", "disabled"),
            Option("pcsx_rearmed_spu_interpolation", "Audio interpolation", "simple", "gaussian", "cubic", "off"),
            Option("pcsx_rearmed_cd_readahead", "CD read-ahead", "12", "0", "2", "4", "8", "16", "32"),
        },
    };

    public static readonly EmulatorSystemDefinition Nintendo64 = new(
        "n64", "Nintendo 64", "N64", "mupen64plus_next_libretro.dll", Nintendo64Pad,
        ".n64", ".v64", ".z64", ".u1")
    {
        InputProfile = EmulatorInputProfile.Nintendo64,
        Description = "Nintendo 64 using software Angrylion rendering inside the plugin.",
        SaveDescription = "Cartridge save / Controller Pak + save states",
        DefaultCoreOptions = Options(
            ("mupen64plus-rdp-plugin", "angrylion"),
            ("mupen64plus-rsp-plugin", "hle"),
            ("mupen64plus-pak1", "memory")),
        CoreOptions = new[]
        {
            Option("mupen64plus-rdp-plugin", "Video renderer", "angrylion"),
            Option("mupen64plus-rsp-plugin", "RSP", "hle", "parallel"),
            Option("mupen64plus-pak1", "Player 1 Pak", "memory", "rumble", "none"),
            Option("mupen64plus-CountPerOp", "CPU timing", "0", "1", "2", "3"),
            Option("mupen64plus-angrylion-vioverlay", "VI filter", "Filtered", "AA+Blur", "AA+Dedither",
                "AA only", "Unfiltered"),
            Option("mupen64plus-angrylion-multithread", "Angrylion threads", "all threads", "1", "2", "3", "4", "5", "6", "7", "8"),
            Option("mupen64plus-astick-deadzone", "Analog deadzone", "15", "20", "25", "30", "0", "5", "10"),
            Option("mupen64plus-astick-sensitivity", "Analog sensitivity", "100", "95", "90", "85", "80", "105", "110"),
        },
    };

    public static IReadOnlyList<EmulatorSystemDefinition> All { get; } =
        new[]
        {
            GameBoy, GameBoyAdvance, Nes, Snes, MegaDrive, SegaCd, Sega8Bit, Atari2600,
            PcEngine, NeoGeo, NeoGeoPocket, WonderSwan,
        };

    public static EmulatorSystemDefinition? ById(string id) =>
        All.FirstOrDefault(system => string.Equals(system.Id, id, StringComparison.OrdinalIgnoreCase));

    public static EmulatorSystemDefinition? Resolve(string path)
    {
        var candidates = All.Where(system => system.Supports(path)).ToArray();
        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".bin", StringComparison.OrdinalIgnoreCase) ? ResolveBinary(path) : null;
    }

    public static string SupportedExtensionsText =>
        string.Join(", ", All.SelectMany(static system => system.Extensions)
            .Append(".bin").Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase));

    private static EmulatorCoreOptionDefinition Option(string key, string label, params string[] values) =>
        new(key, label, values);

    private static EmulatorCoreOptionDefinition OptionWithDisplay(string key, string label, string[] values,
        string[] displayValues) => new(key, label, values, displayValues);

    private static IReadOnlyDictionary<string, string> Options(params (string Key, string Value)[] values) =>
        values.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);

    private static EmulatorSystemDefinition? ResolveBinary(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[0x8000];
            var read = stream.Read(header);
            var data = header[..read];

            if (HasAscii(data, 0x100, "SEGA"))
            {
                return MegaDrive;
            }

            if (HasAscii(data, 0x1ff0, "TMR SEGA") || HasAscii(data, 0x3ff0, "TMR SEGA") ||
                HasAscii(data, 0x7ff0, "TMR SEGA"))
            {
                return Sega8Bit;
            }

            var length = stream.Length;
            if (length is >= 2048 and <= 131072 && (length & (length - 1)) == 0)
            {
                return Atari2600;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Warning($"[Emulator] could not inspect ROM '{path}': {exception.Message}");
        }

        return null;
    }

    private static bool HasAscii(ReadOnlySpan<byte> data, int offset, string value)
    {
        if (offset < 0 || offset + value.Length > data.Length)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (data[offset + index] != value[index])
            {
                return false;
            }
        }

        return true;
    }
}
