namespace MediAdIdentityPoC;

using System.Text.Json;
using System.DirectoryServices.AccountManagement;

public class Worker(ILogger<Worker> logger, ServiceBusService busService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MediAdIdentityPoC is starting...");

        var ctx = new PrincipalContext(ContextType.Domain);
        await busService.SetMessageHandler(msg =>
        {
            using var logScope = logger.BeginScope(msg.Message.MessageId);
            logger.LogInformation("Message received: {msg}", msg);

            var action = JsonSerializer.Deserialize<AdIdentityAction>(msg.Message.Body);
            if (action == null)
            {
                logger.LogWarning("Action not parsable, disregarding...");
                return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Parsing failed");
            }

            UserPrincipal user;

            switch (action.IdentityType) // For future validation
            {
                case IdentityType.Sid:
                    user = UserPrincipal.FindByIdentity(ctx, IdentityType.Sid, action.Sid.Value);
                    break;
                case IdentityType.SamAccountName:
                case IdentityType.Name:
                case IdentityType.UserPrincipalName:
                case IdentityType.DistinguishedName:
                case IdentityType.Guid:
                default:
                    logger.LogWarning("Identity type {type}, not supported, disregarding...", action.IdentityType);
                    return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Identity type not supported");
            }

            switch (action.Action)
            {
                case ActionType.Enable:
                    user.Enabled = true;
                    logger.LogInformation("Identity {principal} enabled", user.UserPrincipalName);
                    break;
                case ActionType.Disable:
                    user.Enabled = false;
                    logger.LogInformation("Identity {principal} disabled", user.UserPrincipalName);
                    break;
                default:
                    logger.LogWarning("Action {action}, not supported, disregarding...", action.Action);
                    return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Action not supported");
            }

            user.Save();
            logger.LogInformation("Changes to {principal} saved", user.UserPrincipalName);

            return msg.CompleteMessageAsync(msg.Message, stoppingToken);
        }).SetErrorHandler(err =>
        {
            logger.LogError("Service Bus error handler called with exception: {ex}", err.Exception);
            return Task.CompletedTask;
        }).StartProcessingAsync(stoppingToken);

        logger.LogInformation("MediAdIdentityPoC is ready");
        await Task.Delay(-1, stoppingToken);
    }
}