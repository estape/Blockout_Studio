using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.NativeInterop;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for reporting C++ SIMD native core status and capabilities.
/// </summary>
public class GetNativeStatusHandler : IIpcActionHandler
{
    /// <summary>Native interop service instance.</summary>
    private readonly INativeInteropService _nativeService;

    /// <summary>Initializes handler with the native interop service.</summary>
    public GetNativeStatusHandler(INativeInteropService nativeService)
    {
        _nativeService = nativeService;
    }

    /// <inheritdoc/>
    public string Action => "get_native_status";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "native_status_response",
            isLoaded = _nativeService.IsNativeLoaded,
            version = _nativeService.NativeVersion,
            simdCaps = _nativeService.SimdCapabilities,
            description = _nativeService.SimdDescription
        }));
        return Task.CompletedTask;
    }
}
