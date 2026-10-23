using System.Text.Json.Serialization;

namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Architectural kind of a visual script node.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GraphNodeKind>))]
public enum GraphNodeKind
{
    Event,
    PureFunction,
    ImpureFunction,
    Branch,
    VariableGet,
    VariableSet
}
