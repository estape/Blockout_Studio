using System.Text.Json.Serialization;

namespace BlockoutStudio.Services.Assets;

public class AssetRegistryItem
{
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = "";

    [JsonPropertyName("virtualPath")]
    public string VirtualPath { get; set; } = "";

    [JsonPropertyName("relativeDiskPath")]
    public string RelativeDiskPath { get; set; } = "";

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("assetType")]
    public string AssetType { get; set; } = "";

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("sourceHash")]
    public string SourceHash { get; set; } = "";

    [JsonPropertyName("lastModifiedUtc")]
    public string LastModifiedUtc { get; set; } = "";

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("referencers")]
    public List<string> Referencers { get; set; } = new();
}

public class AssetRegistryData
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("lastIndexedUtc")]
    public string LastIndexedUtc { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<AssetRegistryItem> Assets { get; set; } = new();
}
