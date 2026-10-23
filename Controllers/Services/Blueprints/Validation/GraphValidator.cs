using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Models.Blueprints.Validation;

namespace BlockoutStudio.Controllers.Services.Blueprints.Validation;

/// <summary>
/// Pre-save and pre-compile validation engine interface.
/// </summary>
public interface IGraphValidator
{
    GraphValidationResult Validate(GraphDocument document);
}

/// <summary>
/// Pre-save and pre-compilation validation service enforcing pin compatibility, single input connections on data pins,
/// and acyclic graph guarantees for both impure execution chains and pure expression DAGs.
/// </summary>
public class GraphValidator : IGraphValidator
{
    public GraphValidationResult Validate(GraphDocument document)
    {
        var result = new GraphValidationResult();
        if (document == null)
        {
            result.AddError("ERR_NULL_DOCUMENT", "Graph document cannot be null.");
            return result;
        }

        var nodeMap = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var pinMap = new Dictionary<string, (GraphNode Node, GraphPin Pin)>(StringComparer.Ordinal);

        // Reset connection states on pins
        foreach (var node in document.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                result.AddError("ERR_INVALID_NODE_ID", "Node ID cannot be null or empty.");
                continue;
            }

            if (nodeMap.ContainsKey(node.Id))
            {
                result.AddError("ERR_DUPLICATE_NODE_ID", $"Duplicate node ID detected: '{node.Id}'.", node.Id);
                continue;
            }

            nodeMap[node.Id] = node;

            foreach (var pin in node.Pins)
            {
                pin.IsConnected = false;
                if (string.IsNullOrWhiteSpace(pin.Id))
                {
                    result.AddError("ERR_INVALID_PIN_ID", $"Pin on node '{node.Id}' has an empty or null ID.", node.Id);
                    continue;
                }

                if (pinMap.ContainsKey(pin.Id))
                {
                    result.AddError("ERR_DUPLICATE_PIN_ID", $"Duplicate pin ID detected: '{pin.Id}'.", node.Id, pin.Id);
                    continue;
                }

                pinMap[pin.Id] = (node, pin);
            }
        }

        // 1. Validate Links & Pin Connections
        var dataInputTargetLinks = new Dictionary<string, List<GraphLink>>(StringComparer.Ordinal);

        foreach (var link in document.Links)
        {
            if (!nodeMap.TryGetValue(link.SourceNodeId, out var sourceNode))
            {
                result.AddError(GraphValidationErrors.NodeNotFound, $"Source node '{link.SourceNodeId}' not found in document.", link.SourceNodeId);
                continue;
            }

            if (!nodeMap.TryGetValue(link.TargetNodeId, out var targetNode))
            {
                result.AddError(GraphValidationErrors.NodeNotFound, $"Target node '{link.TargetNodeId}' not found in document.", link.TargetNodeId);
                continue;
            }

            var sourcePin = sourceNode.FindPin(link.SourcePinId);
            if (sourcePin == null)
            {
                result.AddError(GraphValidationErrors.PinNotFound, $"Source pin '{link.SourcePinId}' not found on node '{link.SourceNodeId}'.", link.SourceNodeId, link.SourcePinId);
                continue;
            }

            var targetPin = targetNode.FindPin(link.TargetPinId);
            if (targetPin == null)
            {
                result.AddError(GraphValidationErrors.PinNotFound, $"Target pin '{link.TargetPinId}' not found on node '{link.TargetNodeId}'.", link.TargetNodeId, link.TargetPinId);
                continue;
            }

            // Direction verification: Source must be Output, Target must be Input
            if (sourcePin.Direction != GraphPinDirection.Output)
            {
                result.AddError(GraphValidationErrors.InvalidPinDirection, $"Source pin '{sourcePin.Name}' must have Direction Output but has '{sourcePin.Direction}'.", link.SourceNodeId, sourcePin.Id);
            }

            if (targetPin.Direction != GraphPinDirection.Input)
            {
                result.AddError(GraphValidationErrors.InvalidPinDirection, $"Target pin '{targetPin.Name}' must have Direction Input but has '{targetPin.Direction}'.", link.TargetNodeId, targetPin.Id);
            }

            // Type Compatibility Check
            if (sourcePin.ValueType == GraphPinType.Exec || targetPin.ValueType == GraphPinType.Exec)
            {
                if (sourcePin.ValueType != targetPin.ValueType)
                {
                    result.AddError(GraphValidationErrors.TypeMismatch, $"Cannot connect execution pin '{sourcePin.Name}' to data pin '{targetPin.Name}' (type: {targetPin.ValueType}).", link.SourceNodeId, sourcePin.Id);
                }
            }
            else
            {
                // Data pin type compatibility
                if (!AreDataPinTypesCompatible(sourcePin.ValueType, targetPin.ValueType))
                {
                    result.AddError(GraphValidationErrors.TypeMismatch, $"Type mismatch: cannot connect pin '{sourcePin.Name}' of type '{sourcePin.ValueType}' to pin '{targetPin.Name}' of type '{targetPin.ValueType}'.", link.SourceNodeId, sourcePin.Id);
                }

                // Check multiple incoming connections on data input pins
                var targetKey = $"{targetNode.Id}:{targetPin.Id}";
                if (!dataInputTargetLinks.TryGetValue(targetKey, out var list))
                {
                    list = [];
                    dataInputTargetLinks[targetKey] = list;
                }
                list.Add(link);
            }

            sourcePin.IsConnected = true;
            targetPin.IsConnected = true;
        }

        // Check data pin fan-in
        foreach (var kvp in dataInputTargetLinks)
        {
            if (kvp.Value.Count > 1)
            {
                var targetPinId = kvp.Value[0].TargetPinId;
                var targetNodeId = kvp.Value[0].TargetNodeId;
                result.AddError(GraphValidationErrors.MultipleInputConnections, $"Data input pin '{targetPinId}' on node '{targetNodeId}' has {kvp.Value.Count} incoming connections. Only one is permitted.", targetNodeId, targetPinId);
            }
        }

        // 2. Validate Impure Execution Cycle (DFS recursion stack)
        DetectExecutionCycles(document, nodeMap, result);

        // 3. Validate Pure Function Dependency Cycle (DAG check)
        DetectDataDependencyCycles(document, nodeMap, result);

        return result;
    }

    private static bool AreDataPinTypesCompatible(GraphPinType source, GraphPinType target)
    {
        if (source == target) return true;

        // Numeric widening: Integer -> Float is valid
        if (source == GraphPinType.Integer && target == GraphPinType.Float)
            return true;

        // Disallow Float -> Struct / Object, etc.
        return false;
    }

    private static void DetectExecutionCycles(GraphDocument document, Dictionary<string, GraphNode> nodeMap, GraphValidationResult result)
    {
        // Build adjacency map for Exec pins
        var execAdj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in document.Nodes)
        {
            execAdj[node.Id] = [];
        }

        foreach (var link in document.Links)
        {
            if (!nodeMap.TryGetValue(link.SourceNodeId, out var srcNode) || !nodeMap.TryGetValue(link.TargetNodeId, out var tgtNode))
                continue;

            var srcPin = srcNode.FindPin(link.SourcePinId);
            var tgtPin = tgtNode.FindPin(link.TargetPinId);
            if (srcPin?.ValueType == GraphPinType.Exec && tgtPin?.ValueType == GraphPinType.Exec)
            {
                if (!execAdj.TryGetValue(srcNode.Id, out var targets))
                {
                    targets = [];
                    execAdj[srcNode.Id] = targets;
                }
                targets.Add(tgtNode.Id);
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var inStack = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nodeId in execAdj.Keys)
        {
            if (!visited.Contains(nodeId))
            {
                if (DfsExecCycle(nodeId, execAdj, visited, inStack, out var cycleNode))
                {
                    result.AddError(GraphValidationErrors.CircularExecutionCycle, $"Detected circular execution cycle involving node '{cycleNode}'.", cycleNode);
                    break;
                }
            }
        }
    }

    private static bool DfsExecCycle(string current, Dictionary<string, List<string>> adj, HashSet<string> visited, HashSet<string> inStack, out string cycleNode)
    {
        visited.Add(current);
        inStack.Add(current);

        if (adj.TryGetValue(current, out var neighbors))
        {
            foreach (var next in neighbors)
            {
                if (inStack.Contains(next))
                {
                    cycleNode = next;
                    return true;
                }

                if (!visited.Contains(next))
                {
                    if (DfsExecCycle(next, adj, visited, inStack, out cycleNode))
                        return true;
                }
            }
        }

        inStack.Remove(current);
        cycleNode = string.Empty;
        return false;
    }

    private static void DetectDataDependencyCycles(GraphDocument document, Dictionary<string, GraphNode> nodeMap, GraphValidationResult result)
    {
        // For pure function nodes:
        // A pure node depends on the nodes feeding its input data pins.
        // If Target depends on Source: edge Target -> Source
        var depAdj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in document.Nodes.Where(n => n.NodeKind == GraphNodeKind.PureFunction))
        {
            depAdj[node.Id] = [];
        }

        foreach (var link in document.Links)
        {
            if (!nodeMap.TryGetValue(link.SourceNodeId, out var srcNode) || !nodeMap.TryGetValue(link.TargetNodeId, out var tgtNode))
                continue;

            // Only track dependencies between pure nodes
            if (srcNode.NodeKind == GraphNodeKind.PureFunction && tgtNode.NodeKind == GraphNodeKind.PureFunction)
            {
                var srcPin = srcNode.FindPin(link.SourcePinId);
                var tgtPin = tgtNode.FindPin(link.TargetPinId);
                if (srcPin?.ValueType != GraphPinType.Exec && tgtPin?.ValueType != GraphPinType.Exec)
                {
                    if (!depAdj.TryGetValue(tgtNode.Id, out var deps))
                    {
                        deps = [];
                        depAdj[tgtNode.Id] = deps;
                    }
                    deps.Add(srcNode.Id);
                }
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var inStack = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pureNodeId in depAdj.Keys)
        {
            if (!visited.Contains(pureNodeId))
            {
                if (DfsDepCycle(pureNodeId, depAdj, visited, inStack, out var cycleNode))
                {
                    result.AddError(GraphValidationErrors.CircularDataDependency, $"Detected circular data dependency involving pure function node '{cycleNode}'.", cycleNode);
                    break;
                }
            }
        }
    }

    private static bool DfsDepCycle(string current, Dictionary<string, List<string>> adj, HashSet<string> visited, HashSet<string> inStack, out string cycleNode)
    {
        visited.Add(current);
        inStack.Add(current);

        if (adj.TryGetValue(current, out var deps))
        {
            foreach (var dep in deps)
            {
                if (inStack.Contains(dep))
                {
                    cycleNode = dep;
                    return true;
                }

                if (!visited.Contains(dep))
                {
                    if (DfsDepCycle(dep, adj, visited, inStack, out cycleNode))
                        return true;
                }
            }
        }

        inStack.Remove(current);
        cycleNode = string.Empty;
        return false;
    }
}
