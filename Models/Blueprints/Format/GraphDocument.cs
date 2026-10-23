namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Root document container representing a complete visual script graph saved as .bgraph.
/// </summary>
public class GraphDocument
{
    public string GraphId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "NewGraph";
    public string TargetActorClass { get; set; } = "Actor";
    public int FormatVersion { get; set; } = 1;
    public List<GraphNode> Nodes { get; set; } = [];
    public List<GraphLink> Links { get; set; } = [];

    public GraphDocument() { }

    public GraphDocument(string graphId, string name, string targetActorClass = "Actor")
    {
        GraphId = graphId;
        Name = name;
        TargetActorClass = targetActorClass;
    }

    public GraphNode? FindNode(string nodeId) => Nodes.FirstOrDefault(n => n.Id == nodeId);
}

