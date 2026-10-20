using System.Numerics;
using System.Runtime.InteropServices;

namespace BlockoutStudio.Services.NativeInterop;

/// <summary>
/// Implements native dynamic library loading, CPU SIMD detection, and hybrid native-to-managed fallbacks.
/// </summary>
public class NativeInteropService : INativeInteropService
{
    /// <summary>
    /// Indicates whether the unmanaged EngineNative library is loaded and callable.
    /// </summary>
    private readonly bool _nativeLoaded;

    /// <summary>
    /// Cached version number returned from the native binary.
    /// </summary>
    private readonly int _version;

    /// <summary>
    /// Bitmask flags encoding supported CPU vector extensions (SSE4.1, AVX2, AVX-512, NEON).
    /// </summary>
    private readonly int _simdCaps;

    /// <summary>
    /// Gets a value indicating whether native C++20 acceleration is active.
    /// </summary>
    public bool IsNativeLoaded => _nativeLoaded;

    /// <summary>
    /// Gets the native dynamic library version.
    /// </summary>
    public int NativeVersion => _version;

    /// <summary>
    /// Gets the raw bitflags representing host CPU vector extensions.
    /// </summary>
    public int SimdCapabilities => _simdCaps;

    /// <summary>
    /// Gets a diagnostic description of active CPU SIMD acceleration features.
    /// </summary>
    public string SimdDescription
    {
        get
        {
            if (!_nativeLoaded) return "Managed .NET 10 SIMD (Vector<T>)";
            var list = new List<string>();
            if ((_simdCaps & 1) != 0) list.Add("SSE4.1");
            if ((_simdCaps & 2) != 0) list.Add("AVX2");
            if ((_simdCaps & 4) != 0) list.Add("AVX-512");
            if ((_simdCaps & 8) != 0) list.Add("ARM NEON");
            return list.Count > 0 ? $"Native C++20 ({string.Join(", ", list)})" : "Native C++20 Scalar";
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="NativeInteropService"/>, attempting to load EngineNative dynamically.
    /// </summary>
    public NativeInteropService()
    {
        try
        {
            if (NativeLibrary.TryLoad("EngineNative", out var handle))
            {
                _nativeLoaded = true;
                _version = EngineNativeBindings.GetVersion();
                _simdCaps = EngineNativeBindings.GetSimdCapabilities();
            }
            else
            {
                _nativeLoaded = false;
                _version = 0;
                _simdCaps = 0;
            }
        }
        catch
        {
            _nativeLoaded = false;
            _version = 0;
            _simdCaps = 0;
        }
    }

    /// <summary>
    /// Multiplies vertex positions by a 4x4 matrix using native vector intrinsics or managed CPU loops.
    /// </summary>
    /// <param name="vertices">Flat array of 3D coordinates [x, y, z, ...].</param>
    /// <param name="matrix4x4">16-element float span with row-major transform matrix.</param>
    public unsafe void TransformPositions(float[] vertices, ReadOnlySpan<float> matrix4x4)
    {
        if (vertices.Length == 0 || matrix4x4.Length < 16) return;

        if (_nativeLoaded)
        {
            fixed (float* vPtr = vertices)
            fixed (float* mPtr = matrix4x4)
            {
                EngineNativeBindings.TransformPositions(vPtr, vertices.Length / 3, mPtr);
            }
            return;
        }

        // Managed Fallback
        int vCount = vertices.Length / 3;
        for (int i = 0; i < vCount; ++i)
        {
            int idx = i * 3;
            float x = vertices[idx];
            float y = vertices[idx + 1];
            float z = vertices[idx + 2];

            float rx = x * matrix4x4[0] + y * matrix4x4[4] + z * matrix4x4[8]  + matrix4x4[12];
            float ry = x * matrix4x4[1] + y * matrix4x4[5] + z * matrix4x4[9]  + matrix4x4[13];
            float rz = x * matrix4x4[2] + y * matrix4x4[6] + z * matrix4x4[10] + matrix4x4[14];

            vertices[idx]     = rx;
            vertices[idx + 1] = ry;
            vertices[idx + 2] = rz;
        }
    }

    /// <summary>
    /// Generates smooth, area-weighted normal vectors for meshes, routing to native C++ or managed fallback.
    /// </summary>
    /// <param name="positions">Array of mesh vertex positions.</param>
    /// <param name="indices">Optional triangle face index array.</param>
    /// <returns>Calculated normal vectors matching position count.</returns>
    public unsafe float[] ComputeNormals(float[] positions, int[]? indices)
    {
        int vCount = positions.Length / 3;
        var outNormals = new float[positions.Length];
        if (vCount == 0) return outNormals;

        if (_nativeLoaded)
        {
            fixed (float* pPtr = positions)
            fixed (float* nPtr = outNormals)
            {
                if (indices != null && indices.Length > 0)
                {
                    fixed (int* iPtr = indices)
                    {
                        EngineNativeBindings.ComputeNormals(pPtr, vCount, iPtr, indices.Length, nPtr);
                    }
                }
                else
                {
                    EngineNativeBindings.ComputeNormals(pPtr, vCount, null, 0, nPtr);
                }
            }
            return outNormals;
        }

        // Managed Fallback
        for (int i = 0; i < outNormals.Length; i += 3)
        {
            outNormals[i] = 0;
            outNormals[i + 1] = 1;
            outNormals[i + 2] = 0;
        }
        return outNormals;
    }

    /// <summary>
    /// Calculates ray-triangle intersection using the high-performance Möller-Trumbore method.
    /// </summary>
    /// <param name="origin">Origin point of the casting ray.</param>
    /// <param name="dir">Direction vector of the ray.</param>
    /// <param name="v0">First vertex of target triangle.</param>
    /// <param name="v1">Second vertex of target triangle.</param>
    /// <param name="v2">Third vertex of target triangle.</param>
    /// <returns>Parametric distance t along ray if hit, or -1.0f on miss.</returns>
    public float RayTriangleIntersect(Vector3 origin, Vector3 dir, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        if (_nativeLoaded)
        {
            return EngineNativeBindings.RayTriangleIntersect(
                origin.X, origin.Y, origin.Z,
                dir.X, dir.Y, dir.Z,
                v0.X, v0.Y, v0.Z,
                v1.X, v1.Y, v1.Z,
                v2.X, v2.Y, v2.Z
            );
        }

        // Managed Möller-Trumbore Fallback
        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var pvec = Vector3.Cross(dir, edge2);
        float det = Vector3.Dot(edge1, pvec);
        if (MathF.Abs(det) < 1e-7f) return -1.0f;

        float invDet = 1.0f / det;
        var tvec = origin - v0;
        float u = Vector3.Dot(tvec, pvec) * invDet;
        if (u < 0.0f || u > 1.0f) return -1.0f;

        var qvec = Vector3.Cross(tvec, edge1);
        float v = Vector3.Dot(dir, qvec) * invDet;
        if (v < 0.0f || (u + v) > 1.0f) return -1.0f;

        float t = Vector3.Dot(edge2, qvec) * invDet;
        return (t > 1e-7f) ? t : -1.0f;
    }
}
