using Photino.NET;

namespace BlockoutStudio.Services.Ipc;

public interface IIpcRouter
{
    Task DispatchAsync(string message, PhotinoWindow window);
}
