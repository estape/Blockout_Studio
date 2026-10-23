using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Models.Blueprints.Validation;

namespace BlockoutStudio.Controllers.Blueprints;

/// <summary>
/// Result envelope for graph controller mutations and queries.
/// </summary>
public sealed record GraphOperationResult<T>(
    bool Success,
    T? Data = default,
    string? Message = null,
    List<GraphValidationError>? Errors = null
);

/// <summary>
/// Response payload for pin connection transactions reporting newly added and replaced link IDs.
/// </summary>
public sealed record ConnectPinsResponse(
    GraphLink? AddedLink,
    string? ReplacedLinkId
);

/// <summary>
/// Central authoritative MVC Controller orchestrating in-memory GraphDocument state,
/// pre-flight transaction validation, disk persistence, and Photino IPC dispatching.
/// </summary>
public interface IGraphController
{
    GraphDocument? ActiveDocument { get; }
    bool IsDirty { get; }

    GraphOperationResult<GraphDocument> CreateGraph(string name, string targetActorClass = "Actor");
    Task<GraphOperationResult<GraphDocument>> LoadGraphAsync(string projectRoot, string graphName);
    Task<GraphOperationResult<string>> SaveGraphAsync(string projectRoot);

    GraphOperationResult<GraphNode> AddNode(string targetMethodIdentifier, GraphNodeType nodeType, float x, float y);
    GraphOperationResult<bool> RemoveNode(string nodeId);
    GraphOperationResult<ConnectPinsResponse> ConnectPins(string sourceNodeId, string sourcePinId, string targetNodeId, string targetPinId);
    GraphOperationResult<bool> DisconnectPins(string linkId);
    GraphOperationResult<bool> SetPinValue(string nodeId, string pinId, string value);
    GraphOperationResult<bool> MoveNode(string nodeId, float x, float y);
    GraphOperationResult<GraphVariable> AddVariable(string name, GraphPinType type, string defaultValue = "0", bool isPublic = true);
    GraphOperationResult<bool> RemoveVariable(string variableId);
    GraphValidationResult ValidateGraph();
}

