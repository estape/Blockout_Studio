using Photino.NET;
using BlockoutStudio.Services.NativeInterop;

namespace BlockoutStudio.Services.Graphics;

/// <summary>
/// Service interface managing the native Vulkan viewport, swapchain synchronization,
/// and isolated rendering thread lifecycle hosted inside Photino.NET.
/// </summary>
public interface INativeVulkanViewportService
{
    /// <summary>
    /// Attaches to the active Photino window instance, hooking creation, resize, and close handlers.
    /// </summary>
    /// <param name="window">Active Photino window instance.</param>
    void AttachToPhotino(PhotinoWindow window);

    /// <summary>
    /// Updates the dynamic viewport and scissor rectangle for rendering without swapchain recreation.
    /// </summary>
    /// <param name="x">Viewport left coordinate in physical pixels.</param>
    /// <param name="y">Viewport top coordinate in physical pixels.</param>
    /// <param name="width">Viewport width in physical pixels.</param>
    /// <param name="height">Viewport height in physical pixels.</param>
    void SetViewportBounds(int x, int y, uint width, uint height);

    /// <summary>
    /// Recreates or resizes the outer swapchain to match new window dimensions.
    /// </summary>
    /// <param name="newWidth">Window client width.</param>
    /// <param name="newHeight">Window client height.</param>
    void ResizeSwapchain(uint newWidth, uint newHeight);

    /// <summary>
    /// Sets the target rendering framerate (e.g. 60, 120, 0 for uncapped).
    /// </summary>
    /// <param name="targetFps">Target frames per second.</param>
    void SetTargetFps(uint targetFps);

    /// <summary>
    /// Pauses or resumes the isolated Vulkan rendering loop.
    /// </summary>
    /// <param name="isPaused">True to pause, false to resume.</param>
    void SetPaused(bool isPaused);

    /// <summary>
    /// Gets current renderer statistics and viewport information.
    /// </summary>
    /// <returns>Current renderer stats snapshot.</returns>
    EngineNativeBindings.NativeRendererStats GetStats();

    /// <summary>
    /// Checks if the native isolated rendering thread is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gracefully stops the native renderer and cleans up swapchain resources.
    /// </summary>
    void Shutdown();
}

