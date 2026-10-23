using System.Text.Json.Serialization;

namespace BlockoutStudio.Models.Assets;

/// <summary>
/// Individual entry in the project-wide AssetRegistry index database.
/// </summary>
public class AssetRegistryItem
{
    /// <summary>Persistent 128-bit GUID matching the asset's .meta sidecar.</summary>
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = "";

    /// <summary>Virtual logical path within the project (e.g. "/Game/Meshes/SM_Pillar").</summary>
    [JsonPropertyName("virtualPath")]
    public string VirtualPath { get; set; } = "";

    /// <summary>Relative file system path from project root (e.g. "Assets/Meshes/SM_Pillar.obj").</summary>
    [JsonPropertyName("relativeDiskPath")]
    public string RelativeDiskPath { get; set; } = "";

    /// <summary>File name including extension.</summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    /// <summary>Asset category type string.</summary>
    [JsonPropertyName("assetType")]
    public string AssetType { get; set; } = "";

    /// <summary>Total size of the asset file in bytes.</summary>
    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    /// <summary>SHA-256 fingerprint hash of the asset source.</summary>
    [JsonPropertyName("sourceHash")]
    public string SourceHash { get; set; } = "";

    /// <summary>Last modification timestamp (ISO 8601 UTC).</summary>
    [JsonPropertyName("lastModifiedUtc")]
    public string LastModifiedUtc { get; set; } = "";

    /// <summary>List of asset GUIDs that this asset depends upon.</summary>
    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    /// <summary>List of asset GUIDs that reference this asset.</summary>
    [JsonPropertyName("referencers")]
    public List<string> Referencers { get; set; } = new();
}

/// <summary>
/// Master asset registry schema stored in Library/AssetRegistry.json.
/// </summary>
public class AssetRegistryData
{
    /// <summary>Schema format version number.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>Timestamp (ISO 8601 UTC) when the project was last scanned and indexed.</summary>
    [JsonPropertyName("lastIndexedUtc")]
    public string LastIndexedUtc { get; set; } = DateTime.UtcNow.ToString("o");

    /// <summary>Target project name.</summary>
    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = "";

    /// <summary>List of all discovered and registered asset records.</summary>
    [JsonPropertyName("assets")]
    public List<AssetRegistryItem> Assets { get; set; } = new();
}



