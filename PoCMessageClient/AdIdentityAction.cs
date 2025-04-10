using System.DirectoryServices.AccountManagement;
using System.Text.Json.Serialization;

namespace ConsoleApp1;

public class AdIdentityAction
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActionType Action { get; set; } = ActionType.Disable;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IdentityType IdentityType { get; set; } = IdentityType.Sid;

    public string Identity { get; set; } = string.Empty;
}

public enum ActionType
{
    Enable,
    Disable
}