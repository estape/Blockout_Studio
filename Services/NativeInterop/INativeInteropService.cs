using System.Numerics;

namespace BlockoutStudio.Services.NativeInterop;

/// <summary>
/// Service abstraction managing lifecycle, fallback routing, and execution for native C++20 SIMD routines.
/// </summary>
public interface INativeInteropService
{
    /// <summary>
    /// Gets whether the unmanaged dynamic library (EngineNative) was successfully located and loaded.
    /// </summary>
    bool IsNativeLoaded { get; }

    /// <summary>
    /// Gets the integer version number reported by the native library binary, or 0 if unloaded.
    /// </summary>
    int NativeVersion { get; }

    /// <summary>
    /// Gets the hardware SIMD capability bitflags reported by the host CPU.
    /// </summary>
    int SimdCapabilities { get; }

    /// <summary>
    /// Gets a human-readable string summarizing the active SIMD instruction sets (e.g., SSE4.1, AVX2, AVX-512).
    /// </summary>
    string SimdDescription { get; }

    /// <summary>
    /// Transforms the provided 3D vertex positions in-place using a 4x4 matrix, routing to native SIMD or managed fallback.
    /// </summary>
    /// <param name="vertices">Array of vertex coordinates [x, y, z, ...].</param>
    /// <param name="matrix4x4">16-element span holding the 4x4 row-major transformation matrix.</param>
    void TransformPositions(float[] vertices, ReadOnlySpan<float> matrix4x4);

    /// <summary>
    /// Computes normalized vertex surface normals across a triangle mesh.
    /// </summary>
    /// <param name="positions">Vertex position coordinates [x, y, z, ...].</param>
    /// <param name="indices">Optional triangle index buffer; if null, vertices are treated as sequential faces.</param>
    /// <returns>Newly allocated float array of normal vectors [nx, ny, nz, ...] matching position count.</returns>
    float[] ComputeNormals(float[] positions, int[]? indices);

    /// <summary>
    /// Computes intersection distance from a 3D ray to a triangle.
    /// </summary>
    /// <param name="origin">Ray origin point in 3D world space.</param>
    /// <param name="dir">Normalized ray direction vector.</param>
    /// <param name="v0">First vertex of triangle.</param>
    /// <param name="v1">Second vertex of triangle.</param>
    /// <param name="v2">Third vertex of triangle.</param>
    /// <returns>Parametric distance t along ray direction if hit; negative float (-1.0f) if miss or parallel.</returns>
    float RayTriangleIntersect(Vector3 origin, Vector3 dir, Vector3 v0, Vector3 v1, Vector3 v2);
}
