namespace MediAdIdentityPoC.Transport;

/// <summary>
/// Message bus adapter to be used in development and testing
/// </summary>
public class DevelopmentBusService : ITransport
{
    public ITransport SetMessageHandler(Func<IMessage, Task> handler)
    {
        throw new NotImplementedException();
    }

    public Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StopProcessingAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}