using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace MediAdIdentityPoC.Transport;

/// <summary>
/// Adapter for the Azure Service Bus
/// </summary>
internal sealed class ServiceBusService : IAsyncDisposable, ITransport
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _processor;

    public ServiceBusService(IConfiguration configStore, ILogger<ServiceBusService> logger)
    {
        var config = configStore.GetRequiredSection("ServiceBus").Get<ServiceBusConfiguration>() ??
                     throw new("No service bus configuration found");
        if (config.ConnectionString == null && config.FullyQualifiedNamespace == null)
            throw new("ConnectionString or FullyQualifiedNamespace has to be configured");

        _client = config.ConnectionString != null
            ? new(config.ConnectionString)
            : new(config.FullyQualifiedNamespace, new DefaultAzureCredential(), new()
            {
                TransportType = ServiceBusTransportType.AmqpTcp, // TODO: Cycle back on used protocol
            });

        _processor = _client.CreateProcessor(config.QueueName);
        _processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus error handler called with exception");
            return Task.CompletedTask;
        };
    }

    public ITransport SetMessageHandler(Func<IMessage, Task> handler)
    {
        _processor.ProcessMessageAsync += args => handler(new Message(args));
        return this;
    }

    /// <summary>
    /// Starts connecting to the message bus and receiving messages.
    /// Only call after <see cref="SetMessageHandler"/>/>
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StartProcessingAsync(CancellationToken cancellationToken = default) => await _processor.StartProcessingAsync(cancellationToken);

    public async Task StopProcessingAsync(CancellationToken cancellationToken = default) => await _processor.StopProcessingAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _processor.StopProcessingAsync();
        await _client.DisposeAsync();
        await _processor.DisposeAsync();
    }

    /// <summary>
    /// "Azure Service Bus"-specific implementation of the message abstraction
    /// </summary>
    private sealed class Message(ProcessMessageEventArgs messageEvent) : IMessage
    {
        public string Body { get; } = messageEvent.Message.Body.ToString();
        public string CorrelationId { get; } = messageEvent.Message.CorrelationId;
        public Task CompleteAsync(CancellationToken cancellationToken = default) => messageEvent.CompleteMessageAsync(messageEvent.Message, cancellationToken);

        public Task DeadLetterAsync(string reason, CancellationToken cancellationToken = default) =>
            messageEvent.DeadLetterMessageAsync(messageEvent.Message, deadLetterReason: reason, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Configuration Datamodel for the adapter. Includes all parameters which are configurable via configuration files or other means.
    /// </summary>
    public sealed class ServiceBusConfiguration
    {
        public string? ConnectionString { get; init; }
        public string? FullyQualifiedNamespace { get; init; }
        public string QueueName { get; init; } = string.Empty;
    }
}