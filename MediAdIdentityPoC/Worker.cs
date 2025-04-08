using System.ComponentModel;
using System.DirectoryServices.AccountManagement;
using System.Text.Json;

namespace MediAdIdentityPoC;

internal sealed class Worker(ILogger<Worker> logger, ServiceBusService busService) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
        logger.LogInformation("MediAdIdentityPoC is starting...");

        var ctx = new PrincipalContext(ContextType.Domain);

        await busService.SetMessageHandler(msg =>
            {
                // whenever a message is received by the service, this function is called 
                // if processing the request is not possible, the corresponding message to moved to the dead-letter queue    

                // for easy tracking and correlation of events, logging is tied to the unique message id
                using var logScope = logger.BeginScope(msg.Message.MessageId);
                logger.LogInformation("Message received: {msg}", msg.Message.Body);

                // parse execution parameters (action the execute, identity to execute on, identity id method)
                AdIdentityAction? action;
                try
                {
                    action = JsonSerializer.Deserialize<AdIdentityAction>(msg.Message.Body);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse message body, disregarding...");
                    return msg.DeadLetterMessageAsync(msg.Message, "Parsing failed", cancellationToken: stoppingToken);
                }

                if (action == null)
                {
                    logger.LogWarning("Failed to parse message body, disregarding...");
                    return msg.DeadLetterMessageAsync(msg.Message, "Parsing failed", cancellationToken: stoppingToken);
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
                            return msg.DeadLetterMessageAsync(msg.Message, "Identity type not supported", cancellationToken: stoppingToken);
                    }
                }
                catch (InvalidEnumArgumentException iee)
                {
                    logger.LogWarning(iee, "Error while uniquely resolving identity '{identity}' by {type}. The identity format does not match the type",
                        action.Identity, action.IdentityType);
                    return msg.DeadLetterMessageAsync(msg.Message, "Failure to resolve identity (identity format mismatch)", cancellationToken: stoppingToken);
                }
                catch (MultipleMatchesException mme)
                {
                    logger.LogWarning(mme, "Error while uniquely resolving identity '{identity}' by {type} (multiple matches)", action.Identity,
                        action.IdentityType);
                    return msg.DeadLetterMessageAsync(msg.Message, "Failure to uniquely resolve identity (multiple matches)", cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error while resolving identity '{identity}' by {type} (unknown exception)", action.Identity, action.IdentityType);
                    return msg.DeadLetterMessageAsync(msg.Message, "Failure to resolve identity (unknown exception)", cancellationToken: stoppingToken);
                }

                if (user == null)
                {
                    logger.LogWarning("Error while resolving identity '{identity}' by {type} (no match)", action.Identity, action.IdentityType);
                    return msg.DeadLetterMessageAsync(msg.Message, "Failure to resolve identity (no match)", cancellationToken: stoppingToken);
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
                            return msg.DeadLetterMessageAsync(msg.Message, "Action not supported", cancellationToken: stoppingToken);
                    }

                    try
                    {
                        user.Save();
                    }
                    catch (InvalidOperationException ioe)
                    {
                        logger.LogWarning(ioe, "Error while saving changes to {principal}", user.UserPrincipalName);
                        return msg.DeadLetterMessageAsync(msg.Message, "Failed to save changes", cancellationToken: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error while saving changes to {principal} (unknown error)", user.UserPrincipalName);
                        return msg.DeadLetterMessageAsync(msg.Message, "Failed to save changes", cancellationToken: stoppingToken);
                    }

                    logger.LogInformation("Changes to {principal} saved", user.UserPrincipalName);
                    return msg.CompleteMessageAsync(msg.Message, stoppingToken);
                }
                finally
                {
                    user.Dispose();
                }
            })
            .SetErrorHandler(err =>
            {
                logger.LogError(err.Exception, "Service Bus error handler called with exception");
                return Task.CompletedTask;
            }).StartProcessingAsync(stoppingToken);

        logger.LogInformation("MediAdIdentityPoC is ready");

        // when this method exits, the service stops
        await Task.Delay(-1, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        await busService.StopProcessingAsync(cancellationToken);
    }
}