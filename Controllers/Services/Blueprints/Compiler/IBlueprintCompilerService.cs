using BlockoutStudio.Models.Blueprints;

namespace BlockoutStudio.Services.Blueprints.Compiler;

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

