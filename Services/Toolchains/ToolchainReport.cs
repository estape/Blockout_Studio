namespace BlockoutStudio.Services.Toolchains;

/// <summary>
/// Status and metadata for a specific compiler or graphics development toolchain.
/// </summary>
public record ToolchainInfo(
    string Name,
    bool IsAvailable,
    string? Version,
    string? Path,
    List<string> InstalledTools,
    string Details
);

/// <summary>
/// System-wide summary report of all detected developer toolchains and runtime translation layers.
/// </summary>
public record ToolchainReport(
    ToolchainInfo Vulkan,
    ToolchainInfo Msvc,
    ToolchainInfo Gcc,
    ToolchainInfo Android,
    string HostOs,
    string ActiveWebViewAngleBackend
);
