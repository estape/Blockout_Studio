using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UE5BlockoutStudio.Services.Assets;

public class AssetImportSettings
{
    [JsonPropertyName("Scale")]
    public float Scale { get; set; } = 1.0f;

    [JsonPropertyName("UnitSystem")]
    public string UnitSystem { get; set; } = "Centimeters";

    [JsonPropertyName("UpAxis")]
    public string UpAxis { get; set; } = "Z";

    [JsonPropertyName("GenerateColliders")]
    public bool GenerateColliders { get; set; } = true;

    [JsonPropertyName("GenerateLODs")]
    public bool GenerateLODs { get; set; } = true;

    [JsonPropertyName("LODLevels")]
    public int LODLevels { get; set; } = 3;

    [JsonPropertyName("CompressionQuality")]
    public int CompressionQuality { get; set; } = 90;

    [JsonPropertyName("BakeAoOnImport")]
    public bool BakeAoOnImport { get; set; } = false;
}

public class AssetMeta
{
    [JsonPropertyName("Guid")]
    public string Guid { get; set; } = System.Guid.NewGuid().ToString("N");

    [JsonPropertyName("AssetType")]
    public string AssetType { get; set; } = "Unknown";

    [JsonPropertyName("SourceHash")]
    public string SourceHash { get; set; } = "";

    [JsonPropertyName("LastModifiedUtc")]
    public string LastModifiedUtc { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("ImportSettings")]
    public AssetImportSettings ImportSettings { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

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
            ".bscene" or ".ueblock" => "Scene",
            ".prefab" => "Prefab",
            ".cs" or ".cpp" or ".h" or ".hpp" or ".js" => "Script",
            _ => "Unknown"
        };
    }

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
