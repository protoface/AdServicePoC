namespace MediAdIdentityPoC.Transport;

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