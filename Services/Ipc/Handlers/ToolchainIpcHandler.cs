using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Toolchains;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for discovering installed compiler toolchains and graphics runtimes.
/// </summary>
public class ToolchainIpcHandler : IIpcActionHandler
{
    /// <summary>Toolchain discovery service instance.</summary>
    private readonly IToolchainDiscoveryService _toolchainService;

    /// <summary>Initializes handler with the toolchain discovery service.</summary>
    public ToolchainIpcHandler(IToolchainDiscoveryService toolchainService)
    {
        _toolchainService = toolchainService;
    }

    /// <inheritdoc/>
    public string Action => "get_toolchains";

    /// <inheritdoc/>
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
