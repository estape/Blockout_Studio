using System.Runtime.InteropServices;

namespace BlockoutStudio.Services.Hosting;

/// <summary>
/// Status enumeration indicating which graphics hardware backend was selected for the embedded WebView2 browser.
/// </summary>
public enum VulkanBackendStatus
{
    /// <summary>Backend probing has not yet run.</summary>
    NotChecked,
    /// <summary>Vulkan ANGLE active (--use-angle=vulkan).</summary>
    VulkanAngleActive,
    /// <summary>Direct3D 11 ANGLE active for stability on hybrid GPUs (--use-angle=d3d11).</summary>
    FallbackD3D11,
    /// <summary>Default system browser graphics backend.</summary>
    Default
}

/// <summary>
/// Probes host graphics capabilities and configures WebView2 / WebKitGTK ANGLE presentation flags on application startup.
/// </summary>
public static class VulkanAngleConfigurator
{
    /// <summary>Gets the current ANGLE backend selection status.</summary>
    public static VulkanBackendStatus Status { get; private set; } = VulkanBackendStatus.NotChecked;

    /// <summary>Gets the resolved path to the Vulkan SDK installation directory, if detected.</summary>
    public static string? VulkanSdkPath { get; private set; }

    /// <summary>Gets the path to the system vulkan-1.dll or libvulkan.so.1 driver library.</summary>
    public static string? VulkanDriverPath { get; private set; }

    /// <summary>Gets the command line argument string injected into WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS.</summary>
    public static string ActiveBrowserArguments { get; private set; } = string.Empty;

    /// <summary>
    /// Configures the browser engine flags, defaulting to Direct3D 11 on Windows for stability, or Vulkan on Linux.
    /// </summary>
    /// <returns>The resolved <see cref="VulkanBackendStatus"/>.</returns>
    public static VulkanBackendStatus Configure()
    {
        bool hasVulkan = DetectVulkanRuntime();
        bool forceVulkan = Environment.GetEnvironmentVariable("FORCE_VULKAN_ANGLE") == "1" ||
                           Environment.GetCommandLineArgs().Contains("--vulkan-angle");

        // On Windows with hybrid GPUs (NVIDIA Optimus / Intel UHD), Chromium's experimental
        // ANGLE Vulkan backend crashes the GPU process on swapchain presentation.
        // Direct3D 11 is Chromium's production-grade, rock-solid WebGL 2.0 backend on Windows.
        // The backend compute pipelines (SPIR-V compilation, raymarching compute baker)
        // still execute directly on the LunarG Vulkan SDK & GPU via vulkan-1.dll.
        if (hasVulkan && (forceVulkan || RuntimeInformation.IsOSPlatform(OSPlatform.Linux)))
        {
            // Vulkan ANGLE acceleration flags for Linux / opt-in desktop environments
            ActiveBrowserArguments = "--use-angle=vulkan --enable-features=Vulkan,DefaultANGLEVulkan";
            Status = VulkanBackendStatus.VulkanAngleActive;
        }
        else
        {
            // Direct3D 11 ANGLE for stable, crash-free WebGL 2.0 on Windows
            ActiveBrowserArguments = "--use-angle=d3d11";
            Status = VulkanBackendStatus.FallbackD3D11;
        }

        try
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", ActiveBrowserArguments);
        }
        catch
        {
            // Non-fatal if environment modification restricted
        }

        return Status;
    }

    /// <summary>
    /// Detects if Vulkan runtime libraries (vulkan-1.dll or libvulkan.so.1) are available on the operating system.
    /// </summary>
    /// <returns>True if a Vulkan driver library was found; otherwise false.</returns>
    public static bool DetectVulkanRuntime()
    {
        // 1. Check VULKAN_SDK environment variable
        var sdk = Environment.GetEnvironmentVariable("VULKAN_SDK");
        if (!string.IsNullOrEmpty(sdk) && Directory.Exists(sdk))
        {
            VulkanSdkPath = sdk;
        }

        // 2. Check system runtime DLL / Shared Object
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var vulkanDll = Path.Combine(system32, "vulkan-1.dll");
            if (File.Exists(vulkanDll))
            {
                VulkanDriverPath = vulkanDll;
                return true;
            }

            if (!string.IsNullOrEmpty(VulkanSdkPath))
            {
                var sdkDll = Path.Combine(VulkanSdkPath, "Bin", "vulkan-1.dll");
                if (File.Exists(sdkDll))
                {
                    VulkanDriverPath = sdkDll;
                    return true;
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string[] linuxPaths = ["/usr/lib/x86_64-linux-gnu/libvulkan.so.1", "/usr/lib/libvulkan.so.1", "/usr/lib64/libvulkan.so.1"];
            foreach (var p in linuxPaths)
            {
                if (File.Exists(p))
                {
                    VulkanDriverPath = p;
                    return true;
                }
            }
        }

        return !string.IsNullOrEmpty(VulkanSdkPath);
    }
}
