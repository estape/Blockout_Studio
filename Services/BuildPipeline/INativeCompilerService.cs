namespace BlockoutStudio.Services.BuildPipeline;

/// <summary>
/// Options customizing the native C++ compilation pass.
/// </summary>
public record CompilationOptions(
    string TargetPlatform = "Win64",
    string Optimization = "/O2",
    bool EnableAvx2 = true,
    string CppStandard = "c++20",
    string? CustomSourceFile = null,
    string OutputName = "GameNative",
    List<string>? ExtraIncludes = null,
    List<string>? ExtraLibs = null
);

/// <summary>
/// Structured compiler error or warning diagnostic message.
/// </summary>
public record CompilerDiagnostic(
    string File,
    int Line,
    int Column,
    string Severity,
    string? Code,
    string Message
);

/// <summary>
/// Output payload describing the result and timing of a native module build pass.
/// </summary>
public record CompilationResult(
    bool Success,
    int ExitCode,
    string? OutputDllPath,
    long DurationMs,
    List<CompilerDiagnostic> Diagnostics,
    List<string> RawLogs,
    string StatusMessage
);

/// <summary>
/// Service interface for invoking platform C++ compilers (MSVC/GCC) to produce native dynamic libraries.
/// </summary>
public interface INativeCompilerService
{
    /// <summary>
    /// Compiles C++ plugin sources in Assets/NativePlugins/ into a shared library (.dll or .so).
    /// </summary>
    /// <param name="projectRoot">Active project root path.</param>
    /// <param name="options">Compiler and optimization flags.</param>
    /// <param name="onLogStream">Optional real-time log output callback.</param>
    /// <returns>Completed <see cref="CompilationResult"/>.</returns>
    Task<CompilationResult> CompileModuleAsync(
        string projectRoot,
        CompilationOptions? options = null,
        Action<string>? onLogStream = null
    );
}
