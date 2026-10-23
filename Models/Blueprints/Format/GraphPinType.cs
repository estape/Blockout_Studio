using System.Text.Json.Serialization;

namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Direction of a node pin connection (Input or Output).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GraphPinDirection>))]
public enum GraphPinDirection
{
    Input,
    Output
}

/// <summary>
/// Value type passed through graph pins.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GraphPinType>))]
public enum GraphPinType
{
    Exec,
    Boolean,
    Integer,
    Float,
    Vector3,
    String,
    ActorRef,
    Object,
    Struct
}

