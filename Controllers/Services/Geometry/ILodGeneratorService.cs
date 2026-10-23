namespace BlockoutStudio.Services.Geometry;

/// <summary>
/// Represents geometry data for a single Level of Detail (LOD) tier.
/// </summary>
public class LodLevelData
{
    /// <summary>
    /// Discrete level index (0 = 100% full detail, 1 = ~50%, 2 = ~20%).
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Target percentage ratio of triangles compared to LOD0 (e.g., 1.0, 0.5, 0.2).
    /// </summary>
    public float TargetPercentage { get; set; }

    /// <summary>
    /// Flat array of 3D vertex positions [x0, y0, z0, x1, y1, z1, ...].
    /// </summary>
    public float[] Positions { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Flat array of vertex normal vectors [nx0, ny0, nz0, nx1, ny1, nz1, ...].
    /// </summary>
    public float[] Normals { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Optional flat array of texture coordinates [u0, v0, u1, v1, ...].
    /// </summary>
    public float[]? Uvs { get; set; }

    /// <summary>
    /// Triangle index buffer defining triangle topology.
    /// </summary>
    public int[] Indices { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Total count of triangles in this LOD mesh.
    /// </summary>
    public int TriangleCount => Indices.Length / 3;

    /// <summary>
    /// Total count of 3D vertices in this LOD mesh.
    /// </summary>
    public int VertexCount => Positions.Length / 3;

    /// <summary>
    /// Effective reduction ratio achieved relative to original index count.
    /// </summary>
    public float ReductionRatio { get; set; } = 1.0f;
}

/// <summary>
/// Container holding a full hierarchy of generated LOD levels (LOD0, LOD1, LOD2).
/// </summary>
public class LodMeshSet
{
    /// <summary>Original unreduced source mesh (100% triangles).</summary>
    public LodLevelData Lod0 { get; set; } = new();

    /// <summary>First reduction tier mesh (approx 50% triangles).</summary>
    public LodLevelData Lod1 { get; set; } = new();

    /// <summary>Second reduction tier mesh (approx 20% triangles).</summary>
    public LodLevelData Lod2 { get; set; } = new();

    /// <summary>Total processing duration in milliseconds.</summary>
    public double ElapsedMs { get; set; }
}

/// <summary>
/// Service interface for generating hierarchical Levels of Detail (LODs) from mesh geometry.
/// </summary>
public interface ILodGeneratorService
{
    /// <summary>
    /// Generates LOD0, LOD1, and LOD2 levels for an input triangle mesh.
    /// </summary>
    /// <param name="positions">Flat vertex position buffer (stride 3).</param>
    /// <param name="normals">Flat vertex normal buffer (stride 3).</param>
    /// <param name="uvs">Optional flat vertex UV buffer (stride 2).</param>
    /// <param name="indices">Triangle index buffer.</param>
    /// <returns>A <see cref="LodMeshSet"/> containing all generated LOD levels.</returns>
    LodMeshSet GenerateLods(float[] positions, float[] normals, float[]? uvs, int[] indices);

    /// <summary>
    /// Simplifies a mesh to a specified target percentage ratio.
    /// </summary>
    /// <param name="positions">Flat vertex position buffer (stride 3).</param>
    /// <param name="normals">Flat vertex normal buffer (stride 3).</param>
    /// <param name="uvs">Optional flat vertex UV buffer (stride 2).</param>
    /// <param name="indices">Triangle index buffer.</param>
    /// <param name="targetRatio">Target triangle fraction (e.g. 0.5 for 50%).</param>
    /// <param name="level">Target LOD level identifier.</param>
    /// <returns>Simplified <see cref="LodLevelData"/>.</returns>
    LodLevelData SimplifyMesh(float[] positions, float[] normals, float[]? uvs, int[] indices, float targetRatio, int level);
}
