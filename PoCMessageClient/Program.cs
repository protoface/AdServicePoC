using System.DirectoryServices.AccountManagement;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using ConsoleApp1;

string? connectionString = args.Length > 0 ? args[0] : null;
while (connectionString == null)
{
    Console.Write("Enter connection string or fqn: ");
    connectionString = Console.ReadLine();
}

string? queue = args.Length > 1 ? args[1] : null;
while (queue == null)
{
    Console.Write("Enter queue name: ");
    queue = Console.ReadLine();
}


var serviceBusClient = connectionString.Contains(';')
    ? new ServiceBusClient(connectionString)
    : new ServiceBusClient(connectionString, new DefaultAzureCredential());
var sender = serviceBusClient.CreateSender(queue);


IdentityType? identityType = args.Length > 2 ? Enum.Parse<IdentityType>(args[2]) : null;
while (!identityType.HasValue)
{
    PrintEnumOptions<ActionType>();

    Console.Write("Enter method of identification: ");
    if (int.TryParse(Console.ReadLine(), out var typeInt))
        identityType = (IdentityType)typeInt;
}

string? id = args.Length > 3 ? args[3] : null;
while (id == null)
{
    Console.Write("Enter identity: ");
    id = Console.ReadLine();
}

ActionType? action = args.Length > 4 ? Enum.Parse<ActionType>(args[4]) : null;
while (!action.HasValue)
{
    PrintEnumOptions<ActionType>();

    Console.Write("Enter action: ");
    if (int.TryParse(Console.ReadLine(), out var actionInt))
        action = (ActionType)actionInt;
}


var adIdentityAction = new AdIdentityAction() { IdentityType = identityType.Value, Action = action.Value, Identity = id };

await sender.SendMessageAsync(new(JsonSerializer.Serialize(adIdentityAction)));
Console.WriteLine("Message sent");
Console.WriteLine();


while (true) await Resend();

async Task Resend()
{
    PrintEnumOptions<IdentityType>();

    Console.Write("id method [leave empty for using last]: ");
    if (int.TryParse(Console.ReadLine(), out var typeInt))
        adIdentityAction.IdentityType = (IdentityType)typeInt;

    Console.Write("identity [leave empty for using last]: ");
    adIdentityAction.Identity = Console.ReadLine() is { Length: > 0 } identity ? identity : adIdentityAction.Identity;

    PrintEnumOptions<ActionType>();

    Console.Write("Enter action: ");
    if (int.TryParse(Console.ReadLine(), out var actionInt))
        adIdentityAction.Action = (ActionType)actionInt;

    await sender.SendMessageAsync(new(JsonSerializer.Serialize(adIdentityAction)));
    Console.WriteLine("Message sent");
    Console.WriteLine();
}

void PrintEnumOptions<TEnum>() where TEnum : struct, Enum
{
    foreach (var value in Enum.GetValues<TEnum>())
    {
        Console.WriteLine($"{value:D}: {value}");
    }   
}