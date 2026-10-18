using System.Numerics;
using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Graphics;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class BakeAoHandler : IIpcActionHandler
{
    private readonly IVulkanComputeService _vulkanCompute;

    public string Action => "bake_ao";

    public BakeAoHandler(IVulkanComputeService vulkanCompute)
    {
        _vulkanCompute = vulkanCompute;
    }

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var trianglesProp = root.GetProperty("triangles");
        var sceneTriangles = new List<SceneTriangle>();
        foreach (var tri in trianglesProp.EnumerateArray())
        {
            var v0 = new Vector3(tri[0].GetSingle(), tri[1].GetSingle(), tri[2].GetSingle());
            var v1 = new Vector3(tri[3].GetSingle(), tri[4].GetSingle(), tri[5].GetSingle());
            var v2 = new Vector3(tri[6].GetSingle(), tri[7].GetSingle(), tri[8].GetSingle());
            sceneTriangles.Add(new SceneTriangle(v0, v1, v2));
        }

        var targetMesh = root.GetProperty("targetMesh");
        var positions = targetMesh.GetProperty("positions").EnumerateArray().Select(x => x.GetSingle()).ToArray();
        var normals = targetMesh.GetProperty("normals").EnumerateArray().Select(x => x.GetSingle()).ToArray();

        int sampleCount = root.TryGetProperty("sampleCount", out var sc) ? sc.GetInt32() : 32;
        float maxDistance = root.TryGetProperty("maxDistance", out var md) ? md.GetSingle() : 120.0f;

        var bakeResult = _vulkanCompute.BakeVertexAo(positions, normals, sceneTriangles, sampleCount, maxDistance);

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "bake_ao_response",
            success = bakeResult.Success,
            targetId = root.TryGetProperty("targetId", out var tid) ? tid.GetString() : null,
            colors = bakeResult.Colors,
            accelerationMode = bakeResult.AccelerationMode,
            elapsedMs = bakeResult.ElapsedMs,
            verticesProcessed = bakeResult.VerticesProcessed,
            trianglesTested = bakeResult.TrianglesTested,
            error = bakeResult.Error
        }));

        return Task.CompletedTask;
    }
}
