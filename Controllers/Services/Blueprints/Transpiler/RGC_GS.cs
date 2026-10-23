using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using BlockoutStudio.Models.Blueprints.Format;

namespace BlockoutStudio.Controllers.Services.Blueprints.Transpiler;

/// <summary>
/// Roslyn SyntaxFactory transpiler service emitting strongly-typed, formatted C# source code (.g.cs)
/// from visual script GraphDocuments within the MVC architecture.
/// Partitions generated classes into Model state (properties) and Controller behaviors (event methods).
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
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("BlockoutStudio.Services.Blueprints.Runtime"))
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

        // 3. Class Declaration: public sealed partial class <Name> : <BaseClass>
        var classDeclaration = SyntaxFactory.ClassDeclaration(className)
            .WithAttributeLists(SyntaxFactory.SingletonList(attributeList))
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.SealedKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .WithBaseList(SyntaxFactory.BaseList(
                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                    SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseClassName)))));

        var members = new List<MemberDeclarationSyntax>();

        // 4. [MODEL STATE] Generate Strongly Typed Auto-Properties from Graph Variables
        foreach (var variable in document.Variables)
        {
            var prop = GenerateVariableProperty(variable);
            if (prop != null)
            {
                members.Add(prop);
            }
        }

        // 5. [CONTROLLER LOGIC] Discover Event Nodes & Generate Lifecycle Methods
        var eventNodes = document.Nodes.Where(n => n.NodeType == GraphNodeType.Event).ToList();

        foreach (var evt in eventNodes)
        {
            var method = GenerateEventMethod(evt, execEmitter);
            if (method != null)
            {
                members.Add(method);
            }
        }

        classDeclaration = classDeclaration.WithMembers(SyntaxFactory.List(members));

        // 6. File-scoped Namespace: namespace BlockoutStudio.Generated;
        var namespaceDeclaration = SyntaxFactory.FileScopedNamespaceDeclaration(
            SyntaxFactory.ParseName("BlockoutStudio.Generated"))
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(classDeclaration));

        // 7. Compilation Unit
        var compilationUnit = SyntaxFactory.CompilationUnit()
            .WithUsings(SyntaxFactory.List(usings))
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(namespaceDeclaration));

        return compilationUnit.NormalizeWhitespace();
    }

    public string GenerateSourceCode(GraphDocument document)
    {
        var syntaxTree = GenerateSyntaxTree(document);
        return RoslynCodeFormatter.FormatCompilationUnit(syntaxTree);
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

    private static PropertyDeclarationSyntax? GenerateVariableProperty(GraphVariable variable)
    {
        var typeName = GetTypeNameForPinType(variable.Type);
        var propName = RoslynPureExpressionEmitter.SanitizeIdentifier(variable.Name);

        var dummyPin = new GraphPin("vp", variable.Name, GraphPinDirection.Input, variable.Type);
        var initExpr = RoslynPureExpressionEmitter.EmitDefaultLiteral(dummyPin, variable.DefaultValue);

        var accessors = SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
        {
            SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
        }));

        return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(typeName), propName)
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(variable.IsPublic ? SyntaxKind.PublicKeyword : SyntaxKind.InternalKeyword)))
            .WithAccessorList(accessors)
            .WithInitializer(SyntaxFactory.EqualsValueClause(initExpr))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
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

    private static string GetTypeNameForPinType(GraphPinType type) => type switch
    {
        GraphPinType.Float => "float",
        GraphPinType.Integer => "int",
        GraphPinType.Boolean => "bool",
        GraphPinType.String => "string",
        GraphPinType.Vector3 => "Vector3",
        GraphPinType.ActorRef => "Actor",
        _ => "object"
    };

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

    public static string SanitizeClassName(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrEmpty(cleaned)) cleaned = "GeneratedBlueprintClass";
        if (char.IsDigit(cleaned[0])) cleaned = "_" + cleaned;
        return cleaned;
    }
}

/// <summary>
/// Architectural class alias for RoslynGraphCodeGeneratorService as requested by user conventions.
/// </summary>
public class RGC_GS : RoslynGraphCodeGeneratorService
{
}
