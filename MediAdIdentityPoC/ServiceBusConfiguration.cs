namespace MediAdIdentityPoC;

public class ServiceBusConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
}