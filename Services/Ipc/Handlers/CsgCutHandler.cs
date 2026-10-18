using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Geometry;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class CsgCutHandler : IIpcActionHandler
{
    private readonly ICsgEngine _csgEngine;

    public CsgCutHandler(ICsgEngine csgEngine)
    {
        _csgEngine = csgEngine;
    }

    public CsgCutHandler() : this(new CsgEngine())
    {
    }

    public string Action => "csg_cut";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var sub = root.GetProperty("subject");
        var cut = root.GetProperty("cutter");

        var subPos = sub.GetProperty("positions").EnumerateArray().Select(x => x.GetSingle()).ToArray();
        var subNorm = sub.TryGetProperty("normals", out var sn) ? sn.EnumerateArray().Select(x => x.GetSingle()).ToArray() : null;
        var subUV = sub.TryGetProperty("uvs", out var su) ? su.EnumerateArray().Select(x => x.GetSingle()).ToArray() : null;
        var subIdx = sub.TryGetProperty("indices", out var si) ? si.EnumerateArray().Select(x => x.GetInt32()).ToArray() : null;

        var cutPos = cut.GetProperty("positions").EnumerateArray().Select(x => x.GetSingle()).ToArray();
        var cutNorm = cut.TryGetProperty("normals", out var cn) ? cn.EnumerateArray().Select(x => x.GetSingle()).ToArray() : null;
        var cutUV = cut.TryGetProperty("uvs", out var cu) ? cu.EnumerateArray().Select(x => x.GetSingle()).ToArray() : null;
        var cutIdx = cut.TryGetProperty("indices", out var ci) ? ci.EnumerateArray().Select(x => x.GetInt32()).ToArray() : null;

        var (positions, normals, uvs, indices) = _csgEngine.Subtract(
            subPos, subNorm, subUV, subIdx,
            cutPos, cutNorm, cutUV, cutIdx
        );

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "csg_cut_response",
            success = true,
            targetId = root.TryGetProperty("targetId", out var tid) ? tid.GetString() : null,
            positions,
            normals,
            uvs,
            indices
        }));

        return Task.CompletedTask;
    }
}
