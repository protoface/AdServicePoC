using System.DirectoryServices.AccountManagement;

namespace MediAdIdentityPoC;

public class AdIdentityAction
{
    public ActionType Action { get; set; } = ActionType.Disable;
    public IdentityType IdentityType { get; set; } = IdentityType.Sid;
    public string Identity { get; set; } = string.Empty;
}

public enum ActionType
{
    Enable,
    Disable,
}