using System.Reflection;
using Dalamud;

namespace Aetherphone.Harness.Fonts;

internal sealed class DalamudAssetFiles
{
    private const string PathAttributeName = "DalamudAssetPathAttribute";
    private const string CjkFallback = "UIRes/NotoSansCJKjp-Medium.otf";
    private const string IconFallback = "UIRes/FontAwesomeFreeSolid.otf";
    private readonly string assetDirectory;
    private readonly Dictionary<DalamudAsset, string?> resolved = new();

    public DalamudAssetFiles(string assetDirectory)
    {
        this.assetDirectory = assetDirectory;
    }

    public string Resolve(DalamudAsset asset)
    {
        if (resolved.TryGetValue(asset, out var cached))
        {
            return cached ?? throw Missing(asset);
        }

        var path = Locate(asset);
        resolved[asset] = path;
        return path ?? throw Missing(asset);
    }

    private string? Locate(DalamudAsset asset)
    {
        var declared = DeclaredPath(asset);
        if (declared is not null && File.Exists(declared))
        {
            return declared;
        }

        var fallback = asset switch
        {
            DalamudAsset.NotoSansCjkRegular or DalamudAsset.NotoSansCjkMedium => CjkFallback,
            DalamudAsset.FontAwesomeFreeSolid => IconFallback,
            _ => null,
        };
        if (fallback is null)
        {
            return null;
        }

        var fallbackPath = Path.Combine(assetDirectory, fallback.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(fallbackPath) ? fallbackPath : null;
    }

    private string? DeclaredPath(DalamudAsset asset)
    {
        var field = typeof(DalamudAsset).GetField(asset.ToString(), BindingFlags.Public | BindingFlags.Static);
        if (field is null)
        {
            return null;
        }

        var attributes = field.GetCustomAttributesData();
        for (var index = 0; index < attributes.Count; index++)
        {
            var attribute = attributes[index];
            if (attribute.AttributeType.Name != PathAttributeName || attribute.ConstructorArguments.Count == 0)
            {
                continue;
            }

            var argument = attribute.ConstructorArguments[0];
            if (argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> parts)
            {
                var segments = new List<string>(parts.Count + 1) { assetDirectory };
                foreach (var part in parts)
                {
                    segments.Add(part.Value?.ToString() ?? string.Empty);
                }

                return Path.Combine(segments.ToArray());
            }

            if (argument.Value is string single)
            {
                return Path.Combine(assetDirectory, single.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        return null;
    }

    private FileNotFoundException Missing(DalamudAsset asset) =>
        new($"Dalamud asset {asset} is not in {assetDirectory}. Run the harness bootstrap to download fonts.");
}
