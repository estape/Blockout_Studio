using BlockoutStudio.Models.Blueprints;

namespace BlockoutStudio.Services.Blueprints.Transpiler;

/// <summary>
/// Service interface for transpiling visual Blueprint graphs into valid C# source code.
/// </summary>
public interface IBlueprintTranspiler
{
    /// <summary>
    /// Transpiles a Blueprint visual graph document into strongly-typed C# 13 source code.
    /// </summary>
    /// <param name="graph">The graph data model.</param>
    /// <returns>Transpiled C# source code string.</returns>
    string TranspileToCSharp(BlueprintGraph graph);
}
