using Microsoft.CodeAnalysis.CSharp.Syntax;
using BlockoutStudio.Models.Blueprints.Format;

namespace BlockoutStudio.Controllers.Services.Blueprints.Transpiler;

/// <summary>
/// Service that generates syntactically valid C# source code (.g.cs) from a .bgraph GraphDocument
/// using Roslyn's SyntaxFactory AST constructs.
/// </summary>
public interface IGraphCodeGeneratorService
{
    CompilationUnitSyntax GenerateSyntaxTree(GraphDocument document);
    string GenerateSourceCode(GraphDocument document);
    Task<string> GenerateAndSaveAsync(string projectRoot, GraphDocument document);
}

