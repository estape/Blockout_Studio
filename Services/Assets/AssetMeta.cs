using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlockoutStudio.Services.Assets;

/// <summary>
/// Configurable import parameters stored per-asset inside .meta sidecar files.
/// </summary>
public class AssetImportSettings
{
    /// <summary>Geometric uniform scale factor applied during import.</summary>
    [JsonPropertyName("Scale")]
    public float Scale { get; set; } = 1.0f;

    /// <summary>Expected unit system of the source file (e.g. "Centimeters", "Meters").</summary>
    [JsonPropertyName("UnitSystem")]
    public string UnitSystem { get; set; } = "Centimeters";

    /// <summary>Coordinate up-axis convention of source geometry ("Z" or "Y").</summary>
    [JsonPropertyName("UpAxis")]
    public string UpAxis { get; set; } = "Z";

    /// <summary>Whether to automatically generate simplified convex/box collision geometry.</summary>
    [JsonPropertyName("GenerateColliders")]
    public bool GenerateColliders { get; set; } = true;

    /// <summary>Whether to synthesize reduced LOD meshes (LOD1, LOD2).</summary>
    [JsonPropertyName("GenerateLODs")]
    public bool GenerateLODs { get; set; } = true;

    /// <summary>Target total LOD cascade count.</summary>
    [JsonPropertyName("LODLevels")]
    public int LODLevels { get; set; } = 3;

    /// <summary>Texture/mesh compression quality setting (1-100).</summary>
    [JsonPropertyName("CompressionQuality")]
    public int CompressionQuality { get; set; } = 90;

    /// <summary>Whether to execute Vulkan/CPU vertex ambient occlusion baking on import.</summary>
    [JsonPropertyName("BakeAoOnImport")]
    public bool BakeAoOnImport { get; set; } = false;
}

/// <summary>
/// Sidecar metadata structure (.meta) tracking asset GUID, SHA-256 fingerprint, and import configuration.
/// </summary>
public class AssetMeta
{
    /// <summary>Globally unique persistent 128-bit hexadecimal identifier for this asset.</summary>
    [JsonPropertyName("Guid")]
    public string Guid { get; set; } = System.Guid.NewGuid().ToString("N");

    /// <summary>Engine asset category (e.g. StaticMesh, UsdStage, Material, Texture).</summary>
    [JsonPropertyName("AssetType")]
    public string AssetType { get; set; } = "Unknown";

    /// <summary>SHA-256 hex digest of the raw source file payload.</summary>
    [JsonPropertyName("SourceHash")]
    public string SourceHash { get; set; } = "";

    /// <summary>Timestamp string (ISO 8601 UTC) when the source file was last observed modified.</summary>
    [JsonPropertyName("LastModifiedUtc")]
    public string LastModifiedUtc { get; set; } = DateTime.UtcNow.ToString("o");

    /// <summary>Specific import settings for this asset type.</summary>
    [JsonPropertyName("ImportSettings")]
    public AssetImportSettings ImportSettings { get; set; } = new();

    /// <summary>Indented JSON serializer options for pretty-printed .meta sidecars.</summary>
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>
    /// Computes the SHA-256 lowercase hexadecimal hash of a given file on disk.
    /// </summary>
    /// <param name="filePath">Target file path.</param>
    /// <returns>64-character lowercase hexadecimal hash string or empty string on failure.</returns>
    public static string ComputeFileSha256(string filePath)
    {
        if (!File.Exists(filePath)) return "";
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Detects the asset category string based on file extension.
    /// </summary>
    /// <param name="filePath">Target asset file name or path.</param>
    /// <returns>Asset type string identifier.</returns>
    public static string DetectAssetType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".obj" or ".fbx" => "StaticMesh",
            ".usd" or ".usda" or ".usdc" or ".usdz" => "UsdStage",
            ".mat" or ".material" => "Material",
            ".glsl" or ".vert" or ".frag" or ".comp" or ".hlsl" or ".spv" => "Shader",
            ".png" or ".jpg" or ".jpeg" or ".tga" or ".webp" or ".bmp" => "Texture",
            ".wav" or ".ogg" or ".mp3" => "Audio",
            ".bscene" or ".pjblock" or ".ueblock" => "Scene",
            ".prefab" => "Prefab",
            ".cs" or ".cpp" or ".h" or ".hpp" or ".js" => "Script",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Loads an existing .meta sidecar file or creates, hashes, and writes a new one if missing or outdated.
    /// </summary>
    /// <param name="assetFilePath">Full path to the source asset.</param>
    /// <returns>Loaded or freshly created <see cref="AssetMeta"/> object.</returns>
    public static AssetMeta LoadOrCreate(string assetFilePath)
    {
        var metaPath = assetFilePath + ".meta";
        var hash = ComputeFileSha256(assetFilePath);
        var type = DetectAssetType(assetFilePath);

        if (File.Exists(metaPath))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<AssetMeta>(File.ReadAllText(metaPath));
                if (existing != null && !string.IsNullOrWhiteSpace(existing.Guid))
                {
                    if (existing.SourceHash != hash)
                    {
                        existing.SourceHash = hash;
                        existing.LastModifiedUtc = DateTime.UtcNow.ToString("o");
                        File.WriteAllText(metaPath, JsonSerializer.Serialize(existing, JsonOpts));
                    }
                    return existing;
                }
            }
            catch
            {
                // Re-create on corruption
            }
        }

        var meta = new AssetMeta
        {
            Guid = System.Guid.NewGuid().ToString("N"),
            AssetType = type,
            SourceHash = hash,
            LastModifiedUtc = DateTime.UtcNow.ToString("o"),
            ImportSettings = new AssetImportSettings()
        };

        try
        {
            File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, JsonOpts));
        }
        catch
        {
            // ignored
        }

        return meta;
    }
}
