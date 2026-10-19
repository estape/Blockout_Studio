using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Toolchains;

namespace BlockoutStudio.Services.Ipc.Handlers;

public class ToolchainIpcHandler : IIpcActionHandler
{
    private readonly IToolchainDiscoveryService _toolchainService;

    public ToolchainIpcHandler(IToolchainDiscoveryService toolchainService)
    {
        _toolchainService = toolchainService;
    }

    public string Action => "get_toolchains";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var report = _toolchainService.DiscoverToolchains();
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "toolchains_response",
            report
        }));
        return Task.CompletedTask;
    }
}
