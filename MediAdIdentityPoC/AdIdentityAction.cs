using System.DirectoryServices.AccountManagement;
using System.Text.Json.Serialization;

namespace MediAdIdentityPoC;

public class AdIdentityAction
{
    [JsonConverter(typeof(StrictEnumConverter<ActionType>))]
    public ActionType Action { get; init; } = ActionType.Disable;

    [JsonConverter(typeof(StrictEnumConverter<IdentityType>))]
    public IdentityType IdentityType { get; init; } = IdentityType.Sid;

    public string Identity { get; init; } = string.Empty;
}

public enum ActionType
{
    Enable,
    Disable
}