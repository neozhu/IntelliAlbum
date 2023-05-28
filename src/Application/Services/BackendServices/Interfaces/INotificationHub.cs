using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Services.BackendServices
{
    public interface INotificationHub
    {
        Task SendMessage(string type, string payload);
    }
}
