using System.Runtime.InteropServices;

namespace BlockoutStudio.Services.NativeInterop;

internal static unsafe partial class EngineNativeBindings
{
    private const string LibraryName = "EngineNative";

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_GetVersion")]
    public static partial int GetVersion();

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_GetSimdCapabilities")]
    public static partial int GetSimdCapabilities();

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_TransformPositions")]
    public static partial void TransformPositions(float* vertices, int vertexCount, float* matrix4x4);

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_ComputeNormals")]
    public static partial void ComputeNormals(float* positions, int vertexCount, int* indices, int indexCount, float* outNormals);

    [LibraryImport(LibraryName, EntryPoint = "EngineNative_RayTriangleIntersect")]
    public static partial float RayTriangleIntersect(
        float origX, float origY, float origZ,
        float dirX, float dirY, float dirZ,
        float v0x, float v0y, float v0z,
        float v1x, float v1y, float v1z,
        float v2x, float v2y, float v2z
    );
}
