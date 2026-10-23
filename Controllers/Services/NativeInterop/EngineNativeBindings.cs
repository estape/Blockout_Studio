using System.Runtime.InteropServices;

namespace BlockoutStudio.Services.NativeInterop;

/// <summary>
/// Source-generated P/Invoke bindings to the native C++20 shared library (EngineNative.dll / libEngineNative.so).
/// Provides zero-allocation interop for hardware SIMD capabilities, affine vertex transforms, and raycasting.
/// </summary>
public static unsafe partial class EngineNativeBindings
{
    /// <summary>
    /// Name of the native dynamic library without platform-specific extensions or prefixes.
    /// </summary>
    private const string LibraryName = "EngineNative";

    /// <summary>
    /// Queries the ABI version number exported by the native dynamic library.
    /// </summary>
    /// <returns>Integer version number matching ENGINE_NATIVE_VERSION (e.g. 100 for 1.0.0).</returns>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_GetVersion")]
    public static partial int GetVersion();

    /// <summary>
    /// Probes the host CPU's instruction set extensions via hardware CPUID registers.
    /// </summary>
    /// <returns>Bitfield containing detected SIMD flags (SSE4.1, AVX2, AVX-512, NEON).</returns>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_GetSimdCapabilities")]
    public static partial int GetSimdCapabilities();

    /// <summary>
    /// In-place multiplies an array of 3D floating-point vertex positions by a 4x4 row-major transform matrix.
    /// </summary>
    /// <param name="vertices">Unmanaged pointer to contiguous buffer of float triplets [x, y, z].</param>
    /// <param name="vertexCount">Number of 3D vertices to transform.</param>
    /// <param name="matrix4x4">Pointer to 16-element float array holding the 4x4 transform matrix.</param>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_TransformPositions")]
    public static partial void TransformPositions(float* vertices, int vertexCount, float* matrix4x4);

    /// <summary>
    /// Generates smooth, area-weighted vertex surface normals for indexed or non-indexed 3D triangle lists.
    /// </summary>
    /// <param name="positions">Pointer to contiguous vertex positions buffer.</param>
    /// <param name="vertexCount">Total number of vertices in positions buffer.</param>
    /// <param name="indices">Optional pointer to index buffer (may be null for raw triangle lists).</param>
    /// <param name="indexCount">Total number of triangle vertex indices.</param>
    /// <param name="outNormals">Pointer to pre-allocated float buffer to receive calculated normal vectors.</param>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_ComputeNormals")]
    public static partial void ComputeNormals(float* positions, int vertexCount, int* indices, int indexCount, float* outNormals);

    /// <summary>
    /// Computes high-precision Möller-Trumbore ray-triangle intersection on the native core.
    /// </summary>
    /// <param name="origX">Ray origin X.</param>
    /// <param name="origY">Ray origin Y.</param>
    /// <param name="origZ">Ray origin Z.</param>
    /// <param name="dirX">Ray direction X.</param>
    /// <param name="dirY">Ray direction Y.</param>
    /// <param name="dirZ">Ray direction Z.</param>
    /// <param name="v0x">Triangle vertex 0 X.</param>
    /// <param name="v0y">Triangle vertex 0 Y.</param>
    /// <param name="v0z">Triangle vertex 0 Z.</param>
    /// <param name="v1x">Triangle vertex 1 X.</param>
    /// <param name="v1y">Triangle vertex 1 Y.</param>
    /// <param name="v1z">Triangle vertex 1 Z.</param>
    /// <param name="v2x">Triangle vertex 2 X.</param>
    /// <param name="v2y">Triangle vertex 2 Y.</param>
    /// <param name="v2z">Triangle vertex 2 Z.</param>
    /// <returns>Distance along ray direction if hit, or -1.0f on miss.</returns>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_RayTriangleIntersect")]
    public static partial float RayTriangleIntersect(
        float origX, float origY, float origZ,
        float dirX, float dirY, float dirZ,
        float v0x, float v0y, float v0z,
        float v1x, float v1y, float v1z,
        float v2x, float v2y, float v2z
    );

    /// <summary>
    /// Struct representing real-time rendering statistics and viewport state.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRendererStats
    {
        public uint CurrentFps;
        public float FrameTimeMs;
        public uint SwapchainWidth;
        public uint SwapchainHeight;
        public uint ViewportX;
        public uint ViewportY;
        public uint ViewportWidth;
        public uint ViewportHeight;
    }

    /// <summary>
    /// Initializes the native Vulkan swapchain and isolated rendering thread for the specified window handle.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_InitRenderer")]
    public static partial int InitRenderer(IntPtr nativeWindowHandle, uint initialWidth, uint initialHeight);

    /// <summary>
    /// Gracefully stops the isolated rendering thread and releases swapchain resources.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_ShutdownRenderer")]
    public static partial void ShutdownRenderer();

    /// <summary>
    /// Resizes the native Vulkan swapchain to match new outer window dimensions.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_ResizeSwapchain")]
    public static partial void ResizeSwapchain(uint newWidth, uint newHeight);

    /// <summary>
    /// Dynamically adjusts the viewport and scissor rectangle without recreating the swapchain.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_SetViewportScissor")]
    public static partial void SetViewportScissor(int x, int y, uint width, uint height);

    /// <summary>
    /// Sets the target rendering framerate (e.g. 60, 120, 0 for uncapped).
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_SetTargetFps")]
    public static partial void SetTargetFps(uint targetFps);

    /// <summary>
    /// Pauses or resumes the isolated rendering loop.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_SetRendererPaused")]
    public static partial void SetRendererPaused(int isPaused);

    /// <summary>
    /// Queries the latest rendering performance metrics and viewport dimensions.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_GetRendererStats")]
    public static partial void GetRendererStats(NativeRendererStats* outStats);

    /// <summary>
    /// Checks whether the isolated rendering thread is running.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "EngineNative_IsRendererRunning")]
    public static partial int IsRendererRunning();
}
