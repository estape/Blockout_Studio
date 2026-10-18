using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.NativeInterop;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class GetNativeStatusHandler : IIpcActionHandler
{
    private readonly INativeInteropService _nativeService;

    public GetNativeStatusHandler(INativeInteropService nativeService)
    {
        _nativeService = nativeService;
    }

    public string Action => "get_native_status";

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
