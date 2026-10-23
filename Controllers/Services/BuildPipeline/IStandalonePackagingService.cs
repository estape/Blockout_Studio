using BlockoutStudio.Services.Blueprints.Runtime;

namespace BlockoutStudio.Services.BuildPipeline;

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

/// <summary>
/// Automated build coordinator compiling exported projects into standalone native binaries
/// linking directly to EngineNative.dll with zero editor overhead.
/// </summary>
public interface IStandalonePackagingService
{
    Task<StandalonePackageResult> PackageStandaloneGameAsync(
        string projectRoot,
        StandalonePackageRequest request,
        Action<string>? onLogStream = null);
}
