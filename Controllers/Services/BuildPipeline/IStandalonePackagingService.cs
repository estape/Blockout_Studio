using BlockoutStudio.Models.BuildPipeline;

namespace BlockoutStudio.Services.BuildPipeline;

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
