namespace BlockoutStudio.Models.Blueprints;

/// <summary>
/// Metadata info for a discovered Blueprint asset in the project.
/// </summary>
public record BlueprintAssetInfo(
    string Name,
    string RelativePath,
    string FullPath,
    string Guid,
    string GraphType,
    long FileSizeBytes,
    DateTime LastModifiedUtc);

/// <summary>
/// Result of saving a Blueprint graph and generating its C# counterpart.
/// </summary>
public record SaveBlueprintResult(
    bool Success,
    string BlueprintPath,
    string GeneratedCSharpPath,
    string? ErrorMessage = null);


