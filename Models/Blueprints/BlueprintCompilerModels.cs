using System.Reflection;

namespace BlockoutStudio.Models.Blueprints;

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


