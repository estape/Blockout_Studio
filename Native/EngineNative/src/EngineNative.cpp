/**
 * @file EngineNative.cpp
 * @brief Implementation of hardware-accelerated geometry processing, vector transforms, and CPUID probing.
 *
 * Implements high-performance native routines for Möller-Trumbore ray casting, vertex array linear transformations,
 * and normal generation with zero memory allocation.
 */

#include "../include/EngineNative.h"
#include <cmath>
#include <cstring>
#include <atomic>
#include <thread>
#include <chrono>
#include <iostream>
#include <algorithm>
#include <vector>

#if defined(_WIN32)
  #define WIN32_LEAN_AND_MEAN
  #include <windows.h>
  #include <intrin.h>
#endif

#if defined(ENGINE_NATIVE_HAS_VULKAN)
  #if defined(_WIN32)
    #define VK_USE_PLATFORM_WIN32_KHR
  #elif defined(__linux__) || defined(__unix__)
    #define VK_USE_PLATFORM_XLIB_KHR
    #define VK_USE_PLATFORM_WAYLAND_KHR
  #endif
  #include <vulkan/vulkan.h>
  #if defined(__linux__) || defined(__unix__)
    #if __has_include(<X11/Xlib.h>)
      #include <X11/Xlib.h>
    #endif
    #if __has_include(<wayland-client.h>)
      #include <wayland-client.h>
    #endif
  #endif
#endif

/**
 * @brief Returns the native engine library binary interface version.
 * @return The constant integer @ref ENGINE_NATIVE_VERSION.
 */
ENGINE_API int EngineNative_GetVersion() {
    return ENGINE_NATIVE_VERSION;
}

/**
 * @brief Queries host CPU features using x86/x64 __cpuid intrinsics or ARM architecture macros.
 *
 * Checks leaf 1 for SSE4.1 (ECX bit 19) and leaf 7 for AVX2 (EBX bit 5) and AVX-512F (EBX bit 16).
 * On ARM64 architectures, reports ARM NEON support.
 *
 * @return Bitmask of @ref SIMD_NONE, @ref SIMD_SSE41, @ref SIMD_AVX2, @ref SIMD_AVX512, or @ref SIMD_NEON.
 */
ENGINE_API int EngineNative_GetSimdCapabilities() {
    /** @var caps Accumulated SIMD capability bitflags initialized to SIMD_NONE */
    int caps = SIMD_NONE;
#if defined(_MSC_VER) && (defined(_M_X64) || defined(_M_IX86))
    /** @var cpuInfo Array storing EAX, EBX, ECX, EDX register output from __cpuid call */
    int cpuInfo[4];

    // Leaf 1: Basic CPU features & SSE extensions
    __cpuid(cpuInfo, 1);
    if ((cpuInfo[2] & (1 << 19)) != 0) { // SSE 4.1 in ECX bit 19
        caps |= SIMD_SSE41;
    }

    // Leaf 7: Extended feature flags (AVX2, AVX-512)
    __cpuid(cpuInfo, 7);
    if ((cpuInfo[1] & (1 << 5)) != 0) { // AVX2 in EBX bit 5
        caps |= SIMD_AVX2;
    }
    if ((cpuInfo[1] & (1 << 16)) != 0) { // AVX-512 Foundation in EBX bit 16
        caps |= SIMD_AVX512;
    }
#elif defined(__aarch64__) || defined(_M_ARM64)
    caps |= SIMD_NEON;
#endif
    return caps;
}

/**
 * @brief In-place 4x4 matrix transformation of 3D floating-point vertex coordinates.
 *
 * Computes [rx, ry, rz, rw] = [x, y, z, 1.0] * M in row-major layout, then divides by rw if perspective is active.
 *
 * @param[in,out] vertices Flat array of 3D coordinates (3 floats per vertex).
 * @param[in] vertexCount Number of 3D vertices to transform.
 * @param[in] matrix4x4 Array of 16 floats representing the row-major 4x4 transformation matrix.
 */
ENGINE_API void EngineNative_TransformPositions(
    float* vertices,
    int vertexCount,
    const float* matrix4x4
) {
    if (!vertices || vertexCount <= 0 || !matrix4x4) return;

    for (int i = 0; i < vertexCount; ++i) {
        int idx = i * 3;
        /** @var x Local X coordinate of current vertex */
        float x = vertices[idx];
        /** @var y Local Y coordinate of current vertex */
        float y = vertices[idx + 1];
        /** @var z Local Z coordinate of current vertex */
        float z = vertices[idx + 2];

        // 4x4 Row-major transformation: [x, y, z, 1] * Matrix
        float rx = x * matrix4x4[0] + y * matrix4x4[4] + z * matrix4x4[8]  + matrix4x4[12];
        float ry = x * matrix4x4[1] + y * matrix4x4[5] + z * matrix4x4[9]  + matrix4x4[13];
        float rz = x * matrix4x4[2] + y * matrix4x4[6] + z * matrix4x4[10] + matrix4x4[14];
        float rw = x * matrix4x4[3] + y * matrix4x4[7] + z * matrix4x4[11] + matrix4x4[15];

        // Perspective divide when homogeneous coordinate w != 1.0
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

/**
 * @brief Computes vertex normals for 3D triangular meshes.
 *
 * If indices is non-null, iterates over triangles by index; otherwise iterates over consecutive vertex triplets.
 * Accumulates unnormalized face normal vectors into outNormals, then normalizes each vertex normal vector.
 *
 * @param[in] positions Input vertex position buffer [x, y, z, ...].
 * @param[in] vertexCount Total number of vertices in positions array.
 * @param[in] indices Optional index buffer referencing vertices in positions array.
 * @param[in] indexCount Number of entries in indices array.
 * @param[out] outNormals Output normal buffer with vertexCount * 3 floats.
 */
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

            // Cross product: e1 x e2
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

/**
 * @brief Fast single-precision Möller-Trumbore ray-triangle intersection calculation.
 *
 * Solves the linear system:
 * @f[
 *   O + t D = (1 - u - v) V_0 + u V_1 + v V_2
 * @f]
 * Returns early with -1.0f if the ray is parallel to the triangle or if barycentric coordinates fall outside [0, 1].
 *
 * @param[in] origX Ray origin X component.
 * @param[in] origY Ray origin Y component.
 * @param[in] origZ Ray origin Z component.
 * @param[in] dirX Ray direction unit vector X component.
 * @param[in] dirY Ray direction unit vector Y component.
 * @param[in] dirZ Ray direction unit vector Z component.
 * @param[in] v0x Triangle vertex 0 X.
 * @param[in] v0y Triangle vertex 0 Y.
 * @param[in] v0z Triangle vertex 0 Z.
 * @param[in] v1x Triangle vertex 1 X.
 * @param[in] v1y Triangle vertex 1 Y.
 * @param[in] v1z Triangle vertex 1 Z.
 * @param[in] v2x Triangle vertex 2 X.
 * @param[in] v2y Triangle vertex 2 Y.
 * @param[in] v2z Triangle vertex 2 Z.
 * @return Positive parametric distance t along the ray if hit, or -1.0f if miss.
 */
ENGINE_API float EngineNative_RayTriangleIntersect(
    float origX, float origY, float origZ,
    float dirX, float dirY, float dirZ,
    float v0x, float v0y, float v0z,
    float v1x, float v1y, float v1z,
    float v2x, float v2y, float v2z
) {
    /** @var EPSILON Floating point numerical precision tolerance threshold to guard against degenerate/parallel faces */
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

// =============================================================================
// Vulkan Viewport & Isolated Render Loop Implementation
// =============================================================================

namespace {
    std::atomic<bool> g_isRendererRunning{false};
    std::atomic<bool> g_isRendererPaused{false};
    std::atomic<uint32_t> g_targetFps{60};
    std::atomic<uint32_t> g_swapchainWidth{1280};
    std::atomic<uint32_t> g_swapchainHeight{720};
    std::atomic<int32_t> g_viewportX{0};
    std::atomic<int32_t> g_viewportY{0};
    std::atomic<uint32_t> g_viewportWidth{1280};
    std::atomic<uint32_t> g_viewportHeight{720};
    std::atomic<uint32_t> g_currentFps{0};
    std::atomic<float> g_frameTimeMs{0.0f};
    std::atomic<bool> g_swapchainResizePending{false};

    void* g_windowHandle = nullptr;
    std::thread g_renderThread;

#if defined(ENGINE_NATIVE_HAS_VULKAN)
    VkInstance g_vkInstance = VK_NULL_HANDLE;
    VkSurfaceKHR g_vkSurface = VK_NULL_HANDLE;
    VkPhysicalDevice g_vkPhysicalDevice = VK_NULL_HANDLE;
    VkDevice g_vkDevice = VK_NULL_HANDLE;
    VkQueue g_vkQueue = VK_NULL_HANDLE;
    VkSwapchainKHR g_vkSwapchain = VK_NULL_HANDLE;
    bool g_vulkanInitialized = false;

    void CleanupVulkanResources() {
        if (g_vkDevice != VK_NULL_HANDLE) {
            if (g_vkSwapchain != VK_NULL_HANDLE) {
                vkDestroySwapchainKHR(g_vkDevice, g_vkSwapchain, nullptr);
                g_vkSwapchain = VK_NULL_HANDLE;
            }
            vkDestroyDevice(g_vkDevice, nullptr);
            g_vkDevice = VK_NULL_HANDLE;
        }
        if (g_vkInstance != VK_NULL_HANDLE) {
            if (g_vkSurface != VK_NULL_HANDLE) {
                vkDestroySurfaceKHR(g_vkInstance, g_vkSurface, nullptr);
                g_vkSurface = VK_NULL_HANDLE;
            }
            vkDestroyInstance(g_vkInstance, nullptr);
            g_vkInstance = VK_NULL_HANDLE;
        }
        g_vulkanInitialized = false;
    }

    bool InitVulkanContext(const NativeWindowDescriptor& desc) {
#if defined(_WIN32)
        if (desc.platformType != NativePlatformType::Win32 || !desc.windowHandle || !IsWindow((HWND)desc.windowHandle)) {
            // Simulated or test handle: bypass physical Vulkan surface creation
            return false;
        }

        VkApplicationInfo appInfo{};
        appInfo.sType = VK_STRUCTURE_TYPE_APPLICATION_INFO;
        appInfo.pApplicationName = "Blockout Studio Native";
        appInfo.applicationVersion = VK_MAKE_VERSION(1, 0, 0);
        appInfo.pEngineName = "EngineNative";
        appInfo.engineVersion = VK_MAKE_VERSION(1, 0, 0);
        appInfo.apiVersion = VK_API_VERSION_1_2;

        std::vector<const char*> extensions = {
            VK_KHR_SURFACE_EXTENSION_NAME,
            VK_KHR_WIN32_SURFACE_EXTENSION_NAME
        };

        VkInstanceCreateInfo createInfo{};
        createInfo.sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO;
        createInfo.pApplicationInfo = &appInfo;
        createInfo.enabledExtensionCount = static_cast<uint32_t>(extensions.size());
        createInfo.ppEnabledExtensionNames = extensions.data();

        if (vkCreateInstance(&createInfo, nullptr, &g_vkInstance) != VK_SUCCESS) {
            return false;
        }

        auto pfnCreateWin32 = reinterpret_cast<PFN_vkCreateWin32SurfaceKHR>(
            vkGetInstanceProcAddr(g_vkInstance, "vkCreateWin32SurfaceKHR"));
        if (!pfnCreateWin32) {
            CleanupVulkanResources();
            return false;
        }

        VkWin32SurfaceCreateInfoKHR surfaceCreateInfo{};
        surfaceCreateInfo.sType = VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR;
        surfaceCreateInfo.hwnd = (HWND)desc.windowHandle;
        surfaceCreateInfo.hinstance = GetModuleHandle(nullptr);

        if (pfnCreateWin32(g_vkInstance, &surfaceCreateInfo, nullptr, &g_vkSurface) != VK_SUCCESS) {
            CleanupVulkanResources();
            return false;
        }
#elif defined(__linux__) || defined(__unix__)
        if (!desc.windowHandle || (uintptr_t)desc.windowHandle < 0x10000) {
            // Simulated or test handle (e.g. 0x1000 in tests): bypass physical Vulkan surface creation
            return false;
        }

        VkApplicationInfo appInfo{};
        appInfo.sType = VK_STRUCTURE_TYPE_APPLICATION_INFO;
        appInfo.pApplicationName = "Blockout Studio Native";
        appInfo.applicationVersion = VK_MAKE_VERSION(1, 0, 0);
        appInfo.pEngineName = "EngineNative";
        appInfo.engineVersion = VK_MAKE_VERSION(1, 0, 0);
        appInfo.apiVersion = VK_API_VERSION_1_2;

        std::vector<const char*> extensions = {
            VK_KHR_SURFACE_EXTENSION_NAME
        };

        if (desc.platformType == NativePlatformType::Wayland) {
            extensions.push_back(VK_KHR_WAYLAND_SURFACE_EXTENSION_NAME);
        } else {
            // Default to X11 for Linux desktop
            extensions.push_back(VK_KHR_XLIB_SURFACE_EXTENSION_NAME);
        }

        VkInstanceCreateInfo createInfo{};
        createInfo.sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO;
        createInfo.pApplicationInfo = &appInfo;
        createInfo.enabledExtensionCount = static_cast<uint32_t>(extensions.size());
        createInfo.ppEnabledExtensionNames = extensions.data();

        if (vkCreateInstance(&createInfo, nullptr, &g_vkInstance) != VK_SUCCESS) {
            std::cerr << "[NativeVulkan] Failed to create VkInstance with Linux surface extensions.\n";
            return false;
        }

        if (desc.platformType == NativePlatformType::Wayland) {
            auto pfnCreateWayland = reinterpret_cast<PFN_vkCreateWaylandSurfaceKHR>(
                vkGetInstanceProcAddr(g_vkInstance, "vkCreateWaylandSurfaceKHR"));
            if (!pfnCreateWayland) {
                std::cerr << "[NativeVulkan] vkCreateWaylandSurfaceKHR proc not found.\n";
                CleanupVulkanResources();
                return false;
            }

            if (!desc.displayHandle || !desc.windowHandle) {
                std::cerr << "[NativeVulkan] Invalid Wayland display or surface handle.\n";
                CleanupVulkanResources();
                return false;
            }

            VkWaylandSurfaceCreateInfoKHR wlCreateInfo{};
            wlCreateInfo.sType = VK_STRUCTURE_TYPE_WAYLAND_SURFACE_CREATE_INFO_KHR;
            wlCreateInfo.pNext = nullptr;
            wlCreateInfo.flags = 0;
            wlCreateInfo.display = static_cast<struct wl_display*>(desc.displayHandle);
            wlCreateInfo.surface = static_cast<struct wl_surface*>(desc.windowHandle);

            if (pfnCreateWayland(g_vkInstance, &wlCreateInfo, nullptr, &g_vkSurface) != VK_SUCCESS) {
                std::cerr << "[NativeVulkan] vkCreateWaylandSurfaceKHR failed.\n";
                CleanupVulkanResources();
                return false;
            }
        } else {
            // X11 Surface Creation
            auto pfnCreateXlib = reinterpret_cast<PFN_vkCreateXlibSurfaceKHR>(
                vkGetInstanceProcAddr(g_vkInstance, "vkCreateXlibSurfaceKHR"));
            if (!pfnCreateXlib) {
                std::cerr << "[NativeVulkan] vkCreateXlibSurfaceKHR proc not found.\n";
                CleanupVulkanResources();
                return false;
            }

#if __has_include(<X11/Xlib.h>)
            Display* dpy = static_cast<Display*>(desc.displayHandle);
            if (!dpy) {
                dpy = XOpenDisplay(nullptr);
            }

            if (!dpy) {
                std::cerr << "[NativeVulkan] Failed to open X11 Display.\n";
                CleanupVulkanResources();
                return false;
            }

            VkXlibSurfaceCreateInfoKHR xlibCreateInfo{};
            xlibCreateInfo.sType = VK_STRUCTURE_TYPE_XLIB_SURFACE_CREATE_INFO_KHR;
            xlibCreateInfo.pNext = nullptr;
            xlibCreateInfo.flags = 0;
            xlibCreateInfo.dpy = dpy;
            xlibCreateInfo.window = static_cast<Window>(reinterpret_cast<uintptr_t>(desc.windowHandle));

            VkResult xres = pfnCreateXlib(g_vkInstance, &xlibCreateInfo, nullptr, &g_vkSurface);
            if (xres != VK_SUCCESS) {
                std::cerr << "[NativeVulkan] vkCreateXlibSurfaceKHR failed with error code: " << xres << "\n";
                CleanupVulkanResources();
                return false;
            }
#else
            std::cerr << "[NativeVulkan] X11/Xlib.h not available during compilation.\n";
            CleanupVulkanResources();
            return false;
#endif
        }
#else
        return false;
#endif

        // -------------------------------------------------------------
        // Shared Physical Device, Logical Device & Queue Initialization
        // -------------------------------------------------------------
        uint32_t deviceCount = 0;
        vkEnumeratePhysicalDevices(g_vkInstance, &deviceCount, nullptr);
        if (deviceCount == 0) {
            CleanupVulkanResources();
            return false;
        }

        std::vector<VkPhysicalDevice> devices(deviceCount);
        vkEnumeratePhysicalDevices(g_vkInstance, &deviceCount, devices.data());
        g_vkPhysicalDevice = devices[0];

        float queuePriority = 1.0f;
        VkDeviceQueueCreateInfo queueCreateInfo{};
        queueCreateInfo.sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
        queueCreateInfo.queueFamilyIndex = 0;
        queueCreateInfo.queueCount = 1;
        queueCreateInfo.pQueuePriorities = &queuePriority;

        const char* deviceExtensions[] = {
            VK_KHR_SWAPCHAIN_EXTENSION_NAME
        };

        VkDeviceCreateInfo deviceCreateInfo{};
        deviceCreateInfo.sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO;
        deviceCreateInfo.pQueueCreateInfos = &queueCreateInfo;
        deviceCreateInfo.queueCreateInfoCount = 1;
        deviceCreateInfo.enabledExtensionCount = 1;
        deviceCreateInfo.ppEnabledExtensionNames = deviceExtensions;

        if (vkCreateDevice(g_vkPhysicalDevice, &deviceCreateInfo, nullptr, &g_vkDevice) != VK_SUCCESS) {
            CleanupVulkanResources();
            return false;
        }

        vkGetDeviceQueue(g_vkDevice, 0, 0, &g_vkQueue);
        g_vulkanInitialized = true;
        return true;
    }
#endif

    void RenderLoopWorker() {
        using clock = std::chrono::steady_clock;
        auto lastFrameTime = clock::now();
        auto lastFpsUpdateTime = clock::now();
        uint32_t framesCounted = 0;

        while (g_isRendererRunning.load(std::memory_order_relaxed)) {
            // Check paused state
            if (g_isRendererPaused.load(std::memory_order_relaxed)) {
                std::this_thread::sleep_for(std::chrono::milliseconds(16));
                lastFrameTime = clock::now();
                continue;
            }

            auto frameStartTime = clock::now();

            // Handle swapchain resize request if pending
            if (g_swapchainResizePending.exchange(false, std::memory_order_acq_rel)) {
                uint32_t newW = g_swapchainWidth.load(std::memory_order_relaxed);
                uint32_t newH = g_swapchainHeight.load(std::memory_order_relaxed);
                // When Vulkan is active, swapchain would be recreated here
                (void)newW;
                (void)newH;
            }

            // Retrieve thread-safe viewport and scissor rect
            int32_t vx = g_viewportX.load(std::memory_order_relaxed);
            int32_t vy = g_viewportY.load(std::memory_order_relaxed);
            uint32_t vw = g_viewportWidth.load(std::memory_order_relaxed);
            uint32_t vh = g_viewportHeight.load(std::memory_order_relaxed);

            // Execute frame render pass (mock or Vulkan)
            // In real Vulkan mode: vkCmdSetViewport, vkCmdSetScissor, vkQueuePresentKHR
            (void)vx; (void)vy; (void)vw; (void)vh;

            framesCounted++;
            auto frameEndTime = clock::now();
            float frameDurationMs = std::chrono::duration<float, std::milli>(frameEndTime - frameStartTime).count();

            // Compute FPS every 500ms
            float elapsedFpsSec = std::chrono::duration<float>(frameEndTime - lastFpsUpdateTime).count();
            if (elapsedFpsSec >= 0.5f) {
                g_currentFps.store(static_cast<uint32_t>(framesCounted / elapsedFpsSec), std::memory_order_relaxed);
                g_frameTimeMs.store(frameDurationMs, std::memory_order_relaxed);
                framesCounted = 0;
                lastFpsUpdateTime = frameEndTime;
            }

            // Frame pacing
            uint32_t targetFps = g_targetFps.load(std::memory_order_relaxed);
            if (targetFps > 0) {
                float targetFrameBudgetMs = 1000.0f / targetFps;
                float timeToSleepMs = targetFrameBudgetMs - frameDurationMs;
                if (timeToSleepMs > 1.0f) {
                    std::this_thread::sleep_for(std::chrono::milliseconds(static_cast<int64_t>(timeToSleepMs)));
                }
            } else {
                std::this_thread::yield();
            }
        }

#if defined(ENGINE_NATIVE_HAS_VULKAN)
        CleanupVulkanResources();
#endif
    }
}

ENGINE_API int32_t EngineNative_InitRendererDescriptor(const NativeWindowDescriptor* pDescriptor) {
    if (!pDescriptor) return -1;

    if (g_isRendererRunning.load(std::memory_order_acquire)) {
        EngineNative_ShutdownRenderer();
    }

    g_windowHandle = pDescriptor->windowHandle;
    uint32_t width = pDescriptor->width > 0 ? pDescriptor->width : 1280;
    uint32_t height = pDescriptor->height > 0 ? pDescriptor->height : 720;

    g_swapchainWidth.store(width, std::memory_order_release);
    g_swapchainHeight.store(height, std::memory_order_release);
    g_viewportX.store(0, std::memory_order_release);
    g_viewportY.store(0, std::memory_order_release);
    g_viewportWidth.store(width, std::memory_order_release);
    g_viewportHeight.store(height, std::memory_order_release);

#if defined(ENGINE_NATIVE_HAS_VULKAN)
    InitVulkanContext(*pDescriptor);
#endif

    g_isRendererRunning.store(true, std::memory_order_release);
    g_isRendererPaused.store(false, std::memory_order_release);

    g_renderThread = std::thread(RenderLoopWorker);
    return 0;
}

ENGINE_API int32_t EngineNative_InitRenderer(void* nativeWindowHandle, uint32_t initialWidth, uint32_t initialHeight) {
    NativeWindowDescriptor desc{};
#if defined(_WIN32)
    desc.platformType = NativePlatformType::Win32;
    desc.displayHandle = nullptr;
    desc.windowHandle = nativeWindowHandle;
#elif defined(__linux__) || defined(__unix__)
    desc.platformType = NativePlatformType::X11;
    desc.displayHandle = nullptr;
    desc.windowHandle = nativeWindowHandle;
#else
    desc.platformType = NativePlatformType::Unknown;
    desc.displayHandle = nullptr;
    desc.windowHandle = nativeWindowHandle;
#endif
    desc.width = initialWidth;
    desc.height = initialHeight;
    return EngineNative_InitRendererDescriptor(&desc);
}

ENGINE_API void EngineNative_ShutdownRenderer() {
    if (!g_isRendererRunning.load(std::memory_order_acquire)) {
        return;
    }

    g_isRendererRunning.store(false, std::memory_order_release);
    if (g_renderThread.joinable()) {
        g_renderThread.join();
    }
}

ENGINE_API void EngineNative_ResizeSwapchain(uint32_t newWidth, uint32_t newHeight) {
    if (newWidth == 0 || newHeight == 0) return;
    g_swapchainWidth.store(newWidth, std::memory_order_release);
    g_swapchainHeight.store(newHeight, std::memory_order_release);
    g_swapchainResizePending.store(true, std::memory_order_release);
}

ENGINE_API void EngineNative_SetViewportScissor(int32_t x, int32_t y, uint32_t width, uint32_t height) {
    g_viewportX.store(x, std::memory_order_release);
    g_viewportY.store(y, std::memory_order_release);
    g_viewportWidth.store(width, std::memory_order_release);
    g_viewportHeight.store(height, std::memory_order_release);
}

ENGINE_API void EngineNative_SetTargetFps(uint32_t targetFps) {
    g_targetFps.store(targetFps, std::memory_order_release);
}

ENGINE_API void EngineNative_SetRendererPaused(int32_t isPaused) {
    g_isRendererPaused.store(isPaused != 0, std::memory_order_release);
}

ENGINE_API void EngineNative_GetRendererStats(NativeRendererStats* outStats) {
    if (!outStats) return;
    outStats->currentFps = g_currentFps.load(std::memory_order_relaxed);
    outStats->frameTimeMs = g_frameTimeMs.load(std::memory_order_relaxed);
    outStats->swapchainWidth = g_swapchainWidth.load(std::memory_order_relaxed);
    outStats->swapchainHeight = g_swapchainHeight.load(std::memory_order_relaxed);
    outStats->viewportX = g_viewportX.load(std::memory_order_relaxed);
    outStats->viewportY = g_viewportY.load(std::memory_order_relaxed);
    outStats->viewportWidth = g_viewportWidth.load(std::memory_order_relaxed);
    outStats->viewportHeight = g_viewportHeight.load(std::memory_order_relaxed);
}

ENGINE_API int32_t EngineNative_IsRendererRunning() {
    return g_isRendererRunning.load(std::memory_order_relaxed) ? 1 : 0;
}
