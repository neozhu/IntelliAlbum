using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain;
public class StatusUpdate
{
    public string NewStatus { get; set; } = null!;
    public string? UserID { get; set; }
}
