using System.Numerics;

namespace BlockoutStudio.Services.NativeInterop;

public interface INativeInteropService
{
    bool IsNativeLoaded { get; }
    int NativeVersion { get; }
    int SimdCapabilities { get; }
    string SimdDescription { get; }

    void TransformPositions(float[] vertices, ReadOnlySpan<float> matrix4x4);
    float[] ComputeNormals(float[] positions, int[]? indices);
    float RayTriangleIntersect(Vector3 origin, Vector3 dir, Vector3 v0, Vector3 v1, Vector3 v2);
}
