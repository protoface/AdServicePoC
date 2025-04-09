namespace MediAdIdentityPoC.Transport;

public interface IMessage
{
    string Body { get; }
    string CorrelationId { get; }
    
    Task CompleteAsync(CancellationToken cancellationToken = default);
    Task DeadLetterAsync(string reason, CancellationToken cancellationToken = default);
}