using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using BlockoutStudio.Models.Blueprints.Format;

namespace BlockoutStudio.Controllers.Services.Blueprints.Transpiler;

/// <summary>
/// Recursively resolves input pins connected to pure function nodes, variable getters, and default values
/// into nested Roslyn ExpressionSyntax trees with zero runtime reflection or boxing.
/// </summary>
public class RoslynPureExpressionEmitter
{
    private readonly GraphDocument _document;
    private readonly Dictionary<string, GraphNode> _nodeMap;
    private readonly Dictionary<string, GraphLink> _incomingDataLinks;

    public RoslynPureExpressionEmitter(GraphDocument document)
    {
        _document = document;
        _nodeMap = document.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        _incomingDataLinks = new Dictionary<string, GraphLink>(StringComparer.Ordinal);

        foreach (var link in document.Links)
        {
            if (_nodeMap.TryGetValue(link.SourceNodeId, out var srcNode) &&
                _nodeMap.TryGetValue(link.TargetNodeId, out var tgtNode))
            {
                var srcPin = srcNode.FindPin(link.SourcePinId);
                var tgtPin = tgtNode.FindPin(link.TargetPinId);
                if (srcPin != null && tgtPin != null && srcPin.ValueType != GraphPinType.Exec && tgtPin.ValueType != GraphPinType.Exec)
                {
                    _incomingDataLinks[$"{tgtNode.Id}:{tgtPin.Id}"] = link;
                }
            }
        }
    }

    /// <summary>
    /// Resolves the specified input pin into an ExpressionSyntax tree.
    /// </summary>
    public ExpressionSyntax ResolveExpression(GraphNode node, GraphPin inputPin, HashSet<string>? recursionGuard = null)
    {
        recursionGuard ??= new HashSet<string>(StringComparer.Ordinal);
        var guardKey = $"{node.Id}:{inputPin.Id}";

        if (recursionGuard.Contains(guardKey))
        {
            // Circular dependency fallback: return default literal to prevent stack overflow
            return EmitDefaultLiteral(inputPin, null);
        }

        recursionGuard.Add(guardKey);

        try
        {
            // 1. Check if an incoming data wire exists for this pin
            if (_incomingDataLinks.TryGetValue(guardKey, out var link) &&
                _nodeMap.TryGetValue(link.SourceNodeId, out var srcNode))
            {
                var srcPin = srcNode.FindPin(link.SourcePinId);
                if (srcPin != null)
                {
                    return ResolveSourceOutputExpression(srcNode, srcPin, recursionGuard);
                }
            }

            // 2. No wire connected: emit default value literal
            string? defVal = null;
            if (node.DefaultPinValues.TryGetValue(inputPin.Name, out var v) ||
                node.DefaultPinValues.TryGetValue(inputPin.Id, out v))
            {
                defVal = v;
            }

            return EmitDefaultLiteral(inputPin, defVal);
        }
        finally
        {
            recursionGuard.Remove(guardKey);
        }
    }

    private ExpressionSyntax ResolveSourceOutputExpression(GraphNode srcNode, GraphPin srcPin, HashSet<string> recursionGuard)
    {
        // VariableGet: resolves to this.VariableName (Model property access)
        if (srcNode.NodeType == GraphNodeType.VariableGet)
        {
            return ResolveVariableGetNode(srcNode);
        }

        // Event output pin (e.g. OnTick -> DeltaSeconds / deltaTime)
        if (srcNode.NodeType == GraphNodeType.Event)
        {
            if (srcPin.Name.Equals("DeltaSeconds", StringComparison.OrdinalIgnoreCase) ||
                srcPin.Name.Equals("deltaTime", StringComparison.OrdinalIgnoreCase) ||
                srcPin.Name.Equals("dt", StringComparison.OrdinalIgnoreCase))
            {
                return SyntaxFactory.IdentifierName("deltaTime");
            }

            if (srcPin.Name.Equals("Other", StringComparison.OrdinalIgnoreCase) ||
                srcPin.Name.Equals("other", StringComparison.OrdinalIgnoreCase))
            {
                return SyntaxFactory.IdentifierName("other");
            }

            return SyntaxFactory.IdentifierName(SanitizeIdentifier(srcPin.Name));
        }

        // Pure function node
        if (srcNode.NodeType == GraphNodeType.PureFunction)
        {
            return ResolvePureFunctionNode(srcNode, srcPin, recursionGuard);
        }

        // Impure function node output (e.g. local variable from previous impure call)
        var varName = $"var_{srcNode.Id}_{SanitizeIdentifier(srcPin.Name)}";
        return SyntaxFactory.IdentifierName(varName);
    }

    public ExpressionSyntax ResolveVariableGetNode(GraphNode varGetNode)
    {
        // Extract variable identifier from TargetMethodIdentifier or default pin
        var varName = varGetNode.TargetMethodIdentifier;
        if (varName.StartsWith("Variable.Get.", StringComparison.OrdinalIgnoreCase))
        {
            varName = varName["Variable.Get.".Length..];
        }
        else if (varName.StartsWith("Get.", StringComparison.OrdinalIgnoreCase))
        {
            varName = varName["Get.".Length..];
        }

        if (string.IsNullOrWhiteSpace(varName) && varGetNode.DefaultPinValues.TryGetValue("VariableName", out var nameVal))
        {
            varName = nameVal;
        }

        if (string.IsNullOrWhiteSpace(varName))
        {
            varName = "Variable";
        }

        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ThisExpression(),
            SyntaxFactory.IdentifierName(SanitizeIdentifier(varName)));
    }

    private ExpressionSyntax ResolvePureFunctionNode(GraphNode pureNode, GraphPin outPin, HashSet<string> recursionGuard)
    {
        var methodId = pureNode.TargetMethodIdentifier;

        // Constant literal nodes (e.g. MathLibrary.FloatConstant)
        if (methodId.EndsWith("Constant", StringComparison.OrdinalIgnoreCase))
        {
            var valPin = pureNode.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Output && p.ValueType != GraphPinType.Exec);
            string? litVal = null;
            if (pureNode.DefaultPinValues.TryGetValue("Value", out var dv) ||
                pureNode.DefaultPinValues.TryGetValue("val", out dv))
            {
                litVal = dv;
            }
            if (valPin != null)
            {
                return EmitDefaultLiteral(valPin, litVal);
            }
        }

        // Vector Arithmetic & Math
        if (methodId.EndsWith("VectorAdd", StringComparison.OrdinalIgnoreCase) || methodId == "Vector3.Add")
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, exprA, exprB));
        }

        if (methodId.EndsWith("VectorSubtract", StringComparison.OrdinalIgnoreCase) || methodId == "Vector3.Subtract")
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.SubtractExpression, exprA, exprB));
        }

        if (methodId.EndsWith("VectorMultiplyScalar", StringComparison.OrdinalIgnoreCase))
        {
            var exprVec = ResolveNamedPinOrIndex(pureNode, "vector", 0, recursionGuard);
            var exprScalar = ResolveNamedPinOrIndex(pureNode, "scalar", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.MultiplyExpression, exprVec, exprScalar));
        }

        if (methodId.EndsWith("VectorDistance", StringComparison.OrdinalIgnoreCase) || methodId == "Vector3.Distance")
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Vector3"),
                    SyntaxFactory.IdentifierName("Distance")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(exprA),
                    SyntaxFactory.Argument(exprB)
                })));
        }

        if (methodId.EndsWith("VectorNormalize", StringComparison.OrdinalIgnoreCase) || methodId == "Vector3.Normalize")
        {
            var exprVec = ResolveNamedPinOrIndex(pureNode, "value", 0, recursionGuard);
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Vector3"),
                    SyntaxFactory.IdentifierName("Normalize")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(exprVec))));
        }

        // Scalar Math
        if (methodId.EndsWith(".Add", StringComparison.OrdinalIgnoreCase) || methodId == "Add" || methodId.EndsWith("FloatAdd", StringComparison.OrdinalIgnoreCase))
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, exprA, exprB));
        }

        if (methodId.EndsWith(".Subtract", StringComparison.OrdinalIgnoreCase) || methodId == "Subtract" || methodId.EndsWith("FloatSubtract", StringComparison.OrdinalIgnoreCase))
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.SubtractExpression, exprA, exprB));
        }

        if (methodId.EndsWith(".Multiply", StringComparison.OrdinalIgnoreCase) || methodId == "Multiply" || methodId.EndsWith("FloatMultiply", StringComparison.OrdinalIgnoreCase))
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.MultiplyExpression, exprA, exprB));
        }

        if (methodId.EndsWith(".Divide", StringComparison.OrdinalIgnoreCase) || methodId == "Divide" || methodId.EndsWith("FloatDivide", StringComparison.OrdinalIgnoreCase))
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression, exprA, exprB));
        }

        if (methodId.EndsWith("Clamp", StringComparison.OrdinalIgnoreCase))
        {
            var exprVal = ResolveNamedPinOrIndex(pureNode, "value", 0, recursionGuard);
            var exprMin = ResolveNamedPinOrIndex(pureNode, "min", 1, recursionGuard);
            var exprMax = ResolveNamedPinOrIndex(pureNode, "max", 2, recursionGuard);
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("MathF"),
                    SyntaxFactory.IdentifierName("Clamp")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(exprVal),
                    SyntaxFactory.Argument(exprMin),
                    SyntaxFactory.Argument(exprMax)
                })));
        }

        if (methodId.EndsWith("Abs", StringComparison.OrdinalIgnoreCase))
        {
            var exprVal = ResolveNamedPinOrIndex(pureNode, "value", 0, recursionGuard);
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("MathF"),
                    SyntaxFactory.IdentifierName("Abs")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(exprVal))));
        }

        // Actor Getters
        if (methodId.EndsWith("GetActorLocation", StringComparison.OrdinalIgnoreCase))
        {
            return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("GetActorLocation"));
        }

        if (methodId.EndsWith("GetActorRotation", StringComparison.OrdinalIgnoreCase))
        {
            return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("GetActorRotation"));
        }

        if (methodId.EndsWith("GetActorScale", StringComparison.OrdinalIgnoreCase))
        {
            return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("GetActorScale"));
        }

        // Boolean operations
        if (methodId.EndsWith("Equal", StringComparison.OrdinalIgnoreCase))
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, exprA, exprB));
        }

        if (methodId.EndsWith("NotEqual", StringComparison.OrdinalIgnoreCase))
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression, exprA, exprB));
        }

        if (methodId.EndsWith("GreaterThan", StringComparison.OrdinalIgnoreCase))
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.GreaterThanExpression, exprA, exprB));
        }

        if (methodId.EndsWith("LessThan", StringComparison.OrdinalIgnoreCase))
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "a", 0, recursionGuard);
            var exprB = ResolveNamedPinOrIndex(pureNode, "b", 1, recursionGuard);
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.LessThanExpression, exprA, exprB));
        }

        if (methodId.EndsWith("Not", StringComparison.OrdinalIgnoreCase))
        {
            var exprA = ResolveNamedPinOrIndex(pureNode, "in", 0, recursionGuard);
            return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, exprA);
        }

        // Default: invoke method by name with all input pins
        var cleanMethod = methodId.Contains('.') ? methodId[(methodId.LastIndexOf('.') + 1)..] : methodId;
        var args = new List<ArgumentSyntax>();
        foreach (var inPin in pureNode.Pins.Where(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec))
        {
            args.Add(SyntaxFactory.Argument(ResolveExpression(pureNode, inPin, recursionGuard)));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(cleanMethod),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args)));
    }

    private ExpressionSyntax ResolveNamedPinOrIndex(GraphNode node, string pinName, int fallbackIndex, HashSet<string> recursionGuard)
    {
        var inputPins = node.Pins.Where(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec).ToList();
        var pin = inputPins.FirstOrDefault(p => p.Name.Equals(pinName, StringComparison.OrdinalIgnoreCase));
        if (pin == null && fallbackIndex >= 0 && fallbackIndex < inputPins.Count)
        {
            pin = inputPins[fallbackIndex];
        }

        if (pin != null)
        {
            return ResolveExpression(node, pin, recursionGuard);
        }

        return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0f));
    }

    public static ExpressionSyntax EmitDefaultLiteral(GraphPin pin, string? defaultValue)
    {
        switch (pin.ValueType)
        {
            case GraphPinType.Float:
                float f = 0f;
                if (!string.IsNullOrWhiteSpace(defaultValue))
                {
                    float.TryParse(defaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
                }
                return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(f));

            case GraphPinType.Integer:
                int i = 0;
                if (!string.IsNullOrWhiteSpace(defaultValue))
                {
                    int.TryParse(defaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out i);
                }
                return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i));

            case GraphPinType.Boolean:
                bool b = false;
                if (!string.IsNullOrWhiteSpace(defaultValue))
                {
                    bool.TryParse(defaultValue, out b);
                }
                return b
                    ? SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)
                    : SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);

            case GraphPinType.String:
                return SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(defaultValue ?? string.Empty));

            case GraphPinType.Vector3:
                return ParseVector3Literal(defaultValue);

            default:
                return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
        }
    }

    public static ExpressionSyntax ParseVector3Literal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("Vector3"),
                SyntaxFactory.IdentifierName("Zero"));
        }

        // Parse (x, y, z) or x, y, z
        var cleaned = text.Trim('(', ')', ' ', '[', ']');
        var parts = cleaned.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);

        float x = 0f, y = 0f, z = 0f;
        if (parts.Length > 0) float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
        if (parts.Length > 1) float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
        if (parts.Length > 2) float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z);

        if (x == 0f && y == 0f && z == 0f)
        {
            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("Vector3"),
                SyntaxFactory.IdentifierName("Zero"));
        }

        return SyntaxFactory.ObjectCreationExpression(
            SyntaxFactory.ParseTypeName("Vector3"),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(x))),
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(y))),
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(z)))
            })),
            null);
    }

    public static string SanitizeIdentifier(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrEmpty(cleaned)) cleaned = "var";
        if (char.IsDigit(cleaned[0])) cleaned = "_" + cleaned;
        return cleaned;
    }
}

/// <summary>
/// Alias class for RoslynPureExpressionEmitter as requested by user architectural conventions.
/// </summary>
public class RPE : RoslynPureExpressionEmitter
{
    public RPE(GraphDocument document) : base(document) { }
}

