using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using UE5BlockoutStudio.Services.Assets;
using UE5BlockoutStudio.Services.Hosting;

namespace UE5BlockoutStudio.Services.BuildPipeline;

public class SdkDetectorService : ISdkDetectorService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SdkEnvironmentReport DetectAllSdks(string? projectRoot = null)
    {
        string? customVulkan = null;
        string? customWindows = null;
        string? customMsvc = null;
        string? customAndroid = null;
        string? customNdk = null;

        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            var settingsPath = Path.Combine(projectRoot, "ProjectSettings", "SDKPaths.json");
            if (File.Exists(settingsPath))
            {
                try
                {
                    var settings = JsonSerializer.Deserialize<SdkPathsSettings>(File.ReadAllText(settingsPath));
                    if (settings != null)
                    {
                        customVulkan = settings.VulkanSdkPath;
                        customWindows = settings.WindowsSdkPath;
                        customMsvc = settings.MsvcPath;
                        customAndroid = settings.AndroidSdkPath;
                        customNdk = settings.AndroidNdkPath;
                    }
                }
                catch { }
            }
        }

        var vulkan = ProbeVulkan(customVulkan);
        var windows = ProbeWindowsSdk(customWindows);
        var msvc = ProbeMsvc(customMsvc);
        var linux = ProbeLinux();
        var android = ProbeAndroid(customAndroid, customNdk);

        return new SdkEnvironmentReport(
            vulkan,
            windows,
            msvc,
            linux,
            android,
            RuntimeInformation.OSDescription,
            VulkanAngleConfigurator.Status.ToString(),
            DateTime.UtcNow
        );
    }

    public VulkanSdkInfo ProbeVulkan(string? customPath = null)
    {
        var tools = new List<string>();
        string? sdkPath = customPath;
        if (string.IsNullOrEmpty(sdkPath) || !Directory.Exists(sdkPath))
        {
            sdkPath = Environment.GetEnvironmentVariable("VULKAN_SDK");
        }

        if (string.IsNullOrEmpty(sdkPath) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const string rootDir = @"C:\VulkanSDK";
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

        string? version = null;
        string? includePath = null;
        string? libPath = null;
        bool hasHeaders = false;
        bool hasLibs = false;

        if (!string.IsNullOrEmpty(sdkPath) && Directory.Exists(sdkPath))
        {
            version = Path.GetFileName(sdkPath.TrimEnd(Path.DirectorySeparatorChar));
            var binDir = Path.Combine(sdkPath, "Bin");
            if (Directory.Exists(binDir))
            {
                string[] candidates = ["glslc.exe", "glslc", "vulkaninfo.exe", "vulkaninfo", "glslangValidator.exe", "glslangValidator", "spirv-opt.exe", "spirv-opt", "spirv-cross.exe", "spirv-cross"];
                foreach (var c in candidates)
                {
                    if (File.Exists(Path.Combine(binDir, c)))
                    {
                        if (!tools.Contains(c)) tools.Add(c);
                    }
                }
            }

            var inc = Path.Combine(sdkPath, "Include", "vulkan", "vulkan.h");
            if (File.Exists(inc))
            {
                includePath = Path.Combine(sdkPath, "Include");
                hasHeaders = true;
            }

            var lib = Path.Combine(sdkPath, "Lib", "vulkan-1.lib");
            if (File.Exists(lib))
            {
                libPath = Path.Combine(sdkPath, "Lib");
                hasLibs = true;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var soLib = Path.Combine(sdkPath, "lib", "libvulkan.so");
                if (File.Exists(soLib))
                {
                    libPath = Path.Combine(sdkPath, "lib");
                    hasLibs = true;
                }
            }
        }

        bool available = (!string.IsNullOrEmpty(sdkPath) && tools.Count > 0) ||
                         (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vulkan-1.dll")));

        string msg = available
            ? $"Vulkan SDK {version ?? "1.4+"} active with {tools.Count} tools. Headers: {(hasHeaders ? "Yes" : "No")}, Libs: {(hasLibs ? "Yes" : "No")}."
            : "Vulkan SDK not detected. Shaders will fall back to default WebGL 2.0 and CPU SIMD AO baking.";

        return new VulkanSdkInfo(available, version ?? "1.4+", sdkPath, includePath, libPath, tools, msg, hasHeaders, hasLibs);
    }

    public WindowsSdkInfo ProbeWindowsSdk(string? customKitsRoot = null)
    {
        var tools = new List<string>();
        string? kitsRoot = customKitsRoot;
        string? version = null;
        string? includePath = null;
        string? libPath = null;
        string? rcPath = null;
        string? signToolPath = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsSdkInfo(false, null, null, null, null, null, null, tools, "Not running on Windows host.");
        }

        if (string.IsNullOrEmpty(kitsRoot) || !Directory.Exists(kitsRoot))
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots");
                if (key != null)
                {
                    kitsRoot = key.GetValue("KitsRoot10") as string;
                }
            }
            catch { }

            if (string.IsNullOrEmpty(kitsRoot) || !Directory.Exists(kitsRoot))
            {
                string[] defaults = [
                    @"C:\Program Files (x86)\Windows Kits\10\",
                    @"C:\Program Files\Windows Kits\10\"
                ];
                foreach (var d in defaults)
                {
                    if (Directory.Exists(d))
                    {
                        kitsRoot = d;
                        break;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(kitsRoot) && Directory.Exists(kitsRoot))
        {
            var incDir = Path.Combine(kitsRoot, "Include");
            if (!Directory.Exists(incDir))
            {
                var altKits = @"C:\Program Files (x86)\Windows Kits\10";
                if (Directory.Exists(Path.Combine(altKits, "Include")))
                {
                    kitsRoot = altKits;
                    incDir = Path.Combine(kitsRoot, "Include");
                }
            }

            if (Directory.Exists(incDir))
            {
                var versions = Directory.GetDirectories(incDir);
                if (versions.Length > 0)
                {
                    Array.Sort(versions);
                    var latest = versions[^1];
                    version = Path.GetFileName(latest);
                    includePath = latest;

                    var binDir = Path.Combine(kitsRoot, "bin", version, "x64");
                    if (!Directory.Exists(binDir))
                    {
                        // Check if bin is in Program Files (x86)
                        var altBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits", "10", "bin", version, "x64");
                        if (Directory.Exists(altBin)) binDir = altBin;
                    }

                    if (Directory.Exists(binDir))
                    {
                        var rc = Path.Combine(binDir, "rc.exe");
                        if (File.Exists(rc))
                        {
                            rcPath = rc;
                            tools.Add("rc.exe (Resource Compiler)");
                        }
                        var signtool = Path.Combine(binDir, "signtool.exe");
                        if (File.Exists(signtool))
                        {
                            signToolPath = signtool;
                            tools.Add("signtool.exe (Code Signing)");
                        }
                    }

                    var ucrtLib = Path.Combine(kitsRoot, "Lib", version, "ucrt", "x64");
                    if (Directory.Exists(ucrtLib)) libPath = Path.Combine(kitsRoot, "Lib", version);
                }
            }
        }

        bool available = !string.IsNullOrEmpty(version) && tools.Count > 0;
        string msg = available
            ? $"Windows SDK {version} detected at {kitsRoot}. Tools: {string.Join(", ", tools)}."
            : "Windows 10/11 SDK not found. Install Windows SDK via Visual Studio Installer.";

        return new WindowsSdkInfo(available, version, kitsRoot, includePath, libPath, rcPath, signToolPath, tools, msg);
    }

    public MsvcInfo ProbeMsvc(string? customVsPath = null)
    {
        var tools = new List<string>();
        string? vsPath = customVsPath;
        string? version = null;
        string? clPath = null;
        string? linkPath = null;
        string? vcVarsPath = null;
        string? includePath = null;
        string? libPath = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new MsvcInfo(false, null, null, null, null, null, null, null, tools, "Not running on Windows host.");
        }

        if (string.IsNullOrEmpty(vsPath) || !Directory.Exists(vsPath))
        {
            var vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
            if (File.Exists(vswhere))
            {
                try
                {
                    var psi = new ProcessStartInfo(vswhere, "-latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath")
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
                string[] defaults = [
                    @"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools",
                    @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise",
                    @"C:\Program Files\Microsoft Visual Studio\2022\Professional",
                    @"C:\Program Files\Microsoft Visual Studio\2022\Community"
                ];
                foreach (var d in defaults)
                {
                    if (Directory.Exists(d)) { vsPath = d; break; }
                }
            }
        }

        if (!string.IsNullOrEmpty(vsPath) && Directory.Exists(vsPath))
        {
            var vcvars = Path.Combine(vsPath, "VC", "Auxiliary", "Build", "vcvars64.bat");
            if (File.Exists(vcvars))
            {
                vcVarsPath = vcvars;
                tools.Add("vcvars64.bat");
            }

            var msvcRoot = Path.Combine(vsPath, "VC", "Tools", "MSVC");
            if (Directory.Exists(msvcRoot))
            {
                var versions = Directory.GetDirectories(msvcRoot);
                if (versions.Length > 0)
                {
                    Array.Sort(versions);
                    var latest = versions[^1];
                    version = Path.GetFileName(latest);

                    var cl = Path.Combine(latest, "bin", "Hostx64", "x64", "cl.exe");
                    if (File.Exists(cl))
                    {
                        clPath = cl;
                        tools.Add("cl.exe (MSVC C++20)");
                    }

                    var link = Path.Combine(latest, "bin", "Hostx64", "x64", "link.exe");
                    if (File.Exists(link))
                    {
                        linkPath = link;
                        tools.Add("link.exe");
                    }

                    var nmake = Path.Combine(latest, "bin", "Hostx64", "x64", "nmake.exe");
                    if (File.Exists(nmake)) tools.Add("nmake.exe");

                    var inc = Path.Combine(latest, "include");
                    if (Directory.Exists(inc)) includePath = inc;

                    var lib = Path.Combine(latest, "lib", "x64");
                    if (Directory.Exists(lib)) libPath = lib;
                }
            }
        }

        bool available = !string.IsNullOrEmpty(clPath) && File.Exists(clPath);
        string msg = available
            ? $"MSVC compiler {version} ready for native C++20 acceleration (cl.exe, link.exe, vcvars64.bat)."
            : "MSVC compiler not found. Install 'Desktop development with C++' via Visual Studio Installer.";

        return new MsvcInfo(available, version, vsPath, clPath, linkPath, vcVarsPath, includePath, libPath, tools, msg);
    }

    public LinuxSdkInfo ProbeLinux()
    {
        var tools = new List<string>();
        string? version = null;
        string? gccPath = null;
        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        bool wslAvailable = false;
        string? wslDetails = null;

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
                    version = proc.StandardOutput.ReadLine()?.Trim();
                    proc.WaitForExit(1000);
                    tools.Add("g++");
                    gccPath = "/usr/bin/g++";
                }
            }
            catch { }

            string[] checks = ["gcc", "make", "ninja", "pkg-config"];
            foreach (var c in checks)
            {
                if (File.Exists($"/usr/bin/{c}")) tools.Add(c);
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Probe WSL2
            try
            {
                var psi = new ProcessStartInfo("wsl", "--status")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(1500);
                    if (proc.ExitCode == 0 && output.Contains("Default", StringComparison.OrdinalIgnoreCase))
                    {
                        wslAvailable = true;
                        wslDetails = "WSL2 Subsystem active.";
                        tools.Add("wsl.exe");
                    }
                }
            }
            catch { }
        }

        bool available = isLinux ? (tools.Count > 0) : wslAvailable;
        string msg = isLinux
            ? (available ? $"Linux GCC/G++ {version} active with {tools.Count} tools." : "Linux GCC/G++ not detected. Install build-essential.")
            : (wslAvailable ? "WSL2 available for Linux C++ compilation." : "Linux GCC/G++ toolchain inactive (Windows Host Active).");

        return new LinuxSdkInfo(available, version, gccPath, wslAvailable, wslDetails, tools, msg);
    }

    public AndroidSdkInfo ProbeAndroid(string? customSdkPath = null, string? customNdkPath = null)
    {
        var tools = new List<string>();
        string? sdkPath = customSdkPath;
        string? ndkPath = customNdkPath;
        string? adbPath = null;
        string? aapt2Path = null;
        string? apkSignerPath = null;
        string? clangPath = null;
        string? cliVersion = null;

        if (string.IsNullOrEmpty(sdkPath) || !Directory.Exists(sdkPath))
        {
            sdkPath = Environment.GetEnvironmentVariable("ANDROID_HOME") ?? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
            if (string.IsNullOrEmpty(sdkPath) || !Directory.Exists(sdkPath))
            {
                var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk");
                if (Directory.Exists(local)) sdkPath = local;
            }
        }

        // Android CLI detection
        var localCli = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "AndroidCLI", "android.exe");
        if (File.Exists(localCli))
        {
            tools.Add("android.exe (Android CLI)");
            cliVersion = "1.0.16500706";
        }
        else
        {
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
            // Probe platform-tools/adb
            var adb = Path.Combine(sdkPath, "platform-tools", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "adb.exe" : "adb");
            if (File.Exists(adb))
            {
                adbPath = adb;
                tools.Add("adb");
            }

            // Probe build-tools
            var buildToolsDir = Path.Combine(sdkPath, "build-tools");
            if (Directory.Exists(buildToolsDir))
            {
                var bVersions = Directory.GetDirectories(buildToolsDir);
                if (bVersions.Length > 0)
                {
                    Array.Sort(bVersions);
                    var latestBuildTools = bVersions[^1];
                    var aapt2 = Path.Combine(latestBuildTools, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "aapt2.exe" : "aapt2");
                    if (File.Exists(aapt2))
                    {
                        aapt2Path = aapt2;
                        tools.Add("aapt2");
                    }
                    var apksigner = Path.Combine(latestBuildTools, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "apksigner.bat" : "apksigner");
                    if (File.Exists(apksigner))
                    {
                        apkSignerPath = apksigner;
                        tools.Add("apksigner");
                    }
                }
            }

            // Probe NDK
            if (string.IsNullOrEmpty(ndkPath) || !Directory.Exists(ndkPath))
            {
                var ndkDir = Path.Combine(sdkPath, "ndk");
                if (Directory.Exists(ndkDir))
                {
                    var nVersions = Directory.GetDirectories(ndkDir);
                    if (nVersions.Length > 0)
                    {
                        Array.Sort(nVersions);
                        ndkPath = nVersions[^1];
                    }
                }
            }

            if (!string.IsNullOrEmpty(ndkPath) && Directory.Exists(ndkPath))
            {
                var llvmBin = Path.Combine(ndkPath, "toolchains", "llvm", "prebuilt", "windows-x86_64", "bin");
                if (!Directory.Exists(llvmBin))
                {
                    llvmBin = Path.Combine(ndkPath, "toolchains", "llvm", "prebuilt", "linux-x86_64", "bin");
                }

                if (Directory.Exists(llvmBin))
                {
                    var clang = Path.Combine(llvmBin, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "clang.exe" : "clang");
                    if (File.Exists(clang))
                    {
                        clangPath = clang;
                        tools.Add("clang (Android NDK)");
                    }
                }
            }
        }

        // Live ADB devices polling
        var connectedDevices = GetConnectedAdbDevices(adbPath);

        bool available = tools.Count > 0 || !string.IsNullOrEmpty(sdkPath);
        string msg = available
            ? $"Android SDK active at {sdkPath ?? "User Location"} with {tools.Count} tools. Connected ADB devices: {connectedDevices.Count}."
            : "Android SDK not configured. Set path in ProjectSettings/SDKPaths.json.";

        return new AndroidSdkInfo(
            available,
            sdkPath,
            ndkPath,
            adbPath,
            aapt2Path,
            apkSignerPath,
            clangPath,
            cliVersion,
            connectedDevices,
            tools,
            msg
        );
    }

    public List<AdbDeviceInfo> GetConnectedAdbDevices(string? adbPath = null)
    {
        var devices = new List<AdbDeviceInfo>();
        var adb = adbPath;
        if (string.IsNullOrEmpty(adb) || !File.Exists(adb))
        {
            var defaultAdb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe");
            if (File.Exists(defaultAdb)) adb = defaultAdb;
        }

        if (string.IsNullOrEmpty(adb) || !File.Exists(adb))
        {
            // Try in PATH
            adb = "adb";
        }

        try
        {
            var psi = new ProcessStartInfo(adb, "devices -l")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var lines = proc.StandardOutput.ReadToEnd().Split('\n');
                proc.WaitForExit(1500);

                // Format:
                // List of devices attached
                // emulator-5554          device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 device:emu64x transport_id:1
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("List of devices")) continue;

                    var match = Regex.Match(trimmed, @"^(\S+)\s+(\S+)(.*)$");
                    if (match.Success)
                    {
                        var serial = match.Groups[1].Value;
                        var state = match.Groups[2].Value;
                        var extra = match.Groups[3].Value;

                        string? model = null;
                        string? product = null;
                        string? device = null;

                        var modelMatch = Regex.Match(extra, @"model:(\S+)");
                        if (modelMatch.Success) model = modelMatch.Groups[1].Value;

                        var productMatch = Regex.Match(extra, @"product:(\S+)");
                        if (productMatch.Success) product = productMatch.Groups[1].Value;

                        var devMatch = Regex.Match(extra, @"device:(\S+)");
                        if (devMatch.Success) device = devMatch.Groups[1].Value;

                        devices.Add(new AdbDeviceInfo(serial, state, model, product, device));
                    }
                }
            }
        }
        catch { }

        return devices;
    }

    public void SaveCustomSdkPaths(string projectRoot, SdkPathsSettings paths)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) return;
        var dir = Path.Combine(projectRoot, "ProjectSettings");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var file = Path.Combine(dir, "SDKPaths.json");
        File.WriteAllText(file, JsonSerializer.Serialize(paths, JsonOpts));
    }
}
