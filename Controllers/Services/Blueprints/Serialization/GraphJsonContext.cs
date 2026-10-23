using System.Text.Json.Serialization;
using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Models.Blueprints.Validation;

namespace BlockoutStudio.Controllers.Services.Blueprints.Serialization;

/// <summary>
/// Source generator JsonSerializerContext enabling compile-time NativeAOT serialization
/// for all .bgraph models without runtime reflection.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(GraphDocument))]
[JsonSerializable(typeof(GraphNode))]
[JsonSerializable(typeof(GraphPin))]
[JsonSerializable(typeof(GraphLink))]
[JsonSerializable(typeof(List<GraphNode>))]
[JsonSerializable(typeof(List<GraphLink>))]
[JsonSerializable(typeof(List<GraphPin>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(GraphValidationResult))]
[JsonSerializable(typeof(GraphValidationError))]
[JsonSerializable(typeof(GraphValidationWarning))]
[JsonSerializable(typeof(List<GraphValidationError>))]
[JsonSerializable(typeof(List<GraphValidationWarning>))]
public partial class GraphJsonContext : JsonSerializerContext
{
}

