using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Graphics;

namespace BlockoutStudio.Services.Ipc.Handlers;

public class GetGridTextureHandler : IIpcActionHandler
{
    public string Action => "get_grid_texture";

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
