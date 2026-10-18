using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using UE5BlockoutStudio.Services.Assets;
using UE5BlockoutStudio.Services.Graphics;

namespace UE5BlockoutStudio.Services.Geometry;

public class CompiledMeshArtifact
{
    public string Guid { get; set; } = "";
    public string Name { get; set; } = "";
    public int VertexCount { get; set; }
    public int IndexCount { get; set; }
    public byte[] BinaryData { get; set; } = [];
    public Vector3 BoundingMin { get; set; }
    public Vector3 BoundingMax { get; set; }
    public Vector3 Center { get; set; }
}

public interface IObjUsdPipelineService
{
    CompiledMeshArtifact? GetOrCompileMeshBinary(string assetPathOrGuid, string projectRoot);
    string SaveBlockoutAsObj(string projectRoot, string blockName, float[] positions, float[]? normals, float[]? uvs, int[]? indices, bool bakeAo = false, ObjPivotMode pivot = ObjPivotMode.BottomCenter);
    string SaveUsdStage(string projectRoot, string stageName, UsdStage stage);
    UsdStage LoadUsdStage(string stagePathOrGuid, string projectRoot);
}

public class ObjUsdPipelineService : IObjUsdPipelineService
{
    private readonly ObjNativeProcessor _objProcessor;
    private readonly IUsdStageService _usdService;

    public ObjUsdPipelineService(IUsdStageService usdService)
    {
        _objProcessor = new ObjNativeProcessor();
        _usdService = usdService;
    }

    public ObjUsdPipelineService() : this(new UsdStageService())
    {
    }

    public CompiledMeshArtifact? GetOrCompileMeshBinary(string assetPathOrGuid, string projectRoot)
    {
        string? resolvedFilePath = null;

        // Try direct file path
        if (File.Exists(assetPathOrGuid))
        {
            resolvedFilePath = assetPathOrGuid;
        }
        else
        {
            // Try inside projectRoot
            var combined = Path.Combine(projectRoot, assetPathOrGuid.TrimStart('/', '\\'));
            if (File.Exists(combined))
            {
                resolvedFilePath = combined;
            }
            else
            {
                // Try finding by GUID or virtual path in AssetRegistry
                var regPath = Path.Combine(projectRoot, "Library", "AssetRegistry.json");
                if (File.Exists(regPath))
                {
                    try
                    {
                        var json = File.ReadAllText(regPath);
                        var reg = System.Text.Json.JsonSerializer.Deserialize<AssetRegistryData>(json);
                        var match = reg?.Assets.FirstOrDefault(a => a.Guid == assetPathOrGuid || a.VirtualPath == assetPathOrGuid);
                        if (match != null)
                        {
                            var candidate = Path.Combine(projectRoot, match.RelativeDiskPath);
                            if (File.Exists(candidate)) resolvedFilePath = candidate;
                        }
                    }
                    catch { }
                }
            }
        }

        if (resolvedFilePath == null || !File.Exists(resolvedFilePath)) return null;

        var ext = Path.GetExtension(resolvedFilePath).ToLowerInvariant();
        var cacheDir = Path.Combine(projectRoot, "Library", "ArtifactCache");
        if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

        var fileHash = AssetMeta.ComputeFileSha256(resolvedFilePath);
        var cacheFile = Path.Combine(cacheDir, $"{fileHash}.mesh.bin");

        // Check if fresh cache exists
        if (File.Exists(cacheFile))
        {
            try
            {
                var cachedBytes = File.ReadAllBytes(cacheFile);
                if (cachedBytes.Length >= 16 && BitConverter.ToUInt32(cachedBytes, 0) == 0x474E4542) // "BENG"
                {
                    int vCount = BitConverter.ToInt32(cachedBytes, 8);
                    int iCount = BitConverter.ToInt32(cachedBytes, 12);
                    return new CompiledMeshArtifact
                    {
                        Name = Path.GetFileNameWithoutExtension(resolvedFilePath),
                        VertexCount = vCount,
                        IndexCount = iCount,
                        BinaryData = cachedBytes
                    };
                }
            }
            catch { }
        }

        // Compile from raw file
        ObjMeshResult meshData;
        if (ext == ".obj")
        {
            var text = File.ReadAllText(resolvedFilePath);
            meshData = _objProcessor.Parse(text, ObjCoordinateConversion.Raw, ObjPivotMode.WorldOrigin);
        }
        else if (ext is ".usd" or ".usda")
        {
            var text = File.ReadAllText(resolvedFilePath);
            var stage = _usdService.ParseUsda(text);
            meshData = ConvertStageToMesh(stage);
        }
        else
        {
            return null;
        }

        // Pack into binary artifact
        var packed = PackMeshBinary(meshData);

        try
        {
            File.WriteAllBytes(cacheFile, packed);
        }
        catch { }

        return new CompiledMeshArtifact
        {
            Name = meshData.Name,
            VertexCount = meshData.Positions.Length / 3,
            IndexCount = meshData.Indices.Length,
            BinaryData = packed,
            BoundingMin = meshData.BoundingMin,
            BoundingMax = meshData.BoundingMax,
            Center = meshData.Center
        };
    }

    private static byte[] PackMeshBinary(ObjMeshResult mesh)
    {
        int vCount = mesh.Positions.Length / 3;
        int iCount = mesh.Indices.Length;

        // Header:
        // 4 bytes: Magic 'BENG' (0x474E4542)
        // 4 bytes: Version (1)
        // 4 bytes: VertexCount
        // 4 bytes: IndexCount
        // Payload:
        // Positions: vCount * 3 * 4 bytes
        // Normals: vCount * 3 * 4 bytes
        // UVs: vCount * 2 * 4 bytes
        // Colors: vCount * 3 * 4 bytes
        // Indices: iCount * 4 bytes

        int payloadSize = 16 +
            (vCount * 3 * 4) +
            (vCount * 3 * 4) +
            (vCount * 2 * 4) +
            (vCount * 3 * 4) +
            (iCount * 4);

        var buffer = new byte[payloadSize];
        int offset = 0;

        // Header
        BinaryPrimitivesWrite(buffer, ref offset, 0x474E4542); // "BENG"
        BinaryPrimitivesWrite(buffer, ref offset, 1);          // Version
        BinaryPrimitivesWrite(buffer, ref offset, vCount);
        BinaryPrimitivesWrite(buffer, ref offset, iCount);

        // Positions
        for (int i = 0; i < mesh.Positions.Length; i++)
            BinaryPrimitivesWrite(buffer, ref offset, mesh.Positions[i]);

        // Normals
        for (int i = 0; i < vCount * 3; i++)
        {
            float n = (mesh.Normals != null && i < mesh.Normals.Length) ? mesh.Normals[i] : 0f;
            BinaryPrimitivesWrite(buffer, ref offset, n);
        }

        // UVs
        for (int i = 0; i < vCount * 2; i++)
        {
            float u = (mesh.UVs != null && i < mesh.UVs.Length) ? mesh.UVs[i] : 0f;
            BinaryPrimitivesWrite(buffer, ref offset, u);
        }

        // Colors
        for (int i = 0; i < vCount * 3; i++)
        {
            float c = (mesh.Colors != null && i < mesh.Colors.Length) ? mesh.Colors[i] : 1f;
            BinaryPrimitivesWrite(buffer, ref offset, c);
        }

        // Indices
        for (int i = 0; i < iCount; i++)
        {
            uint idx = mesh.Indices[i];
            BinaryPrimitivesWrite(buffer, ref offset, (int)idx);
        }

        return buffer;
    }

    private static void BinaryPrimitivesWrite(byte[] dest, ref int offset, int val)
    {
        BitConverter.TryWriteBytes(dest.AsSpan(offset, 4), val);
        offset += 4;
    }

    private static void BinaryPrimitivesWrite(byte[] dest, ref int offset, float val)
    {
        BitConverter.TryWriteBytes(dest.AsSpan(offset, 4), val);
        offset += 4;
    }

    private ObjMeshResult ConvertStageToMesh(UsdStage stage)
    {
        // Simple synthetic representation of stage prims for viewport preview
        var positions = new List<float>();
        var indices = new List<uint>();

        foreach (var prim in stage.RootPrims)
        {
            uint baseIdx = (uint)(positions.Count / 3);
            var t = prim.Translation;
            var s = prim.Scale * 50f; // 50cm box indicator

            // Add cube centered at translation
            float[] boxVerts = [
                t.X - s.X, t.Y - s.Y, t.Z - s.Z,
                t.X + s.X, t.Y - s.Y, t.Z - s.Z,
                t.X + s.X, t.Y + s.Y, t.Z - s.Z,
                t.X - s.X, t.Y + s.Y, t.Z - s.Z,
                t.X - s.X, t.Y - s.Y, t.Z + s.Z,
                t.X + s.X, t.Y - s.Y, t.Z + s.Z,
                t.X + s.X, t.Y + s.Y, t.Z + s.Z,
                t.X - s.X, t.Y + s.Y, t.Z + s.Z,
            ];
            positions.AddRange(boxVerts);

            uint[] boxIdxs = [
                0,1,2, 0,2,3, 4,6,5, 4,7,6,
                0,4,5, 0,5,1, 1,5,6, 1,6,2,
                2,6,7, 2,7,3, 3,7,4, 3,4,0
            ];
            foreach (var idx in boxIdxs) indices.Add(baseIdx + idx);
        }

        return new ObjMeshResult
        {
            Name = stage.StageName,
            Positions = positions.ToArray(),
            Normals = new float[positions.Count],
            UVs = new float[(positions.Count / 3) * 2],
            Colors = new float[positions.Count],
            Indices = indices.ToArray()
        };
    }

    public string SaveBlockoutAsObj(
        string projectRoot,
        string blockName,
        float[] positions,
        float[]? normals,
        float[]? uvs,
        int[]? indices,
        bool bakeAo = false,
        ObjPivotMode pivot = ObjPivotMode.BottomCenter)
    {
        var model = new ModelData { SceneName = blockName };
        var part = new ModelMeshPart
        {
            Name = blockName,
            MaterialName = "M_Prototype",
            Positions = positions,
            Normals = normals ?? new float[positions.Length],
            UVs = uvs ?? new float[(positions.Length / 3) * 2],
            Indices = indices
        };
        model.Parts.Add(part);

        var objText = _objProcessor.ExportToObjString(model, includeAoColors: true, pivot: pivot);

        var meshesDir = Path.Combine(projectRoot, "Assets", "Meshes");
        if (!Directory.Exists(meshesDir)) Directory.CreateDirectory(meshesDir);

        var targetFile = Path.Combine(meshesDir, $"{blockName}.obj");
        File.WriteAllText(targetFile, objText);

        // Generate .meta sidecar
        AssetMeta.LoadOrCreate(targetFile);

        return targetFile;
    }

    public string SaveUsdStage(string projectRoot, string stageName, UsdStage stage)
    {
        var usdaText = _usdService.ExportToUsdaString(stage);
        var scenesDir = Path.Combine(projectRoot, "Assets", "Scenes");
        if (!Directory.Exists(scenesDir)) Directory.CreateDirectory(scenesDir);

        var targetFile = Path.Combine(scenesDir, $"{stageName}.usda");
        File.WriteAllText(targetFile, usdaText);

        AssetMeta.LoadOrCreate(targetFile);
        return targetFile;
    }

    public UsdStage LoadUsdStage(string stagePathOrGuid, string projectRoot)
    {
        string fullPath = File.Exists(stagePathOrGuid) ? stagePathOrGuid : Path.Combine(projectRoot, stagePathOrGuid);
        if (File.Exists(fullPath))
        {
            var text = File.ReadAllText(fullPath);
            return _usdService.ParseUsda(text);
        }
        return _usdService.CreateStage("NewStage");
    }
}
