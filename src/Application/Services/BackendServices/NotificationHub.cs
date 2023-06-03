using CleanArchitecture.Blazor.Application.Services.BackendServices;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public class NotificationHub : Hub<INotificationHub>
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public static string NotificationRoot => "notifications";
    public static string OnSendMessage => "SendMessage";

    public async Task SendMessage(string type, string payload)
    {
      await Clients.All.SendMessage(type, payload);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogTrace("Notify Hub connected.");
        await base.OnConnectedAsync();
    }
}
