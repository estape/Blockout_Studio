using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using BlockoutStudio.Models.Blueprints.Format;

namespace BlockoutStudio.Controllers.Services.Blueprints.Transpiler;

/// <summary>
/// Traverses impure execution flow starting at event nodes and produces sequential
/// Roslyn StatementSyntax nodes along the Exec pin chain.
/// </summary>
public class RoslynExecutionChainEmitter
{
    private readonly GraphDocument _document;
    private readonly Dictionary<string, GraphNode> _nodeMap;
    private readonly RoslynPureExpressionEmitter _pureEmitter;
    private readonly List<GraphLink> _execLinks;

    public RoslynExecutionChainEmitter(GraphDocument document, RoslynPureExpressionEmitter pureEmitter)
    {
        _document = document;
        _pureEmitter = pureEmitter;
        _nodeMap = document.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        _execLinks = document.Links.Where(l =>
        {
            if (_nodeMap.TryGetValue(l.SourceNodeId, out var s) && _nodeMap.TryGetValue(l.TargetNodeId, out var t))
            {
                var sp = s.FindPin(l.SourcePinId);
                var tp = t.FindPin(l.TargetPinId);
                return sp?.ValueType == GraphPinType.Exec && tp?.ValueType == GraphPinType.Exec;
            }
            return false;
        }).ToList();
    }

    /// <summary>
    /// Emits a list of statements beginning from the specified source pin (e.g. an event's ExecOut pin).
    /// </summary>
    public List<StatementSyntax> EmitExecutionChain(string sourceNodeId, string sourcePinId, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.Ordinal);
        var statements = new List<StatementSyntax>();

        var currentPinId = sourcePinId;
        var currentNodeId = sourceNodeId;

        while (true)
        {
            // Find link from current (node, pin)
            var link = _execLinks.FirstOrDefault(l => l.SourceNodeId == currentNodeId && l.SourcePinId == currentPinId);
            if (link == null) break;

            if (!_nodeMap.TryGetValue(link.TargetNodeId, out var targetNode))
                break;

            if (visited.Contains(targetNode.Id))
            {
                // Break cycle
                break;
            }
            visited.Add(targetNode.Id);

            // Handle branching flow control node
            if (IsBranchNode(targetNode))
            {
                var ifStmt = EmitBranchStatement(targetNode, visited);
                statements.Add(ifStmt);
                break; // Branch terminates sequential loop (branches continue in true/false subchains)
            }

            // Handle Sequence flow control node
            if (IsSequenceNode(targetNode))
            {
                var seqStmts = EmitSequenceStatements(targetNode, visited);
                statements.AddRange(seqStmts);
                break;
            }

            // Emit statement for this impure node
            var stmt = EmitImpureNodeStatement(targetNode);
            if (stmt != null)
            {
                statements.Add(stmt);
            }

            // Advance along primary ExecOut
            var nextExecOutPin = targetNode.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Output && p.ValueType == GraphPinType.Exec);
            if (nextExecOutPin == null) break;

            currentNodeId = targetNode.Id;
            currentPinId = nextExecOutPin.Id;
        }

        return statements;
    }

    private StatementSyntax? EmitImpureNodeStatement(GraphNode node)
    {
        var methodId = node.TargetMethodIdentifier;

        // SetActorLocation
        if (methodId.EndsWith("SetActorLocation", StringComparison.OrdinalIgnoreCase))
        {
            var inPin = node.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec);
            var locExpr = inPin != null
                ? _pureEmitter.ResolveExpression(node, inPin)
                : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("Vector3"), SyntaxFactory.IdentifierName("Zero"));

            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("SetActorLocation"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(locExpr)))));
        }

        // SetActorRotation
        if (methodId.EndsWith("SetActorRotation", StringComparison.OrdinalIgnoreCase))
        {
            var inPin = node.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec);
            var rotExpr = inPin != null
                ? _pureEmitter.ResolveExpression(node, inPin)
                : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("Vector3"), SyntaxFactory.IdentifierName("Zero"));

            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("SetActorRotation"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(rotExpr)))));
        }

        // SetActorScale
        if (methodId.EndsWith("SetActorScale", StringComparison.OrdinalIgnoreCase))
        {
            var inPin = node.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec);
            var scaleExpr = inPin != null
                ? _pureEmitter.ResolveExpression(node, inPin)
                : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("Vector3"), SyntaxFactory.IdentifierName("One"));

            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("SetActorScale"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(scaleExpr)))));
        }

        // AddActorWorldOffset
        if (methodId.EndsWith("AddActorWorldOffset", StringComparison.OrdinalIgnoreCase))
        {
            var inPin = node.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec);
            var offsetExpr = inPin != null
                ? _pureEmitter.ResolveExpression(node, inPin)
                : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("Vector3"), SyntaxFactory.IdentifierName("Zero"));

            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("AddActorWorldOffset"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(offsetExpr)))));
        }

        // AddActorWorldRotation
        if (methodId.EndsWith("AddActorWorldRotation", StringComparison.OrdinalIgnoreCase))
        {
            var inPin = node.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec);
            var rotExpr = inPin != null
                ? _pureEmitter.ResolveExpression(node, inPin)
                : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("Vector3"), SyntaxFactory.IdentifierName("Zero"));

            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("AddActorWorldRotation"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(rotExpr)))));
        }

        // PrintString
        if (methodId.EndsWith("PrintString", StringComparison.OrdinalIgnoreCase))
        {
            var inPin = node.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec);
            ExpressionSyntax msgExpr;
            if (inPin != null)
            {
                msgExpr = _pureEmitter.ResolveExpression(node, inPin);
            }
            else if (node.DefaultPinValues.TryGetValue("message", out var defMsg))
            {
                msgExpr = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(defMsg));
            }
            else
            {
                msgExpr = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("Hello"));
            }

            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("PrintString"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(msgExpr)))));
        }

        // PlaySound2D
        if (methodId.EndsWith("PlaySound2D", StringComparison.OrdinalIgnoreCase))
        {
            var soundPin = node.Pins.FirstOrDefault(p => p.Name.Contains("sound", StringComparison.OrdinalIgnoreCase));
            var volPin = node.Pins.FirstOrDefault(p => p.Name.Contains("vol", StringComparison.OrdinalIgnoreCase));

            var soundExpr = soundPin != null ? _pureEmitter.ResolveExpression(node, soundPin) : SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("default_sound"));
            var volExpr = volPin != null ? _pureEmitter.ResolveExpression(node, volPin) : SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1.0f));

            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("PlaySound2D"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.Argument(soundExpr),
                        SyntaxFactory.Argument(volExpr)
                    }))));
        }

        // Generic method invocation
        var cleanMethod = methodId.Contains('.') ? methodId[(methodId.LastIndexOf('.') + 1)..] : methodId;
        var args = new List<ArgumentSyntax>();
        foreach (var inPin in node.Pins.Where(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec))
        {
            args.Add(SyntaxFactory.Argument(_pureEmitter.ResolveExpression(node, inPin)));
        }

        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName(cleanMethod),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args))));
    }

    private IfStatementSyntax EmitBranchStatement(GraphNode branchNode, HashSet<string> visited)
    {
        var condPin = branchNode.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Input && p.ValueType != GraphPinType.Exec);
        var condExpr = condPin != null
            ? _pureEmitter.ResolveExpression(branchNode, condPin)
            : SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);

        var truePin = branchNode.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Output && p.Name.Equals("True", StringComparison.OrdinalIgnoreCase));
        var falsePin = branchNode.Pins.FirstOrDefault(p => p.Direction == GraphPinDirection.Output && p.Name.Equals("False", StringComparison.OrdinalIgnoreCase));

        var trueStmts = truePin != null
            ? EmitExecutionChain(branchNode.Id, truePin.Id, new HashSet<string>(visited, StringComparer.Ordinal))
            : [];

        var falseStmts = falsePin != null
            ? EmitExecutionChain(branchNode.Id, falsePin.Id, new HashSet<string>(visited, StringComparer.Ordinal))
            : [];

        var ifStmt = SyntaxFactory.IfStatement(condExpr, SyntaxFactory.Block(trueStmts));
        if (falseStmts.Count > 0)
        {
            ifStmt = ifStmt.WithElse(SyntaxFactory.ElseClause(SyntaxFactory.Block(falseStmts)));
        }

        return ifStmt;
    }

    private List<StatementSyntax> EmitSequenceStatements(GraphNode seqNode, HashSet<string> visited)
    {
        var result = new List<StatementSyntax>();
        var outExecPins = seqNode.Pins.Where(p => p.Direction == GraphPinDirection.Output && p.ValueType == GraphPinType.Exec).ToList();

        foreach (var pin in outExecPins)
        {
            var branchStmts = EmitExecutionChain(seqNode.Id, pin.Id, new HashSet<string>(visited, StringComparer.Ordinal));
            result.AddRange(branchStmts);
        }

        return result;
    }

    private static bool IsBranchNode(GraphNode node)
    {
        return node.TargetMethodIdentifier.EndsWith("Branch", StringComparison.OrdinalIgnoreCase) ||
               node.TargetMethodIdentifier.Equals("If", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSequenceNode(GraphNode node)
    {
        return node.TargetMethodIdentifier.EndsWith("Sequence", StringComparison.OrdinalIgnoreCase);
    }
}
