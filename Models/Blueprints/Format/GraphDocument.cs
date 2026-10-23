namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Aggregate root document container representing a complete visual script graph saved as .bgraph.
/// Contains pure script topology and metadata, completely decoupled from UI/DOM or IPC.
/// </summary>
public class GraphDocument
{
    public string GraphId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "NewGraph";
    public string TargetActorClass { get; set; } = "Actor";
    public int FormatVersion { get; set; } = 2;
    public List<GraphNode> Nodes { get; set; } = [];
    public List<GraphLink> Links { get; set; } = [];
    public List<GraphVariable> Variables { get; set; } = [];

    public GraphDocument() { }

    public GraphDocument(string graphId, string name, string targetActorClass = "Actor")
    {
        GraphId = graphId;
        Name = name;
        TargetActorClass = targetActorClass;
    }

    public GraphNode? FindNode(string nodeId) => Nodes.FirstOrDefault(n => n.Id == nodeId);

    public GraphLink? FindLink(string linkId) => Links.FirstOrDefault(l => l.Id == linkId);

    public GraphVariable? FindVariable(string variableIdOrName) =>
        Variables.FirstOrDefault(v => v.Id == variableIdOrName || string.Equals(v.Name, variableIdOrName, StringComparison.OrdinalIgnoreCase));

    public (GraphNode Node, GraphPin Pin)? FindPin(string pinId)
    {
        foreach (var node in Nodes)
        {
            var pin = node.FindPin(pinId);
            if (pin != null) return (node, pin);
        }
        return null;
    }
}
