using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

namespace MediAdIdentityPoC;

public class AdIdentityAction
{
    public ActionType Action { get; set; }
    public IdentityType IdentityType { get; set; } = IdentityType.Sid;
    public string Identity { get; set; } = string.Empty;

    public SecurityIdentifier Sid => new(Identity);
}

public enum ActionType
{
    Enable,
    Disable,
}