using System.Numerics;
using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services;
using UE5BlockoutStudio.Services.Geometry;
using UE5BlockoutStudio.Services.Graphics;

namespace UE5BlockoutStudio;

internal class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test")
        {
            return AutomatedTests.Run();
        }

        var window = new PhotinoWindow()
            .SetTitle("UE5 Blockout Studio — SketchUp-Style 3D Editor (.NET 10)")
            .SetUseOsDefaultSize(false)
            .SetSize(1440, 900)
            .Center()
            .SetResizable(true);

        window.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                if (!root.TryGetProperty("action", out var actionProp)) return;
                var action = actionProp.GetString();

                switch (action)
                {
                    case "copy_ue5":
                    {
                        var data = root.GetProperty("data");
                        var (success, msg, count) = Ue5ClipboardService.CopyToUe5(data);
                        window.SendWebMessage(JsonSerializer.Serialize(new
                        {
                            type = "copy_ue5_response",
                            success,
                            message = msg,
                            actorCount = count
                        }));
                        break;
                    }

                    case "csg_cut":
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

                        var (positions, normals, uvs, indices) = CsgEngine.Subtract(
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
                        break;
                    }

                    case "bake_ao":
                    {
                        // Parse scene triangles and target mesh vertices
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

                        var colors = VertexAoBaker.BakeMeshAo(positions, normals, sceneTriangles);

                        window.SendWebMessage(JsonSerializer.Serialize(new
                        {
                            type = "bake_ao_response",
                            success = true,
                            targetId = root.TryGetProperty("targetId", out var tid) ? tid.GetString() : null,
                            colors
                        }));
                        break;
                    }

                    case "get_grid_texture":
                    {
                        var url = TextureBaker.GetUe5GridDataUrl();
                        window.SendWebMessage(JsonSerializer.Serialize(new
                        {
                            type = "grid_texture_response",
                            url
                        }));
                        break;
                    }

                    case "save_project":
                    {
                        var json = root.GetProperty("projectData").GetString() ?? "{}";
                        var path = ProjectStorageService.SaveProjectDialog(json);
                        window.SendWebMessage(JsonSerializer.Serialize(new
                        {
                            type = "save_project_response",
                            success = path != null,
                            path
                        }));
                        break;
                    }

                    case "load_project":
                    {
                        var (path, content) = ProjectStorageService.LoadProjectDialog();
                        window.SendWebMessage(JsonSerializer.Serialize(new
                        {
                            type = "load_project_response",
                            success = content != null,
                            path,
                            content
                        }));
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                window.SendWebMessage(JsonSerializer.Serialize(new
                {
                    type = "error",
                    message = ex.Message
                }));
            }
        });

        var indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html");
        if (File.Exists(indexPath))
        {
            window.Load(indexPath);
        }
        else
        {
            window.Load("wwwroot/index.html");
        }
        window.WaitForClose();
        return 0;
    }
}
