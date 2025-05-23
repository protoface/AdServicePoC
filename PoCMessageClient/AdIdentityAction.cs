﻿using System.DirectoryServices.AccountManagement;
using System.Text.Json.Serialization;

namespace PocMessageClient;

public class AdIdentityAction
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActionType Action { get; init; } = ActionType.Disable;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IdentityType IdentityType { get; init; } = IdentityType.Sid;

    public string Identity { get; set; } = string.Empty;
}

public enum ActionType
{
    Enable,
    Disable
}