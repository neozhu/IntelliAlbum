using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Features.Folders.Services;
public interface IStatusService
{
    void UpdateStatus(string newStatus, string? userId = null);
    event Action<string> OnStatusChanged;
}
