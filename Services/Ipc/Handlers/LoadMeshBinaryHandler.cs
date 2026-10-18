using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Assets;
using UE5BlockoutStudio.Services.Geometry;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class LoadMeshBinaryHandler : IIpcActionHandler
{
    private readonly IObjUsdPipelineService _pipeline;
    private readonly IAssetDatabaseService _assetDb;

    public LoadMeshBinaryHandler(IObjUsdPipelineService pipeline, IAssetDatabaseService assetDb)
    {
        _pipeline = pipeline;
        _assetDb = assetDb;
    }

    public string Action => "load_mesh_binary";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var target = payload.TryGetProperty("guid", out var gp) ? gp.GetString() : null;
        if (string.IsNullOrWhiteSpace(target) && payload.TryGetProperty("virtualPath", out var vp))
        {
            target = vp.GetString();
        }

        var projectRoot = _assetDb.CurrentProjectRoot;

        if (!string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(projectRoot))
        {
            var artifact = _pipeline.GetOrCompileMeshBinary(target, projectRoot);
            if (artifact != null)
            {
                window.SendWebMessage(JsonSerializer.Serialize(new
                {
                    type = "mesh_binary_response",
                    success = true,
                    guid = target,
                    name = artifact.Name,
                    vertexCount = artifact.VertexCount,
                    indexCount = artifact.IndexCount,
                    binaryBase64 = Convert.ToBase64String(artifact.BinaryData),
                    min = new[] { artifact.BoundingMin.X, artifact.BoundingMin.Y, artifact.BoundingMin.Z },
                    max = new[] { artifact.BoundingMax.X, artifact.BoundingMax.Y, artifact.BoundingMax.Z },
                    center = new[] { artifact.Center.X, artifact.Center.Y, artifact.Center.Z }
                }));
                return Task.CompletedTask;
            }
        }

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "mesh_binary_response",
            success = false,
            message = "Failed to load or compile mesh binary"
        }));

        return Task.CompletedTask;
    }
}
