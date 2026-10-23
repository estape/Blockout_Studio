using System.Reflection;

namespace BlockoutStudio.Services.Blueprints.Compiler;

/// <summary>
/// Compiler diagnostic report item (error or warning).
/// </summary>
public record BlueprintDiagnostic(
    string Severity,
    string Message,
    int Line,
    int Column,
    string Id);

/// <summary>
/// Result of a Roslyn in-memory compilation.
/// </summary>
public class BlueprintCompilationResult
{
    public bool Success { get; set; }
    public string AssemblyName { get; set; } = string.Empty;
    public Assembly? CompiledAssembly { get; set; }
    public List<BlueprintDiagnostic> Diagnostics { get; set; } = [];
    public long CompilationTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Service interface for compiling C# Blueprint source code using Roslyn.
/// </summary>
public interface IBlueprintCompilerService
{
    /// <summary>
    /// Compiles C# source code in-memory into an executable .NET Assembly.
    /// </summary>
    /// <param name="csharpCode">C# source code.</param>
    /// <param name="assemblyName">Optional assembly name.</param>
    /// <returns>Compilation result with emitted assembly or diagnostics.</returns>
    BlueprintCompilationResult CompileSource(string csharpCode, string? assemblyName = null);
}
