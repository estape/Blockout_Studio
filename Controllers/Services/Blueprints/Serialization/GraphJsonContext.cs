using System.Text.Json.Serialization;
using BlockoutStudio.Controllers.Blueprints;
using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Models.Blueprints.Validation;

namespace BlockoutStudio.Controllers.Services.Blueprints.Serialization;

/// <summary>
/// Source generator JsonSerializerContext enabling compile-time NativeAOT serialization
/// for all .bgraph models and MVC controller DTOs without runtime reflection.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default,
    UseStringEnumConverter = true)]
// Domain Models
[JsonSerializable(typeof(GraphDocument))]
[JsonSerializable(typeof(GraphNode))]
[JsonSerializable(typeof(GraphPin))]
[JsonSerializable(typeof(GraphLink))]
[JsonSerializable(typeof(GraphVariable))]
[JsonSerializable(typeof(NodeSpatialMetadata))]
[JsonSerializable(typeof(GraphNodeType))]
[JsonSerializable(typeof(GraphPinDirection))]
[JsonSerializable(typeof(GraphPinType))]

// Collections
[JsonSerializable(typeof(List<GraphNode>))]
[JsonSerializable(typeof(List<GraphPin>))]
[JsonSerializable(typeof(List<GraphLink>))]
[JsonSerializable(typeof(List<GraphVariable>))]
[JsonSerializable(typeof(Dictionary<string, string>))]

// Validation DTOs
[JsonSerializable(typeof(GraphValidationResult))]
[JsonSerializable(typeof(GraphValidationError))]
[JsonSerializable(typeof(GraphValidationWarning))]
[JsonSerializable(typeof(List<GraphValidationError>))]
[JsonSerializable(typeof(List<GraphValidationWarning>))]

// Controller Result DTOs
[JsonSerializable(typeof(ConnectPinsResponse))]
[JsonSerializable(typeof(GraphOperationResult<GraphDocument>))]
[JsonSerializable(typeof(GraphOperationResult<GraphNode>))]
[JsonSerializable(typeof(GraphOperationResult<GraphVariable>))]
[JsonSerializable(typeof(GraphOperationResult<ConnectPinsResponse>))]
[JsonSerializable(typeof(GraphOperationResult<bool>))]
[JsonSerializable(typeof(GraphOperationResult<string>))]
public partial class GraphJsonContext : JsonSerializerContext
{
}
