using System.Text.Json.Serialization;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.BuildPipeline;

/// <summary>
/// Status and configuration of the discovered Vulkan SDK environment.
/// </summary>
public record VulkanSdkInfo(
    bool Available,
    string? Version,
    string? SdkPath,
    string? IncludePath,
    string? LibPath,
    List<string> Tools,
    string StatusMessage,
    bool HasHeaders,
    bool HasLibs
);

/// <summary>
/// Status and configuration of the discovered Windows 10/11 SDK.
/// </summary>
public record WindowsSdkInfo(
    bool Available,
    string? Version,
    string? KitsRoot,
    string? IncludePath,
    string? LibPath,
    string? RcPath,
    string? SignToolPath,
    List<string> Tools,
    string StatusMessage
);

/// <summary>
/// Status and toolchain paths for Microsoft Visual C++ (MSVC) compiler and linker.
/// </summary>
public record MsvcInfo(
    bool Available,
    string? Version,
    string? InstallationPath,
    string? ClPath,
    string? LinkPath,
    string? VcVarsPath,
    string? IncludePath,
    string? LibPath,
    List<string> Tools,
    string StatusMessage
);

/// <summary>
/// Status and availability of Linux GCC toolchain or Windows Subsystem for Linux (WSL).
/// </summary>
public record LinuxSdkInfo(
    bool Available,
    string? GccVersion,
    string? GccPath,
    bool WslAvailable,
    string? WslDetails,
    List<string> Tools,
    string StatusMessage
);

/// <summary>
/// Information describing a connected Android physical or virtual device via ADB.
/// </summary>
public record AdbDeviceInfo(
    string Serial,
    string State,
    string? Model,
    string? Product,
    string? Device
);

/// <summary>
/// Status of the Android SDK, NDK, and connected device deployment tools.
/// </summary>
public record AndroidSdkInfo(
    bool Available,
    string? SdkPath,
    string? NdkPath,
    string? AdbPath,
    string? Aapt2Path,
    string? ApkSignerPath,
    string? ClangPath,
    string? CliVersion,
    List<AdbDeviceInfo> ConnectedDevices,
    List<string> Tools,
    string StatusMessage
);

/// <summary>
/// Comprehensive system report aggregating status across all platform SDKs and host environment.
/// </summary>
public record SdkEnvironmentReport(
    VulkanSdkInfo Vulkan,
    WindowsSdkInfo Windows,
    MsvcInfo Msvc,
    LinuxSdkInfo Linux,
    AndroidSdkInfo Android,
    string HostOs,
    string AngleStatus,
    DateTime Timestamp
);

/// <summary>
/// Service interface for detecting installed compilers, graphics SDKs, and platform toolchains.
/// </summary>
public interface ISdkDetectorService
{
    /// <summary>
    /// Performs a full probing pass across Vulkan, Windows SDK, MSVC, Linux/WSL, and Android SDK/NDK.
    /// </summary>
    /// <param name="projectRoot">Optional active project path for loading custom SDK path overrides.</param>
    /// <returns>Populated <see cref="SdkEnvironmentReport"/>.</returns>
    SdkEnvironmentReport DetectAllSdks(string? projectRoot = null);

    /// <summary>
    /// Probes the local machine for LunarG Vulkan SDK and shader tool binaries.
    /// </summary>
    /// <param name="customPath">Optional explicit path override.</param>
    /// <returns>Discovered <see cref="VulkanSdkInfo"/>.</returns>
    VulkanSdkInfo ProbeVulkan(string? customPath = null);

    /// <summary>
    /// Probes the local registry and Windows Kits directories for Windows 10/11 SDK.
    /// </summary>
    /// <param name="customKitsRoot">Optional custom kits root path override.</param>
    /// <returns>Discovered <see cref="WindowsSdkInfo"/>.</returns>
    WindowsSdkInfo ProbeWindowsSdk(string? customKitsRoot = null);

    /// <summary>
    /// Uses vswhere or standard install directories to locate Visual C++ toolset.
    /// </summary>
    /// <param name="customVsPath">Optional custom Visual Studio path.</param>
    /// <returns>Discovered <see cref="MsvcInfo"/>.</returns>
    MsvcInfo ProbeMsvc(string? customVsPath = null);

    /// <summary>
    /// Probes for native GCC/Clang or WSL Linux distribution.
    /// </summary>
    /// <returns>Discovered <see cref="LinuxSdkInfo"/>.</returns>
    LinuxSdkInfo ProbeLinux();

    /// <summary>
    /// Probes Android SDK/NDK environment variables and standard Android Studio paths.
    /// </summary>
    /// <param name="customSdkPath">Optional custom Android SDK root path.</param>
    /// <param name="customNdkPath">Optional custom Android NDK root path.</param>
    /// <returns>Discovered <see cref="AndroidSdkInfo"/>.</returns>
    AndroidSdkInfo ProbeAndroid(string? customSdkPath = null, string? customNdkPath = null);

    /// <summary>
    /// Queries ADB for physically connected or emulated Android targets.
    /// </summary>
    /// <param name="adbPath">Path to adb executable.</param>
    /// <returns>List of connected ADB devices.</returns>
    List<AdbDeviceInfo> GetConnectedAdbDevices(string? adbPath = null);

    /// <summary>
    /// Persists custom SDK path configuration into ProjectSettings/SDKPaths.json.
    /// </summary>
    /// <param name="projectRoot">Project directory root path.</param>
    /// <param name="paths">Settings instance to write.</param>
    void SaveCustomSdkPaths(string projectRoot, SdkPathsSettings paths);
}
