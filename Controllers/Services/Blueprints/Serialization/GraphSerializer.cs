using System.Text.Json;
using BlockoutStudio.Models.Blueprints.Format;

namespace BlockoutStudio.Controllers.Services.Blueprints.Serialization;

/// <summary>
/// AOT-compliant serialization interface for .bgraph documents using compile-time source-generated code.
/// </summary>
public interface IGraphSerializer
{
    string Serialize(GraphDocument document);
    byte[] SerializeToUtf8Bytes(GraphDocument document);
    GraphDocument? Deserialize(string json);
    GraphDocument? Deserialize(ReadOnlySpan<byte> utf8Json);
}

/// <summary>
/// Zero-allocation, AOT-compliant JSON serializer and deserializer for .bgraph files.
/// </summary>
public class GraphSerializer : IGraphSerializer
{
    public string Serialize(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, GraphJsonContext.Default.GraphDocument);
    }

    public byte[] SerializeToUtf8Bytes(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.SerializeToUtf8Bytes(document, GraphJsonContext.Default.GraphDocument);
    }

    public GraphDocument? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize(json, GraphJsonContext.Default.GraphDocument);
    }

    public GraphDocument? Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty) return null;
        return JsonSerializer.Deserialize(utf8Json, GraphJsonContext.Default.GraphDocument);
    }
}

