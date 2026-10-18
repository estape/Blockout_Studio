using System.Runtime.InteropServices;

namespace UE5BlockoutStudio.Services.Hosting;

public enum VulkanBackendStatus
{
    NotChecked,
    VulkanAngleActive,
    FallbackD3D11,
    Default
}

public static class VulkanAngleConfigurator
{
    public static VulkanBackendStatus Status { get; private set; } = VulkanBackendStatus.NotChecked;
    public static string? VulkanSdkPath { get; private set; }
    public static string? VulkanDriverPath { get; private set; }
    public static string ActiveBrowserArguments { get; private set; } = string.Empty;

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
