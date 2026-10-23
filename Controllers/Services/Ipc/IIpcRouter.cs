using Photino.NET;

namespace BlockoutStudio.Services.Ipc;

/// <summary>
/// Router dispatching frontend webview messages to appropriate registered <see cref="IIpcActionHandler"/> instances.
/// </summary>
public interface IIpcRouter
{
    /// <summary>
    /// Deserializes a message string and invokes the matching registered handler.
    /// </summary>
    /// <param name="message">Raw JSON message string from Photino.</param>
    /// <param name="window">Target host window.</param>
    /// <returns>Completed dispatch task.</returns>
    Task DispatchAsync(string message, PhotinoWindow window);
}
