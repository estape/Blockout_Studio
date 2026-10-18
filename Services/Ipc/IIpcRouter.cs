using Photino.NET;

namespace UE5BlockoutStudio.Services.Ipc;

public interface IIpcRouter
{
    Task DispatchAsync(string message, PhotinoWindow window);
}
