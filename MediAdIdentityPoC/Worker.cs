using System.ComponentModel;
using System.DirectoryServices.AccountManagement;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediAdIdentityPoC.Transport;

namespace MediAdIdentityPoC;

internal sealed class Worker(ILogger<Worker> logger, ITransport busService) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("MediAdIdentityPoC is starting...");

        var ctx = new PrincipalContext(ContextType.Domain);
        logger.LogDebug("Connected to {dc}", ctx.ConnectedServer);

        await busService
            .SetMessageHandler(msg => ProcessMessage(msg, ctx, stoppingToken))
            .StartProcessingAsync(stoppingToken);

        logger.LogDebug("MediAdIdentityPoC is ready");

        // when this method exits, the service stops
        await Task.Delay(-1, stoppingToken);
    }

    private Task ProcessMessage(IMessage msg, PrincipalContext ctx, CancellationToken stoppingToken)
    {
        // whenever a message is received by the service, this function is called 
        // if processing the request is not possible, the corresponding message to moved to the dead-letter queue    

        // for easy tracking and correlation of events, logging is tied to the unique correlation id of the message
        using var logScope = logger.BeginScope(msg.CorrelationId);
        logger.LogInformation("Message received: {msg}", msg.Body);

        // parse execution parameters (action to execute, identity to execute on, method of identification)
        AdIdentityAction? action;
        try
        {
            action = JsonSerializer.Deserialize<AdIdentityAction>(msg.Body, JsonSerializerOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse message body, disregarding...");
            return msg.DeadLetterAsync("Parsing failed", cancellationToken: stoppingToken);
        }

        if (action == null)
        {
            logger.LogWarning("Failed to parse message body, disregarding...");
            return msg.DeadLetterAsync("Parsing failed", cancellationToken: stoppingToken);
        }

        // resolve the user principal via the chosen method
        UserPrincipal? user;
        try
        {
            // In this case UserPrincipal.FindByIdentity(ctx, action.IdentityType, action.Identity) would work the exact same way,
            // but when written like this extra input validation can easily be added in the future.
            
            switch (action.IdentityType)
            {
                case IdentityType.Sid:
                    user = UserPrincipal.FindByIdentity(ctx, IdentityType.Sid, action.Identity);
                    break;
                case IdentityType.SamAccountName:
                    user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, action.Identity);
                    break;
                case IdentityType.Guid:
                    user = UserPrincipal.FindByIdentity(ctx, IdentityType.Guid, action.Identity);
                    break;
                case IdentityType.Name:
                case IdentityType.UserPrincipalName:
                case IdentityType.DistinguishedName:
                default:
                    logger.LogWarning("Identity type '{type}' not supported, disregarding...", action.IdentityType);
                    return msg.DeadLetterAsync("Identity type not supported", cancellationToken: stoppingToken);
            }
        }
        catch (InvalidEnumArgumentException iee)
        {
            logger.LogWarning(iee, "Error while uniquely resolving identity '{identity}' by {type}. The identity format does not match the type",
                action.Identity, action.IdentityType);
            return msg.DeadLetterAsync("Failure to resolve identity (identity format mismatch)", cancellationToken: stoppingToken);
        }
        catch (MultipleMatchesException mme)
        {
            logger.LogWarning(mme, "Error while uniquely resolving identity '{identity}' by {type} (multiple matches)", action.Identity,
                action.IdentityType);
            return msg.DeadLetterAsync("Failure to uniquely resolve identity (multiple matches)", cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while resolving identity '{identity}' by {type} (unknown exception)", action.Identity, action.IdentityType);
            return msg.DeadLetterAsync("Failure to resolve identity (unknown exception)", cancellationToken: stoppingToken);
        }

        if (user == null)
        {
            logger.LogWarning("Error while resolving identity '{identity}' by {type} (no match)", action.Identity, action.IdentityType);
            return msg.DeadLetterAsync("Failure to resolve identity (no match)", cancellationToken: stoppingToken);
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
                    return msg.DeadLetterAsync("Action not supported", cancellationToken: stoppingToken);
            }

            try
            {
                user.Save();
            }
            catch (InvalidOperationException ioe)
            {
                logger.LogWarning(ioe, "Error while saving changes to {principal}", user.UserPrincipalName);
                return msg.DeadLetterAsync("Failed to save changes", cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while saving changes to {principal} (unknown error)", user.UserPrincipalName);
                return msg.DeadLetterAsync("Failed to save changes", cancellationToken: stoppingToken);
            }

            logger.LogDebug("Changes to {principal} saved", user.UserPrincipalName);
            return msg.CompleteAsync(stoppingToken);
        }
        finally
        {
            user.Dispose();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        await busService.StopProcessingAsync(cancellationToken);
    }
}