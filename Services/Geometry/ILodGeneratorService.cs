namespace UE5BlockoutStudio.Services.Geometry;

public class LodLevelData
{
    public int Level { get; set; } // 0 = 100%, 1 = 50%, 2 = 20%
    public float TargetPercentage { get; set; } // 1.0f, 0.5f, 0.2f
    public float[] Positions { get; set; } = Array.Empty<float>();
    public float[] Normals { get; set; } = Array.Empty<float>();
    public float[]? Uvs { get; set; }
    public int[] Indices { get; set; } = Array.Empty<int>();
    public int TriangleCount => Indices.Length / 3;
    public int VertexCount => Positions.Length / 3;
    public float ReductionRatio { get; set; } = 1.0f;
}

public class LodMeshSet
{
    public LodLevelData Lod0 { get; set; } = new();
    public LodLevelData Lod1 { get; set; } = new();
    public LodLevelData Lod2 { get; set; } = new();
    public double ElapsedMs { get; set; }
}

public interface ILodGeneratorService
{
    LodMeshSet GenerateLods(float[] positions, float[] normals, float[]? uvs, int[] indices);
    LodLevelData SimplifyMesh(float[] positions, float[] normals, float[]? uvs, int[] indices, float targetRatio, int level);
}
