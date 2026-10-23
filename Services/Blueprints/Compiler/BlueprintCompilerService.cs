using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using BlockoutStudio.Services.Blueprints.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BlockoutStudio.Services.Blueprints.Compiler;

/// <summary>
/// Compiles transpiled C# Blueprint source code directly into in-memory .NET 10 assemblies
/// using Microsoft.CodeAnalysis.CSharp (Roslyn).
/// </summary>
public class BlueprintCompilerService : IBlueprintCompilerService
{
    private readonly List<MetadataReference> _defaultReferences;

    public BlueprintCompilerService()
    {
        _defaultReferences = BuildDefaultReferences();
    }

    public BlueprintCompilationResult CompileSource(string csharpCode, string? assemblyName = null)
    {
        var sw = Stopwatch.StartNew();
        var asmName = assemblyName ?? $"BlueprintAssembly_{Guid.NewGuid():N}";

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpCode);

            var compilation = CSharpCompilation.Create(
                asmName,
                syntaxTrees: [syntaxTree],
                references: _defaultReferences,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    warningLevel: 4,
                    concurrentBuild: true));

            using var peStream = new MemoryStream();
            var emitResult = compilation.Emit(peStream);

            sw.Stop();

            var diagnostics = emitResult.Diagnostics
                .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
                .Select(d =>
                {
                    var lineSpan = d.Location.GetLineSpan();
                    return new BlueprintDiagnostic(
                        Severity: d.Severity.ToString(),
                        Message: d.GetMessage(),
                        Line: lineSpan.StartLinePosition.Line + 1,
                        Column: lineSpan.StartLinePosition.Character + 1,
                        Id: d.Id);
                })
                .ToList();

            if (!emitResult.Success)
            {
                var firstError = diagnostics.FirstOrDefault(d => d.Severity == "Error")?.Message ?? "Compilation failed";
                return new BlueprintCompilationResult
                {
                    Success = false,
                    AssemblyName = asmName,
                    Diagnostics = diagnostics,
                    CompilationTimeMs = sw.ElapsedMilliseconds,
                    ErrorMessage = firstError
                };
            }

            peStream.Seek(0, SeekOrigin.Begin);
            var loadContext = new AssemblyLoadContext(asmName, isCollectible: true);
            var assembly = loadContext.LoadFromStream(peStream);

            return new BlueprintCompilationResult
            {
                Success = true,
                AssemblyName = asmName,
                CompiledAssembly = assembly,
                Diagnostics = diagnostics,
                CompilationTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new BlueprintCompilationResult
            {
                Success = false,
                AssemblyName = asmName,
                CompilationTimeMs = sw.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                Diagnostics = [new BlueprintDiagnostic("Error", ex.Message, 1, 1, "ROS001")]
            };
        }
    }

    private static List<MetadataReference> BuildDefaultReferences()
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Core required types
        void AddType(Type t)
        {
            if (!string.IsNullOrEmpty(t.Assembly.Location))
                refs.Add(t.Assembly.Location);
        }

        AddType(typeof(object));
        AddType(typeof(System.Numerics.Vector3));
        AddType(typeof(ActorBlueprint));
        AddType(typeof(Console));

        // Trusted platform assemblies from host runtime
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            var paths = tpa.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                var fileName = Path.GetFileName(path);
                if (fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
                {
                    refs.Add(path);
                }
            }
        }

        return refs
            .Where(File.Exists)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    }
}
