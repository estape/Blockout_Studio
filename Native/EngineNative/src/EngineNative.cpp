#include "../include/EngineNative.h"
#include <cmath>
#include <cstring>

#if defined(_WIN32)
  #include <intrin.h>
#endif

ENGINE_API int EngineNative_GetVersion() {
    return ENGINE_NATIVE_VERSION;
}

ENGINE_API int EngineNative_GetSimdCapabilities() {
    int caps = SIMD_NONE;
#if defined(_MSC_VER) && (defined(_M_X64) || defined(_M_IX86))
    int cpuInfo[4];
    __cpuid(cpuInfo, 1);
    if ((cpuInfo[2] & (1 << 19)) != 0) { // SSE 4.1
        caps |= SIMD_SSE41;
    }

    __cpuid(cpuInfo, 7);
    if ((cpuInfo[1] & (1 << 5)) != 0) { // AVX2
        caps |= SIMD_AVX2;
    }
    if ((cpuInfo[1] & (1 << 16)) != 0) { // AVX-512F
        caps |= SIMD_AVX512;
    }
#elif defined(__aarch64__) || defined(_M_ARM64)
    caps |= SIMD_NEON;
#endif
    return caps;
}

ENGINE_API void EngineNative_TransformPositions(
    float* vertices,
    int vertexCount,
    const float* matrix4x4
) {
    if (!vertices || vertexCount <= 0 || !matrix4x4) return;

    for (int i = 0; i < vertexCount; ++i) {
        int idx = i * 3;
        float x = vertices[idx];
        float y = vertices[idx + 1];
        float z = vertices[idx + 2];

        // 4x4 Row-major transformation: [x, y, z, 1] * Matrix
        float rx = x * matrix4x4[0] + y * matrix4x4[4] + z * matrix4x4[8]  + matrix4x4[12];
        float ry = x * matrix4x4[1] + y * matrix4x4[5] + z * matrix4x4[9]  + matrix4x4[13];
        float rz = x * matrix4x4[2] + y * matrix4x4[6] + z * matrix4x4[10] + matrix4x4[14];
        float rw = x * matrix4x4[3] + y * matrix4x4[7] + z * matrix4x4[11] + matrix4x4[15];

        if (std::abs(rw) > 1e-6f && std::abs(rw - 1.0f) > 1e-6f) {
            float invW = 1.0f / rw;
            rx *= invW;
            ry *= invW;
            rz *= invW;
        }

        vertices[idx]     = rx;
        vertices[idx + 1] = ry;
        vertices[idx + 2] = rz;
    }
}

ENGINE_API void EngineNative_ComputeNormals(
    const float* positions,
    int vertexCount,
    const int* indices,
    int indexCount,
    float* outNormals
) {
    if (!positions || vertexCount <= 0 || !outNormals) return;

    std::memset(outNormals, 0, sizeof(float) * vertexCount * 3);

    if (indices && indexCount >= 3) {
        for (int i = 0; i < indexCount; i += 3) {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            if (i0 < 0 || i0 >= vertexCount ||
                i1 < 0 || i1 >= vertexCount ||
                i2 < 0 || i2 >= vertexCount) continue;

            float p0x = positions[i0 * 3], p0y = positions[i0 * 3 + 1], p0z = positions[i0 * 3 + 2];
            float p1x = positions[i1 * 3], p1y = positions[i1 * 3 + 1], p1z = positions[i1 * 3 + 2];
            float p2x = positions[i2 * 3], p2y = positions[i2 * 3 + 1], p2z = positions[i2 * 3 + 2];

            // Edge vectors
            float e1x = p1x - p0x, e1y = p1y - p0y, e1z = p1z - p0z;
            float e2x = p2x - p0x, e2y = p2y - p0y, e2z = p2z - p0z;

            // Cross product
            float nx = e1y * e2z - e1z * e2y;
            float ny = e1z * e2x - e1x * e2z;
            float nz = e1x * e2y - e1y * e2x;

            outNormals[i0 * 3]     += nx; outNormals[i0 * 3 + 1] += ny; outNormals[i0 * 3 + 2] += nz;
            outNormals[i1 * 3]     += nx; outNormals[i1 * 3 + 1] += ny; outNormals[i1 * 3 + 2] += nz;
            outNormals[i2 * 3]     += nx; outNormals[i2 * 3 + 1] += ny; outNormals[i2 * 3 + 2] += nz;
        }
    } else {
        // Non-indexed triangle list
        for (int i = 0; i < vertexCount; i += 3) {
            if (i + 2 >= vertexCount) break;
            float p0x = positions[i * 3],     p0y = positions[i * 3 + 1],     p0z = positions[i * 3 + 2];
            float p1x = positions[(i+1) * 3], p1y = positions[(i+1) * 3 + 1], p1z = positions[(i+1) * 3 + 2];
            float p2x = positions[(i+2) * 3], p2y = positions[(i+2) * 3 + 1], p2z = positions[(i+2) * 3 + 2];

            float e1x = p1x - p0x, e1y = p1y - p0y, e1z = p1z - p0z;
            float e2x = p2x - p0x, e2y = p2y - p0y, e2z = p2z - p0z;

            float nx = e1y * e2z - e1z * e2y;
            float ny = e1z * e2x - e1x * e2z;
            float nz = e1x * e2y - e1y * e2x;

            for (int k = 0; k < 3; ++k) {
                outNormals[(i + k) * 3]     = nx;
                outNormals[(i + k) * 3 + 1] = ny;
                outNormals[(i + k) * 3 + 2] = nz;
            }
        }
    }

    // Normalize all vectors
    for (int i = 0; i < vertexCount; ++i) {
        int idx = i * 3;
        float nx = outNormals[idx];
        float ny = outNormals[idx + 1];
        float nz = outNormals[idx + 2];
        float lenSq = nx * nx + ny * ny + nz * nz;
        if (lenSq > 1e-8f) {
            float invLen = 1.0f / std::sqrt(lenSq);
            outNormals[idx]     = nx * invLen;
            outNormals[idx + 1] = ny * invLen;
            outNormals[idx + 2] = nz * invLen;
        } else {
            outNormals[idx]     = 0.0f;
            outNormals[idx + 1] = 1.0f;
            outNormals[idx + 2] = 0.0f;
        }
    }
}

ENGINE_API float EngineNative_RayTriangleIntersect(
    float origX, float origY, float origZ,
    float dirX, float dirY, float dirZ,
    float v0x, float v0y, float v0z,
    float v1x, float v1y, float v1z,
    float v2x, float v2y, float v2z
) {
    const float EPSILON = 1e-7f;

    // Edge vectors
    float e1x = v1x - v0x, e1y = v1y - v0y, e1z = v1z - v0z;
    float e2x = v2x - v0x, e2y = v2y - v0y, e2z = v2z - v0z;

    // Cross product: dir x e2
    float px = dirY * e2z - dirZ * e2y;
    float py = dirZ * e2x - dirX * e2z;
    float pz = dirX * e2y - dirY * e2x;

    float det = e1x * px + e1y * py + e1z * pz;
    if (det > -EPSILON && det < EPSILON) return -1.0f;

    float invDet = 1.0f / det;

    // Vector from v0 to ray origin
    float tx = origX - v0x, ty = origY - v0y, tz = origZ - v0z;
    float u = (tx * px + ty * py + tz * pz) * invDet;
    if (u < 0.0f || u > 1.0f) return -1.0f;

    // Cross product: t x e1
    float qx = ty * e1z - tz * e1y;
    float qy = tz * e1x - tx * e1z;
    float qz = tx * e1y - ty * e1x;

    float v = (dirX * qx + dirY * qy + dirZ * qz) * invDet;
    if (v < 0.0f || (u + v) > 1.0f) return -1.0f;

    float t = (e2x * qx + e2y * qy + e2z * qz) * invDet;
    return (t > EPSILON) ? t : -1.0f;
}
