global using GraphNodeKind = BlockoutStudio.Models.Blueprints.Format.GraphNodeType;
using System.Text.Json.Serialization;

namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Architectural node type of a visual script graph node.
/// Decoupled from UI/DOM or IPC layer.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GraphNodeType>))]
public enum GraphNodeType
{
    Event,
    PureFunction,
    ImpureFunction,
    Branch,
    VariableGet,
    VariableSet
}

