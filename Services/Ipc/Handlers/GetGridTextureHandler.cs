using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Graphics;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class GetGridTextureHandler : IIpcActionHandler
{
    public string Action => "get_grid_texture";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var url = TextureBaker.GetUe5GridDataUrl();
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "grid_texture_response",
            url
        }));
        return Task.CompletedTask;
    }
}
