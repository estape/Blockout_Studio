namespace UE5BlockoutStudio.Services.BuildPipeline;

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

public record CompilerDiagnostic(
    string File,
    int Line,
    int Column,
    string Severity,
    string? Code,
    string Message
);

public record CompilationResult(
    bool Success,
    int ExitCode,
    string? OutputDllPath,
    long DurationMs,
    List<CompilerDiagnostic> Diagnostics,
    List<string> RawLogs,
    string StatusMessage
);

public interface INativeCompilerService
{
    Task<CompilationResult> CompileModuleAsync(
        string projectRoot,
        CompilationOptions? options = null,
        Action<string>? onLogStream = null
    );
}
