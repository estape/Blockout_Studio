namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Strongly-typed connection linking an output pin on a source node to an input pin on a target node.
/// </summary>
public class GraphLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePinId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPinId { get; set; } = string.Empty;

    public GraphLink() { }

    public GraphLink(string sourceNodeId, string sourcePinId, string targetNodeId, string targetPinId, string? id = null)
    {
        Id = id ?? Guid.NewGuid().ToString("N");
        SourceNodeId = sourceNodeId;
        SourcePinId = sourcePinId;
        TargetNodeId = targetNodeId;
        TargetPinId = targetPinId;
    }
}
