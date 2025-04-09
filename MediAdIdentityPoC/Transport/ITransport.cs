namespace MediAdIdentityPoC.Transport;

/// <summary>
/// Abstraction layer for message bus adapters. Only exposes the functions needed for receiving and processing messages.
/// </summary>
public interface ITransport
{
    ITransport SetMessageHandler(Func<IMessage, Task> handler);
    Task StartProcessingAsync(CancellationToken cancellationToken = default);
    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}