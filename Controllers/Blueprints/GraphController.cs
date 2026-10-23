using BlockoutStudio.Controllers.Services.Blueprints.Serialization;
using BlockoutStudio.Controllers.Services.Blueprints.Validation;
using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Models.Blueprints.Validation;
using BlockoutStudio.Services.Blueprints.Storage;

namespace BlockoutStudio.Controllers.Blueprints;

/// <summary>
/// Authoritative MVC Controller coordinating visual script graph operations,
/// transactional pre-flight validation, disk persistence, and frontend interaction.
/// </summary>
public sealed class GraphController : IGraphController
{
    private readonly IGraphValidationService _validator;
    private readonly IBlueprintStorageService _storage;
    private readonly IGraphSerializer _serializer;

    public GraphDocument? ActiveDocument { get; private set; }
    public bool IsDirty { get; private set; }

    public GraphController(
        IGraphValidationService validator,
        IBlueprintStorageService storage,
        IGraphSerializer serializer)
    {
        _validator = validator;
        _storage = storage;
        _serializer = serializer;
    }

    public GraphOperationResult<GraphDocument> CreateGraph(string name, string targetActorClass = "Actor")
    {
        ActiveDocument = new GraphDocument(Guid.NewGuid().ToString("N"), name, targetActorClass);
        IsDirty = true;
        return new(true, ActiveDocument);
    }

    public async Task<GraphOperationResult<GraphDocument>> LoadGraphAsync(string projectRoot, string graphName)
    {
        var doc = await _storage.LoadGraphAsync(projectRoot, graphName);
        if (doc == null)
        {
            return new(false, null, $"Graph '{graphName}' not found.");
        }

        ActiveDocument = doc;
        IsDirty = false;
        return new(true, ActiveDocument);
    }

    public async Task<GraphOperationResult<string>> SaveGraphAsync(string projectRoot)
    {
        if (ActiveDocument == null)
        {
            return new(false, null, "No active graph document to save.");
        }

        // Exhaustive pre-save validation
        var validation = _validator.Validate(ActiveDocument);
        if (!validation.IsValid)
        {
            return new(false, null, "Graph contains validation errors.", validation.Errors);
        }

        var saveRes = await _storage.SaveGraphAsync(projectRoot, ActiveDocument);
        if (!saveRes.Success)
        {
            return new(false, null, saveRes.ErrorMessage);
        }

        IsDirty = false;
        return new(true, saveRes.BlueprintPath);
    }

    public GraphOperationResult<GraphNode> AddNode(string targetMethodIdentifier, GraphNodeKind kind, float x, float y)
    {
        if (ActiveDocument == null)
        {
            return new(false, null, "No active graph document.");
        }

        var node = new GraphNode(
            Guid.NewGuid().ToString("N"),
            targetMethodIdentifier,
            kind,
            new NodeSpatialMetadata(x, y));

        ActiveDocument.Nodes.Add(node);
        IsDirty = true;
        return new(true, node);
    }

    public GraphOperationResult<bool> RemoveNode(string nodeId)
    {
        if (ActiveDocument == null)
        {
            return new(false, false, "No active graph document.");
        }

        var node = ActiveDocument.FindNode(nodeId);
        if (node == null)
        {
            return new(false, false, $"Node '{nodeId}' not found.");
        }

        // Transactional cascade: remove all incoming and outgoing links
        ActiveDocument.Links.RemoveAll(l =>
            string.Equals(l.SourceNodeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.TargetNodeId, nodeId, StringComparison.OrdinalIgnoreCase));

        ActiveDocument.Nodes.Remove(node);
        IsDirty = true;
        return new(true, true);
    }

    public GraphOperationResult<ConnectPinsResponse> ConnectPins(string sourceNodeId, string sourcePinId, string targetNodeId, string targetPinId)
    {
        if (ActiveDocument == null)
        {
            return new(false, null, "No active graph document.");
        }

        // 1. Transactional Pre-Flight Validation
        var preFlight = _validator.CanConnectPins(ActiveDocument, sourceNodeId, sourcePinId, targetNodeId, targetPinId);
        if (!preFlight.IsValid)
        {
            return new(false, null, "Validation rejected pin connection.", preFlight.Errors);
        }

        var targetNode = ActiveDocument.FindNode(targetNodeId)!;
        var targetPin = targetNode.FindPin(targetPinId)!;

        string? replacedLinkId = null;
        // 2. Transactional Single Fan-In Enforcement for Data Inputs
        if (targetPin.ValueType != GraphPinType.Exec)
        {
            var existingLink = ActiveDocument.Links.FirstOrDefault(l =>
                string.Equals(l.TargetNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(l.TargetPinId, targetPinId, StringComparison.OrdinalIgnoreCase));

            if (existingLink != null)
            {
                replacedLinkId = existingLink.Id;
                ActiveDocument.Links.Remove(existingLink);
            }
        }

        // 3. Commit New Link
        var newLink = GraphLink.Create(sourceNodeId, sourcePinId, targetNodeId, targetPinId);
        ActiveDocument.Links.Add(newLink);

        var sourceNode = ActiveDocument.FindNode(sourceNodeId)!;
        var sourcePin = sourceNode.FindPin(sourcePinId)!;
        sourcePin.IsConnected = true;
        targetPin.IsConnected = true;

        IsDirty = true;
        return new(true, new ConnectPinsResponse(newLink, replacedLinkId));
    }

    public GraphOperationResult<bool> DisconnectPins(string linkId)
    {
        if (ActiveDocument == null)
        {
            return new(false, false, "No active graph document.");
        }

        var link = ActiveDocument.FindLink(linkId);
        if (link == null)
        {
            return new(false, false, $"Link '{linkId}' not found.");
        }

        ActiveDocument.Links.Remove(link);

        // Update pin connection flags
        var sourceNode = ActiveDocument.FindNode(link.SourceNodeId);
        var targetNode = ActiveDocument.FindNode(link.TargetNodeId);
        if (sourceNode?.FindPin(link.SourcePinId) is { } srcPin)
        {
            srcPin.IsConnected = ActiveDocument.Links.Any(l =>
                string.Equals(l.SourcePinId, srcPin.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.TargetPinId, srcPin.Id, StringComparison.OrdinalIgnoreCase));
        }

        if (targetNode?.FindPin(link.TargetPinId) is { } tgtPin)
        {
            tgtPin.IsConnected = ActiveDocument.Links.Any(l =>
                string.Equals(l.SourcePinId, tgtPin.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.TargetPinId, tgtPin.Id, StringComparison.OrdinalIgnoreCase));
        }

        IsDirty = true;
        return new(true, true);
    }

    public GraphOperationResult<bool> SetPinValue(string nodeId, string pinId, string value)
    {
        if (ActiveDocument == null)
        {
            return new(false, false, "No active graph document.");
        }

        var node = ActiveDocument.FindNode(nodeId);
        if (node == null)
        {
            return new(false, false, $"Node '{nodeId}' not found.");
        }

        var pin = node.FindPin(pinId) ?? node.Pins.FirstOrDefault(p => string.Equals(p.Name, pinId, StringComparison.OrdinalIgnoreCase));
        if (pin == null)
        {
            return new(false, false, $"Pin '{pinId}' not found.");
        }

        if (pin.Direction != GraphPinDirection.Input)
        {
            return new(false, false, "Cannot set default value on output pin.");
        }

        node.DefaultPinValues[pin.Name] = value;
        node.DefaultPinValues[pin.Id] = value;
        IsDirty = true;
        return new(true, true);
    }

    public GraphOperationResult<bool> MoveNode(string nodeId, float x, float y)
    {
        if (ActiveDocument == null)
        {
            return new(false, false, "No active graph document.");
        }

        var node = ActiveDocument.FindNode(nodeId);
        if (node == null)
        {
            return new(false, false, $"Node '{nodeId}' not found.");
        }

        // Fast-path spatial update: no semantic re-validation or AST invalidation
        node.Spatial = node.Spatial with { X = x, Y = y };
        IsDirty = true;
        return new(true, true);
    }

    public GraphValidationResult ValidateGraph()
    {
        return ActiveDocument != null
            ? _validator.Validate(ActiveDocument)
            : new GraphValidationResult();
    }
}
