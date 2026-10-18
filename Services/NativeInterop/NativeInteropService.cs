using System.Numerics;
using System.Runtime.InteropServices;

namespace UE5BlockoutStudio.Services.NativeInterop;

public class NativeInteropService : INativeInteropService
{
    private readonly bool _nativeLoaded;
    private readonly int _version;
    private readonly int _simdCaps;

    public bool IsNativeLoaded => _nativeLoaded;
    public int NativeVersion => _version;
    public int SimdCapabilities => _simdCaps;

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
