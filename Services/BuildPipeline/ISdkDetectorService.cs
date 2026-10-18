using System.Text.Json.Serialization;
using UE5BlockoutStudio.Services.Assets;

namespace UE5BlockoutStudio.Services.BuildPipeline;

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

public record LinuxSdkInfo(
    bool Available,
    string? GccVersion,
    string? GccPath,
    bool WslAvailable,
    string? WslDetails,
    List<string> Tools,
    string StatusMessage
);

public record AdbDeviceInfo(
    string Serial,
    string State,
    string? Model,
    string? Product,
    string? Device
);

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

public interface ISdkDetectorService
{
    SdkEnvironmentReport DetectAllSdks(string? projectRoot = null);
    VulkanSdkInfo ProbeVulkan(string? customPath = null);
    WindowsSdkInfo ProbeWindowsSdk(string? customKitsRoot = null);
    MsvcInfo ProbeMsvc(string? customVsPath = null);
    LinuxSdkInfo ProbeLinux();
    AndroidSdkInfo ProbeAndroid(string? customSdkPath = null, string? customNdkPath = null);
    List<AdbDeviceInfo> GetConnectedAdbDevices(string? adbPath = null);
    void SaveCustomSdkPaths(string projectRoot, SdkPathsSettings paths);
}
