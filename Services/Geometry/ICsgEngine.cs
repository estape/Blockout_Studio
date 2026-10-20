namespace BlockoutStudio.Services.Geometry;

/// <summary>
/// Computational Solid Geometry (CSG) interface for exact 3D boolean operations on polygonal meshes.
/// </summary>
public interface ICsgEngine
{
    /// <summary>
    /// Performs exact 3D boolean subtraction (Mesh A minus Mesh B) to carve openings, niches, and recesses.
    /// </summary>
    /// <param name="subPos">Target subject mesh vertex positions [x, y, z, ...].</param>
    /// <param name="subNorm">Optional target mesh vertex normal vectors.</param>
    /// <param name="subUV">Optional target mesh texture coordinates [u, v, ...].</param>
    /// <param name="subIdx">Optional target mesh triangle index buffer.</param>
    /// <param name="cutPos">Cutting brush mesh vertex positions [x, y, z, ...].</param>
    /// <param name="cutNorm">Optional cutter mesh vertex normal vectors.</param>
    /// <param name="cutUV">Optional cutter mesh texture coordinates.</param>
    /// <param name="cutIdx">Optional cutter mesh triangle index buffer.</param>
    /// <returns>A tuple of (positions, normals, uvs, indices) representing the resulting watertight polygonal mesh.</returns>
    (float[] positions, float[] normals, float[] uvs, int[] indices) Subtract(
        float[] subPos, float[]? subNorm, float[]? subUV, int[]? subIdx,
        float[] cutPos, float[]? cutNorm, float[]? cutUV, int[]? cutIdx);
}
