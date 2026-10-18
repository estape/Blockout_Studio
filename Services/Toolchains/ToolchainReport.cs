namespace UE5BlockoutStudio.Services.Toolchains;

public record ToolchainInfo(
    string Name,
    bool IsAvailable,
    string? Version,
    string? Path,
    List<string> InstalledTools,
    string Details
);

public record ToolchainReport(
    ToolchainInfo Vulkan,
    ToolchainInfo Msvc,
    ToolchainInfo Gcc,
    ToolchainInfo Android,
    string HostOs,
    string ActiveWebViewAngleBackend
);
