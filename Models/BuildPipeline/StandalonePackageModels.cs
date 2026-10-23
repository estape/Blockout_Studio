using BlockoutStudio.Services.Blueprints.Runtime;

namespace BlockoutStudio.Models.BuildPipeline;

/// <summary>
/// Packaging options for standalone NativeAOT build export.
/// </summary>
public record StandalonePackageRequest(
    string OutputDirectory,
    string Configuration = "Release",
    string RuntimeIdentifier = "win-x64",
    bool SkipNativeAotPublish = false,
    List<Actor>? SceneActors = null);

/// <summary>
/// Detailed result metrics of the standalone game build and packaging pipeline.
/// </summary>
public record StandalonePackageResult(
    bool Success,
    string OutputExecutablePath,
    string OutputDirectory,
    long ExecutableSizeBytes,
    int TranspiledScriptCount,
    int StagedAssetCount,
    double ElapsedSeconds,
    string? ErrorMessage = null);


