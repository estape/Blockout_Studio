/**
 * @file EngineNative.h
 * @brief Native C++20 hardware-accelerated SIMD computation and geometry processing header for Blockout Studio.
 *
 * This header defines the C-ABI export interface for hardware-intrinsic AVX2/AVX-512/NEON ray-triangle
 * intersection, vertex transformations, normal generation, and SIMD CPU capability probing.
 * Designed for zero-overhead P/Invoke interoperability with .NET 10 via [LibraryImport].
 */

#pragma once

#include <cstdint>

#ifdef _WIN32
  /**
   * @def ENGINE_API
   * @brief Calling convention and export linkage macro for Windows DLLs and Unix shared objects.
   */
  #define ENGINE_API extern "C" __declspec(dllexport)
#else
  /**
   * @def ENGINE_API
   * @brief Calling convention and export linkage macro for GCC/Clang dynamic shared libraries.
   */
  #define ENGINE_API extern "C" __attribute__((visibility("default")))
#endif

/**
 * @def ENGINE_NATIVE_VERSION
 * @brief Current native shared library binary interface version (encoded as integer: 100 = 1.0.0).
 */
#define ENGINE_NATIVE_VERSION 100

/**
 * @def SIMD_NONE
 * @brief Bitflag indicating no advanced vector extensions detected; standard scalar execution.
 */
#define SIMD_NONE   0

/**
 * @def SIMD_SSE41
 * @brief Bitflag indicating Intel/AMD Streaming SIMD Extensions 4.1 hardware support.
 */
#define SIMD_SSE41  (1 << 0)

/**
 * @def SIMD_AVX2
 * @brief Bitflag indicating Advanced Vector Extensions 2 (256-bit integer and floating point) support.
 */
#define SIMD_AVX2   (1 << 1)

/**
 * @def SIMD_AVX512
 * @brief Bitflag indicating AVX-512 Foundation (512-bit wide vector registers) support.
 */
#define SIMD_AVX512 (1 << 2)

/**
 * @def SIMD_NEON
 * @brief Bitflag indicating ARM NEON 128-bit SIMD hardware architecture support.
 */
#define SIMD_NEON   (1 << 3)

/**
 * @brief Queries the version of the compiled native engine library.
 * @return Integer version code corresponding to @ref ENGINE_NATIVE_VERSION (e.g., 100 for v1.0.0).
 */
ENGINE_API int EngineNative_GetVersion();

/**
 * @brief Probes CPU hardware registers at runtime via __cpuid to detect available vector instruction sets.
 * @return Bitmask combination of @ref SIMD_NONE, @ref SIMD_SSE41, @ref SIMD_AVX2, @ref SIMD_AVX512, and @ref SIMD_NEON.
 */
ENGINE_API int EngineNative_GetSimdCapabilities();

/**
 * @brief Transforms a contiguous array of 3D vertex positions in-place using a 4x4 row-major matrix.
 *
 * Performs perspective divide if the homogeneous coordinate w differs significantly from 1.0.
 *
 * @param[in,out] vertices Pointer to contiguous array of float coordinates arranged as [x, y, z, x, y, z, ...].
 * @param[in] vertexCount Number of vertices in the buffer (total floats = vertexCount * 3).
 * @param[in] matrix4x4 Pointer to 16-element float array representing the 4x4 row-major transformation matrix.
 */
ENGINE_API void EngineNative_TransformPositions(
    float* vertices,
    int vertexCount,
    const float* matrix4x4
);

/**
 * @brief Computes smooth area-weighted vertex surface normals for indexed or non-indexed triangle meshes.
 *
 * Traverses mesh triangles, calculates cross-product face normals, accumulates them onto vertex vertices,
 * and normalizes all vectors to unit length.
 *
 * @param[in] positions Pointer to contiguous array of float coordinates [x0, y0, z0, x1, y1, z1, ...].
 * @param[in] vertexCount Total number of vertices in the mesh.
 * @param[in] indices Optional pointer to triangle index buffer (3 indices per face). If null, positions are treated as sequential triangles.
 * @param[in] indexCount Total number of indices in the index buffer (must be multiple of 3 if non-zero).
 * @param[out] outNormals Output buffer allocated by caller with capacity >= vertexCount * 3 floats.
 */
ENGINE_API void EngineNative_ComputeNormals(
    const float* positions,
    int vertexCount,
    const int* indices,
    int indexCount,
    float* outNormals
);

/**
 * @brief Tests for single-precision ray-triangle intersection using the Möller–Trumbore algorithm.
 *
 * Calculates barycentric coordinates (u, v) and ray parametric distance t.
 *
 * @param[in] origX Ray origin X component.
 * @param[in] origY Ray origin Y component.
 * @param[in] origZ Ray origin Z component.
 * @param[in] dirX Ray direction vector X component (should be normalized).
 * @param[in] dirY Ray direction vector Y component (should be normalized).
 * @param[in] dirZ Ray direction vector Z component (should be normalized).
 * @param[in] v0x Triangle vertex 0 X component.
 * @param[in] v0y Triangle vertex 0 Y component.
 * @param[in] v0z Triangle vertex 0 Z component.
 * @param[in] v1x Triangle vertex 1 X component.
 * @param[in] v1y Triangle vertex 1 Y component.
 * @param[in] v1z Triangle vertex 1 Z component.
 * @param[in] v2x Triangle vertex 2 X component.
 * @param[in] v2y Triangle vertex 2 Y component.
 * @param[in] v2z Triangle vertex 2 Z component.
 * @return Parametric distance t along ray direction if intersection occurs (t > 1e-7), or -1.0f on miss or parallel ray.
 */
ENGINE_API float EngineNative_RayTriangleIntersect(
    float origX, float origY, float origZ,
    float dirX, float dirY, float dirZ,
    float v0x, float v0y, float v0z,
    float v1x, float v1y, float v1z,
    float v2x, float v2y, float v2z
);

#pragma pack(push, 8)
/**
 * @brief Sub-rectangle bounds representing a docked viewport inside the host window.
 */
struct NativeViewportBounds {
    int32_t x;
    int32_t y;
    uint32_t width;
    uint32_t height;
};

/**
 * @brief Platform identifier for cross-platform window handle binding.
 */
enum class NativePlatformType : uint32_t {
    Unknown = 0,
    Win32   = 1,
    X11     = 2,
    Wayland = 3,
    macOS   = 4,
};

/**
 * @brief Unified cross-platform window descriptor struct passed across the C-ABI.
 * Exactly 32 bytes with 64-bit natural alignment.
 */
#pragma pack(push, 8)
struct NativeWindowDescriptor {
    NativePlatformType platformType;
    uint32_t           padding;        ///< Explicit 32-bit padding for clean 8-byte alignment
    void*              displayHandle;  ///< Win32: nullptr | X11: Display* | Wayland: wl_display*
    void*              windowHandle;   ///< Win32: HWND | X11: Window XID | Wayland: wl_surface*
    uint32_t           width;
    uint32_t           height;
};
#pragma pack(pop)

/**
 * @brief Real-time performance metrics and swapchain/scissor state.
 */
struct NativeRendererStats {
    uint32_t currentFps;
    float frameTimeMs;
    uint32_t swapchainWidth;
    uint32_t swapchainHeight;
    uint32_t viewportX;
    uint32_t viewportY;
    uint32_t viewportWidth;
    uint32_t viewportHeight;
};
#pragma pack(pop)

/**
 * @brief Initializes the Vulkan swapchain and spawns an isolated rendering thread using a cross-platform descriptor.
 * @param pDescriptor Pointer to NativeWindowDescriptor with platform, display, and window handles.
 * @return 0 on success, or non-zero error code.
 */
ENGINE_API int32_t EngineNative_InitRendererDescriptor(const NativeWindowDescriptor* pDescriptor);

/**
 * @brief Initializes the Vulkan swapchain and spawns an isolated rendering thread.
 * Backward-compatible entry point forwarding to EngineNative_InitRendererDescriptor.
 * @param nativeWindowHandle Native OS window handle (HWND on Win32, X11 Window on Linux).
 * @param initialWidth Initial swapchain width in pixels.
 * @param initialHeight Initial swapchain height in pixels.
 * @return 0 on success, or non-zero error code.
 */
ENGINE_API int32_t EngineNative_InitRenderer(void* nativeWindowHandle, uint32_t initialWidth, uint32_t initialHeight);

/**
 * @brief Gracefully terminates the isolated rendering loop and releases all Vulkan swapchain resources.
 */
ENGINE_API void EngineNative_ShutdownRenderer();

/**
 * @brief Recreates the native swapchain to match new outer window dimensions.
 * @param newWidth Updated window width in pixels.
 * @param newHeight Updated window height in pixels.
 */
ENGINE_API void EngineNative_ResizeSwapchain(uint32_t newWidth, uint32_t newHeight);

/**
 * @brief Updates the dynamic viewport and scissor rectangle for rendering without swapchain recreation.
 * @param x Viewport left coordinate in pixels.
 * @param y Viewport top coordinate in pixels.
 * @param width Viewport width in pixels.
 * @param height Viewport height in pixels.
 */
ENGINE_API void EngineNative_SetViewportScissor(int32_t x, int32_t y, uint32_t width, uint32_t height);

/**
 * @brief Configures target rendering framerate (e.g., 30, 60, 120, 144, 0 for uncapped).
 */
ENGINE_API void EngineNative_SetTargetFps(uint32_t targetFps);

/**
 * @brief Pauses or resumes the isolated rendering loop (e.g. when minimized).
 */
ENGINE_API void EngineNative_SetRendererPaused(int32_t isPaused);

/**
 * @brief Queries current rendering statistics and viewport state.
 */
ENGINE_API void EngineNative_GetRendererStats(NativeRendererStats* outStats);

/**
 * @brief Returns whether the isolated Vulkan rendering loop is currently active.
 * @return 1 if active, 0 otherwise.
 */
ENGINE_API int32_t EngineNative_IsRendererRunning();
