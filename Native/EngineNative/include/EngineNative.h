#pragma once

#ifdef _WIN32
  #define ENGINE_API extern "C" __declspec(dllexport)
#else
  #define ENGINE_API extern "C" __attribute__((visibility("default")))
#endif

// Version 1.0.0
#define ENGINE_NATIVE_VERSION 100

// SIMD capability bitflags
#define SIMD_NONE   0
#define SIMD_SSE41  (1 << 0)
#define SIMD_AVX2   (1 << 1)
#define SIMD_AVX512 (1 << 2)
#define SIMD_NEON   (1 << 3)

ENGINE_API int EngineNative_GetVersion();
ENGINE_API int EngineNative_GetSimdCapabilities();

ENGINE_API void EngineNative_TransformPositions(
    float* vertices,
    int vertexCount,
    const float* matrix4x4
);

ENGINE_API void EngineNative_ComputeNormals(
    const float* positions,
    int vertexCount,
    const int* indices,
    int indexCount,
    float* outNormals
);

ENGINE_API float EngineNative_RayTriangleIntersect(
    float origX, float origY, float origZ,
    float dirX, float dirY, float dirZ,
    float v0x, float v0y, float v0z,
    float v1x, float v1y, float v1z,
    float v2x, float v2y, float v2z
);
