using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Models.Blueprints;

namespace BlockoutStudio.Controllers.Services.Blueprints.Converters;

/// <summary>
/// Bidirectional converter facilitating migration between legacy BlueprintGraph (.bprint)
/// and the high-performance NativeAOT GraphDocument (.bgraph) format.
/// </summary>
public static class GraphFormatConverter
{
    public static GraphDocument ToGraphDocument(BlueprintGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var doc = new GraphDocument
        {
            GraphId = graph.Id,
            Name = graph.Name,
            TargetActorClass = graph.Type.ToString(),
            FormatVersion = 1
        };

        foreach (var node in graph.Nodes)
        {
            var kind = GraphNodeKind.ImpureFunction;
            if (node.NodeType.StartsWith("Event", StringComparison.OrdinalIgnoreCase))
            {
                kind = GraphNodeKind.Event;
            }
            else if (node.Category == BlueprintNodeCategory.Math)
            {
                kind = GraphNodeKind.PureFunction;
            }

            var gNode = new GraphNode
            {
                Id = node.Id,
                TargetMethodIdentifier = node.NodeType,
                X = node.PosX,
                Y = node.PosY,
                NodeKind = kind
            };

            foreach (var prop in node.Properties)
            {
                gNode.DefaultPinValues[prop.Key] = prop.Value;
            }

            foreach (var inPin in node.Inputs)
            {
                var pinType = MapPinType(inPin.Type);
                var gPin = new GraphPin(inPin.Id, inPin.Name, GraphPinDirection.Input, pinType);
                gNode.Pins.Add(gPin);

                if (!string.IsNullOrEmpty(inPin.DefaultValue))
                {
                    gNode.DefaultPinValues[inPin.Name] = inPin.DefaultValue;
                }
            }

            foreach (var outPin in node.Outputs)
            {
                var pinType = MapPinType(outPin.Type);
                var gPin = new GraphPin(outPin.Id, outPin.Name, GraphPinDirection.Output, pinType);
                gNode.Pins.Add(gPin);
            }

            doc.Nodes.Add(gNode);
        }

        foreach (var conn in graph.Connections)
        {
            doc.Links.Add(new GraphLink
            {
                Id = conn.Id,
                SourceNodeId = conn.FromNodeId,
                SourcePinId = conn.FromPinId,
                TargetNodeId = conn.ToNodeId,
                TargetPinId = conn.ToPinId
            });
        }

        return doc;
    }

    public static BlueprintGraph ToBlueprintGraph(GraphDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var graph = new BlueprintGraph
        {
            Id = doc.GraphId,
            Name = doc.Name,
            Type = Enum.TryParse<BlueprintGraphType>(doc.TargetActorClass, true, out var gType) ? gType : BlueprintGraphType.Actor
        };

        foreach (var gNode in doc.Nodes)
        {
            var nodeCat = BlueprintNodeCategory.Actor;
            if (gNode.NodeKind == GraphNodeKind.Event) nodeCat = BlueprintNodeCategory.Event;
            else if (gNode.NodeKind == GraphNodeKind.PureFunction) nodeCat = BlueprintNodeCategory.Math;

            var bpNode = new BlueprintNode
            {
                Id = gNode.Id,
                NodeType = gNode.TargetMethodIdentifier,
                Title = gNode.TargetMethodIdentifier,
                Category = nodeCat,
                PosX = gNode.X,
                PosY = gNode.Y
            };

            foreach (var defVal in gNode.DefaultPinValues)
            {
                bpNode.Properties[defVal.Key] = defVal.Value;
            }

            foreach (var pin in gNode.Pins)
            {
                var bpPinType = MapBlueprintPinType(pin.ValueType);
                string? defVal = gNode.DefaultPinValues.TryGetValue(pin.Name, out var v) ? v : null;

                var bpPin = new BlueprintPin
                {
                    Id = pin.Id,
                    Name = pin.Name,
                    Type = bpPinType,
                    Direction = pin.Direction == GraphPinDirection.Input ? BlueprintPinDirection.Input : BlueprintPinDirection.Output,
                    DefaultValue = defVal
                };

                if (pin.Direction == GraphPinDirection.Input)
                {
                    bpNode.Inputs.Add(bpPin);
                }
                else
                {
                    bpNode.Outputs.Add(bpPin);
                }
            }

            graph.Nodes.Add(bpNode);
        }

        foreach (var link in doc.Links)
        {
            graph.Connections.Add(new BlueprintConnection
            {
                Id = link.Id,
                FromNodeId = link.SourceNodeId,
                FromPinId = link.SourcePinId,
                ToNodeId = link.TargetNodeId,
                ToPinId = link.TargetPinId
            });
        }

        return graph;
    }

    private static GraphPinType MapPinType(BlueprintPinType type) => type switch
    {
        BlueprintPinType.Exec => GraphPinType.Exec,
        BlueprintPinType.Boolean => GraphPinType.Boolean,
        BlueprintPinType.Integer => GraphPinType.Integer,
        BlueprintPinType.Float => GraphPinType.Float,
        BlueprintPinType.Vector3 => GraphPinType.Vector3,
        BlueprintPinType.String => GraphPinType.String,
        BlueprintPinType.ActorRef => GraphPinType.ActorRef,
        _ => GraphPinType.Exec
    };

    private static BlueprintPinType MapBlueprintPinType(GraphPinType type) => type switch
    {
        GraphPinType.Exec => BlueprintPinType.Exec,
        GraphPinType.Boolean => BlueprintPinType.Boolean,
        GraphPinType.Integer => BlueprintPinType.Integer,
        GraphPinType.Float => BlueprintPinType.Float,
        GraphPinType.Vector3 => BlueprintPinType.Vector3,
        GraphPinType.String => BlueprintPinType.String,
        GraphPinType.ActorRef => BlueprintPinType.ActorRef,
        _ => BlueprintPinType.Float
    };
}

