namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Pure value record encapsulating spatial positioning and visual editor state.
/// Decouples canvas presentation from semantic compilation.
/// </summary>
public sealed record NodeSpatialMetadata(
    float X = 0f,
    float Y = 0f,
    float Width = 160f,
    float Height = 80f,
    bool IsCollapsed = false
);

/// <summary>
/// Strongly-typed domain node model representing an event, pure function, or impure function.
/// Completely decoupled from UI/DOM and IPC logic.
/// </summary>
public class GraphNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TargetMethodIdentifier { get; set; } = string.Empty;
    public GraphNodeKind NodeKind { get; set; } = GraphNodeKind.ImpureFunction;

    /// <summary>
    /// Explicit spatial metadata sub-component.
    /// </summary>
    public NodeSpatialMetadata Spatial { get; set; } = new();

    /// <summary>
    /// Convenience accessor for horizontal canvas position.
    /// </summary>
    public float X
    {
        get => Spatial.X;
        set => Spatial = Spatial with { X = value };
    }

    /// <summary>
    /// Convenience accessor for vertical canvas position.
    /// </summary>
    public float Y
    {
        get => Spatial.Y;
        set => Spatial = Spatial with { Y = value };
    }

    /// <summary>
    /// Default literal constant values assigned to unconnected input data pins.
    /// </summary>
    public Dictionary<string, string> DefaultPinValues { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Collection of input and output pins attached to this node.
    /// </summary>
    public List<GraphPin> Pins { get; set; } = [];

    public GraphNode() { }

    public GraphNode(string id, string targetMethodIdentifier, float x, float y, GraphNodeKind nodeKind)
    {
        Id = id;
        TargetMethodIdentifier = targetMethodIdentifier;
        Spatial = new NodeSpatialMetadata(x, y);
        NodeKind = nodeKind;
    }

    public GraphNode(string id, string targetMethodIdentifier, GraphNodeKind nodeKind, NodeSpatialMetadata? spatial = null)
    {
        Id = id;
        TargetMethodIdentifier = targetMethodIdentifier;
        NodeKind = nodeKind;
        Spatial = spatial ?? new NodeSpatialMetadata();
    }

    public GraphPin? FindPin(string pinId) => Pins.FirstOrDefault(p => p.Id == pinId);
    public GraphPin? FindPinByName(string pinName, GraphPinDirection direction) =>
        Pins.FirstOrDefault(p => p.Name == pinName && p.Direction == direction);
}
