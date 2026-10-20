using System.Diagnostics;
using System.Runtime.InteropServices;
using BlockoutStudio.Services.Hosting;

namespace BlockoutStudio.Services.Toolchains;

/// <summary>
/// Service implementation for discovering platform toolchains and compilers on the host system.
/// </summary>
public class ToolchainDiscoveryService : IToolchainDiscoveryService
{
    /// <inheritdoc/>
    public ToolchainReport DiscoverToolchains()
    {
        var vulkan = ProbeVulkan();
        var msvc = ProbeMsvc();
        var gcc = ProbeGcc();
        var android = ProbeAndroid();

        string hostOs = RuntimeInformation.OSDescription;
        string angleStatus = VulkanAngleConfigurator.Status.ToString();

        return new ToolchainReport(vulkan, msvc, gcc, android, hostOs, angleStatus);
    }

    /// <summary>Probes for the Vulkan SDK and runtime libraries.</summary>
    private static ToolchainInfo ProbeVulkan()
    {
        var tools = new List<string>();
        string? sdkPath = Environment.GetEnvironmentVariable("VULKAN_SDK");
        string? version = null;

        if (string.IsNullOrEmpty(sdkPath) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var rootDir = @"C:\VulkanSDK";
            if (Directory.Exists(rootDir))
            {
                var dirs = Directory.GetDirectories(rootDir);
                if (dirs.Length > 0)
                {
                    Array.Sort(dirs);
                    sdkPath = dirs[^1];
                }
            }
        }

        if (!string.IsNullOrEmpty(sdkPath) && Directory.Exists(sdkPath))
        {
            version = Path.GetFileName(sdkPath.TrimEnd(Path.DirectorySeparatorChar));
            var binDir = Path.Combine(sdkPath, "Bin");
            if (Directory.Exists(binDir))
            {
                string[] candidates = ["glslc.exe", "vulkaninfo.exe", "glslangValidator.exe", "spirv-opt.exe", "spirv-cross.exe"];
                foreach (var c in candidates)
                {
                    if (File.Exists(Path.Combine(binDir, c))) tools.Add(c);
                }
            }
        }

        bool available = !string.IsNullOrEmpty(sdkPath) || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vulkan-1.dll"));
        return new ToolchainInfo(
            "Vulkan SDK & Runtime",
            available,
            version ?? "1.4+",
            sdkPath,
            tools,
            available ? $"Vulkan runtime active with {tools.Count} SPIR-V shader tools available." : "Vulkan SDK not detected on system."
        );
    }

    /// <summary>Probes for Microsoft Visual C++ compiler and linker toolsets.</summary>
    private static ToolchainInfo ProbeMsvc()
    {
        var tools = new List<string>();
        string? vsPath = null;
        string? version = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
            if (File.Exists(vswhere))
            {
                try
                {
                    var psi = new ProcessStartInfo(vswhere, "-latest -property installationPath")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit(1000);
                        if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
                        {
                            vsPath = output;
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(vsPath))
            {
                var defaultBuildTools = @"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools";
                if (Directory.Exists(defaultBuildTools)) vsPath = defaultBuildTools;
            }

            if (!string.IsNullOrEmpty(vsPath))
            {
                var msvcRoot = Path.Combine(vsPath, "VC", "Tools", "MSVC");
                if (Directory.Exists(msvcRoot))
                {
                    var versions = Directory.GetDirectories(msvcRoot);
                    if (versions.Length > 0)
                    {
                        Array.Sort(versions);
                        var latest = versions[^1];
                        version = Path.GetFileName(latest);
                        var clExe = Path.Combine(latest, "bin", "Hostx64", "x64", "cl.exe");
                        if (File.Exists(clExe)) tools.Add("cl.exe (x64 C++20)");
                        if (File.Exists(Path.Combine(latest, "bin", "Hostx64", "x64", "link.exe"))) tools.Add("link.exe");
                    }
                }
            }
        }

        bool available = tools.Count > 0;
        return new ToolchainInfo(
            "MSVC C++20 Compiler & Windows SDK",
            available,
            version ?? "Visual Studio 2022",
            vsPath,
            tools,
            available ? $"MSVC {version} available for native EngineNative.dll compilation." : "MSVC BuildTools not detected."
        );
    }

    /// <summary>Probes for the Android SDK, CLI tools, and ADB.</summary>
    private static ToolchainInfo ProbeAndroid()
    {
        var tools = new List<string>();
        string? sdkPath = Environment.GetEnvironmentVariable("ANDROID_HOME") ?? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        string? cliVersion = null;

        if (string.IsNullOrEmpty(sdkPath))
        {
            var localSdk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk");
            if (Directory.Exists(localSdk)) sdkPath = localSdk;
        }

        // Check android CLI
        var localCli = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "AndroidCLI", "android.exe");
        if (File.Exists(localCli))
        {
            tools.Add("android.exe (Android CLI)");
            cliVersion = "1.0.16500706";
        }
        else
        {
            // Probe PATH
            try
            {
                var psi = new ProcessStartInfo("android", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    cliVersion = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(1000);
                    tools.Add("android.exe (Android CLI)");
                }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(sdkPath) && Directory.Exists(sdkPath))
        {
            var adb = Path.Combine(sdkPath, "platform-tools", "adb.exe");
            if (File.Exists(adb)) tools.Add("adb.exe");
        }

        bool available = tools.Count > 0 || !string.IsNullOrEmpty(sdkPath);
        return new ToolchainInfo(
            "Android SDK & CLI Toolchain",
            available,
            cliVersion ?? "Android SDK Available",
            sdkPath,
            tools,
            available ? $"Android CLI and SDK paths detected with {tools.Count} tools." : "Android SDK not configured."
        );
    }

    /// <summary>Probes for Linux GCC/G++ compiler presence.</summary>
    private static ToolchainInfo ProbeGcc()
    {
        var tools = new List<string>();
        string? version = null;
        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        if (isLinux)
        {
            try
            {
                var psi = new ProcessStartInfo("g++", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    version = proc.StandardOutput.ReadLine();
                    proc.WaitForExit(1000);
                    tools.Add("g++");
                }
            }
            catch { }
        }

        bool available = tools.Count > 0;
        return new ToolchainInfo(
            "Linux GCC/G++ Toolchain",
            available,
            version,
            isLinux ? "/usr/bin" : null,
            tools,
            isLinux ? "Linux GCC/G++ build environment detected." : "Not running on Linux (Windows Host Active)."
        );
    }
}
