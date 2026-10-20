using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Graphics;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for generating and retrieving the base64 data URL of the prototype checkerboard texture.
/// </summary>
public class GetGridTextureHandler : IIpcActionHandler
{
    /// <inheritdoc/>
    public string Action => "get_grid_texture";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var url = TextureBaker.GetGridDataUrl();
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "grid_texture_response",
            url
        }));
        return Task.CompletedTask;
    }
}
