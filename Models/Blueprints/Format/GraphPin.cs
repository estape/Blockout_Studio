namespace BlockoutStudio.Models.Blueprints.Format;

/// <summary>
/// Strongly-typed pin model representing an input or output terminal on a node.
/// </summary>
public class GraphPin
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public GraphPinDirection Direction { get; set; } = GraphPinDirection.Input;
    public GraphPinType ValueType { get; set; } = GraphPinType.Exec;
    public bool IsConnected { get; set; } = false;

    public GraphPin() { }

    public GraphPin(string id, string name, GraphPinDirection direction, GraphPinType valueType, bool isConnected = false)
    {
        Id = id;
        Name = name;
        Direction = direction;
        ValueType = valueType;
        IsConnected = isConnected;
    }
}
