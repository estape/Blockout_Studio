using System.Text.Json.Serialization;

namespace BlockoutStudio.Models.Blueprints;

/// <summary>
/// Data types supported for Blueprint execution pins and data pins.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlueprintPinType>))]
public enum BlueprintPinType
{
    Exec,
    Boolean,
    Integer,
    Float,
    Vector3,
    String,
    ActorRef
}

/// <summary>
/// Direction of a Blueprint pin (Input or Output).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlueprintPinDirection>))]
public enum BlueprintPinDirection
{
    Input,
    Output
}

/// <summary>
/// High-level visual category for node classification and UE5 header accent styling.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlueprintNodeCategory>))]
public enum BlueprintNodeCategory
{
    Event,
    Actor,
    Math,
    FlowControl,
    Variable
}

/// <summary>
/// Top-level graph classification (Actor / Entity Blueprint or global Level Blueprint).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlueprintGraphType>))]
public enum BlueprintGraphType
{
    Actor,
    Level
}

/// <summary>
/// Represents a single input or output connector pin on a Blueprint node.
/// </summary>
public class BlueprintPin
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public BlueprintPinType Type { get; set; } = BlueprintPinType.Exec;
    public BlueprintPinDirection Direction { get; set; } = BlueprintPinDirection.Input;
    public string? DefaultValue { get; set; }

    public BlueprintPin() { }

    public BlueprintPin(string name, BlueprintPinType type, BlueprintPinDirection direction, string? defaultValue = null)
    {
        Name = name;
        Type = type;
        Direction = direction;
        DefaultValue = defaultValue;
    }
}

/// <summary>
/// Represents a discrete operational node within the Blueprint graph.
/// </summary>
public class BlueprintNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string NodeType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public BlueprintNodeCategory Category { get; set; } = BlueprintNodeCategory.Actor;
    public float PosX { get; set; } = 0f;
    public float PosY { get; set; } = 0f;
    public List<BlueprintPin> Inputs { get; set; } = [];
    public List<BlueprintPin> Outputs { get; set; } = [];
    public Dictionary<string, string> Properties { get; set; } = [];
}

/// <summary>
/// Represents a directed wire connection between an output pin and an input pin.
/// </summary>
public class BlueprintConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FromNodeId { get; set; } = string.Empty;
    public string FromPinId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string ToPinId { get; set; } = string.Empty;
}

/// <summary>
/// Represents a user-defined member variable belonging to the Blueprint class.
/// </summary>
public class BlueprintVariable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "NewVariable";
    public BlueprintPinType Type { get; set; } = BlueprintPinType.Float;
    public string DefaultValue { get; set; } = "0";
    public bool IsPublic { get; set; } = true;
}

/// <summary>
/// Complete visual scripting graph document containing nodes, wires, and variable definitions.
/// </summary>
public class BlueprintGraph
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "NewBlueprint";
    public BlueprintGraphType Type { get; set; } = BlueprintGraphType.Actor;
    public List<BlueprintNode> Nodes { get; set; } = [];
    public List<BlueprintConnection> Connections { get; set; } = [];
    public List<BlueprintVariable> Variables { get; set; } = [];
}
