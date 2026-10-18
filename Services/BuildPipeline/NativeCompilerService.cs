using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace UE5BlockoutStudio.Services.BuildPipeline;

public class NativeCompilerService : INativeCompilerService
{
    private readonly ISdkDetectorService _sdkDetector;

    public NativeCompilerService(ISdkDetectorService sdkDetector)
    {
        _sdkDetector = sdkDetector;
    }

    public async Task<CompilationResult> CompileModuleAsync(
        string projectRoot,
        CompilationOptions? options = null,
        Action<string>? onLogStream = null)
    {
        options ??= new CompilationOptions();
        var sw = Stopwatch.StartNew();
        var rawLogs = new List<string>();
        var diagnostics = new List<CompilerDiagnostic>();

        void Log(string line)
        {
            rawLogs.Add(line);
            onLogStream?.Invoke(line);
        }

        var pluginsDir = Path.Combine(projectRoot, "Assets", "NativePlugins");
        if (!Directory.Exists(pluginsDir)) Directory.CreateDirectory(pluginsDir);

        // Find or create source files
        var sourceFiles = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.CustomSourceFile) && File.Exists(options.CustomSourceFile))
        {
            sourceFiles.Add(options.CustomSourceFile);
        }
        else
        {
            var cppFiles = Directory.GetFiles(pluginsDir, "*.cpp", SearchOption.AllDirectories);
            if (cppFiles.Length > 0)
            {
                sourceFiles.AddRange(cppFiles);
            }
            else
            {
                // Scaffold starter plugin
                var starter = Path.Combine(pluginsDir, "GamePlugin.cpp");
                File.WriteAllText(starter, DefaultPluginSourceCode());
                sourceFiles.Add(starter);
                Log($"[INFO] Scaffolding starter C++20 plugin: {starter}");
            }
        }

        Log($"[INFO] Target Platform: {options.TargetPlatform} | C++ Standard: {options.CppStandard} | Optimization: {options.Optimization}");
        Log($"[INFO] Source Files ({sourceFiles.Count}): {string.Join(", ", sourceFiles.Select(Path.GetFileName))}");

        var sdks = _sdkDetector.DetectAllSdks(projectRoot);

        if (options.TargetPlatform == "Win64" || (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && options.TargetPlatform != "Linux64"))
        {
            return await CompileMsvcAsync(projectRoot, sourceFiles, options, sdks, sw, rawLogs, diagnostics, Log);
        }
        else if (options.TargetPlatform == "Linux64")
        {
            return await CompileGccAsync(projectRoot, sourceFiles, options, sdks, sw, rawLogs, diagnostics, Log);
        }
        else
        {
            Log($"[ERROR] Unsupported compilation target platform: {options.TargetPlatform}");
            return new CompilationResult(false, -1, null, sw.ElapsedMilliseconds, diagnostics, rawLogs, "Unsupported platform");
        }
    }

    private static async Task<CompilationResult> CompileMsvcAsync(
        string projectRoot,
        List<string> sourceFiles,
        CompilationOptions options,
        SdkEnvironmentReport sdks,
        Stopwatch sw,
        List<string> rawLogs,
        List<CompilerDiagnostic> diagnostics,
        Action<string> log)
    {
        var outDir = Path.Combine(projectRoot, "Binaries", "Win64");
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        var outputDll = Path.Combine(outDir, $"{options.OutputName}.dll");

        if (!sdks.Msvc.Available || string.IsNullOrEmpty(sdks.Msvc.VcVarsPath))
        {
            log("[WARN] MSVC compiler or vcvars64.bat not found on host system.");
            log("[INFO] Operating in fallback mode: Install Visual Studio C++ BuildTools to generate binary DLLs.");
            diagnostics.Add(new CompilerDiagnostic("BuildPipeline", 1, 1, "warning", "MSVC_NOT_FOUND", "MSVC C++ BuildTools not detected on system."));
            return new CompilationResult(false, 1, null, sw.ElapsedMilliseconds, diagnostics, rawLogs, "MSVC toolchain not found.");
        }

        log($"[INFO] Utilizing MSVC {sdks.Msvc.Version} via {sdks.Msvc.VcVarsPath}");

        // Build includes
        var incArgs = new List<string> { $"/I \"{Path.Combine(projectRoot, "Assets", "NativePlugins")}\"" };
        if (sdks.Vulkan.Available && !string.IsNullOrEmpty(sdks.Vulkan.IncludePath))
        {
            incArgs.Add($"/I \"{sdks.Vulkan.IncludePath}\"");
        }
        if (options.ExtraIncludes != null)
        {
            foreach (var inc in options.ExtraIncludes) incArgs.Add($"/I \"{inc}\"");
        }

        // Build lib paths & libs
        var libArgs = new List<string>();
        if (sdks.Vulkan.Available && !string.IsNullOrEmpty(sdks.Vulkan.LibPath))
        {
            libArgs.Add($"/LIBPATH:\"{sdks.Vulkan.LibPath}\"");
            libArgs.Add("vulkan-1.lib");
        }
        if (options.ExtraLibs != null)
        {
            foreach (var lib in options.ExtraLibs) libArgs.Add(lib);
        }

        var optFlag = options.Optimization.StartsWith("/") ? options.Optimization : $"/O2";
        var avxFlag = options.EnableAvx2 ? "/arch:AVX2" : "";
        var sourcesString = string.Join(" ", sourceFiles.Select(s => $"\"{s}\""));
        var includesString = string.Join(" ", incArgs);
        var libsString = string.Join(" ", libArgs);

        var clCommand = $"cl.exe /nologo /std:{options.CppStandard} {optFlag} /Oi /GL {avxFlag} /LD /EHsc /MD /DNDEBUG /DENGINENATIVE_EXPORTS {includesString} {sourcesString} /Fe:\"{outputDll}\" /link {libsString} /DLL";

        log($"[EXEC] {clCommand}");

        var batchFile = Path.Combine(outDir, $"build_{Guid.NewGuid():N}.cmd");
        var batchContent = $@"@echo off
call ""{sdks.Msvc.VcVarsPath}""
if errorlevel 1 exit /b %errorlevel%
{clCommand}
";
        await File.WriteAllTextAsync(batchFile, batchContent);

        var psi = new ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/c \"{batchFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = outDir
        };

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                log(e.Data);
                ParseMsvcDiagnostic(e.Data, diagnostics);
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                log($"[STDERR] {e.Data}");
                ParseMsvcDiagnostic(e.Data, diagnostics);
            }
        };

        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
        }
        finally
        {
            try { if (File.Exists(batchFile)) File.Delete(batchFile); } catch { }
        }

        bool success = proc.ExitCode == 0 && File.Exists(outputDll);
        string status = success
            ? $"Successfully compiled {options.OutputName}.dll in {sw.ElapsedMilliseconds} ms."
            : $"Compilation failed with exit code {proc.ExitCode}.";

        log(success ? $"[SUCCESS] {status}" : $"[FAILED] {status}");

        return new CompilationResult(success, proc.ExitCode, success ? outputDll : null, sw.ElapsedMilliseconds, diagnostics, rawLogs, status);
    }

    private static async Task<CompilationResult> CompileGccAsync(
        string projectRoot,
        List<string> sourceFiles,
        CompilationOptions options,
        SdkEnvironmentReport sdks,
        Stopwatch sw,
        List<string> rawLogs,
        List<CompilerDiagnostic> diagnostics,
        Action<string> log)
    {
        var outDir = Path.Combine(projectRoot, "Binaries", "Linux64");
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        var outputSo = Path.Combine(outDir, $"lib{options.OutputName}.so");
        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        if (!isLinux && !sdks.Linux.WslAvailable)
        {
            log("[WARN] Native Linux host or WSL2 not detected. Linux compilation requires g++ on Linux or WSL2.");
            diagnostics.Add(new CompilerDiagnostic("BuildPipeline", 1, 1, "warning", "GCC_NOT_FOUND", "Linux GCC/G++ not detected on system."));
            return new CompilationResult(false, 1, null, sw.ElapsedMilliseconds, diagnostics, rawLogs, "Linux GCC/G++ toolchain unavailable.");
        }

        var optFlag = options.Optimization.StartsWith("-") ? options.Optimization : "-O3";
        var sourcesString = string.Join(" ", sourceFiles.Select(s => $"\"{s}\""));
        var incArgs = $"-I \"{Path.Combine(projectRoot, "Assets", "NativePlugins")}\"";
        if (sdks.Vulkan.Available && !string.IsNullOrEmpty(sdks.Vulkan.IncludePath))
        {
            incArgs += $" -I \"{sdks.Vulkan.IncludePath}\"";
        }

        var gccCommand = $"g++ {optFlag} -march=x86-64-v3 -flto -fPIC -shared -std={options.CppStandard} -DNDEBUG {incArgs} {sourcesString} -o \"{outputSo}\" -lvulkan";
        log($"[EXEC] {gccCommand}");

        ProcessStartInfo psi;
        if (isLinux)
        {
            psi = new ProcessStartInfo("bash", $"-c \"{gccCommand}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = outDir
            };
        }
        else
        {
            psi = new ProcessStartInfo("wsl", gccCommand)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = outDir
            };
        }

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                log(e.Data);
                ParseGccDiagnostic(e.Data, diagnostics);
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                log($"[STDERR] {e.Data}");
                ParseGccDiagnostic(e.Data, diagnostics);
            }
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();

        bool success = proc.ExitCode == 0 && File.Exists(outputSo);
        string status = success
            ? $"Successfully compiled lib{options.OutputName}.so in {sw.ElapsedMilliseconds} ms."
            : $"Linux compilation exited with code {proc.ExitCode}.";

        log(success ? $"[SUCCESS] {status}" : $"[FAILED] {status}");
        return new CompilationResult(success, proc.ExitCode, success ? outputSo : null, sw.ElapsedMilliseconds, diagnostics, rawLogs, status);
    }

    private static void ParseMsvcDiagnostic(string line, List<CompilerDiagnostic> diagnostics)
    {
        // Example: C:\Path\Source.cpp(14,5): error C2143: syntax error : missing ';' before '}'
        var match = Regex.Match(line, @"^(?<file>[^(]+)\((?<line>\d+)(?:,(?<col>\d+))?\):\s*(?<severity>error|warning|fatal error)\s*(?<code>[A-Z0-9]+)?:\s*(?<msg>.*)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            diagnostics.Add(new CompilerDiagnostic(
                match.Groups["file"].Value.Trim(),
                int.TryParse(match.Groups["line"].Value, out var l) ? l : 1,
                int.TryParse(match.Groups["col"].Value, out var c) ? c : 1,
                match.Groups["severity"].Value.ToLowerInvariant(),
                match.Groups["code"].Success ? match.Groups["code"].Value : null,
                match.Groups["msg"].Value.Trim()
            ));
            return;
        }

        // Linker error example: GamePlugin.obj : error LNK2001: unresolved external symbol
        var lnkMatch = Regex.Match(line, @"^(?<file>[^:]+)\s*:\s*(?<severity>error|fatal error|warning)\s*(?<code>LNK\d+):\s*(?<msg>.*)$", RegexOptions.IgnoreCase);
        if (lnkMatch.Success)
        {
            diagnostics.Add(new CompilerDiagnostic(
                lnkMatch.Groups["file"].Value.Trim(),
                1,
                1,
                lnkMatch.Groups["severity"].Value.ToLowerInvariant(),
                lnkMatch.Groups["code"].Value,
                lnkMatch.Groups["msg"].Value.Trim()
            ));
        }
    }

    private static void ParseGccDiagnostic(string line, List<CompilerDiagnostic> diagnostics)
    {
        // Example: /path/file.cpp:12:5: error: expected ';' before '}'
        var match = Regex.Match(line, @"^(?<file>[^:]+):(?<line>\d+):(?<col>\d+):\s*(?<severity>error|warning|note):\s*(?<msg>.*)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            diagnostics.Add(new CompilerDiagnostic(
                match.Groups["file"].Value.Trim(),
                int.TryParse(match.Groups["line"].Value, out var l) ? l : 1,
                int.TryParse(match.Groups["col"].Value, out var c) ? c : 1,
                match.Groups["severity"].Value.ToLowerInvariant(),
                null,
                match.Groups["msg"].Value.Trim()
            ));
        }
    }

    public static string DefaultPluginSourceCode()
    {
        return @"// ==============================================================================
// UE5BlockoutStudio Native C++20 Plugin
// Compiled with MSVC (Windows) / GCC (Linux) with AVX2 & C++20 Acceleration
// ==============================================================================

#ifdef _WIN32
  #define ENGINE_EXPORT extern ""C"" __declspec(dllexport)
#else
  #define ENGINE_EXPORT extern ""C"" __attribute__((visibility(""default"")))
#endif

#include <cmath>
#include <cstdint>
#include <utility>

static inline void SwapFloat(float& a, float& b) {
    float tmp = a;
    a = b;
    b = tmp;
}

// High-speed SIMD vector length computation in UE5 centimeters
ENGINE_EXPORT float ComputeVectorLength(float x, float y, float z) {
    return std::sqrt(x * x + y * y + z * z);
}

// Ray-box intersection routine for fast physics queries
ENGINE_EXPORT int IntersectRayBox(
    float roX, float roY, float roZ,
    float rdX, float rdY, float rdZ,
    float minX, float minY, float minZ,
    float maxX, float maxY, float maxZ,
    float* outT)
{
    float tmin = (minX - roX) / (rdX != 0.0f ? rdX : 1e-6f);
    float tmax = (maxX - roX) / (rdX != 0.0f ? rdX : 1e-6f);
    if (tmin > tmax) SwapFloat(tmin, tmax);

    float tymin = (minY - roY) / (rdY != 0.0f ? rdY : 1e-6f);
    float tymax = (maxY - roY) / (rdY != 0.0f ? rdY : 1e-6f);
    if (tymin > tymax) SwapFloat(tymin, tymax);

    if ((tmin > tymax) || (tymin > tmax)) return 0;
    if (tymin > tmin) tmin = tymin;
    if (tymax < tmax) tmax = tymax;

    float tzmin = (minZ - roZ) / (rdZ != 0.0f ? rdZ : 1e-6f);
    float tzmax = (maxZ - roZ) / (rdZ != 0.0f ? rdZ : 1e-6f);
    if (tzmin > tzmax) SwapFloat(tzmin, tzmax);

    if ((tmin > tzmax) || (tzmin > tmax)) return 0;
    if (tzmin > tmin) tmin = tzmin;

    if (tmin < 0.0f) return 0;
    if (outT) *outT = tmin;
    return 1;
}

// Plugin version check
ENGINE_EXPORT uint32_t GetPluginVersion() {
    return 20261015;
}
";
    }
}
