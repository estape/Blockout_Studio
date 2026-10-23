using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Graphics;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler synchronizing DOM layout bounding rectangle changes from the web frontend
/// directly to the native Vulkan viewport and scissor state without swapchain recreation.
/// </summary>
public class SyncViewportBoundsHandler : IIpcActionHandler
{
    private readonly INativeVulkanViewportService _viewportService;

    /// <inheritdoc/>
    public string Action => "sync_viewport_bounds";

    public SyncViewportBoundsHandler(INativeVulkanViewportService viewportService)
    {
        _viewportService = viewportService;
    }

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        try
        {
            // Support both root-level and payload-level properties
            var target = root.TryGetProperty("payload", out var p) && p.ValueKind == JsonValueKind.Object ? p : root;

            int x = 0;
            int y = 0;
            uint width = 0;
            uint height = 0;

            if (target.TryGetProperty("x", out var propX)) x = (int)Math.Round(propX.GetDouble());
            if (target.TryGetProperty("y", out var propY)) y = (int)Math.Round(propY.GetDouble());

            if (target.TryGetProperty("width", out var propW)) width = (uint)Math.Max(0, Math.Round(propW.GetDouble()));
            else if (target.TryGetProperty("w", out var propW2)) width = (uint)Math.Max(0, Math.Round(propW2.GetDouble()));

            if (target.TryGetProperty("height", out var propH)) height = (uint)Math.Max(0, Math.Round(propH.GetDouble()));
            else if (target.TryGetProperty("h", out var propH2)) height = (uint)Math.Max(0, Math.Round(propH2.GetDouble()));

            if (width > 0 && height > 0)
            {
                _viewportService.SetViewportBounds(x, y, width, height);
            }

            var stats = _viewportService.GetStats();

            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "sync_viewport_bounds_response",
                success = true,
                x,
                y,
                width,
                height,
                currentFps = stats.CurrentFps,
                frameTimeMs = stats.FrameTimeMs
            }));
        }
        catch (Exception ex)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "sync_viewport_bounds_response",
                success = false,
                error = ex.Message
            }));
        }

        return Task.CompletedTask;
    }
}

