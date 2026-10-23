namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Strongly-typed domain model for member variables within a GraphDocument.
/// Represents script state properties that will be emitted as auto-properties in generated C# classes.
/// Completely decoupled from UI/DOM or IPC layer.
/// </summary>
public sealed class GraphVariable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "NewVariable";
    public GraphPinType Type { get; set; } = GraphPinType.Float;
    public string DefaultValue { get; set; } = "0";
    public bool IsPublic { get; set; } = true;

    public GraphVariable() { }

    public GraphVariable(string name, GraphPinType type, string defaultValue = "0", bool isPublic = true)
    {
        Name = name;
        Type = type;
        DefaultValue = defaultValue;
        IsPublic = isPublic;
    }
}
