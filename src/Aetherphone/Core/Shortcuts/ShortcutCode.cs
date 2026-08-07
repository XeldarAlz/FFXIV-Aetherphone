using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Core.Shortcuts;

internal enum ShortcutCodeError : byte
{
    None,
    NotACode,
    Malformed,
    UnsafeLink,
}

internal sealed class ShortcutStepDto
{
    public byte Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Seconds { get; set; }
}

internal sealed class ShortcutCodeDto
{
    public string Name { get; set; } = string.Empty;
    public int Glyph { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Tint { get; set; } = string.Empty;
    public ShortcutStepDto[] Steps { get; set; } = Array.Empty<ShortcutStepDto>();
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ShortcutCodeDto))]
internal sealed partial class ShortcutCodeJsonContext : JsonSerializerContext
{
}

internal static class ShortcutCode
{
    public const string Prefix = "AEPS1.";

    private const int MaxCodeLength = 32000;
    private const int MaxIconNameLength = 64;

    public static string Encode(ShortcutEntry entry)
    {
        var dto = new ShortcutCodeDto
        {
            Name = entry.Name,
            Glyph = entry.Glyph,
            Icon = entry.IconPlugin,
            Tint = entry.Tint,
            Steps = new ShortcutStepDto[entry.Steps.Count],
        };

        for (var index = 0; index < entry.Steps.Count; index++)
        {
            var step = entry.Steps[index];
            dto.Steps[index] = new ShortcutStepDto
            {
                Kind = (byte)step.Kind,
                Text = step.Text,
                Seconds = step.Seconds,
            };
        }

        var json = JsonSerializer.Serialize(dto, ShortcutCodeJsonContext.Default.ShortcutCodeDto);
        return string.Concat(Prefix, Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    public static bool TryDecode(string text, out ShortcutEntry entry, out ShortcutCodeError error)
    {
        entry = null!;
        error = ShortcutCodeError.NotACode;
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length <= Prefix.Length || trimmed.Length > MaxCodeLength ||
            !trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dto = Read(trimmed.Substring(Prefix.Length));
        if (dto is null)
        {
            error = ShortcutCodeError.Malformed;
            return false;
        }

        return TryBuild(dto, out entry, out error);
    }

    private static ShortcutCodeDto? Read(string payload)
    {
        try
        {
            var bytes = Convert.FromBase64String(payload);
            return JsonSerializer.Deserialize(Encoding.UTF8.GetString(bytes),
                ShortcutCodeJsonContext.Default.ShortcutCodeDto);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"A shortcut code could not be read: {exception.Message}");
            return null;
        }
    }

    private static bool TryBuild(ShortcutCodeDto dto, out ShortcutEntry entry, out ShortcutCodeError error)
    {
        entry = null!;
        error = ShortcutCodeError.Malformed;
        var name = (dto.Name ?? string.Empty).Trim();
        if (name.Length == 0 || dto.Steps is null || dto.Steps.Length > ShortcutStore.MaxSteps)
        {
            return false;
        }

        var built = new ShortcutEntry
        {
            Name = Clamp(name, ShortcutStore.NameMaxLength),
            Glyph = KnownGlyph(dto.Glyph),
            IconPlugin = Clamp((dto.Icon ?? string.Empty).Trim(), MaxIconNameLength),
            Tint = HexColor.TryParse(dto.Tint ?? string.Empty, out var tint) ? HexColor.ToDigits(tint) : string.Empty,
        };

        error = ShortcutCodeError.None;
        for (var index = 0; index < dto.Steps.Length; index++)
        {
            if (!TryStep(dto.Steps[index], built.Steps, out error))
            {
                return false;
            }
        }

        if (built.Steps.Count == 0)
        {
            error = ShortcutCodeError.Malformed;
            return false;
        }

        entry = built;
        return true;
    }

    private static bool TryStep(ShortcutStepDto dto, List<ShortcutStep> into, out ShortcutCodeError error)
    {
        error = ShortcutCodeError.None;
        if (!Enum.IsDefined(typeof(ShortcutStepKind), dto.Kind))
        {
            error = ShortcutCodeError.Malformed;
            return false;
        }

        var kind = (ShortcutStepKind)dto.Kind;
        if (kind == ShortcutStepKind.Wait)
        {
            into.Add(new ShortcutStep
            {
                Kind = kind,
                Seconds = Math.Clamp(dto.Seconds, ShortcutRunner.MinWaitSeconds, ShortcutRunner.MaxWaitSeconds),
            });
            return true;
        }

        var body = (dto.Text ?? string.Empty).Trim();
        if (body.Length > ShortcutStore.CommandMaxLength)
        {
            error = ShortcutCodeError.Malformed;
            return false;
        }

        if (body.Length == 0)
        {
            return true;
        }

        if (kind == ShortcutStepKind.OpenUrl && !ShortcutCommandText.IsWebUrl(body))
        {
            error = ShortcutCodeError.UnsafeLink;
            return false;
        }

        into.Add(new ShortcutStep { Kind = kind, Text = body });
        return true;
    }

    private static int KnownGlyph(int glyph) =>
        glyph != 0 && Enum.IsDefined(typeof(FontAwesomeIcon), glyph) ? glyph : 0;

    private static string Clamp(string text, int maxLength) =>
        text.Length <= maxLength ? text : text.Substring(0, maxLength);
}
