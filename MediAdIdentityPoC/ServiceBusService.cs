using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace MediAdIdentityPoC;

public class ServiceBusService : IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _processor;


    public ServiceBusService(IConfiguration configStore)
    {
        var config = configStore.GetRequiredSection("ServiceBus").Get<ServiceBusConfiguration>() ??
                     throw new("No service bus configuration found");
        _client = new(config.ConnectionString, new DefaultAzureCredential(), new()
        {
            TransportType = ServiceBusTransportType.AmqpTcp // TODO: Cycle back on used protocol
        });

        _processor = _client.CreateProcessor(config.QueueName);
    }

    public ServiceBusService SetMessageHandler(Func<ProcessMessageEventArgs, Task> handler)
    {
        // Throws when handler is already assigned
        _processor.ProcessMessageAsync += handler;
        return this;
    }

    public ServiceBusService SetErrorHandler(Func<ProcessErrorEventArgs, Task> handler)
    {
        // Throws when handler is already assigned
        _processor.ProcessErrorAsync += handler;
        return this;
    }

    /// <summary>
    /// Starts connecting to the message bus and receiving messages.
    /// Only call after <see cref="SetMessageHandler"/> and <see cref="SetErrorHandler"/>
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        await _processor.StartProcessingAsync(cancellationToken);
    }

    public async Task StopProcessingAsync(CancellationToken cancellationToken = default)
    {
        await _processor.StopProcessingAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.StopProcessingAsync();
        await _client.DisposeAsync();
        await _processor.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}