namespace BlockoutStudio.Services.Toolchains;

/// <summary>
/// Service interface for querying host system toolchain installations and graphics API availability.
/// </summary>
public interface IToolchainDiscoveryService
{
    /// <summary>
    /// Scans the machine for Vulkan, MSVC, GCC, and Android toolchains.
    /// </summary>
    /// <returns>A consolidated <see cref="ToolchainReport"/>.</returns>
    ToolchainReport DiscoverToolchains();
}
