namespace MediAdIdentityPoC.Transport;

///<summary>
/// Abstraction layer for messages received by the message bus. Only contains the necessary data and functions for processing.
/// </summary>
public interface IMessage
{
    string Body { get; }
    string CorrelationId { get; }

    Task CompleteAsync(CancellationToken cancellationToken = default);
    Task DeadLetterAsync(string reason, CancellationToken cancellationToken = default);
}