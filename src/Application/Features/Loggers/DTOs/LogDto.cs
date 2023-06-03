// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Domain.Entities.Logger;

namespace CleanArchitecture.Blazor.Application.Features.Loggers.DTOs;

public class LogDto : IMapFrom<Logger>
{
    [Description("Id")] 
    public int Id { get; set; }

    [Description("Message")] 
    public string? Message { get; set; }

    [Description("Message Template")] 
    public string? MessageTemplate { get; set; }

    [Description("Level")] 
    public string Level { get; set; } = default!;

    public bool ShowDetails { get; set; }

    [Description("Timestamp")] 
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;

    [Description("Exception")] 
    public string? Exception { get; set; }

    [Description("Username")] 
    public string? UserName { get; set; }

    [Description("Client IP")] 
    public string? ClientIP { get; set; }

    [Description("Client Agent")] 
    public string? ClientAgent { get; set; }

    [Description("Properties")] 
    public string? Properties { get; set; }

    [Description("Log Event")] 
    public string? LogEvent { get; set; }
}