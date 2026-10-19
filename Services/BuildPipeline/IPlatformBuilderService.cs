namespace BlockoutStudio.Services.BuildPipeline;

public record BuildRequest(
    string TargetPlatform = "Windows",
    string ScalabilityTier = "DesktopPC",
    bool CookShaders = true,
    bool BakeGeometryCaches = true,
    bool PackageSelfContained = true,
    string? CustomOutputDir = null,
    string? TargetDeviceId = null
);

public record BuildStepInfo(
    string StepName,
    string Status,
    long DurationMs,
    string? Details
);

public record BuildResult(
    bool Success,
    string TargetPlatform,
    string OutputDirectory,
    string? ExecutableOrApkPath,
    long DurationMs,
    List<BuildStepInfo> Steps,
    List<string> Logs,
    string StatusMessage
);

public interface IPlatformBuilderService
{
    Task<BuildResult> BuildProjectAsync(
        string projectRoot,
        BuildRequest request,
        Action<string>? onLogStream = null
    );

    Task<bool> DeployToDeviceAsync(
        string projectRoot,
        string? deviceId = null,
        Action<string>? onLogStream = null
    );
}
