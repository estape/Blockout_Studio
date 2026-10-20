namespace BlockoutStudio.Services.BuildPipeline;

/// <summary>
/// Packaging specification defining target operating system, quality tier, and bundling flags.
/// </summary>
public record BuildRequest(
    string TargetPlatform = "Windows",
    string ScalabilityTier = "DesktopPC",
    bool CookShaders = true,
    bool BakeGeometryCaches = true,
    bool PackageSelfContained = true,
    string? CustomOutputDir = null,
    string? TargetDeviceId = null
);

/// <summary>
/// Execution metrics and status for an individual build pipeline phase.
/// </summary>
public record BuildStepInfo(
    string StepName,
    string Status,
    long DurationMs,
    string? Details
);

/// <summary>
/// Full summary result of a project build/packaging execution pass.
/// </summary>
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

/// <summary>
/// Service interface for orchestrating asset cooking, binary staging, and device deployment.
/// </summary>
public interface IPlatformBuilderService
{
    /// <summary>
    /// Packages the project for a targeted platform (Windows, Linux, Android) into a standalone staging folder.
    /// </summary>
    /// <param name="projectRoot">Project directory root.</param>
    /// <param name="request">Build options and platform choices.</param>
    /// <param name="onLogStream">Optional streaming log callback.</param>
    /// <returns>Completed <see cref="BuildResult"/>.</returns>
    Task<BuildResult> BuildProjectAsync(
        string projectRoot,
        BuildRequest request,
        Action<string>? onLogStream = null
    );

    /// <summary>
    /// Installs and launches the packaged application on a connected hardware device or emulator via ADB.
    /// </summary>
    /// <param name="projectRoot">Project directory root.</param>
    /// <param name="deviceId">Optional specific device serial ID.</param>
    /// <param name="onLogStream">Optional streaming log callback.</param>
    /// <returns>True if installation and launch succeeded.</returns>
    Task<bool> DeployToDeviceAsync(
        string projectRoot,
        string? deviceId = null,
        Action<string>? onLogStream = null
    );
}
