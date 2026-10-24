using System.Drawing;
using Photino.NET;
using BlockoutStudio.Services.NativeInterop;

namespace BlockoutStudio.Services.Graphics;

/// <summary>
/// High-performance interop bridge connecting Photino.NET's window lifecycle and web viewport layout
/// to EngineNative.dll's isolated Vulkan render thread.
/// </summary>
public class NativeVulkanViewportService : INativeVulkanViewportService
{
    private readonly Action<string>? _logCallback;
    private bool _isAttached;
    private PhotinoWindow? _window;

    public bool IsRunning => EngineNativeBindings.IsRendererRunning() != 0;

    public NativeVulkanViewportService(Action<string>? logCallback = null)
    {
        _logCallback = logCallback;
    }

    /// <inheritdoc/>
    public void AttachToPhotino(PhotinoWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_isAttached) return;

        _window = window;
        _isAttached = true;

        // 1. Capture native window and display handles safely across platforms when window is created
        window.RegisterWindowCreatedHandler((sender, args) =>
        {
            try
            {
                IntPtr rawHandle = window.WindowHandle;
                Size size = window.Size;
                uint w = (uint)Math.Max(1, size.Width);
                uint h = (uint)Math.Max(1, size.Height);

                NativeWindowDescriptor desc;
                if (OperatingSystem.IsWindows())
                {
                    desc = new NativeWindowDescriptor(NativePlatformType.Win32, IntPtr.Zero, rawHandle, w, h);
                }
                else if (OperatingSystem.IsLinux())
                {
                    desc = LinuxWindowHelper.ExtractDescriptor(rawHandle, w, h);
                }
                else
                {
                    desc = new NativeWindowDescriptor(NativePlatformType.Unknown, IntPtr.Zero, rawHandle, w, h);
                }

                _logCallback?.Invoke($"[NativeVulkan] Captured WindowHandle 0x{rawHandle:X} (Platform: {desc.PlatformType}), Initializing renderer {w}x{h}...");
                EngineNativeBindings.InitRendererDescriptor(ref desc);
            }
            catch (Exception ex)
            {
                _logCallback?.Invoke($"[NativeVulkan] Failed to initialize native renderer: {ex.Message}");
            }
        });

        // 2. Hook outer window resize to recreate swapchain
        window.RegisterSizeChangedHandler((sender, size) =>
        {
            if (size.Width > 0 && size.Height > 0)
            {
                ResizeSwapchain((uint)size.Width, (uint)size.Height);
            }
        });

        // 3. Graceful shutdown when window closes
        window.RegisterWindowClosingHandler((sender, args) =>
        {
            Shutdown();
            return false; // Do not cancel close
        });
    }

    /// <inheritdoc/>
    public void SetViewportBounds(int x, int y, uint width, uint height)
    {
        if (width == 0 || height == 0) return;
        EngineNativeBindings.SetViewportScissor(x, y, width, height);
    }

    /// <inheritdoc/>
    public void ResizeSwapchain(uint newWidth, uint newHeight)
    {
        if (newWidth == 0 || newHeight == 0) return;
        EngineNativeBindings.ResizeSwapchain(newWidth, newHeight);
    }

    /// <inheritdoc/>
    public void SetTargetFps(uint targetFps)
    {
        EngineNativeBindings.SetTargetFps(targetFps);
    }

    /// <inheritdoc/>
    public void SetPaused(bool isPaused)
    {
        EngineNativeBindings.SetRendererPaused(isPaused ? 1 : 0);
    }

    /// <inheritdoc/>
    public unsafe EngineNativeBindings.NativeRendererStats GetStats()
    {
        EngineNativeBindings.NativeRendererStats stats;
        EngineNativeBindings.GetRendererStats(&stats);
        return stats;
    }

    /// <inheritdoc/>
    public void Shutdown()
    {
        try
        {
            if (IsRunning)
            {
                _logCallback?.Invoke("[NativeVulkan] Shutting down isolated render thread and releasing swapchain...");
                EngineNativeBindings.ShutdownRenderer();
            }
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"[NativeVulkan] Error shutting down renderer: {ex.Message}");
        }
    }
}
