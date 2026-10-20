using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Geometry;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for generating multi-level LOD cascades (LOD0, LOD1, LOD2) for mesh assets.
/// </summary>
public class GenerateLodsHandler : IIpcActionHandler
{
    /// <summary>LOD decimation generator service.</summary>
    private readonly ILodGeneratorService _lodGenerator;

    /// <inheritdoc/>
    public string Action => "generate_lods";

    /// <summary>Initializes handler with the LOD generator service.</summary>
    public GenerateLodsHandler(ILodGeneratorService lodGenerator)
    {
        _lodGenerator = lodGenerator;
    }

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        try
        {
            float[] positions = payload.TryGetProperty("positions", out var pp)
                ? pp.EnumerateArray().Select(x => x.GetSingle()).ToArray()
                : Array.Empty<float>();

            float[] normals = payload.TryGetProperty("normals", out var np)
                ? np.EnumerateArray().Select(x => x.GetSingle()).ToArray()
                : Array.Empty<float>();

            float[]? uvs = payload.TryGetProperty("uvs", out var up)
                ? up.EnumerateArray().Select(x => x.GetSingle()).ToArray()
                : null;

            int[] indices = payload.TryGetProperty("indices", out var ip)
                ? ip.EnumerateArray().Select(x => x.GetInt32()).ToArray()
                : Array.Empty<int>();

            string? targetId = payload.TryGetProperty("targetId", out var tid) ? tid.GetString() : null;

            var lods = _lodGenerator.GenerateLods(positions, normals, uvs, indices);

            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "generate_lods_response",
                success = true,
                targetId,
                lod0 = new
                {
                    level = lods.Lod0.Level,
                    triangles = lods.Lod0.TriangleCount,
                    vertices = lods.Lod0.VertexCount,
                    reduction = lods.Lod0.ReductionRatio,
                    positions = lods.Lod0.Positions,
                    normals = lods.Lod0.Normals,
                    uvs = lods.Lod0.Uvs,
                    indices = lods.Lod0.Indices
                },
                lod1 = new
                {
                    level = lods.Lod1.Level,
                    triangles = lods.Lod1.TriangleCount,
                    vertices = lods.Lod1.VertexCount,
                    reduction = lods.Lod1.ReductionRatio,
                    positions = lods.Lod1.Positions,
                    normals = lods.Lod1.Normals,
                    uvs = lods.Lod1.Uvs,
                    indices = lods.Lod1.Indices
                },
                lod2 = new
                {
                    level = lods.Lod2.Level,
                    triangles = lods.Lod2.TriangleCount,
                    vertices = lods.Lod2.VertexCount,
                    reduction = lods.Lod2.ReductionRatio,
                    positions = lods.Lod2.Positions,
                    normals = lods.Lod2.Normals,
                    uvs = lods.Lod2.Uvs,
                    indices = lods.Lod2.Indices
                },
                elapsedMs = lods.ElapsedMs
            }));
        }
        catch (Exception ex)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "generate_lods_response",
                success = false,
                error = ex.Message
            }));
        }

        return Task.CompletedTask;
    }
}
