using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;

namespace BlockoutStudio.Controllers.Services.Blueprints.Transpiler;

/// <summary>
/// Service encapsulating Roslyn's workspace code formatting using AdhocWorkspace and Formatter.Format,
/// guaranteeing clean, production-grade C# formatting for generated gameplay classes.
/// </summary>
public static class RoslynCodeFormatter
{
    /// <summary>
    /// Formats the specified Roslyn SyntaxNode into standard C# code using Formatter.Format.
    /// Falls back safely to NormalizeWhitespace if an unexpected exception occurs.
    /// </summary>
    public static string FormatCompilationUnit(SyntaxNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        try
        {
            using var workspace = new AdhocWorkspace();
            var formatted = Formatter.Format(root, workspace);
            return formatted.ToFullString();
        }
        catch
        {
            return root.NormalizeWhitespace().ToFullString();
        }
    }
}

