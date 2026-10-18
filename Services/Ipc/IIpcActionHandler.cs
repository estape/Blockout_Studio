using System.Text.Json;
using Photino.NET;

namespace UE5BlockoutStudio.Services.Ipc;

public interface IIpcActionHandler
{
    string Action { get; }
    Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window);
}
