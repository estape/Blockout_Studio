namespace BlockoutStudio.Models.Blueprints;

/// <summary>
/// Model representing a discovered input or output pin on a dynamically reflected Blueprint node.
/// </summary>
public class DiscoveredNodePin
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Exec"; // Exec, Float, Integer, Boolean, Vector3, String, ActorRef
    public string Direction { get; set; } = "Input"; // Input or Output
    public string Color { get; set; } = "#ffffff";
    public string? DefaultValue { get; set; }

    public DiscoveredNodePin() { }

    public DiscoveredNodePin(string name, string type, string direction, string color, string? defaultValue = null)
    {
        Name = name;
        Type = type;
        Direction = direction;
        Color = color;
        DefaultValue = defaultValue;
    }
}

/// <summary>
/// Model representing a complete reflected node definition synthesized from a C# method.
/// </summary>
public class DiscoveredNodeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Actor";
    public bool IsPure { get; set; }
    public string DeclaringType { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public List<DiscoveredNodePin> Inputs { get; set; } = [];
    public List<DiscoveredNodePin> Outputs { get; set; } = [];
    public Dictionary<string, string> Properties { get; set; } = [];
}

/// <summary>
/// Container catalog holding all discovered dynamic Blueprint nodes and categorized groupings.
/// </summary>
public class NodeCatalog
{
    public int Version { get; set; } = 1;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public int NodeCount => Nodes.Count;
    public List<DiscoveredNodeDefinition> Nodes { get; set; } = [];
    public Dictionary<string, List<string>> Categories { get; set; } = [];
}
