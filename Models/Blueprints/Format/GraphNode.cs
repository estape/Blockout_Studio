namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Strongly-typed node model representing an event, pure function, or impure function.
/// </summary>
public class GraphNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TargetMethodIdentifier { get; set; } = string.Empty;
    public float X { get; set; } = 0f;
    public float Y { get; set; } = 0f;
    public GraphNodeKind NodeKind { get; set; } = GraphNodeKind.ImpureFunction;
    public Dictionary<string, string> DefaultPinValues { get; set; } = new(StringComparer.Ordinal);
    public List<GraphPin> Pins { get; set; } = [];

    public GraphNode() { }

    public GraphNode(string id, string targetMethodIdentifier, float x, float y, GraphNodeKind nodeKind)
    {
        Id = id;
        TargetMethodIdentifier = targetMethodIdentifier;
        X = x;
        Y = y;
        NodeKind = nodeKind;
    }

    public GraphPin? FindPin(string pinId) => Pins.FirstOrDefault(p => p.Id == pinId);
    public GraphPin? FindPinByName(string pinName, GraphPinDirection direction) =>
        Pins.FirstOrDefault(p => p.Name == pinName && p.Direction == direction);
}
