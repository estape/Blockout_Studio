using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using BlockoutStudio.Models.Blueprints.Format;

namespace BlockoutStudio.Controllers.Services.Blueprints.Transpiler;

/// <summary>
/// Roslyn SyntaxFactory transpiler service emitting strongly-typed, formatted C# source code (.g.cs)
/// from visual script GraphDocuments.
/// </summary>
public class RoslynGraphCodeGeneratorService : IGraphCodeGeneratorService
{
    public CompilationUnitSyntax GenerateSyntaxTree(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var className = SanitizeClassName(document.Name);
        var baseClassName = DetermineBaseClass(document.TargetActorClass);

        var pureEmitter = new RoslynPureExpressionEmitter(document);
        var execEmitter = new RoslynExecutionChainEmitter(document, pureEmitter);

        // 1. Usings
        var usings = new[]
        {
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Numerics")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.CodeDom.Compiler")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("BlockoutStudio.Services.Blueprints.Runtime")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("BlockoutStudio.Models.Blueprints"))
        };

        // 2. Class Attributes: [GeneratedCode("BlockoutStudio.GraphCodeGenerator", "1.0.0")]
        var generatedCodeAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName("GeneratedCode"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("BlockoutStudio.GraphCodeGenerator"))),
                    SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("1.0.0")))
                })));

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(generatedCodeAttribute));

        // 3. Class Declaration
        var classDeclaration = SyntaxFactory.ClassDeclaration(className)
            .WithAttributeLists(SyntaxFactory.SingletonList(attributeList))
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .WithBaseList(SyntaxFactory.BaseList(
                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                    SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseClassName)))));

        // 4. Discover Event Nodes & Generate Methods
        var eventNodes = document.Nodes.Where(n => n.NodeKind == GraphNodeKind.Event).ToList();
        var memberMethods = new List<MemberDeclarationSyntax>();

        foreach (var evt in eventNodes)
        {
            var method = GenerateEventMethod(evt, execEmitter);
            if (method != null)
            {
                memberMethods.Add(method);
            }
        }

        classDeclaration = classDeclaration.WithMembers(SyntaxFactory.List(memberMethods));

        // 5. File-scoped Namespace: namespace BlockoutStudio.Generated;
        var namespaceDeclaration = SyntaxFactory.FileScopedNamespaceDeclaration(
            SyntaxFactory.ParseName("BlockoutStudio.Generated"))
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(classDeclaration));

        // 6. Compilation Unit
        var compilationUnit = SyntaxFactory.CompilationUnit()
            .WithUsings(SyntaxFactory.List(usings))
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(namespaceDeclaration));

        return compilationUnit.NormalizeWhitespace();
    }

    public string GenerateSourceCode(GraphDocument document)
    {
        var syntaxTree = GenerateSyntaxTree(document);
        return syntaxTree.ToFullString();
    }

    public async Task<string> GenerateAndSaveAsync(string projectRoot, GraphDocument document)
    {
        var code = GenerateSourceCode(document);
        var genDir = Path.Combine(projectRoot, "Assets", "Scripts", "Generated");
        if (!Directory.Exists(genDir))
        {
            Directory.CreateDirectory(genDir);
        }

        var fileName = $"{SanitizeClassName(document.Name)}.g.cs";
        var fullPath = Path.Combine(genDir, fileName);
        await File.WriteAllTextAsync(fullPath, code);
        return fullPath;
    }

    private static MethodDeclarationSyntax? GenerateEventMethod(GraphNode evtNode, RoslynExecutionChainEmitter execEmitter)
    {
        var methodId = evtNode.TargetMethodIdentifier;
        var outExecPin = evtNode.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Output && p.ValueType == GraphPinType.Exec);
        if (outExecPin == null) return null;

        var statements = execEmitter.EmitExecutionChain(evtNode.Id, outExecPin.Id);

        if (methodId.EndsWith("OnBeginPlay", StringComparison.OrdinalIgnoreCase))
        {
            var bodyStatements = new List<StatementSyntax>
            {
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.BaseExpression(),
                            SyntaxFactory.IdentifierName("OnBeginPlay"))))
            };
            bodyStatements.AddRange(statements);

            return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "OnBeginPlay")
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword)))
                .WithBody(SyntaxFactory.Block(bodyStatements));
        }

        if (methodId.EndsWith("OnTick", StringComparison.OrdinalIgnoreCase))
        {
            var bodyStatements = new List<StatementSyntax>
            {
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.BaseExpression(),
                            SyntaxFactory.IdentifierName("OnTick")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("deltaTime"))))))
            };
            bodyStatements.AddRange(statements);

            return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "OnTick")
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("deltaTime"))
                            .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.FloatKeyword))))))
                .WithBody(SyntaxFactory.Block(bodyStatements));
        }

        if (methodId.EndsWith("OnCollisionEnter", StringComparison.OrdinalIgnoreCase))
        {
            var bodyStatements = new List<StatementSyntax>
            {
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.BaseExpression(),
                            SyntaxFactory.IdentifierName("OnCollisionEnter")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("other"))))))
            };
            bodyStatements.AddRange(statements);

            return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "OnCollisionEnter")
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("other"))
                            .WithType(SyntaxFactory.ParseTypeName("Actor")))))
                .WithBody(SyntaxFactory.Block(bodyStatements));
        }

        // Custom Event
        var cleanEventName = methodId.Contains('.') ? methodId[(methodId.LastIndexOf('.') + 1)..] : methodId;
        return SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            cleanEventName)
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithBody(SyntaxFactory.Block(statements));
    }

    private static string DetermineBaseClass(string? targetActorClass)
    {
        if (string.IsNullOrWhiteSpace(targetActorClass) ||
            targetActorClass.Equals("ActorComponent", StringComparison.OrdinalIgnoreCase))
        {
            return "ActorComponent";
        }

        if (targetActorClass.Equals("ActorBlueprint", StringComparison.OrdinalIgnoreCase) ||
            targetActorClass.Equals("Actor", StringComparison.OrdinalIgnoreCase))
        {
            return "ActorBlueprint";
        }

        if (targetActorClass.Equals("LevelBlueprint", StringComparison.OrdinalIgnoreCase) ||
            targetActorClass.Equals("Level", StringComparison.OrdinalIgnoreCase))
        {
            return "LevelBlueprint";
        }

        return targetActorClass;
    }

    private static string SanitizeClassName(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrEmpty(cleaned)) cleaned = "GeneratedBlueprintClass";
        if (char.IsDigit(cleaned[0])) cleaned = "_" + cleaned;
        return cleaned;
    }
}
