namespace MediAdIdentityPoC.Transport;

public interface ITransport
{
    ITransport SetMessageHandler(Func<IMessage, Task> handler);
    Task StartProcessingAsync(CancellationToken cancellationToken = default);
    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}