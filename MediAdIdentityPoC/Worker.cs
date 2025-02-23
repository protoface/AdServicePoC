using System.ComponentModel;

namespace MediAdIdentityPoC;

using System.Text.Json;
using System.DirectoryServices.AccountManagement;

internal sealed class Worker(ILogger<Worker> logger, ServiceBusService busService) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MediAdIdentityPoC is starting...");

        var ctx = new PrincipalContext(ContextType.Domain);

        // whenever a message is received by the service, this function is called 
        // if processing the request is not possible, the corresponding message to moved to the dead-letter queue
        await busService.SetMessageHandler(msg =>
        {
            // for easy tracking and correlation of events, logging is tied to the unique message id
            using var logScope = logger.BeginScope(msg.Message.MessageId);
            logger.LogInformation("Message received: {msg}", msg);

            // parse execution parameters (action the execute, identity to execute on, identity id method)
            AdIdentityAction? action;
            try
            {
                action = JsonSerializer.Deserialize<AdIdentityAction>(msg.Message.Body);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse message body, disregarding...");
                return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Parsing failed");
            }

            if (action == null)
            {
                logger.LogWarning("Failed to parse message body, disregarding...");
                return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Parsing failed");
            }

            // resolve the user principal via the chosen method
            UserPrincipal? user;
            try
            {
                switch (action.IdentityType) // For future validation
                {
                    case IdentityType.Sid:
                        user = UserPrincipal.FindByIdentity(ctx, IdentityType.Sid, action.Identity);
                        break;
                    case IdentityType.SamAccountName:
                        user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, action.Identity);
                        break;
                    case IdentityType.Name:
                    case IdentityType.UserPrincipalName:
                    case IdentityType.DistinguishedName:
                    case IdentityType.Guid:
                        user = UserPrincipal.FindByIdentity(ctx, IdentityType.Guid, action.Identity);
                        break;
                    default:
                        logger.LogWarning("Identity type '{type}' not supported, disregarding...", action.IdentityType);
                        return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Identity type not supported");
                }
            }
            catch (InvalidEnumArgumentException iee)
            {
                logger.LogWarning(iee, "Error while uniquely resolving identity '{identity}' by {type}. The identity format does not match the type",
                    action.Identity, action.IdentityType);
                return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken,
                    deadLetterReason: "Failure to resolve identity (identity format mismatch)");
            }
            catch (MultipleMatchesException mme)
            {
                logger.LogWarning(mme, "Error while uniquely resolving identity '{identity}' by {type} (multiple matches)", action.Identity,
                    action.IdentityType);
                return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken,
                    deadLetterReason: "Failure to uniquely resolve identity (multiple matches)");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while resolving identity '{identity}' by {type} (unknown exception)", action.Identity, action.IdentityType);
                return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken,
                    deadLetterReason: "Failure to resolve identity (unknown exception)");
            }

            if (user == null)
            {
                logger.LogWarning("Error while resolving identity '{identity}' by {type} (no match)", action.Identity, action.IdentityType);
                return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Failure to resolve identity (no match)");
            }

            try
            {
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
                        logger.LogWarning("Action '{action}' not supported, disregarding...", action.Action);
                        return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Action not supported");
                }

                try
                {
                    user.Save();
                }
                catch (InvalidOperationException ioe)
                {
                    logger.LogWarning(ioe, "Error while saving changes to {principal}", user.UserPrincipalName);
                    return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Failed to save changes");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error while saving changes to {principal} (unknown error)", user.UserPrincipalName);
                    return msg.DeadLetterMessageAsync(msg.Message, cancellationToken: stoppingToken, deadLetterReason: "Failed to save changes");
                }

                logger.LogInformation("Changes to {principal} saved", user.UserPrincipalName);
                return msg.CompleteMessageAsync(msg.Message, stoppingToken);
            }
            finally
            {
                user.Dispose();
            }
        }).SetErrorHandler(err =>
        {
            logger.LogError(err.Exception, "Service Bus error handler called with exception");
            return Task.CompletedTask;
        }).StartProcessingAsync(stoppingToken);

        logger.LogInformation("MediAdIdentityPoC is ready");
        await Task.Delay(-1, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        await busService.StopProcessingAsync(cancellationToken);
    }
}