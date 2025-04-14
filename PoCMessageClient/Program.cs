using System.DirectoryServices.AccountManagement;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Cocona;
using PocMessageClient;

var app = CoconaLiteApp.Create();

app.AddCommand("fqn", ([Argument("namespace", Description = "Fully Qualified Namespace")] string fqn, Parameters parameters)
        => SendMessage(parameters, new(fqn, new DefaultAzureCredential())))
    .OptionLikeCommand(x => x.Add("raw",
        ([Argument("namespace", Description = "Fully Qualified Namespace")] string fqn,
                [Argument("queue", Description = "Message Queue Name")]
                string queue,
                [Argument("message", Description = "Raw Json-message to send")]
                string message)
            => SendMessageRaw(message, queue, new(fqn, new DefaultAzureCredential()))))
    .WithDescription("Connect to Azure Service Bus using a fully qualified namespace");

app.AddCommand("conn", ([Argument("conn", Description = "Connection String")] string connectionString, Parameters parameters)
        => SendMessage(parameters, new(connectionString)))
    .OptionLikeCommand(x => x.Add("raw",
        ([Argument("conn", Description = "Connection String")] string connectionString,
                [Argument("queue", Description = "Message Queue Name")]
                string queue,
                [Argument("message", Description = "Raw Json-message to send")]
                string message)
            => SendMessageRaw(message, queue, new(connectionString))))
    .WithDescription("Connect to Azure Service Bus using a connection string");

app.Run();

return;

static async Task SendMessage(Parameters parameters, ServiceBusClient client)
{
    var msgObject = new AdIdentityAction() { IdentityType = parameters.IdType, Action = parameters.Action, Identity = parameters.Identity };
    var msg = JsonSerializer.Serialize(msgObject);
    await SendMessageRaw(msg, parameters.Queue, client);
}

static async Task SendMessageRaw(string message, string queue, ServiceBusClient client)
{
    await client.CreateSender(queue).SendMessageAsync(new(message));
    Console.WriteLine($"Message sent: {message}");
}

internal record struct Parameters(
    [Argument("queue", Description = "Message Queue Name")]
    string Queue,
    [Argument("type", Description = "Id Type")]
    IdentityType IdType,
    [Argument("identity", Description = "Identity")]
    string Identity,
    [Argument("action", Description = "Action to execute")]
    ActionType Action
) : ICommandParameterSet;